using UdonSharp;
using UnityEngine;
using UnityEngine.UI;

namespace SabaProps.Capture
{
    /// <summary>
    /// CaptureRecorder が保持している画像を時系列で見返す再生器。
    /// <para>
    /// 表示先は RawImage と Renderer のどちらか、または両方です。Renderer へは
    /// MaterialPropertyBlock で渡すので、共有マテリアルを書き換えません。
    /// 画像を複製せず、Recorder の RenderTexture をそのまま参照します。
    /// </para>
    /// <para>
    /// 追従モードでは常に最新の画像を表示します。スクラブ、コマ送り、再生の操作で追従を
    /// 解除し、_Latest で戻ります。追従を解除している間に Recorder の内容が変わった場合
    /// (リングバッファの上書きや間引き) は、表示中の画像と同じ時刻に最も近い画像へ移ります。
    /// </para>
    /// </summary>
    [AddComponentMenu("SabaProps/Capture Player")]
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public partial class CapturePlayer : UdonSharpBehaviour
    {
        [Header("入力")]
        public CaptureRecorder recorder;

        [Header("表示先")]
        [Tooltip("画像を表示する RawImage。")]
        public RawImage display;

        [Tooltip("画像を表示する Renderer。MaterialPropertyBlock で texturePropertyName に渡します。")]
        public Renderer displayRenderer;

        public string texturePropertyName = "_MainTex";

        [Tooltip("タイムラインに等間隔で並べるサムネイル。")]
        public RawImage[] thumbnails;

        [Header("操作 UI")]
        [Tooltip("0 から 1 のタイムライン。On Value Changed から _OnScrub を呼びます。")]
        public Slider timeline;

        [Tooltip("表示中の画像の番号と時刻。")]
        public Text positionLabel;

        [Tooltip("Recorder の状態。")]
        public Text statusLabel;

        [Header("再生")]
        [Tooltip("再生速度 (枚/s)。")]
        public float playbackRate = 8f;

        [Tooltip("最後まで再生したら先頭へ戻ります。")]
        public bool loop = true;

        private float playhead;
        private bool playing;
        private bool following = true;
        private int lastRevision = -1;
        private int shownFrame = -2;
        private RenderTexture shownTexture;
        private double shownTime = -1.0;
        private float nextStatusTime;
        private MaterialPropertyBlock block;

        private void Update()
        {
            if (recorder == null)
            {
                return;
            }

            int count = recorder.GetFrameCount();
            int revision = recorder.GetRevision();

            if (revision != lastRevision)
            {
                lastRevision = revision;
                OnRecorderChanged(count);
            }

            if (playing)
            {
                playhead = AdvancePlayhead(playhead, playbackRate, Time.deltaTime, count, loop);
                if (!loop && PlayheadFrame(playhead, count) >= count - 1)
                {
                    playing = false;
                }
            }

            Show(PlayheadFrame(playhead, count), count);

            // 文字列の組み立ては Udon では安くないため、状態表示は 0.5 秒ごとに留めます。
            if (Time.time >= nextStatusTime)
            {
                nextStatusTime = Time.time + 0.5f;
                RefreshStatus(count);
            }
        }

        // ------------------------------------------------------------------
        // 操作
        // ------------------------------------------------------------------

        public void _Play()
        {
            int count = CurrentCount();
            if (count <= 0)
            {
                return;
            }

            following = false;
            if (!loop && PlayheadFrame(playhead, count) >= count - 1)
            {
                playhead = 0f;
            }

            playing = true;
        }

        public void _Pause()
        {
            playing = false;
        }

        public void _TogglePlay()
        {
            if (playing)
            {
                _Pause();
            }
            else
            {
                _Play();
            }
        }

        public void _StepForward() { Step(1); }

        public void _StepBackward() { Step(-1); }

        public void _First()
        {
            following = false;
            playing = false;
            playhead = 0f;
        }

        /// <summary>最新の画像へ移り、以後は新しい画像が撮られるたびに追従します。</summary>
        public void _Latest()
        {
            following = true;
            playing = false;
            playhead = Mathf.Max(CurrentCount() - 1, 0);
        }

        public void _Faster()
        {
            playbackRate = Mathf.Clamp(playbackRate * 2f, MinPlaybackRate, MaxPlaybackRate);
        }

        public void _Slower()
        {
            playbackRate = Mathf.Clamp(playbackRate * 0.5f, MinPlaybackRate, MaxPlaybackRate);
        }

        /// <summary>タイムラインのスライダーから呼ばれます。</summary>
        public void _OnScrub()
        {
            if (timeline == null)
            {
                return;
            }

            int count = CurrentCount();
            int frame = SliderToFrame(timeline.value, count);
            if (frame < 0)
            {
                return;
            }

            following = false;
            playing = false;
            playhead = frame;
        }

        // ------------------------------------------------------------------
        // 内部
        // ------------------------------------------------------------------

        private int CurrentCount()
        {
            return recorder == null ? 0 : recorder.GetFrameCount();
        }

        private void Step(int delta)
        {
            int count = CurrentCount();
            if (count <= 0)
            {
                return;
            }

            following = false;
            playing = false;
            playhead = StepFrame(PlayheadFrame(playhead, count), delta, count, loop);
        }

        private void OnRecorderChanged(int count)
        {
            if (count <= 0)
            {
                playhead = 0f;
                playing = false;
            }
            else if (following)
            {
                playhead = count - 1;
            }
            else if (shownTime >= 0.0)
            {
                // 上書きや間引きで順番がずれても、同じ場面を表示し続けます。
                int nearest = recorder.FindFrameAt(shownTime);
                if (nearest >= 0)
                {
                    playhead = nearest + (playhead - Mathf.Floor(playhead));
                }
            }

            RefreshThumbnails(count);

            // 番号が同じでも中身が入れ替わっている場合があるので、次の表示で必ず貼り直します。
            shownFrame = -2;
            nextStatusTime = 0f;
        }

        private void Show(int frame, int count)
        {
            if (frame == shownFrame)
            {
                return;
            }

            shownFrame = frame;
            RenderTexture texture = recorder.GetFrame(frame);
            shownTime = recorder.GetFrameTime(frame);

            if (texture != shownTexture)
            {
                shownTexture = texture;
                ApplyTexture(texture);
            }

            if (timeline != null)
            {
                // 通知付きで書くと _OnScrub が呼ばれ、追従が解除されてしまいます。
                timeline.SetValueWithoutNotify(FrameToSlider(frame, count));
            }

            if (positionLabel != null)
            {
                positionLabel.text = frame < 0
                    ? "NO FRAMES"
                    : (frame + 1) + " / " + count + "   " + FormatClock(shownTime);
            }
        }

        private void ApplyTexture(Texture texture)
        {
            if (display != null)
            {
                display.texture = texture;
            }

            // null は MaterialPropertyBlock に渡せないため、空になった場合は直前の画像を残します。
            if (displayRenderer != null && texture != null)
            {
                if (block == null)
                {
                    block = new MaterialPropertyBlock();
                }

                displayRenderer.GetPropertyBlock(block);
                block.SetTexture(texturePropertyName, texture);
                displayRenderer.SetPropertyBlock(block);
            }
        }

        private void RefreshThumbnails(int count)
        {
            if (thumbnails == null)
            {
                return;
            }

            for (int i = 0; i < thumbnails.Length; i++)
            {
                RawImage thumbnail = thumbnails[i];
                if (thumbnail == null)
                {
                    continue;
                }

                int frame = ThumbnailFrame(i, thumbnails.Length, count);
                thumbnail.texture = recorder.GetFrame(frame);
                thumbnail.enabled = frame >= 0;
            }
        }

        private void RefreshStatus(int count)
        {
            if (statusLabel == null)
            {
                return;
            }

            string state = recorder.IsRecording() ? "REC" : (recorder.IsFull() ? "FULL" : "PAUSED");
            string mode = following ? "LATEST" : (playing ? "PLAY x" + playbackRate : "HOLD");

            statusLabel.text = state + "  " + FormatClock(recorder.GetElapsed())
                + "   " + count + " / " + recorder.GetCapacity() + " frames"
                + "   every " + Mathf.Round((float)recorder.GetCurrentInterval() * 10f) / 10f + " s"
                + "   " + Mathf.RoundToInt(recorder.GetAllocatedMegabytes()) + " MB"
                + "\n" + mode;
        }
    }
}
