using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

namespace SabaProps.Liquid
{
    /// <summary>
    /// 流下の Source。固定シャワー、水道、手に持つシャワーヘッドです。
    /// <para>
    /// 場の評価を行います。ノズルから円錐状に光線を出し、当たったプレイヤーの
    /// Body Canvas へ、着水点の高さより下の濡れと洗浄を与えます。水は着いた点から
    /// 体を伝って流れ落ちるためです。着水点には飛沫の付着も残します。
    /// 差し出した手に当たった場合は、手に付着を残すだけです。
    /// </para>
    /// <para>
    /// 同期するのは放水しているかどうかだけです。光線の方向はサーバー時刻から求めた
    /// 評価周期の番号で作るため、当たり方はクライアント間でおおむね一致しますが、
    /// プレイヤー位置の同期遅延の分だけずれることは許容します。
    /// </para>
    /// </summary>
    [AddComponentMenu("SabaProps/Liquid/Shower")]
    [UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
    public class LiquidShower : UdonSharpBehaviour
    {
        [Header("構成")]
        [Tooltip("Canvas を割り当て、命中を判定するプール。")]
        public LiquidCanvasPool pool;

        [Tooltip("出す液体の定義。通常は水です。")]
        public LiquidProfile profile;

        [Tooltip("放出口。前方（+Z）へ放出します。未設定ならこの Transform です。")]
        public Transform nozzle;

        [Tooltip("放水中だけ再生する見た目のパーティクル。命中の判定には使いません。")]
        public ParticleSystem stream;

        [Header("放水")]
        [Tooltip("届く距離（m）。")]
        [Min(0.1f)]
        public float range = 2.5f;

        [Tooltip("放水の広がり（円錐の半頂角、度）。")]
        [Range(0f, 60f)]
        public float coneAngle = 12f;

        [Tooltip("1 回の評価で出す光線の数。")]
        [Range(1, 16)]
        public int raysPerEvaluation = 6;

        [Tooltip("着水点に残す飛沫の半径（m）。")]
        [Min(0.01f)]
        public float splashRadius = 0.07f;

        [Tooltip("着水点に残す飛沫の量（1 秒あたり、光線 1 本がすべて当たった場合）。")]
        [Min(0f)]
        public float splashPerSecond = 2f;

        [Header("操作")]
        [Tooltip("開始時に放水しているか。")]
        public bool startRunning = true;

        [Tooltip("Interact で放水を切り替えられるか。固定シャワーと水道で使います。")]
        public bool toggleOnInteract = true;

        [Tooltip("Pickup の使用ボタンを押している間だけ放水するか。手に持つシャワーヘッドで使います。")]
        public bool runWhileUsed = false;

        [Header("評価")]
        [Tooltip("放水を評価する間隔（秒）。")]
        [Range(0.05f, 1f)]
        public float evaluationInterval = 0.1f;

        [UdonSynced] private bool _running;

        private float _lastEvaluation;

        private void Start()
        {
            if (nozzle == null)
            {
                nozzle = transform;
            }

            if (Networking.IsOwner(gameObject))
            {
                _running = startRunning && !runWhileUsed;
                RequestSerialization();
            }

            _lastEvaluation = Time.time;
            ApplyVisual();
        }

        /// <summary>放水しているかどうか。</summary>
        public bool IsRunning()
        {
            return _running;
        }

        /// <summary>放水を切り替えます。所有権を取ってから状態を同期します。</summary>
        public void Toggle()
        {
            SetRunning(!_running);
        }

        public override void Interact()
        {
            if (toggleOnInteract)
            {
                Toggle();
            }
        }

        public override void OnPickupUseDown()
        {
            if (runWhileUsed)
            {
                SetRunning(true);
            }
        }

        public override void OnPickupUseUp()
        {
            if (runWhileUsed)
            {
                SetRunning(false);
            }
        }

        public override void OnDrop()
        {
            if (runWhileUsed)
            {
                SetRunning(false);
            }
        }

        public override void OnDeserialization()
        {
            ApplyVisual();
        }

        private void SetRunning(bool running)
        {
            if (!Networking.IsOwner(gameObject))
            {
                Networking.SetOwner(Networking.LocalPlayer, gameObject);
            }

            _running = running;
            RequestSerialization();
            ApplyVisual();
        }

        private void ApplyVisual()
        {
            if (stream == null)
            {
                return;
            }

            if (_running && !stream.isPlaying)
            {
                stream.Play();
            }
            else if (!_running && stream.isPlaying)
            {
                stream.Stop();
            }
        }

        private void Update()
        {
            if (!_running || pool == null || profile == null)
            {
                _lastEvaluation = Time.time;
                return;
            }

            float elapsed = Time.time - _lastEvaluation;
            if (elapsed < evaluationInterval)
            {
                return;
            }

            _lastEvaluation = Time.time;
            Vector3 origin = nozzle.position;
            Vector3 axis = nozzle.forward;
            float share = elapsed / raysPerEvaluation;
            int periodMs = Mathf.Max(1, Mathf.RoundToInt(evaluationInterval * 1000f));
            int tick = Networking.GetServerTimeInMilliseconds() / periodMs;

            for (int r = 0; r < raysPerEvaluation; r++)
            {
                int sample = tick * raysPerEvaluation + r;
                Vector3 direction = pool.SampleCone(axis, coneAngle, sample);
                int playerId = pool.CastPlayers(origin, direction, range, true);
                if (playerId < 0)
                {
                    continue;
                }

                VRCPlayerApi player = VRCPlayerApi.GetPlayerById(playerId);
                LiquidBodyCanvas canvas = pool.AcquireCanvas(player);
                if (canvas == null)
                {
                    continue;
                }

                Vector3 point = pool.lastHitPoint;

                // 体に当たった水は伝い落ちて下を濡らします。差し出した手に当たった水は
                // 体を伝わらないため、手に飛沫を残すだけにします。
                if (!pool.lastHitHand)
                {
                    canvas.ApplyImmersion(point.y, profile, share);
                }

                canvas.QueueStamp(point, pool.lastHitNormal, splashRadius, profile, splashPerSecond * share,
                    pool.Random01(sample) * 100f);
            }
        }
    }
}
