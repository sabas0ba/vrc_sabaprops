using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

namespace SabaProps.Liquid
{
    /// <summary>
    /// ワールドの主光源を、付着を描くシェーダへ渡します。
    /// <para>
    /// Projector の描画パスにはライトの定数が設定されないため、付着の顔料は環境光でしか
    /// 照らせず、泥や塗料に陰影が付きません。ここで主光源の向きと色をグローバルな
    /// シェーダ定数として設定し、シェーダが拡散光と濡れたハイライトを計算します。
    /// VRChat はワールドから設定できるグローバル定数を "_Udon" で始まる名前に限っています。
    /// </para>
    /// <para>
    /// ワールドに 1 つだけ置きます。影は考慮しないため、屋内で主光源が遮られる場所では
    /// intensityScale を下げてください。
    /// </para>
    /// </summary>
    [AddComponentMenu("SabaProps/Liquid/Liquid Lighting")]
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class LiquidLighting : UdonSharpBehaviour
    {
        public const string DirectionProperty = "_Udon_SabaLiquidLightDirection";
        public const string ColorProperty = "_Udon_SabaLiquidLightColor";

        [Tooltip("付着を照らす主光源。通常はワールドの Directional Light です。")]
        public Light mainLight;

        [Tooltip("光の強さの倍率。")]
        [Range(0f, 2f)]
        public float intensityScale = 1f;

        [Tooltip("光源の向きや色を読み直す間隔（秒）。光源を動かさないなら長くして構いません。")]
        [Min(0.05f)]
        public float refreshInterval = 0.5f;

        private int _directionId;
        private int _colorId;
        private float _nextRefresh;

        private void Start()
        {
            _directionId = VRCShader.PropertyToID(DirectionProperty);
            _colorId = VRCShader.PropertyToID(ColorProperty);
            Refresh();
        }

        private void Update()
        {
            if (Time.time >= _nextRefresh)
            {
                Refresh();
            }
        }

        private void OnDisable()
        {
            VRCShader.SetGlobalVector(_directionId, Vector4.zero);
        }

        /// <summary>光源の向きと色を読み直して設定します。</summary>
        public void Refresh()
        {
            _nextRefresh = Time.time + refreshInterval;

            if (mainLight == null || !mainLight.enabled || !mainLight.gameObject.activeInHierarchy)
            {
                VRCShader.SetGlobalVector(_directionId, Vector4.zero);
                return;
            }

            Vector3 toLight = -mainLight.transform.forward;
            Color color = mainLight.color * mainLight.intensity * intensityScale;
            VRCShader.SetGlobalVector(_directionId, new Vector4(toLight.x, toLight.y, toLight.z, 1f));
            VRCShader.SetGlobalVector(_colorId, new Vector4(color.r, color.g, color.b, 1f));
        }
    }
}
