using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

namespace SabaProps.Capture
{
    /// <summary>
    /// 一定間隔で画像を撮り、有限枚数を VRAM 上に保持する Recorder。
    /// <para>
    /// 入力は RenderTexture (sourceTexture) か Camera (sourceCamera) です。Camera を指定した場合は
    /// 撮影時にだけ Render() を呼ぶので、Camera 自体を無効にしておけば撮影間隔の間は描画しません。
    /// 保存先は実行時に確保する RenderTexture の配列で、1 枚ずつ必要になった時点で作ります。
    /// </para>
    /// <para>
    /// 保存は VRAM 上だけです。Udon からディスクへ書き出す手段はなく、インスタンスを出ると消えます。
    /// 同期は持たず、各クライアントが自分の Recorder を持ちます。
    /// </para>
    /// <para>
    /// 計算部分は CaptureRecorderSchedule.cs にあり、Unity 無しで実行して検査しています。
    /// </para>
    /// </summary>
    [AddComponentMenu("SabaProps/Capture Recorder")]
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public partial class CaptureRecorder : UdonSharpBehaviour
    {
        [Header("入力")]
        [Tooltip("撮影元の RenderTexture。sourceCamera が設定されている場合は使いません。")]
        public Texture sourceTexture;

        [Tooltip("撮影元の Camera。撮影時にだけ Render() します。無効にしておくと撮影間隔の間は描画しません。")]
        public Camera sourceCamera;

        [Header("撮影")]
        [Tooltip("撮影間隔 (s)。0.1 未満は 0.1 に丸めます。間引きモードでは満杯になるたびに実効値が 2 倍になります。")]
        public float interval = 10f;

        [Tooltip("起動時に撮影を始めます。")]
        public bool recordOnStart;

        [Header("保存")]
        [Tooltip("保存する画像の幅 (px)。")]
        public int frameWidth = 384;

        [Tooltip("保存する画像の高さ (px)。")]
        public int frameHeight = 216;

        [Tooltip("画素形式。FormatARGB32 (0) または FormatRGB565 (1)。RGB565 は VRAM が半分です。")]
        public int pixelFormat = FormatARGB32;

        [Tooltip("保持する最大枚数。上限は 4096 です。")]
        public int maxFrames = 360;

        [Tooltip("VRAM の予算 (MB)。0 より大きければ、maxFrames と予算から求めた枚数の小さい方を使います。")]
        public float memoryBudgetMegabytes;

        [Tooltip("満杯時の動作。PolicyRing (0) は上書き、PolicyStop (1) は停止、PolicyThin (2) は間引き。")]
        public int fullPolicy = PolicyThin;

        private RenderTexture[] frames;
        private double[] frameTimes;
        private RenderTexture[] scratchFrames;
        private double[] scratchTimes;
        private int[] thinOrder;

        /// <summary>Camera 入力のための深度付きの中間バッファ。保存枚数には数えません。</summary>
        private RenderTexture staging;

        private int capacity;
        private int head;
        private int count;

        private int allocatedWidth;
        private int allocatedHeight;
        private int allocatedFormat;
        private int allocatedFrames;

        private bool recording;
        private bool stoppedByPolicy;
        private double clockBeforeResume;
        private double resumedAt;
        private double nextCaptureTime;
        private double currentInterval;

        /// <summary>保存内容が変わるたびに増えます。再生側はこれを見て表示を更新します。</summary>
        private int revision;

        private void Start()
        {
            ApplySettings();

            if (recordOnStart)
            {
                _StartRecording();
            }
        }

        private void Update()
        {
            if (!recording)
            {
                return;
            }

            double now = RecordingClock();
            if (now < nextCaptureTime)
            {
                return;
            }

            if (count >= capacity)
            {
                if (fullPolicy == PolicyStop)
                {
                    StopByPolicy();
                    return;
                }

                if (fullPolicy == PolicyThin && capacity > 1)
                {
                    // 間引き後の格子に今の時刻が載るとは限らないため、この回は撮らずに次の予定を決め直します。
                    Thin();
                    return;
                }
            }

            StoreFrame(now);
            nextCaptureTime = NextCaptureTime(nextCaptureTime, currentInterval, now);
        }

        // ------------------------------------------------------------------
        // 操作
        // ------------------------------------------------------------------

        /// <summary>撮影を始めるか、一時停止から再開します。空の状態から始めた場合はすぐに 1 枚撮ります。</summary>
        public void _StartRecording()
        {
            if (recording)
            {
                return;
            }

            if (count == 0)
            {
                ApplySettings();
                clockBeforeResume = 0.0;
                nextCaptureTime = 0.0;
                currentInterval = Mathf.Max(interval, MinInterval);
            }

            if (stoppedByPolicy && count >= capacity && fullPolicy == PolicyStop)
            {
                return;
            }

            stoppedByPolicy = false;
            resumedAt = Time.timeAsDouble;
            recording = true;
            revision++;
        }

        /// <summary>撮影を一時停止します。撮影時計も止まるので、再開後の間隔は保たれます。</summary>
        public void _StopRecording()
        {
            if (!recording)
            {
                return;
            }

            clockBeforeResume = RecordingClock();
            recording = false;
            revision++;
        }

        public void _ToggleRecording()
        {
            if (recording)
            {
                _StopRecording();
            }
            else
            {
                _StartRecording();
            }
        }

        /// <summary>予定と無関係に 1 枚撮ります。撮影周期は変えません。満杯なら満杯時の動作に従います。</summary>
        public void _CaptureNow()
        {
            if (count == 0 && !recording)
            {
                ApplySettings();
                clockBeforeResume = 0.0;
                currentInterval = Mathf.Max(interval, MinInterval);
            }

            if (count >= capacity)
            {
                if (fullPolicy == PolicyStop)
                {
                    return;
                }

                if (fullPolicy == PolicyThin && capacity > 1)
                {
                    Thin();
                }
            }

            StoreFrame(RecordingClock());
        }

        /// <summary>
        /// 保存内容を消して止めます。RenderTexture は次の撮影で再利用するため解放しません。
        /// 解像度や枚数の設定はここで反映されます。
        /// </summary>
        public void _Clear()
        {
            recording = false;
            stoppedByPolicy = false;
            head = 0;
            count = 0;
            clockBeforeResume = 0.0;
            nextCaptureTime = 0.0;
            currentInterval = Mathf.Max(interval, MinInterval);
            ApplySettings();
            revision++;
        }

        /// <summary>保存内容を消し、確保した RenderTexture をすべて破棄して VRAM を返します。</summary>
        public void _ReleaseFrames()
        {
            _Clear();
            DestroyFrames();
            revision++;
        }

        // ------------------------------------------------------------------
        // 再生側へ公開する読み取り
        // ------------------------------------------------------------------

        /// <summary>保持している枚数。</summary>
        public int GetFrameCount() { return count; }

        /// <summary>保持できる枚数。</summary>
        public int GetCapacity() { return capacity; }

        public int GetRevision() { return revision; }

        public bool IsRecording() { return recording; }

        /// <summary>保持枚数が上限に達しているかどうか。</summary>
        public bool IsFull() { return count >= capacity; }

        /// <summary>現在の実効撮影間隔 (s)。間引きのたびに 2 倍になります。</summary>
        public double GetCurrentInterval() { return currentInterval; }

        /// <summary>撮影開始からの経過時間 (s)。一時停止中は進みません。</summary>
        public double GetElapsed() { return RecordingClock(); }

        /// <summary>確保済みの保存用 RenderTexture の合計 (MB)。中間バッファは含みません。</summary>
        public float GetAllocatedMegabytes()
        {
            return (float)(allocatedFrames * FrameBytes(allocatedWidth, allocatedHeight, allocatedFormat) / 1048576.0);
        }

        /// <summary>古い方から数えて logical 番目の画像。範囲外なら null。</summary>
        public RenderTexture GetFrame(int logical)
        {
            if (logical < 0 || logical >= count)
            {
                return null;
            }

            return frames[PhysicalIndex(head, logical, capacity)];
        }

        /// <summary>logical 番目の画像を撮った時刻 (撮影開始からの秒)。範囲外なら -1。</summary>
        public double GetFrameTime(int logical)
        {
            if (logical < 0 || logical >= count)
            {
                return -1.0;
            }

            return frameTimes[PhysicalIndex(head, logical, capacity)];
        }

        /// <summary>時刻 t に最も近い画像の順番。空なら -1。</summary>
        public int FindFrameAt(double t)
        {
            return NearestLogical(frameTimes, head, count, capacity, t);
        }

        // ------------------------------------------------------------------
        // 内部
        // ------------------------------------------------------------------

        private double RecordingClock()
        {
            if (!recording)
            {
                return clockBeforeResume;
            }

            return clockBeforeResume + (Time.timeAsDouble - resumedAt);
        }

        private void StopByPolicy()
        {
            clockBeforeResume = RecordingClock();
            recording = false;
            stoppedByPolicy = true;
            revision++;
        }

        /// <summary>
        /// 解像度、画素形式、枚数の設定を保存配列へ反映します。保持中の画像がある間は変えません。
        /// 解像度か形式が変わった場合は、確保済みの RenderTexture を破棄します。
        /// </summary>
        private void ApplySettings()
        {
            if (count > 0)
            {
                return;
            }

            int width = Mathf.Max(frameWidth, 1);
            int height = Mathf.Max(frameHeight, 1);
            int format = pixelFormat == FormatRGB565 ? FormatRGB565 : FormatARGB32;

            if (frames != null && (width != allocatedWidth || height != allocatedHeight || format != allocatedFormat))
            {
                DestroyFrames();
            }

            allocatedWidth = width;
            allocatedHeight = height;
            allocatedFormat = format;

            int wanted = CapacityFor(width, height, format, maxFrames, memoryBudgetMegabytes);
            if (frames != null && wanted == capacity)
            {
                return;
            }

            var resizedFrames = new RenderTexture[wanted];
            if (frames != null)
            {
                int keep = Mathf.Min(frames.Length, wanted);
                for (int i = 0; i < frames.Length; i++)
                {
                    if (i < keep)
                    {
                        resizedFrames[i] = frames[i];
                    }
                    else if (frames[i] != null)
                    {
                        Destroy(frames[i]);
                        allocatedFrames--;
                    }
                }
            }

            frames = resizedFrames;
            frameTimes = new double[wanted];
            scratchFrames = new RenderTexture[wanted];
            scratchTimes = new double[wanted];
            thinOrder = new int[wanted];
            capacity = wanted;
            head = 0;
        }

        private void DestroyFrames()
        {
            if (frames != null)
            {
                for (int i = 0; i < frames.Length; i++)
                {
                    if (frames[i] != null)
                    {
                        Destroy(frames[i]);
                        frames[i] = null;
                    }
                }
            }

            if (staging != null)
            {
                Destroy(staging);
                staging = null;
            }

            allocatedFrames = 0;
        }

        /// <summary>次の枠へ 1 枚書き込みます。満杯ならリングバッファとして最古の枠を上書きします。</summary>
        private void StoreFrame(double time)
        {
            if (sourceCamera == null && sourceTexture == null)
            {
                return;
            }

            if (frames == null)
            {
                ApplySettings();
            }

            int slot;
            if (count < capacity)
            {
                slot = PhysicalIndex(head, count, capacity);
                count++;
            }
            else
            {
                slot = head;
                head = PhysicalIndex(head, 1, capacity);
            }

            RenderTexture target = EnsureFrame(slot);
            WriteSource(target);
            frameTimes[slot] = time;
            revision++;
        }

        private RenderTexture EnsureFrame(int slot)
        {
            RenderTexture frame = frames[slot];
            if (frame != null)
            {
                return frame;
            }

            // 深度は持たせません。Quest の GPU では、深度付きの書き込み先への Blit が失敗するためです。
            frame = new RenderTexture(allocatedWidth, allocatedHeight, 0, FrameFormat());
            frame.name = "CaptureFrame" + slot;
            frame.filterMode = FilterMode.Bilinear;
            frame.wrapMode = TextureWrapMode.Clamp;
            frame.Create();
            frames[slot] = frame;
            allocatedFrames++;
            return frame;
        }

        private void WriteSource(RenderTexture target)
        {
            if (sourceCamera != null)
            {
                // 通常のシーン描画には深度が要るため、深度付きの中間バッファへ描いてから写します。
                // 保存用の各枠に深度を持たせるより、中間バッファ 1 枚の方が VRAM が少なく済みます。
                if (staging == null)
                {
                    staging = new RenderTexture(allocatedWidth, allocatedHeight, 24, FrameFormat());
                    staging.name = "CaptureStaging";
                    staging.Create();
                }

                RenderTexture previous = sourceCamera.targetTexture;
                sourceCamera.targetTexture = staging;
                sourceCamera.Render();
                sourceCamera.targetTexture = previous;
                VRCGraphics.Blit(staging, target);
                return;
            }

            if (sourceTexture != null)
            {
                VRCGraphics.Blit(sourceTexture, target);
            }
        }

        private RenderTextureFormat FrameFormat()
        {
            return allocatedFormat == FormatRGB565 ? RenderTextureFormat.RGB565 : RenderTextureFormat.ARGB32;
        }

        /// <summary>1 枚おきに捨て、撮影間隔を 2 倍にします。RenderTexture は並べ替えるだけで複製しません。</summary>
        private void Thin()
        {
            int kept = ThinOrder(thinOrder, head, count, capacity);

            for (int i = 0; i < capacity; i++)
            {
                scratchFrames[i] = frames[thinOrder[i]];
                scratchTimes[i] = frameTimes[thinOrder[i]];
            }

            RenderTexture[] swapFrames = frames;
            frames = scratchFrames;
            scratchFrames = swapFrames;

            double[] swapTimes = frameTimes;
            frameTimes = scratchTimes;
            scratchTimes = swapTimes;

            head = 0;
            count = kept;
            currentInterval *= 2.0;
            nextCaptureTime = frameTimes[kept - 1] + currentInterval;
            revision++;
        }
    }
}
