using UdonSharp;
using UnityEngine;
using VRC.SDK3.UdonNetworkCalling;
using VRC.SDKBase;
using VRC.Udon.Common.Interfaces;

namespace SabaProps.Liquid
{
    /// <summary>
    /// 遠距離の流下の Source。水鉄砲です。
    /// <para>
    /// 事象の評価を行います。命中は持っている人（所有者）のクライアントだけが判定し、
    /// 当たった位置を対象プレイヤーの足元と向きを基準にした座標で全員へ送ります。
    /// プレイヤーの位置はクライアントごとに少しずつ異なるため、ワールド座標で送ると
    /// 受信側で体から外れた位置になるためです。
    /// </para>
    /// <para>
    /// 受信側は、送り主がこの水鉄砲の所有者であることを確かめてから付着を積みます。
    /// 放水の開始と停止もイベントで送り、見た目の水流は各クライアントがそれを見て再生します。
    /// </para>
    /// <para>
    /// 同期変数は持ちません。使用ボタンのイベントを受けるため、この behaviour は Pickup と
    /// VRCObjectSync のある GameObject に置く必要があり、そこで Manual sync を使うと
    /// VRCObjectSync と干渉するためです。途中参加者に放水中の見た目が届かないことは許容します。
    /// </para>
    /// </summary>
    [AddComponentMenu("SabaProps/Liquid/Water Gun")]
    [UdonBehaviourSyncMode(BehaviourSyncMode.NoVariableSync)]
    public class LiquidWaterGun : UdonSharpBehaviour
    {
        /// <summary>命中イベントの送信上限（回/秒）。fireInterval の下限と対応させます。</summary>
        public const int MaxHitsPerSecond = 10;

        [Header("構成")]
        [Tooltip("Canvas を割り当て、命中を判定するプール。")]
        public LiquidCanvasPool pool;

        [Tooltip("出す液体の定義。")]
        public LiquidProfile profile;

        [Tooltip("銃口。前方（+Z）へ放水します。未設定ならこの Transform です。")]
        public Transform muzzle;

        [Tooltip("放水中だけ再生する見た目のパーティクル。命中の判定には使いません。")]
        public ParticleSystem stream;

        [Header("放水")]
        [Tooltip("届く距離（m）。")]
        [Min(0.1f)]
        public float range = 8f;

        [Tooltip("狙いのばらつき（円錐の半頂角、度）。")]
        [Range(0f, 15f)]
        public float spread = 2f;

        [Tooltip("命中を判定する間隔（秒）。1 / MaxHitsPerSecond 未満にはなりません。")]
        [Range(0.1f, 1f)]
        public float fireInterval = 0.1f;

        [Tooltip("1 回の命中で付く量。")]
        [Min(0f)]
        public float amountPerHit = 0.35f;

        [Tooltip("1 回の命中で付く範囲の半径（m）。")]
        [Min(0.01f)]
        public float hitRadius = 0.06f;

        private bool _firing;

        private float _nextFire;
        private int _shot;

        private void Start()
        {
            if (muzzle == null)
            {
                muzzle = transform;
            }

            ApplyVisual();
        }

        public override void OnPickupUseDown()
        {
            SetFiring(true);
        }

        public override void OnPickupUseUp()
        {
            SetFiring(false);
        }

        public override void OnDrop()
        {
            SetFiring(false);
        }

        private void SetFiring(bool firing)
        {
            if (!Networking.IsOwner(gameObject) || firing == _firing)
            {
                return;
            }

            _firing = firing;
            ApplyVisual();
            SendCustomNetworkEvent(NetworkEventTarget.Others, nameof(ReceiveFiring), firing);
        }

        /// <summary>放水の開始と停止を受け取り、見た目の水流を切り替えます。送り主が所有者でない場合は無視します。</summary>
        [NetworkCallable]
        public void ReceiveFiring(bool firing)
        {
            VRCPlayerApi sender = NetworkCalling.CallingPlayer;
            VRCPlayerApi owner = Networking.GetOwner(gameObject);
            if (!Utilities.IsValid(sender) || !Utilities.IsValid(owner) || sender.playerId != owner.playerId)
            {
                return;
            }

            _firing = firing;
            ApplyVisual();
        }

        private void ApplyVisual()
        {
            if (stream == null)
            {
                return;
            }

            if (_firing && !stream.isPlaying)
            {
                stream.Play();
            }
            else if (!_firing && stream.isPlaying)
            {
                stream.Stop();
            }
        }

        private void Update()
        {
            if (!_firing || pool == null || !Networking.IsOwner(gameObject))
            {
                return;
            }

            if (Time.time < _nextFire)
            {
                return;
            }

            _nextFire = Time.time + Mathf.Max(fireInterval, 1f / MaxHitsPerSecond);
            _shot++;

            Vector3 direction = pool.SampleCone(muzzle.forward, spread, _shot);
            int target = pool.CastTargets(muzzle.position, direction, range, false);
            if (target == -1)
            {
                return;
            }

            Vector3 localPoint = pool.WorldToTarget(target, pool.lastHitPoint);
            Vector3 localNormal = pool.WorldToTargetDirection(target, pool.lastHitNormal);
            SendCustomNetworkEvent(NetworkEventTarget.All, nameof(ReceiveHit), target, localPoint, localNormal, _shot);
        }

        /// <summary>
        /// 命中を受け取り、ターゲット（プレイヤーまたはマネキン）の Body Canvas へ付着を積みます。
        /// 送り主が所有者でない場合は無視します。
        /// </summary>
        [NetworkCallable(MaxHitsPerSecond)]
        public void ReceiveHit(int target, Vector3 localPoint, Vector3 localNormal, int shot)
        {
            VRCPlayerApi sender = NetworkCalling.CallingPlayer;
            VRCPlayerApi owner = Networking.GetOwner(gameObject);
            if (!Utilities.IsValid(sender) || !Utilities.IsValid(owner) || sender.playerId != owner.playerId)
            {
                return;
            }

            if (pool == null || profile == null)
            {
                return;
            }

            LiquidBodyCanvas canvas = pool.CanvasForTarget(target);
            if (canvas == null)
            {
                return;
            }

            Vector3 point = pool.TargetToWorld(target, localPoint);
            Vector3 normal = pool.TargetToWorldDirection(target, localNormal);
            canvas.QueueStamp(point, normal, hitRadius, profile, amountPerHit, pool.Random01(shot) * 100f);
        }
    }
}
