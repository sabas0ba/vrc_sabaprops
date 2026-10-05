using UdonSharp;
using UnityEngine;
using VRC.SDK3.UdonNetworkCalling;
using VRC.SDKBase;
using VRC.Udon.Common.Interfaces;

namespace SabaProps.Liquid
{
    /// <summary>
    /// 向けた先の面に描くツール。ペン、スタンプ、消しゴムです。
    /// <para>
    /// 先端（tip）から前方へ光線を飛ばし、最初に当たったターゲット（ワールドの面、マネキン、プレイヤー）に描きます。
    /// ペンは使用ボタンを押している間、当たった点の動きに沿って線を引きます。
    /// スタンプは使用ボタンを押すたびに形（四角、星、ハート、輪、円）を 1 つ置きます。
    /// 消しゴムは使用ボタンを押している間、当たった所の顔料と液を消します。
    /// </para>
    /// <para>
    /// 描画は 1 区間（始点と終点）ごとにイベントで全員へ送り、各クライアントが同じ形を自分の Canvas に描きます。
    /// 太さ、形、液体は受け取った側のツールの設定を使うため、イベントで変えられるのは位置だけです。
    /// 位置はターゲット基準の座標で送ります。ワールドの面に描いた分は、プールに描画の履歴
    /// （LiquidPaintLog）があればそこへ記録され、後から入った人にも伝わります。
    /// </para>
    /// <para>
    /// Pickup に付ける場合は、VRCObjectSync と干渉しないよう、このツールを子の GameObject に置き、
    /// 使用ボタンのイベントを LiquidButton で中継します（Prefab はそうしています）。
    /// 持っている本人の画面でだけ、当たっている点に目印（marker）を表示します。
    /// </para>
    /// </summary>
    [AddComponentMenu("SabaProps/Liquid/Paint Tool")]
    [UdonBehaviourSyncMode(BehaviourSyncMode.NoVariableSync)]
    public partial class LiquidPaintTool : UdonSharpBehaviour
    {
        public const int ModePen = 0;
        public const int ModeStamp = 1;
        public const int ModeEraser = 2;

        /// <summary>描画イベントの送信上限（回/秒）。</summary>
        public const int MaxStrokesPerSecond = 20;

        [Header("構成")]
        [Tooltip("ターゲットを探すプール。未設定なら名前で探します。")]
        public LiquidCanvasPool pool;

        [Tooltip("描く液体の定義。消しゴムでは使いません。")]
        public LiquidProfile profile;

        [Tooltip("先端。前方（+Z）へ光線を飛ばします。未設定ならこの Transform です。")]
        public Transform tip;

        [Tooltip("持ち運ぶ場合の Pickup。設定すると、自分が持っている間だけ働きます。")]
        public VRC_Pickup pickup;

        [Tooltip("当たっている点に置く目印。持っている本人にだけ表示します。")]
        public Transform marker;

        [Header("描き方")]
        [Tooltip("0: ペン、1: スタンプ、2: 消しゴム。")]
        public int mode = ModePen;

        [Tooltip("スタンプの形。1: 円、2: 四角、3: 星、4: ハート、5: 輪。")]
        public int shape = LiquidBodyCanvas.ShapeStar;

        [Tooltip("線の太さの半分、またはスタンプと消しゴムの半径（m）。")]
        [Min(0.005f)]
        public float radius = 0.02f;

        [Tooltip("届く距離（m）。")]
        [Min(0.1f)]
        public float range = 4f;

        [Tooltip("他のプレイヤーの体にも描くか。無効ならワールドの面とマネキンだけに描きます。")]
        public bool hitPlayers = true;

        [Tooltip("描画の履歴でのこのツールの番号。履歴の tools の並びと一致させます。負なら履歴に記録しません。")]
        public int toolIndex = -1;

        private bool _drawing;
        private bool _hasLast;
        private int _lastTarget = -1;
        private Vector3 _lastLocal;
        private float _nextSample;
        private int _strokesReceived;

        private void Start()
        {
            if (tip == null)
            {
                tip = transform;
            }

            if (pool == null)
            {
                GameObject found = GameObject.Find(LiquidCanvasPool.DefaultName);
                if (found != null)
                {
                    pool = found.GetComponent<LiquidCanvasPool>();
                }
            }

            ShowMarker(false);
        }

        /// <summary>受け取った描画の数。テストと動作確認に使います。</summary>
        public int GetStrokesReceived()
        {
            return _strokesReceived;
        }

        /// <summary>使用ボタンを押したとき。ペンと消しゴムは描き始め、スタンプは 1 つ置きます。</summary>
        public void Trigger()
        {
            _drawing = true;
            _hasLast = false;
            _nextSample = 0f;

            if (mode == ModeStamp && IsInHand() && Aim())
            {
                Vector3 local = pool.WorldToTarget(_aimTarget, pool.lastHitPoint);
                Send(_aimTarget, local, local);
            }
        }

        /// <summary>使用ボタンを離したとき、または手放したとき。</summary>
        public void Release()
        {
            _drawing = false;
            _hasLast = false;
        }

        private void Update()
        {
            if (pool == null || !IsInHand())
            {
                _drawing = _drawing && pickup == null;
                ShowMarker(false);
                return;
            }

            bool aimed = Aim();
            ShowMarker(aimed);
            if (aimed && marker != null)
            {
                marker.SetPositionAndRotation(pool.lastHitPoint + pool.lastHitNormal * 0.004f,
                    Quaternion.LookRotation(pool.lastHitNormal));
            }

            if (!_drawing || mode == ModeStamp)
            {
                return;
            }

            if (!aimed)
            {
                _hasLast = false;
                return;
            }

            if (Time.time < _nextSample)
            {
                return;
            }

            int target = _aimTarget;
            Vector3 local = pool.WorldToTarget(target, pool.lastHitPoint);
            bool same = _hasLast && _lastTarget == target;

            // 動いていない間は送りません。同じ所を塗り重ねても見た目は変わらないためです。
            if (same && (local - _lastLocal).magnitude < radius * 0.3f)
            {
                return;
            }

            Send(target, StrokeStart(_hasLast, same, _lastLocal, local, MaxSegment), local);
            _hasLast = true;
            _lastTarget = target;
            _lastLocal = local;
            _nextSample = Time.time + 1f / (MaxStrokesPerSecond - 4);
        }

        // Aim の結果。Udon ではメソッドから複数の値を返せないため、フィールドで受け渡します。
        private int _aimTarget;

        /// <summary>先端の向く先のターゲットを探します。当たれば true。当たった点と法線はプールの lastHit に入ります。</summary>
        private bool Aim()
        {
            // 自分の体には描きません。先端は自分の手の中にあり、手や体の判定に最初に当たるためです。
            _aimTarget = pool.CastTargets(tip.position, tip.forward, range, false);
            if (_aimTarget == -1 || (!hitPlayers && _aimTarget >= 0))
            {
                return false;
            }

            return true;
        }

        /// <summary>このクライアントで操作できる状態か。Pickup なら、自分が持っている間だけです。</summary>
        private bool IsInHand()
        {
            if (pickup == null)
            {
                return true;
            }

            return pickup.IsHeld && Networking.IsOwner(pickup.gameObject);
        }

        private void ShowMarker(bool show)
        {
            if (marker != null && marker.gameObject.activeSelf != show)
            {
                marker.gameObject.SetActive(show);
            }
        }

        private void Send(int target, Vector3 from, Vector3 to)
        {
            Vector3 normal = pool.WorldToTargetDirection(target, pool.lastHitNormal);
            Vector3 forward = pool.WorldToTargetDirection(target, tip.forward);
            int code = PackStroke(normal, mode == ModeStamp ? ShapeAngle(normal, forward) : 0f);
            SendCustomNetworkEvent(NetworkEventTarget.All, nameof(ReceiveStroke), target, from, to, code);
        }

        /// <summary>
        /// 描画を受け取り、ターゲットの Canvas に描きます。ワールドの面に描いた分は履歴に記録します。
        /// </summary>
        [NetworkCallable(MaxStrokesPerSecond)]
        public void ReceiveStroke(int target, Vector3 from, Vector3 to, int code)
        {
            if (!Utilities.IsValid(NetworkCalling.CallingPlayer) || pool == null)
            {
                return;
            }

            if ((to - from).magnitude > MaxSegment * 1.5f)
            {
                from = to;
            }

            if (!Draw(target, from, to, code))
            {
                return;
            }

            _strokesReceived++;
            if (toolIndex >= 0 && pool.paintLog != null && pool.IsSurfaceTarget(target))
            {
                pool.paintLog.Record(toolIndex, target, from, to, code);
            }
        }

        /// <summary>
        /// 1 区間をターゲットの Canvas に描きます。位置はターゲット基準、code は PackStroke の符号です。
        /// 描画の履歴の再生もこれを呼びます。
        /// </summary>
        public bool Draw(int target, Vector3 from, Vector3 to, int code)
        {
            if (pool == null || (mode != ModeEraser && profile == null))
            {
                return false;
            }

            LiquidBodyCanvas canvas = pool.CanvasForTarget(target);
            if (canvas == null)
            {
                return false;
            }

            Vector3 normal = pool.TargetToWorldDirection(target, UnpackDirection(code));
            bool erasing = mode == ModeEraser;
            canvas.QueueShape(pool.TargetToWorld(target, from), pool.TargetToWorld(target, to), normal, radius,
                erasing ? null : profile, mode == ModeStamp ? shape : LiquidBodyCanvas.ShapeStroke, StrokeAngle(code),
                erasing ? 1f : 0f);
            return true;
        }
    }
}
