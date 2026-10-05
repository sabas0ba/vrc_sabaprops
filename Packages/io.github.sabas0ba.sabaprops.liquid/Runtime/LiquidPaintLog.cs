using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

namespace SabaProps.Liquid
{
    /// <summary>
    /// ワールドの面への描画（ペン、スタンプ、消しゴム）の履歴。後から入った人へ直近の描画を伝えます。
    /// <para>
    /// 描画そのものはツールがイベントで全員へ送り、その場にいる全員が自分の Canvas に描きます。
    /// Canvas の内容（RenderTexture）は同期できないため、後から入った人には何も見えません。
    /// そこで全員が受け取った描画を直近 capacity 件まで覚えておき、所有者がそれを同期変数として渡します。
    /// 受け取った側は、最初の 1 回だけ、古い順に描き直します。
    /// </para>
    /// <para>
    /// 同期変数へ写すのは送る直前（OnPreSerialization）だけで、描くたびには同期しません。
    /// 送るのは人が入ったときで、1 件は 28 バイト（位置 2 つと符号 1 つ）です。
    /// 履歴は全員が持っているため、所有者が退出しても、引き継いだ人がそのまま渡せます。
    /// </para>
    /// <para>
    /// 再生できるのは描画だけです。シャワーや雨など Source による付着、流下や乾燥の途中の状態は伝わりません。
    /// 入った直後に届いた描画と履歴の再生は順序が前後することがあり、重なった所の上下が
    /// 他の人と異なって見えることがあります。
    /// </para>
    /// </summary>
    [AddComponentMenu("SabaProps/Liquid/Paint Log")]
    [UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
    public partial class LiquidPaintLog : UdonSharpBehaviour
    {
        /// <summary>1 フレームに描き直す件数。Canvas は 16 件ごとに更新を 1 回進めるため、その倍数にします。</summary>
        public const int ReplayPerFrame = 32;

        [Tooltip("描画の相手を探すプール。未設定なら名前で探します。")]
        public LiquidCanvasPool pool;

        [Tooltip("履歴に記録するツール。並びの番号が各ツールの toolIndex と一致している必要があります。")]
        public LiquidPaintTool[] tools;

        [Tooltip("覚えておく描画の件数。これを超えた分は古いものから忘れます。")]
        [Range(16, 4000)]
        public int capacity = 1000;

        [UdonSynced] private Vector3[] _syncFrom = new Vector3[0];
        [UdonSynced] private Vector3[] _syncTo = new Vector3[0];
        [UdonSynced] private int[] _syncEntry = new int[0];

        // このクライアントが受け取った描画。環状に持ちます。
        private Vector3[] _from;
        private Vector3[] _to;
        private int[] _entry;
        private int _next;
        private int _count;

        private bool _received;
        private bool _replaying;
        private int _replayIndex;
        private float _replayAt;
        private int _replayed;

        private void Start()
        {
            EnsureStorage();
            if (pool == null)
            {
                GameObject found = GameObject.Find(LiquidCanvasPool.DefaultName);
                if (found != null)
                {
                    pool = found.GetComponent<LiquidCanvasPool>();
                }
            }
        }

        private void EnsureStorage()
        {
            if (_entry != null)
            {
                return;
            }

            int size = Mathf.Max(capacity, 16);
            _from = new Vector3[size];
            _to = new Vector3[size];
            _entry = new int[size];
        }

        /// <summary>覚えている描画の件数。</summary>
        public int GetCount()
        {
            return _count;
        }

        /// <summary>履歴から描き直した件数。テストと動作確認に使います。</summary>
        public int GetReplayedCount()
        {
            return _replayed;
        }

        /// <summary>描画を 1 件記録します。ツールが、ワールドの面への描画を受け取るたびに呼びます。</summary>
        public void Record(int tool, int target, Vector3 from, Vector3 to, int strokeCode)
        {
            if (pool == null || tool < 0 || tool >= MaxTools)
            {
                return;
            }

            int surface = pool.GetSurfaceTarget(0) - target;
            if (surface < 0 || surface >= MaxSurfaces)
            {
                return;
            }

            EnsureStorage();
            _from[_next] = from;
            _to[_next] = to;
            _entry[_next] = PackEntry(strokeCode, tool, surface);
            _next = (_next + 1) % _entry.Length;
            _count = Mathf.Min(_count + 1, _entry.Length);
        }

        /// <summary>履歴を捨てます。ワールドの面を消去したときにプールが呼びます。</summary>
        public void Forget()
        {
            _next = 0;
            _count = 0;
            _replaying = false;
        }

        public override void OnPlayerJoined(VRCPlayerApi player)
        {
            if (Utilities.IsValid(player) && !player.isLocal && Networking.IsOwner(gameObject))
            {
                RequestSerialization();
            }
        }

        public override void OnPreSerialization()
        {
            EnsureStorage();
            _syncFrom = new Vector3[_count];
            _syncTo = new Vector3[_count];
            _syncEntry = new int[_count];
            for (int i = 0; i < _count; i++)
            {
                int index = RingIndex(_next, _count, _entry.Length, i);
                _syncFrom[i] = _from[index];
                _syncTo[i] = _to[index];
                _syncEntry[i] = _entry[index];
            }
        }

        public override void OnDeserialization()
        {
            // その場にいた人は、同じ描画をイベントで受け取り済みです。描き直すのは最初の 1 回だけにします。
            if (_received)
            {
                return;
            }

            _received = true;
            if (_syncEntry == null || _syncFrom == null || _syncTo == null)
            {
                return;
            }

            EnsureStorage();
            int count = Mathf.Min(_syncEntry.Length, Mathf.Min(_syncFrom.Length, _syncTo.Length));
            int skip = Mathf.Max(0, count - _entry.Length);
            _next = 0;
            _count = 0;
            for (int i = skip; i < count; i++)
            {
                _from[_next] = _syncFrom[i];
                _to[_next] = _syncTo[i];
                _entry[_next] = _syncEntry[i];
                _next = (_next + 1) % _entry.Length;
                _count++;
            }

            // Canvas が始まる前に届くことがあるため、少し待ってから描き直します。
            _replaying = _count > 0;
            _replayIndex = 0;
            _replayAt = Time.time + 1f;
        }

        private void Update()
        {
            if (!_replaying || Time.time < _replayAt || pool == null)
            {
                return;
            }

            int end = Mathf.Min(_replayIndex + ReplayPerFrame, _count);
            for (int i = _replayIndex; i < end; i++)
            {
                Replay(RingIndex(_next, _count, _entry.Length, i));
            }

            _replayIndex = end;
            if (_replayIndex >= _count)
            {
                _replaying = false;
            }
        }

        private void Replay(int index)
        {
            int entry = _entry[index];
            int tool = EntryTool(entry);
            if (tools == null || tool >= tools.Length || tools[tool] == null)
            {
                return;
            }

            if (tools[tool].Draw(pool.GetSurfaceTarget(EntrySurface(entry)), _from[index], _to[index], EntryStroke(entry)))
            {
                _replayed++;
            }
        }
    }
}
