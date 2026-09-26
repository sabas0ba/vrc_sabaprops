using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

namespace SabaProps.Liquid
{
    /// <summary>
    /// 範囲全体に雨または雪を降らせる Source。降下の常在 Source で、場として評価します。
    /// <para>
    /// 範囲は、この Transform を中心とする箱です。範囲内のプレイヤーとマネキンのうち、
    /// 頭上が遮られていない相手にだけ降ります。遮りの判定は、頭上へ伸ばした光線が
    /// プールの遮蔽レイヤー（屋根、庇など）に当たるかで決めます。
    /// </para>
    /// <para>
    /// 雨は、頭上の円内から落とした粒ごとに命中を判定し、当たった所に小さく付けます。
    /// あわせて、上を向いた面が全体に濡れていく量を Canvas に伝えます。
    /// 液体の定義を差し替えれば、色の付いた雨にもなります（全体の濡れは水として描きます）。雪は Canvas に積雪の深さとして
    /// 伝え、上を向いた面に積もる様子と、止んだ後に溶けて濡れる様子を Canvas 側で描きます。
    /// </para>
    /// <para>
    /// 降る周期はサーバー時刻から決め、地面のマテリアルの濡れと積雪もサーバー時刻の関数として
    /// 求めるため、何も同期せずに全クライアントで同じ天候になります。
    /// </para>
    /// </summary>
    [AddComponentMenu("SabaProps/Liquid/Weather")]
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public partial class LiquidWeather : UdonSharpBehaviour
    {
        [Header("構成")]
        [Tooltip("Canvas を割り当て、命中を判定するプール。")]
        public LiquidCanvasPool pool;

        [Tooltip("雨の液体の定義。雪では使いません。")]
        public LiquidProfile profile;

        [Tooltip("有効にすると雪、無効なら雨を降らせます。")]
        public bool snow;

        [Tooltip("降っている間だけ再生する見た目のパーティクル。命中の判定には使いません。")]
        public ParticleSystem precipitation;

        [Header("範囲")]
        [Tooltip("降る範囲の大きさ（m）。この Transform を中心とする箱です。")]
        public Vector3 areaSize = new Vector3(10f, 6f, 10f);

        [Tooltip("頭上の遮りを調べる距離（m）。")]
        [Min(0.1f)]
        public float shelterCheckDistance = 20f;

        [Tooltip("プレイヤーにも降らせるか。無効にするとマネキンだけに降らせます。")]
        public bool affectPlayers = true;

        [Header("周期")]
        [Tooltip("降っている時間（秒）。")]
        [Min(0f)]
        public float precipitationSeconds = 30f;

        [Tooltip("止んでいる時間（秒）。0 なら降り続けます。")]
        [Min(0f)]
        public float clearSeconds = 30f;

        [Tooltip("降り始めと降り終わりに強さが変わる時間（秒）。")]
        [Min(0f)]
        public float rampSeconds = 3f;

        [Tooltip("時刻のずれ（秒）。複数の天候の範囲で周期をずらすのに使います。")]
        public float phaseOffset = 0f;

        [Header("雨")]
        [Tooltip("1 人あたり 1 秒に当てる雨粒の数（最も強いとき）。")]
        [Min(0f)]
        public float dropsPerSecond = 20f;

        [Tooltip("雨粒が付く範囲の半径（m）。0.6〜1.4 倍のばらつきが付きます。")]
        [Min(0.005f)]
        public float dropRadius = 0.05f;

        [Tooltip("1 粒で付く量。")]
        [Min(0f)]
        public float dropAmount = 0.5f;

        [Tooltip("1 秒に上を向いた面が濡れる量（最も強いとき）。1 で濡れきります。")]
        [Min(0f)]
        public float soakPerSecond = 0.05f;

        [Tooltip("風。雨粒が 1 m 落ちる間に横へ流される量（m）です。")]
        public Vector3 wind = new Vector3(0.15f, 0f, 0.05f);

        [Header("雪")]
        [Tooltip("1 秒に積もる深さ（最も強いとき）。深さ 1 で上を向いた面が覆われます。")]
        [Min(0f)]
        public float snowPerSecond = 0.04f;

        [Header("地面")]
        [Tooltip("天候に応じて濡れと積雪を描く地面のマテリアル（SabaProps/Liquid/Weather Surface）。")]
        public Material[] groundMaterials;

        [Tooltip("降り始めから地面が濡れきる、または雪が積もりきるまでの秒数。")]
        [Min(0f)]
        public float groundBuildSeconds = 20f;

        [Tooltip("止んでから、雨なら乾ききる、雪なら溶けきるまでの秒数。")]
        [Min(0f)]
        public float groundClearSeconds = 40f;

        [Tooltip("雪が溶けきった後、地面が乾くまでの秒数。")]
        [Min(0f)]
        public float groundDrySeconds = 30f;

        [Header("評価")]
        [Tooltip("命中を評価する間隔（秒）。")]
        [Min(0.02f)]
        public float evaluationInterval = 0.1f;

        // 頭上のこの高さから雨粒を落とします。
        private const float DropHeight = 1.2f;

        // 雨粒を散らす円の半径。体の近似より少し広くし、肩や腕にも当たるようにします。
        private const float DropSpread = 0.35f;

        // 乱数の種の周期。
        private const int SeedPeriod = 100000;

        private VRCPlayerApi[] _players = new VRCPlayerApi[100];
        private float _lastEvaluation;
        private float _dropCarry;

        private void Update()
        {
            double time = Networking.GetServerTimeInSeconds() + phaseOffset;
            float level = PrecipitationLevel(time, precipitationSeconds, clearSeconds, rampSeconds);

            UpdateParticles(level);
            UpdateGround(time);

            float elapsed = Time.time - _lastEvaluation;
            if (elapsed < evaluationInterval)
            {
                return;
            }

            _lastEvaluation = Time.time;
            if (level <= 0f || pool == null || (!snow && profile == null))
            {
                return;
            }

            int periodMs = Mathf.Max(1, Mathf.RoundToInt(evaluationInterval * 1000f));
            int tick = (Networking.GetServerTimeInMilliseconds() / periodMs) % SeedPeriod;

            // 1 人あたりの雨粒の数。端数は次の評価へ持ち越します。
            float drops = dropsPerSecond * level * elapsed + _dropCarry;
            int dropCount = Mathf.Min(Mathf.FloorToInt(drops), 16);
            _dropCarry = drops - Mathf.FloorToInt(drops);

            EvaluateMannequins(level * elapsed, dropCount, tick);
            if (affectPlayers)
            {
                EvaluatePlayers(level * elapsed, dropCount, tick);
            }
        }

        /// <summary>今、降っているか。</summary>
        public bool IsFalling()
        {
            double time = Networking.GetServerTimeInSeconds() + phaseOffset;
            return PrecipitationLevel(time, precipitationSeconds, clearSeconds, rampSeconds) > 0f;
        }

        private void EvaluateMannequins(float exposure, int dropCount, int tick)
        {
            int count = pool.GetMannequinCount();
            for (int i = 0; i < count; i++)
            {
                int target = pool.GetMannequinTarget(i);
                LiquidBodyCanvas canvas = pool.CanvasForTarget(target);
                if (canvas == null || !canvas.IsMannequin())
                {
                    continue;
                }

                Vector3 top = canvas.GetBodyTop() + Vector3.up * pool.bodyRadius;
                EvaluateTarget(target, canvas.GetBodyBottom(), top, exposure, dropCount, tick);
            }
        }

        private void EvaluatePlayers(float exposure, int dropCount, int tick)
        {
            int count = VRCPlayerApi.GetPlayerCount();
            if (_players.Length < count)
            {
                _players = new VRCPlayerApi[count];
            }

            VRCPlayerApi.GetPlayers(_players);
            for (int i = 0; i < count; i++)
            {
                VRCPlayerApi player = _players[i];
                if (!Utilities.IsValid(player))
                {
                    continue;
                }

                Vector3 feet = player.GetPosition();
                Vector3 top = player.GetBonePosition(HumanBodyBones.Head);
                if (top == Vector3.zero)
                {
                    top = feet + Vector3.up * player.GetAvatarEyeHeightAsMeters();
                }

                top += Vector3.up * pool.bodyRadius;
                EvaluateTarget(player.playerId, feet, top, exposure, dropCount, tick);
            }
        }

        private void EvaluateTarget(int target, Vector3 bottom, Vector3 top, float exposure, int dropCount, int tick)
        {
            Vector3 center = transform.InverseTransformPoint((bottom + top) * 0.5f);
            if (!InsideArea(center, areaSize * 0.5f))
            {
                return;
            }

            if (Physics.Raycast(top, Vector3.up, shelterCheckDistance, pool.occluderLayers, QueryTriggerInteraction.Ignore))
            {
                return;
            }

            if (snow)
            {
                LiquidBodyCanvas canvas = pool.CanvasForTarget(target);
                if (canvas != null)
                {
                    canvas.ApplySnow(snowPerSecond * exposure);
                }

                return;
            }

            LiquidBodyCanvas soaked = pool.CanvasForTarget(target);
            if (soaked != null)
            {
                soaked.ApplyRain(soakPerSecond * exposure);
            }

            Vector3 direction = DropDirection(wind);
            for (int d = 0; d < dropCount; d++)
            {
                int sample = (tick * 16 + d) * 131 + (target & 1023);
                Vector3 origin = DropOrigin(top, DropSpread, DropHeight, wind,
                    pool.Random01(sample), pool.Random01(sample + 4099));
                int hit = pool.CastTargets(origin, direction, DropHeight * 3f, true);
                if (hit == -1 || (!affectPlayers && hit >= 0))
                {
                    continue;
                }

                LiquidBodyCanvas canvas = pool.CanvasForTarget(hit);
                if (canvas == null)
                {
                    continue;
                }

                float size = dropRadius * (0.6f + 0.8f * pool.Random01(sample + 7919));
                canvas.QueueStamp(pool.lastHitPoint, pool.lastHitNormal, size, profile, dropAmount,
                    pool.Random01(sample + 12289) * 100f);
            }
        }

        private void UpdateParticles(float level)
        {
            if (precipitation == null)
            {
                return;
            }

            bool falling = level > 0.05f;
            if (falling && !precipitation.isPlaying)
            {
                precipitation.Play();
            }
            else if (!falling && precipitation.isPlaying)
            {
                precipitation.Stop();
            }
        }

        private void UpdateGround(double time)
        {
            if (groundMaterials == null || groundMaterials.Length == 0)
            {
                return;
            }

            float wet;
            float cover;
            if (snow)
            {
                cover = GroundCover(time, precipitationSeconds, clearSeconds, groundBuildSeconds, groundClearSeconds);
                wet = MeltWetness(time, precipitationSeconds, clearSeconds, groundBuildSeconds, groundClearSeconds,
                    groundDrySeconds);
            }
            else
            {
                cover = 0f;
                wet = GroundCover(time, precipitationSeconds, clearSeconds, groundBuildSeconds, groundClearSeconds);
            }

            Vector4 state = new Vector4(wet, cover, 0f, 0f);
            for (int i = 0; i < groundMaterials.Length; i++)
            {
                if (groundMaterials[i] != null)
                {
                    groundMaterials[i].SetVector("_WeatherState", state);
                }
            }
        }
    }
}
