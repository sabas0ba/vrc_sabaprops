using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

namespace SabaProps.Liquid
{
    /// <summary>
    /// 一定の速さで回る台。マネキンを載せ、付着が体に追従する様子と、体の全周の付き方を見せます。
    /// <para>
    /// 角度はサーバー時刻から決めるため、全クライアントで同じ向きになります。何も同期しません。
    /// </para>
    /// </summary>
    [AddComponentMenu("SabaProps/Liquid/Turntable")]
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class LiquidTurntable : UdonSharpBehaviour
    {
        [Tooltip("回す速さ（度/秒）。負で逆回りです。")]
        public float degreesPerSecond = 12f;

        private Quaternion _baseRotation;

        private void Start()
        {
            _baseRotation = transform.localRotation;
        }

        private void Update()
        {
            if (degreesPerSecond == 0f)
            {
                return;
            }

            double period = 360.0 / Mathf.Abs(degreesPerSecond);
            float angle = (float)(Networking.GetServerTimeInSeconds() % period / period * 360.0);
            if (degreesPerSecond < 0f)
            {
                angle = -angle;
            }

            transform.localRotation = _baseRotation * Quaternion.Euler(0f, angle, 0f);
        }
    }
}
