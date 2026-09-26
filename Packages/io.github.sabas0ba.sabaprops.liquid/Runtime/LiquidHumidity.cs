using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

namespace SabaProps.Liquid
{
    /// <summary>
    /// 湿度の Source。凝結の常在 Source で、場として評価します。サウナ、浴室、じめじめした部屋を想定します。
    /// <para>
    /// 範囲（この Transform を中心とする箱）の中の相手に湿度を伝えます。湿度が高いほど付着は乾きにくく、
    /// 湿度が threshold を超えると体の表面に結露が生じます。結露が進むと、細かい水滴がまとまって
    /// 垂れ落ちるため、その分をときどき小さな水の付着として置き、流下させます。
    /// </para>
    /// <para>
    /// 湿度は、基本の湿度と、連動するシャワーが出ている間の湿度の間を、riseSeconds と fallSeconds の
    /// 時定数で行き来します。シャワーの状態は同期されているため、湿度自体は同期しません。
    /// 湯気のパーティクル、霧の体積のマテリアル、壁や鏡の曇りのマテリアルも湿度に合わせて更新します。
    /// </para>
    /// </summary>
    [AddComponentMenu("SabaProps/Liquid/Humidity")]
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class LiquidHumidity : UdonSharpBehaviour
    {
        [Header("構成")]
        [Tooltip("ターゲットを探すプール。")]
        public LiquidCanvasPool pool;

        [Tooltip("垂れ落ちる水滴の液体の定義。")]
        public LiquidProfile dripProfile;

        [Tooltip("範囲の大きさ（m）。この Transform を中心とする箱です。")]
        public Vector3 areaSize = new Vector3(4f, 3f, 4f);

        [Header("湿度")]
        [Tooltip("基本の湿度（0〜1）。")]
        [Range(0f, 1f)]
        public float humidity = 0.95f;

        [Tooltip("結露が始まる湿度。")]
        [Range(0f, 0.99f)]
        public float condensationThreshold = 0.75f;

        [Tooltip("飽和した空気で、結露が 0 から 1 に達するまでの秒数。")]
        [Min(1f)]
        public float condenseSeconds = 25f;

        [Header("シャワーとの連動")]
        [Tooltip("連動するシャワー。出ている間は湿度が showerHumidity に向かって上がります。")]
        public LiquidShower linkedShower;

        [Tooltip("シャワーが出ている間の湿度。")]
        [Range(0f, 1f)]
        public float showerHumidity = 1f;

        [Tooltip("湿度が上がる時定数（秒）。")]
        [Min(0.1f)]
        public float riseSeconds = 12f;

        [Tooltip("湿度が下がる時定数（秒）。")]
        [Min(0.1f)]
        public float fallSeconds = 40f;

        [Header("垂れ落ちる水滴")]
        [Tooltip("結露が飽和した体で、1 秒あたりに垂れ始める水滴の数。")]
        [Min(0f)]
        public float dripsPerSecond = 1.5f;

        [Tooltip("水滴の半径（m）。")]
        [Min(0.005f)]
        public float dripRadius = 0.03f;

        [Tooltip("水滴 1 つの量。")]
        [Min(0f)]
        public float dripAmount = 0.4f;

        [Header("見た目")]
        [Tooltip("湯気のパーティクル。湿度が結露の閾値を超えている間だけ再生します。")]
        public ParticleSystem steam;

        [Tooltip("霧の体積のマテリアル（SabaProps/Liquid/Fog Volume）。_Density を湿度に合わせます。")]
        public Material fogMaterial;

        [Tooltip("飽和した空気での霧の濃さ（1/m）。")]
        [Min(0f)]
        public float fogDensity = 0.35f;

        [Tooltip("曇る面のマテリアル（SabaProps/Liquid/Weather Surface または Fogged Glass）。_WeatherState.z に曇りを渡します。")]
        public Material[] surfaceMaterials;

        [Tooltip("面が曇りきるまでの秒数。")]
        [Min(1f)]
        public float surfaceFogSeconds = 20f;

        [Header("評価")]
        [Tooltip("プレイヤーにも伝えるか。")]
        public bool affectPlayers = true;

        [Min(0.02f)]
        public float evaluationInterval = 0.1f;

        private const int MaxTargets = 32;
        private int[] _targets = new int[MaxTargets];
        private Vector3[] _centres = new Vector3[MaxTargets];
        private Vector3[] _tops = new Vector3[MaxTargets];
        private float _lastEvaluation;
        private float _current = -1f;
        private float _surfaceFog;
        private int _drip;

        private void Start()
        {
            if (pool == null)
            {
                GameObject found = GameObject.Find(LiquidCanvasPool.DefaultName);
                if (found != null)
                {
                    pool = found.GetComponent<LiquidCanvasPool>();
                }
            }
        }

        /// <summary>今の湿度（0〜1）。</summary>
        public float GetHumidity()
        {
            return _current < 0f ? humidity : _current;
        }

        private void Update()
        {
            float elapsed = Time.time - _lastEvaluation;
            if (elapsed < evaluationInterval)
            {
                return;
            }

            _lastEvaluation = Time.time;
            float dt = Mathf.Min(elapsed, 1f);
            AdvanceHumidity(dt);
            UpdateVisuals(dt);

            if (pool == null)
            {
                return;
            }

            float h = GetHumidity();
            int count = pool.CollectTargetsInBox(transform, areaSize, affectPlayers, _targets, _centres, _tops);
            for (int i = 0; i < count; i++)
            {
                LiquidBodyCanvas canvas = pool.CanvasForTarget(_targets[i]);
                if (canvas == null)
                {
                    continue;
                }

                canvas.ApplyHumidity(h, condensationThreshold, condenseSeconds);
                Drip(canvas, _targets[i], _centres[i], _tops[i], dt);
            }
        }

        private void AdvanceHumidity(float dt)
        {
            float target = humidity;
            if (linkedShower != null && linkedShower.IsRunning())
            {
                target = Mathf.Max(humidity, showerHumidity);
            }

            if (_current < 0f)
            {
                _current = humidity;
            }

            float seconds = target > _current ? riseSeconds : fallSeconds;
            _current += (target - _current) * (1f - Mathf.Exp(-dt / seconds));
        }

        private void UpdateVisuals(float dt)
        {
            float h = GetHumidity();
            float excess = Mathf.Clamp01((h - condensationThreshold) / Mathf.Max(1f - condensationThreshold, 0.01f));

            if (steam != null)
            {
                bool show = excess > 0.05f;
                if (show && !steam.isPlaying)
                {
                    steam.Play();
                }
                else if (!show && steam.isPlaying)
                {
                    steam.Stop();
                }
            }

            if (fogMaterial != null)
            {
                fogMaterial.SetFloat("_Density", fogDensity * excess);
            }

            // 面の曇りは、閾値を超えている間に増え、下回ると同じ速さで晴れます。
            float step = dt / surfaceFogSeconds;
            _surfaceFog = excess > 0f ? Mathf.Min(1f, _surfaceFog + step * excess) : Mathf.Max(0f, _surfaceFog - step);
            if (surfaceMaterials != null)
            {
                for (int i = 0; i < surfaceMaterials.Length; i++)
                {
                    Material surface = surfaceMaterials[i];
                    if (surface != null)
                    {
                        Vector4 state = surface.GetVector("_WeatherState");
                        state.z = _surfaceFog;
                        surface.SetVector("_WeatherState", state);
                    }
                }
            }
        }

        /// <summary>
        /// 結露が半分を超えた体に、ときどき水滴を置きます。体の周りの無作為な向きと高さから
        /// 体の軸へ光線を飛ばし、当たった所に小さな水を付けます。付いた水は流下で垂れていきます。
        /// </summary>
        private void Drip(LiquidBodyCanvas canvas, int target, Vector3 centre, Vector3 top, float dt)
        {
            if (dripProfile == null)
            {
                return;
            }

            float condensation = canvas.GetCondensation();
            if (condensation <= 0.5f)
            {
                return;
            }

            float expected = dripsPerSecond * (condensation - 0.5f) * 2f * dt;
            _drip++;
            int sample = (_drip % 100000) * 7 + (target & 1023);
            if (pool.Random01(sample) >= expected)
            {
                return;
            }

            float angle = pool.Random01(sample + 1) * 2f * Mathf.PI;
            float height = (top.y - centre.y) * 2f * (pool.Random01(sample + 2) - 0.5f);
            Vector3 outward = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
            Vector3 origin = new Vector3(centre.x, centre.y + height * 0.9f, centre.z) + outward;
            int hit = pool.CastTargets(origin, -outward, 1.5f, true);
            if (hit != target)
            {
                return;
            }

            canvas.QueueStamp(pool.lastHitPoint, pool.lastHitNormal, dripRadius, dripProfile, dripAmount,
                pool.Random01(sample + 3) * 100f);
        }
    }
}
