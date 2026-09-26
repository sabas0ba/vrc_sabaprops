using UnityEngine;

namespace SabaProps.Tablet
{
    // TabletController の幾何計算と状態遷移。基底型も VRChat の参照も持たないため、
    // このファイル単体で Unity なしにコンパイルできます。
    // .github/verify/offline/OfflineTabletTests.cs が同じクラスの partial として検査します。
    //
    // 成立条件: ここに置くメソッドは TabletController のフィールドを参照しません。
    // 必要な値はすべて引数で受け取ります。
    public partial class TabletController
    {
        /// <summary>指先がボタンの前にいない状態。</summary>
        public const int PokeIdle = 0;

        /// <summary>指先がボタンの前方領域に入り、押し込みを待っている状態。</summary>
        public const int PokeArmed = 1;

        /// <summary>押し込みが成立し、指先が引き戻されるのを待っている状態。</summary>
        public const int PokePressed = 2;

        /// <summary>水平面に投影した向きの方位角 (度)。真上や真下を向いている場合は fallback を返します。</summary>
        private float HorizontalYaw(Vector3 forward, float fallback)
        {
            if (forward.x * forward.x + forward.z * forward.z < 1e-8f)
            {
                return fallback;
            }

            return Mathf.Atan2(forward.x, forward.z) * Mathf.Rad2Deg;
        }

        /// <summary>方位角 yaw から水平の前方向ベクトルを求めます。</summary>
        private Vector3 YawForward(float yaw)
        {
            return Quaternion.AngleAxis(yaw, Vector3.up) * Vector3.forward;
        }

        /// <summary>
        /// 頭の前方に出すときの位置。頭の上下の向きは使わず、水平方向の前方に distance、
        /// 鉛直方向に height だけずらします。
        /// </summary>
        private Vector3 SummonPosition(Vector3 head, float yaw, float distance, float height)
        {
            return head + YawForward(yaw) * distance + Vector3.up * height;
        }

        /// <summary>
        /// 召喚時の姿勢。タブレットの表面は local -Z を向くため、yaw で利用者から遠ざかる向きに
        /// +Z を合わせると表面が利用者を向きます。tilt が正のとき表面は上向きに傾きます。
        /// roll は構造的にゼロです。
        /// </summary>
        private Quaternion SummonRotation(float yaw, float tilt)
        {
            return Quaternion.AngleAxis(yaw, Vector3.up) * Quaternion.AngleAxis(tilt, Vector3.right);
        }

        /// <summary>
        /// 子の姿勢から親の位置を逆算します。子の local 回転が単位回転であることが前提です。
        /// Pickup で掴んだハンドルに本体を追従させるために使います。
        /// </summary>
        private Vector3 ParentPositionFromChild(Vector3 childPosition, Quaternion childRotation, Vector3 childLocalPosition)
        {
            return childPosition - childRotation * childLocalPosition;
        }

        /// <summary>
        /// 指先の推定位置。Humanoid の IndexDistal ボーンは末節の付け根 (第一関節) にあるため、
        /// 中節から末節への向きに ratio 倍だけ延長します。中節が取れない場合は末節の位置を返します。
        /// </summary>
        private Vector3 FingertipFromBones(Vector3 intermediate, Vector3 distal, float ratio)
        {
            if (intermediate.sqrMagnitude <= 1e-8f)
            {
                return distal;
            }

            return distal + (distal - intermediate) * ratio;
        }

        /// <summary>count 個の要素を循環するインデックス。count が 0 以下なら 0 を返します。</summary>
        private int WrapIndex(int current, int delta, int count)
        {
            if (count <= 0)
            {
                return 0;
            }

            int next = (current + delta) % count;
            return next < 0 ? next + count : next;
        }

        /// <summary>
        /// 押下領域の前面からの押し込み量。前面は local -Z 側にあり、前面より手前では負になります。
        /// </summary>
        private float PokeDepth(Vector3 local, Vector3 center, Vector3 size)
        {
            return local.z - (center.z - size.z * 0.5f);
        }

        /// <summary>押下領域の正面から見た矩形 (x, y) に入っているか。奥行きは判定しません。</summary>
        private bool PokeInsideRect(Vector3 local, Vector3 center, Vector3 size)
        {
            return Mathf.Abs(local.x - center.x) <= size.x * 0.5f
                && Mathf.Abs(local.y - center.y) <= size.y * 0.5f;
        }

        /// <summary>
        /// 指先による押下の状態遷移。PokeArmed から PokePressed への遷移が 1 回の押下です。
        /// <para>
        /// 前方から入った場合だけ Armed になり、横や裏から深い位置へ入っても押下になりません。
        /// 解除は pressDepth より浅い releaseDepth まで戻ったときで、境界付近の揺れで
        /// 連続して押下が発生しないようにヒステリシスを持たせています。
        /// </para>
        /// </summary>
        private int NextPokeState(int state, bool insideRect, float depth, float pressDepth, float releaseDepth)
        {
            if (!insideRect)
            {
                return PokeIdle;
            }

            if (state == PokePressed)
            {
                return depth < releaseDepth ? PokeArmed : PokePressed;
            }

            if (state == PokeArmed)
            {
                if (depth < 0f)
                {
                    return PokeIdle;
                }

                return depth >= pressDepth ? PokePressed : PokeArmed;
            }

            return depth >= 0f && depth < pressDepth ? PokeArmed : PokeIdle;
        }

        /// <summary>利用者が離れすぎたか。limit が 0 以下なら自動収納を行いません。</summary>
        private bool ShouldAutoStow(Vector3 tablet, Vector3 player, float limit)
        {
            if (limit <= 0f)
            {
                return false;
            }

            return (tablet - player).sqrMagnitude > limit * limit;
        }
    }
}
