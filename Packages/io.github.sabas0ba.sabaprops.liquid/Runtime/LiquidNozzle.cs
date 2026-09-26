using UdonSharp;
using UnityEngine;
using UnityEngine.UI;
using VRC.SDK3.UdonNetworkCalling;
using VRC.SDKBase;
using VRC.Udon.Common.Interfaces;

namespace SabaProps.Liquid
{
    /// <summary>
    /// 液体を放つ汎用のノズル。飛散と流下の Source です。
    /// <para>
    /// 放ち方は 4 通りです。
    /// 1 回（OneShot）：Interact や使用ボタンで、設定した量を一度に放ちます。コップやバケツもこれです。
    /// 定期（Periodic）：サーバー時刻に合わせて、設定した間隔で放ちます。
    /// 連続（Continuous）：切り替えで出し続け、1 秒あたり設定した量を放ちます。
    /// 押す間（Hold）：使用ボタンを押している間だけ出し続けます。手に持つ放水具に使います。
    /// </para>
    /// <para>
    /// 量（L）、届く距離（m）、速さ（m/s）、断面の直径（m）を持ち、操作盤から変えられます。
    /// 液は放物線で飛び、命中はその放物線を区間に分けた光線で判定します。遅いほど手前に落ち、
    /// 届くまでの時間も長くなります。断面は付く範囲の広さと、放つ向きのばらつきに効きます。
    /// </para>
    /// <para>
    /// 同期は設定と放出中かどうかだけです（Manual）。1 回の放出は、放出の番号をイベントで全員へ送り、
    /// 各クライアントがその番号を乱数の種として同じ向きに光線を放ちます。定期の放出はサーバー時刻が種です。
    /// 命中の判定は各クライアントで行うため、プレイヤー位置の同期遅延の分だけ当たり方がずれることは許容します。
    /// </para>
    /// <para>
    /// Pickup と同じ GameObject に置く場合は、VRCObjectSync と干渉しないよう、ノズルを子の GameObject に
    /// 置いてください（Prefab のコップとバケツはそうしています）。使用ボタン、離したとき、手放したときの
    /// イベントは Pickup の GameObject にしか届かないため、LiquidButton で中継します。
    /// </para>
    /// <para>
    /// 持ち運ぶ場合は pickup を設定します。押す間の放出中に手放された（持ち主が退出した場合を含む）ことを
    /// 所有者が見つけると、放出を止めます。
    /// </para>
    /// </summary>
    [AddComponentMenu("SabaProps/Liquid/Nozzle")]
    [UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
    public partial class LiquidNozzle : UdonSharpBehaviour
    {
        public const int ModeOneShot = 0;
        public const int ModePeriodic = 1;
        public const int ModeContinuous = 2;
        public const int ModeHold = 3;

        /// <summary>1 回の放出イベントの送信上限（回/秒）。</summary>
        public const int MaxShotsPerSecond = 5;

        [Header("構成")]
        [Tooltip("Canvas を割り当て、命中を判定するプール。未設定なら名前で探します。")]
        public LiquidCanvasPool pool;

        [Tooltip("放つ液体の定義。")]
        public LiquidProfile profile;

        [Tooltip("放出口。前方（+Z）へ放ちます。未設定ならこの Transform です。")]
        public Transform nozzle;

        [Tooltip("見た目のパーティクル。1 回と定期では放出のたびに粒を出し、連続では出している間だけ再生します。")]
        public ParticleSystem stream;

        [Tooltip("1 L あたりに出す見た目の粒の数（1 回と定期）。")]
        [Min(0f)]
        public float particlesPerLitre = 60f;

        [Tooltip("設定を表示する文字（UI の Text）。未設定なら表示しません。TextMesh の文字は Udon から変えられません。")]
        public Text readout;

        [Header("放ち方")]
        [Tooltip("0: 1 回, 1: 定期, 2: 連続（切り替え）, 3: 押す間。")]
        [Range(0, 3)]
        public int mode = ModeOneShot;

        [Tooltip("持ち運ぶ場合の Pickup。押す間の放出中に手放されたら止めます。")]
        public VRC_Pickup pickup;

        [Tooltip("定期の間隔（秒）。")]
        [Min(0.2f)]
        public float period = 2f;

        [Tooltip("定期の時刻のずれ（秒）。")]
        public float phaseOffset = 0f;

        [Tooltip("Interact で放つか。Pickup の場合は使用ボタンで放ちます。")]
        public bool fireOnInteract = true;

        [Header("設定（操作盤で変えられます）")]
        [Tooltip("量（L）。1 回と定期では 1 回の量、連続では 1 秒あたりの量です。")]
        [Range(0.05f, 10f)]
        public float volume = 0.5f;

        [Tooltip("届く距離（m）。")]
        [Range(0.5f, 12f)]
        public float range = 4f;

        [Tooltip("速さ（m/s）。")]
        [Range(1f, 30f)]
        public float speed = 8f;

        [Tooltip("断面の直径（m）。")]
        [Range(0.01f, 0.6f)]
        public float diameter = 0.08f;

        [Tooltip("プレイヤーにも当てるか。")]
        public bool hitPlayers = true;

        [Header("連続")]
        [Tooltip("連続放出で命中を評価する間隔（秒）。")]
        [Min(0.05f)]
        public float evaluationInterval = 0.1f;

        [UdonSynced] private float _volume;
        [UdonSynced] private float _range;
        [UdonSynced] private float _speed;
        [UdonSynced] private float _diameter;
        [UdonSynced] private bool _running;
        private bool _synced;

        // 放出から命中までの遅れの間、命中を預かっておく。ターゲット基準の座標で持ち、届いた時点の体に付けます。
        private const int MaxPending = 48;
        private float[] _pendingTime = new float[MaxPending];
        private int[] _pendingTarget = new int[MaxPending];
        private Vector3[] _pendingPoint = new Vector3[MaxPending];
        private Vector3[] _pendingNormal = new Vector3[MaxPending];
        private float[] _pendingRadius = new float[MaxPending];
        private float[] _pendingAmount = new float[MaxPending];
        private float[] _pendingSeed = new float[MaxPending];
        private int _pendingCount;

        private const int SeedPeriod = 100000;
        private int _lastPeriod = int.MinValue;
        private float _lastEvaluation;
        private float _lastRequest = -10f;
        private int _shotsReceived;
        private int _raysCast;
        private int _raysHit;
        private int _stampsDelivered;
        private bool _wasHeld;

        private void Start()
        {
            if (nozzle == null)
            {
                nozzle = transform;
            }

            if (pool == null)
            {
                GameObject found = GameObject.Find(LiquidCanvasPool.DefaultName);
                if (found != null)
                {
                    pool = found.GetComponent<LiquidCanvasPool>();
                }
            }

            if (!_synced)
            {
                _volume = volume;
                _range = range;
                _speed = speed;
                _diameter = diameter;
            }

            ApplySettings();
        }

        public override void OnDeserialization()
        {
            _synced = true;
            ApplySettings();
        }

        // ------------------------------------------------------------------
        // 操作
        // ------------------------------------------------------------------

        public override void Interact()
        {
            if (fireOnInteract)
            {
                Trigger();
            }
        }

        public override void OnPickupUseDown()
        {
            Trigger();
        }

        public override void OnPickupUseUp()
        {
            Release();
        }

        /// <summary>放ち方に応じて、1 回放つ、連続放出を切り替える、または押す間の放出を始めます。</summary>
        public void Trigger()
        {
            if (mode == ModeContinuous)
            {
                Toggle();
            }
            else if (mode == ModeOneShot)
            {
                Fire();
            }
            else if (mode == ModeHold)
            {
                StartFiring();
            }
        }

        /// <summary>使用ボタンを離したとき。押す間の放出を止めます。</summary>
        public void Release()
        {
            if (mode == ModeHold)
            {
                StopFiring();
            }
        }

        /// <summary>出し続けるのを始めます（連続と押す間）。</summary>
        public void StartFiring()
        {
            SetRunning(true);
        }

        /// <summary>出し続けるのを止めます（連続と押す間）。手放したときにも呼びます。</summary>
        public void StopFiring()
        {
            SetRunning(false);
        }

        /// <summary>1 回ぶんを全員の画面で放ちます。</summary>
        public void Fire()
        {
            if (Time.time - _lastRequest < 1f / MaxShotsPerSecond)
            {
                return;
            }

            _lastRequest = Time.time;
            SendCustomNetworkEvent(NetworkEventTarget.All, nameof(ReceiveShot),
                Networking.GetServerTimeInMilliseconds() % SeedPeriod);
        }

        [NetworkCallable(MaxShotsPerSecond)]
        public void ReceiveShot(int seed)
        {
            _shotsReceived++;
            Shoot(_volume, seed, true);
        }

        /// <summary>このクライアントが受け取った 1 回の放出の数。</summary>
        public int GetShotsReceived()
        {
            return _shotsReceived;
        }

        /// <summary>連続放出を切り替えます。</summary>
        public void Toggle()
        {
            SetRunning(!_running);
        }

        private void SetRunning(bool running)
        {
            if (_running == running)
            {
                return;
            }

            TakeOwnership();
            _running = running;
            RequestSerialization();
            ApplySettings();
        }

        /// <summary>出し続けているか（連続と押す間）。</summary>
        public bool IsRunning()
        {
            return (mode == ModeContinuous || mode == ModeHold) && _running;
        }

        public void VolumeUp() { ChangeVolume(1); }
        public void VolumeDown() { ChangeVolume(-1); }
        public void RangeUp() { ChangeRange(1); }
        public void RangeDown() { ChangeRange(-1); }
        public void SpeedUp() { ChangeSpeed(1); }
        public void SpeedDown() { ChangeSpeed(-1); }
        public void DiameterUp() { ChangeDiameter(1); }
        public void DiameterDown() { ChangeDiameter(-1); }

        private void ChangeVolume(int direction)
        {
            TakeOwnership();
            // 少量は細かく、大量は粗く刻みます。
            float step = _volume < 1f - 1e-3f || (_volume <= 1f + 1e-3f && direction < 0) ? 0.1f : 1f;
            _volume = Step(_volume, step, direction, 0.1f, 10f);
            Commit();
        }

        private void ChangeRange(int direction)
        {
            TakeOwnership();
            _range = Step(_range, 0.5f, direction, 0.5f, 12f);
            Commit();
        }

        private void ChangeSpeed(int direction)
        {
            TakeOwnership();
            _speed = Step(_speed, 1f, direction, 1f, 30f);
            Commit();
        }

        private void ChangeDiameter(int direction)
        {
            TakeOwnership();
            _diameter = Step(_diameter, 0.02f, direction, 0.02f, 0.6f);
            Commit();
        }

        private void Commit()
        {
            RequestSerialization();
            ApplySettings();
        }

        private void TakeOwnership()
        {
            if (!Networking.IsOwner(gameObject))
            {
                Networking.SetOwner(Networking.LocalPlayer, gameObject);
            }
        }

        private void ApplySettings()
        {
            if (stream != null)
            {
                ParticleSystem.MainModule main = stream.main;
                main.startSpeedMultiplier = _speed;
                main.startLifetimeMultiplier = Mathf.Clamp(_range / Mathf.Max(_speed, 0.1f) * 1.3f, 0.15f, 3f);
                ParticleSystem.ShapeModule shape = stream.shape;
                shape.angle = ConeAngleFor(_diameter, _range);
                shape.radius = Mathf.Max(_diameter * 0.5f, 0.005f);

                bool continuous = IsRunning();
                if (mode == ModeContinuous || mode == ModeHold)
                {
                    if (continuous && !stream.isPlaying)
                    {
                        stream.Play();
                    }
                    else if (!continuous && stream.isPlaying)
                    {
                        stream.Stop();
                    }
                }
            }

            if (readout != null)
            {
                bool flowing = mode == ModeContinuous || mode == ModeHold;
                string unit = flowing ? " L/s" : " L";
                readout.text = "Volume " + _volume.ToString("0.0") + unit
                    + "\nRange " + _range.ToString("0.0") + " m"
                    + "\nSpeed " + _speed.ToString("0") + " m/s"
                    + "\nNozzle " + (_diameter * 100f).ToString("0") + " cm"
                    + (flowing ? (_running ? "\nRunning" : "\nStopped") : "");
            }
        }

        // ------------------------------------------------------------------
        // 放出
        // ------------------------------------------------------------------

        private void Update()
        {
            DeliverPending();
            WatchPickup();

            if (pool == null || profile == null)
            {
                return;
            }

            if (mode == ModePeriodic)
            {
                double time = Networking.GetServerTimeInSeconds() + phaseOffset;
                int index = (int)(time / period % int.MaxValue);
                if (index != _lastPeriod)
                {
                    bool first = _lastPeriod == int.MinValue;
                    _lastPeriod = index;
                    if (!first)
                    {
                        Shoot(_volume, index % SeedPeriod, true);
                    }
                }
            }
            else if (IsRunning())
            {
                float elapsed = Time.time - _lastEvaluation;
                if (elapsed < evaluationInterval)
                {
                    return;
                }

                _lastEvaluation = Time.time;
                int periodMs = Mathf.Max(1, Mathf.RoundToInt(evaluationInterval * 1000f));
                int tick = (Networking.GetServerTimeInMilliseconds() / periodMs) % SeedPeriod;
                Shoot(_volume * Mathf.Min(elapsed, 0.5f), tick, false);
            }
            else
            {
                _lastEvaluation = Time.time;
            }
        }

        /// <summary>
        /// 押す間の放出中に、持っていた人が手放したら止めます。所有者だけが見ます。
        /// 使用ボタンを離したイベントが届かない場合（持ったまま退出したなど）のためです。
        /// 一度も持たれていないノズルは止めません。
        /// </summary>
        private void WatchPickup()
        {
            if (pickup == null || mode != ModeHold || !Networking.IsOwner(gameObject))
            {
                return;
            }

            bool held = pickup.IsHeld;
            if (_wasHeld && !held && _running)
            {
                StopFiring();
            }

            _wasHeld = held;
        }

        /// <summary>
        /// volume（L）ぶんの光線を放ち、命中を届く時刻まで預けます。
        /// </summary>
        private void Shoot(float amount, int seed, bool emitParticles)
        {
            if (pool == null || profile == null || amount <= 0f)
            {
                return;
            }

            if (emitParticles && stream != null)
            {
                int particles = Mathf.Clamp(Mathf.RoundToInt(amount * particlesPerLitre), 1, 400);
                stream.Emit(particles);
            }

            Vector3 origin = nozzle.position;
            Vector3 axis = nozzle.forward;
            float cone = ConeAngleFor(_diameter, _range);
            float flight = FlightTime(_range, _speed);
            int rays = RaysFor(amount);
            float perRay = AmountPerRay(amount);

            for (int r = 0; r < rays; r++)
            {
                _raysCast++;
                int sample = seed * MaxRaysPerShot + r;
                Vector3 velocity = pool.SampleCone(axis, cone, sample) * _speed;
                if (!CastArc(origin, velocity, flight))
                {
                    continue;
                }

                int target = _arcTarget;
                float size = HitRadiusFor(_diameter, _arcDistance) * (0.75f + 0.5f * pool.Random01(sample + 7919));
                Hold(target, pool.lastHitPoint, pool.lastHitNormal, size, perRay, pool.Random01(sample) * 100f,
                    Time.time + _arcTime);
            }
        }

        // CastArc の結果。Udon ではメソッドから複数の値を返せないため、フィールドで受け渡します。
        private int _arcTarget;
        private float _arcDistance;
        private float _arcTime;

        /// <summary>
        /// 放物線を ArcSegments 本の区間に分けて、最初に当たるターゲットを探します。区間が環境
        /// （地面や壁）に当たったら、そこで止めます。当たれば true を返し、ターゲット、飛んだ距離、
        /// 届くまでの時間をフィールドに入れます。当たった点と法線はプールの lastHit に入ります。
        /// </summary>
        private bool CastArc(Vector3 origin, Vector3 velocity, float flight)
        {
            if (flight <= 0f)
            {
                return false;
            }

            float travelled = 0f;
            Vector3 from = origin;
            for (int s = 1; s <= ArcSegments; s++)
            {
                float t = flight * s / ArcSegments;
                Vector3 to = ArcPoint(origin, velocity, t);
                Vector3 step = to - from;
                float length = step.magnitude;
                if (length < 1e-5f)
                {
                    continue;
                }

                Vector3 direction = step / length;
                int target = pool.CastTargets(from, direction, length, hitPlayers);
                if (target != -1 && (hitPlayers || target < 0))
                {
                    _arcTarget = target;
                    _arcDistance = travelled + pool.lastHitDistance;
                    // 区間内の時間は、区間に沿った距離の割合で補います。
                    _arcTime = flight * (s - 1 + pool.lastHitDistance / length) / ArcSegments;
                    return true;
                }

                if (Physics.Raycast(from, direction, length, pool.occluderLayers, QueryTriggerInteraction.Ignore))
                {
                    return false;
                }

                travelled += length;
                from = to;
            }

            return false;
        }

        private void Hold(int target, Vector3 point, Vector3 normal, float radius, float amount, float seed, float due)
        {
            _raysHit++;
            if (_pendingCount >= MaxPending)
            {
                DeliverAll();
            }

            int i = _pendingCount;
            _pendingTarget[i] = target;
            _pendingPoint[i] = pool.WorldToTarget(target, point);
            _pendingNormal[i] = pool.WorldToTargetDirection(target, normal);
            _pendingRadius[i] = radius;
            _pendingAmount[i] = amount;
            _pendingSeed[i] = seed;
            _pendingTime[i] = due;
            _pendingCount = i + 1;
        }

        private void DeliverPending()
        {
            int i = 0;
            while (i < _pendingCount)
            {
                if (_pendingTime[i] <= Time.time)
                {
                    Deliver(i);
                    RemovePending(i);
                }
                else
                {
                    i++;
                }
            }
        }

        private void DeliverAll()
        {
            for (int i = 0; i < _pendingCount; i++)
            {
                Deliver(i);
            }

            _pendingCount = 0;
        }

        private void Deliver(int i)
        {
            int target = _pendingTarget[i];
            LiquidBodyCanvas canvas = pool.CanvasForTarget(target);
            if (canvas == null)
            {
                return;
            }

            Vector3 point = pool.TargetToWorld(target, _pendingPoint[i]);
            Vector3 normal = pool.TargetToWorldDirection(target, _pendingNormal[i]);
            canvas.QueueStamp(point, normal, _pendingRadius[i], profile, _pendingAmount[i], _pendingSeed[i]);
            _stampsDelivered++;
        }

        private void RemovePending(int i)
        {
            int last = _pendingCount - 1;
            _pendingTarget[i] = _pendingTarget[last];
            _pendingPoint[i] = _pendingPoint[last];
            _pendingNormal[i] = _pendingNormal[last];
            _pendingRadius[i] = _pendingRadius[last];
            _pendingAmount[i] = _pendingAmount[last];
            _pendingSeed[i] = _pendingSeed[last];
            _pendingTime[i] = _pendingTime[last];
            _pendingCount = last;
        }
    }
}
