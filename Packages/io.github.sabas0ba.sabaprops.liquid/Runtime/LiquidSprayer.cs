using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

namespace SabaProps.Liquid
{
    /// <summary>
    /// 自動で液体をまき続ける Source。飛散・流下の常在 Source です。
    /// <para>
    /// 一定の間隔で、円錐状に液体の塊を放ちます。放つ時刻、方向の揺れ、ばらつきは
    /// すべてサーバー時刻から決めるため、全クライアントが同じ時刻に同じ方向へ放ち、
    /// 同じ相手に当てます。何も同期しません。プレイヤー位置の同期遅延の分だけ
    /// 当たり方がずれることは許容します。
    /// </para>
    /// <para>
    /// 用途は、操作しなくても液体の付き方が分かるデモと、ワールドの演出
    /// （噴水、雨どいからの滴り、塗料をまく装置など）です。
    /// </para>
    /// </summary>
    [AddComponentMenu("SabaProps/Liquid/Sprayer")]
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class LiquidSprayer : UdonSharpBehaviour
    {
        [Header("構成")]
        [Tooltip("Canvas を割り当て、命中を判定するプール。")]
        public LiquidCanvasPool pool;

        [Tooltip("まく液体の定義。")]
        public LiquidProfile profile;

        [Tooltip("放出口。前方（+Z）へ放ちます。未設定ならこの Transform です。")]
        public Transform nozzle;

        [Tooltip("放つたびに粒を出す見た目のパーティクル。命中の判定には使いません。")]
        public ParticleSystem stream;

        [Tooltip("1 回に出す見た目の粒の数。")]
        [Min(0)]
        public int particlesPerBurst = 40;

        [Header("放出")]
        [Tooltip("放つ間隔（秒）。")]
        [Min(0.05f)]
        public float burstInterval = 1.2f;

        [Tooltip("1 回に放つ塊の数。")]
        [Range(1, 16)]
        public int raysPerBurst = 8;

        [Tooltip("放つ広がり（円錐の半頂角、度）。")]
        [Range(0f, 60f)]
        public float coneAngle = 8f;

        [Tooltip("届く距離（m）。")]
        [Min(0.1f)]
        public float range = 4f;

        [Tooltip("1 つの塊が付く範囲の半径（m）。0.7〜1.3 倍のばらつきが付きます。")]
        [Min(0.01f)]
        public float hitRadius = 0.14f;

        [Tooltip("1 つの塊で付く量。")]
        [Min(0f)]
        public float amountPerHit = 0.7f;

        [Tooltip("プレイヤーにも当てるか。無効にするとマネキンだけに当てます。")]
        public bool hitPlayers = true;

        [Header("首振り")]
        [Tooltip("左右に首を振る角度（度）。0 で固定です。")]
        [Range(0f, 90f)]
        public float sweepAngle = 0f;

        [Tooltip("首振りの周期（秒）。")]
        [Min(0.5f)]
        public float sweepPeriod = 8f;

        [Tooltip("時刻のずれ（秒）。同じ設定の Sprayer を並べたとき、放つ時刻をずらすのに使います。")]
        public float phaseOffset = 0f;

        // 乱数の種の周期。サーバー時刻から作る放出番号は大きくなるため、種に使う前に畳みます。
        private const int SeedPeriod = 100000;

        private Quaternion _baseRotation;
        private int _lastBurst = int.MinValue;

        private void Start()
        {
            if (nozzle == null)
            {
                nozzle = transform;
            }

            _baseRotation = nozzle.localRotation;
        }

        private void Update()
        {
            if (pool == null || profile == null)
            {
                return;
            }

            double time = Networking.GetServerTimeInSeconds() + phaseOffset;
            ApplySweep(time);

            int burst = (int)(time / burstInterval % int.MaxValue);
            if (burst == _lastBurst)
            {
                return;
            }

            bool first = _lastBurst == int.MinValue;
            _lastBurst = burst;
            if (!first)
            {
                Fire(burst % SeedPeriod);
            }
        }

        private void ApplySweep(double time)
        {
            if (sweepAngle <= 0f)
            {
                return;
            }

            float phase = (float)(time % sweepPeriod) / sweepPeriod;
            float yaw = sweepAngle * Mathf.Sin(phase * 2f * Mathf.PI);
            nozzle.localRotation = _baseRotation * Quaternion.Euler(0f, yaw, 0f);
        }

        private void Fire(int seed)
        {
            if (stream != null && particlesPerBurst > 0)
            {
                stream.Emit(particlesPerBurst);
            }

            Vector3 origin = nozzle.position;
            Vector3 axis = nozzle.forward;

            for (int r = 0; r < raysPerBurst; r++)
            {
                int sample = seed * raysPerBurst + r;
                Vector3 direction = pool.SampleCone(axis, coneAngle, sample);
                int target = pool.CastTargets(origin, direction, range, hitPlayers);
                if (target == -1 || (!hitPlayers && target >= 0))
                {
                    continue;
                }

                LiquidBodyCanvas canvas = pool.CanvasForTarget(target);
                if (canvas == null)
                {
                    continue;
                }

                float size = hitRadius * (0.7f + 0.6f * pool.Random01(sample + 7919));
                canvas.QueueStamp(pool.lastHitPoint, pool.lastHitNormal, size, profile, amountPerHit,
                    pool.Random01(sample) * 100f);
            }
        }
    }
}
