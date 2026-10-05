using UnityEngine;

namespace SabaProps.Tablet
{
    // TabletReachTrigger の計算部分。基底型も VRChat の参照も持たず、フィールドを参照しません。
    // オフライン検査は .github/verify/offline/OfflineTabletTests.cs です。
    public partial class TabletReachTrigger
    {
        /// <summary>
        /// 取り出し位置。offset は頭の水平方向の向きを基準にした座標で、頭の上下の傾きは無視します。
        /// 見上げたり見下ろしたりしても頭上の位置が動かないようにするためです。
        /// </summary>
        private Vector3 AnchorPosition(Vector3 head, Vector3 headForward, Vector3 offset)
        {
            float yaw = 0f;
            if (headForward.x * headForward.x + headForward.z * headForward.z >= 1e-8f)
            {
                yaw = Mathf.Atan2(headForward.x, headForward.z) * Mathf.Rad2Deg;
            }

            return head + Quaternion.AngleAxis(yaw, Vector3.up) * offset;
        }

        private bool IsWithin(Vector3 point, Vector3 center, float radius)
        {
            return (point - center).sqrMagnitude <= radius * radius;
        }

        /// <summary>
        /// 手を置き続けた時間の更新。発火後は負の値で保持し、一度領域から出るまで再発火しません。
        /// </summary>
        private float NextDwell(float dwell, bool inside, float deltaTime)
        {
            if (!inside)
            {
                return 0f;
            }

            return dwell < 0f ? dwell : dwell + deltaTime;
        }
    }
}
