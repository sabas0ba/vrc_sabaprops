using UnityEngine;

namespace SabaProps.Tablet
{
    // TabletTeleport の計算部分。TabletControllerSolver.cs と同じく、基底型も VRChat の参照も持たず、
    // フィールドを参照しません。オフライン検査は .github/verify/offline/OfflineTabletTests.cs です。
    public partial class TabletTeleport
    {
        /// <summary>
        /// プレイヤー ID を昇順に巡回したときの次の ID。ids の並び順には依存しません。
        /// 候補が 1 人もいなければ -1 を返します。
        /// </summary>
        private int NextPlayerId(int[] ids, int count, int current, int direction)
        {
            int candidate = -1;
            int wrap = -1;
            int limit = ids == null ? 0 : Mathf.Min(count, ids.Length);
            for (int i = 0; i < limit; i++)
            {
                int id = ids[i];
                if (id < 0)
                {
                    continue;
                }

                if (direction >= 0)
                {
                    if (wrap < 0 || id < wrap) wrap = id;
                    if (id > current && (candidate < 0 || id < candidate)) candidate = id;
                }
                else
                {
                    if (wrap < 0 || id > wrap) wrap = id;
                    if (id < current && (candidate < 0 || id > candidate)) candidate = id;
                }
            }

            return candidate >= 0 ? candidate : wrap;
        }

        /// <summary>後方から順に調べる周囲 8 方向の候補。向きの上下成分は無視します。</summary>
        private Vector3 AroundPlayer(Vector3 target, Vector3 targetForward, float distance, int candidate)
        {
            Vector3 flat = new Vector3(targetForward.x, 0f, targetForward.z);
            if (flat.sqrMagnitude < 1e-8f)
            {
                flat = Vector3.forward;
            }

            float angle = 180f;
            if (candidate == 1) angle = 135f;
            else if (candidate == 2) angle = 225f;
            else if (candidate == 3) angle = 90f;
            else if (candidate == 4) angle = 270f;
            else if (candidate == 5) angle = 45f;
            else if (candidate == 6) angle = 315f;
            else if (candidate == 7) angle = 0f;
            return target + (Quaternion.AngleAxis(angle, Vector3.up) * flat.normalized) * distance;
        }

        /// <summary>from から to を向く水平の回転。同じ位置なら単位回転を返します。</summary>
        private Quaternion FacingRotation(Vector3 from, Vector3 to)
        {
            Vector3 d = to - from;
            if (d.x * d.x + d.z * d.z < 1e-8f)
            {
                return Quaternion.identity;
            }

            return Quaternion.AngleAxis(Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg, Vector3.up);
        }
    }
}
