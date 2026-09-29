using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

namespace SabaProps.Liquid
{
    /// <summary>
    /// 明かりの範囲。暗い部屋の照明と紫外線（ブラックライト）を、範囲内の付着に伝えます。
    /// <para>
    /// 付着は Projector で描くため、Unity のライトの定数を受け取れません。範囲の中の相手には、
    /// ワールドの主光源の代わりに、この範囲の照明（lamp）の光と、範囲の環境光を伝えます。
    /// 照明を消すと付着も暗くなり、発光する顔料だけが見えるようになります。
    /// </para>
    /// <para>
    /// 紫外線は blacklight が点いている間だけ伝え、蛍光の顔料はその強さに比例して光ります。
    /// 蓄光の顔料は、照明が点いている間に光を蓄え、消えた後にそれを放ちます。
    /// </para>
    /// <para>
    /// 照明と紫外線の状態は Manual sync で同期します。automatic のときは、操作されるまで
    /// サーバー時刻に合わせて「点灯 → 紫外線のみ → 消灯」を繰り返します。
    /// </para>
    /// </summary>
    [AddComponentMenu("SabaProps/Liquid/Light Zone")]
    [UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
    public class LiquidLightZone : UdonSharpBehaviour
    {
        [Header("構成")]
        [Tooltip("ターゲットを探すプール。")]
        public LiquidCanvasPool pool;

        [Tooltip("範囲の大きさ（m）。この Transform を中心とする箱です。")]
        public Vector3 areaSize = new Vector3(6f, 3.5f, 6f);

        [Tooltip("部屋の照明。点灯と消灯をこのコンポーネントが切り替えます。")]
        public Light lamp;

        [Tooltip("紫外線の光源（ブラックライト）。見た目の光として紫色の弱いライトを置きます。")]
        public Light blacklight;

        [Header("光")]
        [Tooltip("照明を消したときの環境光（リニア）。")]
        public Color darkAmbient = new Color(0.004f, 0.004f, 0.006f, 1f);

        [Tooltip("照明を点けたときに加わる環境光（リニア）。壁からの照り返しの分です。")]
        public Color lampAmbient = new Color(0.12f, 0.11f, 0.1f, 1f);

        [Tooltip("紫外線を点けたときに加わる環境光（リニア）。紫外線灯の可視光の分です。")]
        public Color blacklightAmbient = new Color(0.02f, 0.0f, 0.05f, 1f);

        [Tooltip("紫外線の強さ。蛍光の顔料の光り方に比例します。")]
        [Min(0f)]
        public float ultravioletStrength = 1f;

        [Header("自動運転")]
        [Tooltip("操作されるまで、サーバー時刻に合わせて明かりを切り替えます。")]
        public bool automatic = true;

        [Tooltip("照明が点いている時間（秒）。")]
        [Min(1f)]
        public float litSeconds = 15f;

        [Tooltip("照明を消し、紫外線だけを点ける時間（秒）。")]
        [Min(0f)]
        public float blacklightSeconds = 12f;

        [Tooltip("照明も紫外線も消す時間（秒）。蓄光だけが光ります。")]
        [Min(0f)]
        public float darkSeconds = 12f;

        [Header("評価")]
        [Tooltip("プレイヤーにも伝えるか。")]
        public bool affectPlayers = true;

        [Min(0.02f)]
        public float evaluationInterval = 0.1f;

        [UdonSynced] private bool _manual;
        [UdonSynced] private bool _lampOn = true;
        [UdonSynced] private bool _blacklightOn;

        private const int MaxTargets = 32;
        private int[] _targets = new int[MaxTargets];
        private Vector3[] _centres = new Vector3[MaxTargets];
        private Vector3[] _tops = new Vector3[MaxTargets];
        private float _lastEvaluation;

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

        /// <summary>照明が点いているか。</summary>
        public bool IsLampOn()
        {
            if (!_manual && automatic)
            {
                return AutomaticPhase() == 0;
            }

            return _lampOn;
        }

        /// <summary>紫外線が点いているか。</summary>
        public bool IsBlacklightOn()
        {
            if (!_manual && automatic)
            {
                return AutomaticPhase() == 1;
            }

            return _blacklightOn;
        }

        /// <summary>照明を切り替えます。以後、自動運転を止めます。</summary>
        public void ToggleLamp()
        {
            TakeManual();
            _lampOn = !_lampOn;
            RequestSerialization();
        }

        /// <summary>紫外線を切り替えます。以後、自動運転を止めます。</summary>
        public void ToggleBlacklight()
        {
            TakeManual();
            _blacklightOn = !_blacklightOn;
            RequestSerialization();
        }

        /// <summary>自動運転に戻します。</summary>
        public void ResumeAutomatic()
        {
            if (!Networking.IsOwner(gameObject))
            {
                Networking.SetOwner(Networking.LocalPlayer, gameObject);
            }

            _manual = false;
            RequestSerialization();
        }

        private void TakeManual()
        {
            if (!Networking.IsOwner(gameObject))
            {
                Networking.SetOwner(Networking.LocalPlayer, gameObject);
            }

            if (!_manual)
            {
                // 今見えている状態から手動に移ります。
                _lampOn = IsLampOn();
                _blacklightOn = IsBlacklightOn();
                _manual = true;
            }
        }

        /// <summary>自動運転の段階。0: 点灯, 1: 紫外線のみ, 2: 消灯。</summary>
        private int AutomaticPhase()
        {
            float cycle = litSeconds + blacklightSeconds + darkSeconds;
            float t = (float)(Networking.GetServerTimeInSeconds() % cycle);
            if (t < litSeconds)
            {
                return 0;
            }

            return t < litSeconds + blacklightSeconds ? 1 : 2;
        }

        private void Update()
        {
            bool lampOn = IsLampOn();
            bool blacklightOn = IsBlacklightOn();
            if (lamp != null && lamp.enabled != lampOn)
            {
                lamp.enabled = lampOn;
            }

            if (blacklight != null && blacklight.enabled != blacklightOn)
            {
                blacklight.enabled = blacklightOn;
            }

            if (pool == null || Time.time - _lastEvaluation < evaluationInterval)
            {
                return;
            }

            _lastEvaluation = Time.time;
            int count = pool.CollectTargetsInBox(transform, areaSize, affectPlayers, _targets, _centres, _tops);
            for (int i = 0; i < count; i++)
            {
                LiquidBodyCanvas canvas = pool.CanvasForTarget(_targets[i]);
                if (canvas == null)
                {
                    continue;
                }

                Vector3 centre = _centres[i];
                Color light = Color.black;
                Vector3 towards = Vector3.up;
                if (lampOn && lamp != null)
                {
                    Vector3 offset = lamp.transform.position - centre;
                    towards = offset;
                    light = LampColor(lamp, offset.magnitude);
                }

                Color ambient = darkAmbient;
                if (lampOn)
                {
                    ambient += lampAmbient;
                }

                float ultraviolet = 0f;
                if (blacklightOn && blacklight != null)
                {
                    ambient += blacklightAmbient;
                    float distance = Vector3.Distance(blacklight.transform.position, centre);
                    ultraviolet = ultravioletStrength * Attenuation(distance, blacklight.range);
                }

                canvas.ApplyLightEnvironment(towards, light, ambient, ultraviolet);
            }
        }

        private Color LampColor(Light source, float distance)
        {
            if (source.type == LightType.Directional)
            {
                return source.color.linear * source.intensity;
            }

            return source.color.linear * (source.intensity * Attenuation(distance, source.range));
        }

        /// <summary>点光源の距離による減衰。範囲の端で 0 になる、Unity の既定に近い形です。</summary>
        private float Attenuation(float distance, float range)
        {
            if (range <= 0f)
            {
                return 0f;
            }

            float x = Mathf.Clamp01(distance / range);
            float falloff = 1f - x * x;
            return falloff * falloff;
        }
    }
}
