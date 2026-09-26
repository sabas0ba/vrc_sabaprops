using System;
using UnityEngine;

namespace SabaProps.Flock
{
    /// <summary>
    /// Body plan, colouring and movement of one species. Built-in values come
    /// from <see cref="FlockSpeciesCatalog"/>; a swarm keeps its own copy so it
    /// can be edited without changing the preset.
    /// <para>
    /// The five colours are read differently per category:
    /// birds use primary = upperparts, secondary = underparts, accent = head,
    /// detail = wing tips and flight feathers, extra = beak and legs;
    /// fish use primary = back, secondary = belly, accent = pattern,
    /// detail = fins, extra = tail fin.
    /// </para>
    /// </summary>
    [Serializable]
    public sealed class FlockSpecies
    {
        [Header("Identity")]
        public string id = "custom";
        public string displayName = "Custom";
        public FlockCategory category = FlockCategory.Bird;

        [Header("Size")]
        [Tooltip("体長 (m)。鳥は嘴から尾まで、魚は吻端から尾鰭の付け根まで。")]
        [Min(0.005f)]
        public float bodyLength = 0.3f;

        [Tooltip("個体ごとの大きさのばらつき (±割合)")]
        [Range(0f, 0.5f)]
        public float sizeVariance = 0.1f;

        [Header("Colour")]
        public Color primary = new Color(0.35f, 0.33f, 0.3f);
        public Color secondary = new Color(0.75f, 0.72f, 0.68f);
        public Color accent = new Color(0.2f, 0.2f, 0.2f);
        public Color detail = new Color(0.1f, 0.1f, 0.1f);
        public Color extra = new Color(0.9f, 0.6f, 0.2f);
        public FlockColorPattern pattern = FlockColorPattern.Plain;
        [Range(1, 12)]
        public int patternCount = 3;
        [Tooltip("鱗の銀色の反射 (0-1)")]
        [Range(0f, 1f)]
        public float sheen = 0f;

        [Header("Motion")]
        public FlockAnimation animation = FlockAnimation.Flap;
        [Tooltip("羽ばたき、または体をくねらせる周波数 (Hz)")]
        [Min(0f)]
        public float beatFrequency = 4f;
        [Tooltip("羽ばたき角 (度)、または体長に対する振幅")]
        [Min(0f)]
        public float beatAmplitude = 40f;
        [Tooltip("羽ばたかずに滑空する時間の割合 (0-1)")]
        [Range(0f, 1f)]
        public float glide = 0.2f;
        [Tooltip("巡航速度 (m/s)")]
        [Min(0f)]
        public float cruiseSpeed = 8f;

        [Header("Default swarm")]
        public FlockPattern defaultPattern = FlockPattern.Cruise;
        [Min(1)]
        public int defaultCount = 40;
        [Tooltip("群れが動き回る範囲の半径 (m)")]
        public Vector3 defaultArea = new Vector3(60f, 15f, 60f);

        [Header("Bird")]
        [Tooltip("体長に対する翼開長の比")]
        [Min(0f)]
        public float wingspan = 1.7f;
        public FlockWingShape wingShape = FlockWingShape.Pointed;
        public FlockTailShape tailShape = FlockTailShape.Square;
        [Tooltip("体長に対する尾の長さの比")]
        [Range(0f, 1f)]
        public float tailLength = 0.25f;
        [Tooltip("体長に対する首の長さの比")]
        [Range(0f, 1f)]
        public float neckLength = 0.1f;
        [Tooltip("体長に対する嘴の長さの比")]
        [Range(0f, 0.6f)]
        public float beakLength = 0.06f;
        [Tooltip("飛翔中に脚を後方へ伸ばす種 (ツル、サギなど)")]
        public bool trailingLegs = false;
        [Tooltip("翼端色が占める翼の外側の割合")]
        [Range(0f, 1f)]
        public float wingTipFraction = 0f;
        [Tooltip("風切羽の色が占める翼の後縁側の割合")]
        [Range(0f, 1f)]
        public float flightFeatherFraction = 0f;

        [Header("Fish")]
        public FlockFishBody fishBody = FlockFishBody.Fusiform;
        [Tooltip("体長に対する体高の比")]
        [Range(0.02f, 1.2f)]
        public float bodyDepth = 0.22f;
        [Tooltip("体長に対する体幅の比")]
        [Range(0.02f, 1.5f)]
        public float bodyWidth = 0.12f;
        public FlockCaudalFin caudalFin = FlockCaudalFin.Forked;
        [Tooltip("体長に対する尾鰭の大きさの比")]
        [Range(0f, 1.5f)]
        public float caudalSize = 0.25f;
        [Tooltip("体長に対する背鰭の高さの比")]
        [Range(0f, 1.5f)]
        public float dorsalHeight = 0.08f;
        [Tooltip("体長に対する臀鰭の高さの比")]
        [Range(0f, 1.5f)]
        public float analHeight = 0.05f;
        [Tooltip("体長に対する胸鰭の長さの比。エイでは体盤の半幅")]
        [Range(0f, 1.5f)]
        public float pectoralSize = 0.1f;

        public FlockSpecies Clone()
        {
            return (FlockSpecies)MemberwiseClone();
        }

        /// <summary>Wingspan in metres for birds, body length for fish.</summary>
        public float Span
        {
            get
            {
                if (category == FlockCategory.Bird)
                {
                    return bodyLength * Mathf.Max(wingspan, 0.1f);
                }

                return fishBody == FlockFishBody.Ray
                    ? bodyLength * Mathf.Max(pectoralSize * 2f, 0.1f)
                    : bodyLength;
            }
        }
    }
}
