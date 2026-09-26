using UdonSharp;
using UnityEngine;

namespace SabaProps.Liquid
{
    /// <summary>
    /// 液体の定義。
    /// <para>
    /// Udon では ScriptableObject を参照できないため、値だけを持つ UdonSharpBehaviour にしています。
    /// Source はこのコンポーネントを参照し、付着の量と性質をここから読みます。
    /// </para>
    /// <para>
    /// 液体は「顔料」と「液膜」の 2 成分で表します。顔料は色を持ち、乾いても残ります。
    /// 液膜は表面の濡れで、暗化と反射を生み、時間とともに蒸発します。
    /// 水は液膜だけ、塗料と泥は両方を持ちます。
    /// </para>
    /// </summary>
    [AddComponentMenu("SabaProps/Liquid/Liquid Profile")]
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class LiquidProfile : UdonSharpBehaviour
    {
        [Header("顔料")]
        [Tooltip("顔料の色。水のように色を持たない液体では使われません。")]
        public Color pigmentColor = new Color(0.35f, 0.24f, 0.15f, 1f);

        [Tooltip("単位量あたりの顔料の被覆。0 で顔料を持たない液体になります。")]
        [Range(0f, 1f)]
        public float pigmentAmount = 0f;

        [Header("液膜")]
        [Tooltip("単位量あたりの液膜の量。表面の暗化と反射の強さに効きます。")]
        [Range(0f, 1f)]
        public float filmAmount = 1f;

        [Tooltip("濡れた面の平滑度。反射の強さに効きます。")]
        [Range(0f, 1f)]
        public float smoothness = 0.9f;

        [Tooltip("粘性。0 でさらさらと流れ落ち、1 でほとんど流れません。")]
        [Range(0f, 1f)]
        public float viscosity = 0.1f;

        [Tooltip("液膜が乾ききるまでの秒数。0 以下で乾きません。")]
        public float dryingSeconds = 90f;

        [Header("洗浄")]
        [Tooltip("単位量あたりに他の液体の顔料を洗い流す割合。水は大きく、塗料は 0 です。")]
        [Range(0f, 1f)]
        public float washStrength = 0.5f;

        [Header("発光")]
        [Tooltip("蛍光。紫外線（ブラックライト）を受けている間だけ、顔料の色で光ります。")]
        [Range(0f, 1f)]
        public float fluorescence = 0f;

        [Tooltip("蓄光。明るい所で光を蓄え、暗くなってからしばらく顔料の色で光ります。")]
        [Range(0f, 1f)]
        public float luminescence = 0f;

        [Header("付着形状")]
        [Tooltip("付着の輪郭の不規則さ。粘性の高い液体ほど小さくします。")]
        [Range(0f, 1f)]
        public float edgeIrregularity = 0.6f;
    }
}
