using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

namespace SabaProps.Liquid
{
    /// <summary>
    /// Body Canvas をプレイヤーへ割り当てるプール。
    /// <para>
    /// Canvas は RenderTexture を 6 枚持つため、全員分を常に確保するとメモリが足りません。
    /// 付着の入力を受けたプレイヤーにだけ割り当て、足りなくなったら最も長く入力の無い
    /// Canvas を取り上げます。ローカルプレイヤーの Canvas は取り上げの対象から外します。
    /// 自分の体に付いた液体が他人の都合で消えるのは、見え方として最も不自然なためです。
    /// </para>
    /// <para>
    /// 割り当ては各クライアントが独立に決めます。同じプレイヤーでも、クライアントごとに
    /// 別の Canvas が割り当たることがありますが、見た目には影響しません。
    /// </para>
    /// </summary>
    [AddComponentMenu("SabaProps/Liquid/Canvas Pool")]
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public partial class LiquidCanvasPool : UdonSharpBehaviour
    {
        [Tooltip("割り当てに使う Canvas。数がそのまま同時に付着を表示できる人数の上限です。")]
        public LiquidBodyCanvas[] canvases;

        [Header("命中判定")]
        [Tooltip("液体を遮る環境のレイヤ。既定は Default と Environment です。プレイヤーのレイヤは含めません。")]
        public LayerMask occluderLayers = (1 << 0) | (1 << 11);

        [Tooltip("目の高さ 1.6 m のアバターでの体のカプセル半径（m）。体格に比例させます。")]
        public float bodyRadius = 0.2f;

        [Tooltip("目の高さ 1.6 m のアバターでの手の球の半径（m）。体格に比例させます。")]
        public float handRadius = 0.07f;

        /// <summary>直前の CastPlayers で当たった点。</summary>
        [HideInInspector] public Vector3 lastHitPoint;

        /// <summary>直前の CastPlayers で当たった点の外向き法線。</summary>
        [HideInInspector] public Vector3 lastHitNormal;

        /// <summary>直前の CastPlayers で当たった点までの距離。</summary>
        [HideInInspector] public float lastHitDistance;

        /// <summary>直前の CastPlayers で当たったのが手なら true、体なら false。</summary>
        [HideInInspector] public bool lastHitHand;

        private VRCPlayerApi[] _players = new VRCPlayerApi[100];

        /// <summary>割り当て済みの Canvas を返します。無ければ null。</summary>
        public LiquidBodyCanvas FindCanvas(VRCPlayerApi player)
        {
            if (!Utilities.IsValid(player) || canvases == null)
            {
                return null;
            }

            int id = player.playerId;
            for (int i = 0; i < canvases.Length; i++)
            {
                LiquidBodyCanvas canvas = canvases[i];
                if (canvas != null && canvas.GetPlayerId() == id)
                {
                    return canvas;
                }
            }

            return null;
        }

        /// <summary>
        /// プレイヤーの Canvas を返します。無ければ空きを割り当て、空きも無ければ
        /// 最も長く入力の無い Canvas を取り上げて割り当てます。
        /// </summary>
        public LiquidBodyCanvas AcquireCanvas(VRCPlayerApi player)
        {
            LiquidBodyCanvas existing = FindCanvas(player);
            if (existing != null || !Utilities.IsValid(player) || canvases == null)
            {
                return existing;
            }

            LiquidBodyCanvas chosen = null;
            float oldest = float.MaxValue;
            VRCPlayerApi local = Networking.LocalPlayer;
            int localId = Utilities.IsValid(local) ? local.playerId : -1;

            for (int i = 0; i < canvases.Length; i++)
            {
                LiquidBodyCanvas canvas = canvases[i];
                if (canvas == null)
                {
                    continue;
                }

                int owner = canvas.GetPlayerId();
                if (owner < 0)
                {
                    chosen = canvas;
                    break;
                }

                if (owner == localId)
                {
                    continue;
                }

                float activity = canvas.GetLastActivityTime();
                if (activity < oldest)
                {
                    oldest = activity;
                    chosen = canvas;
                }
            }

            if (chosen != null)
            {
                chosen.Assign(player);
            }

            return chosen;
        }

        /// <summary>
        /// 光線が最初に当たるプレイヤーの ID を返します。当たらない、または環境に遮られた場合は -1。
        /// <para>
        /// プレイヤーの体は足元から頭頂までのカプセルで、手は手首のボーンを中心とする球で近似します。
        /// 手を別に扱うのは、体から離して差し出した手（蛇口の下など）が体のカプセルに入らないためです。
        /// Collider の構成に依存しないため、ローカルとリモートのプレイヤーを同じ規則で判定できます。
        /// 当たった点と法線は lastHitPoint と lastHitNormal に、手かどうかは lastHitHand に入ります。
        /// </para>
        /// </summary>
        public int CastPlayers(Vector3 origin, Vector3 direction, float maxDistance, bool includeLocal)
        {
            Vector3 dir = direction.normalized;
            int count = VRCPlayerApi.GetPlayerCount();
            VRCPlayerApi.GetPlayers(_players);

            int hitId = -1;
            float nearest = maxDistance;
            Vector3 hitA = Vector3.zero;
            Vector3 hitB = Vector3.zero;
            bool hitHand = false;

            for (int i = 0; i < count && i < _players.Length; i++)
            {
                VRCPlayerApi player = _players[i];
                if (!Utilities.IsValid(player) || (!includeLocal && player.isLocal))
                {
                    continue;
                }

                Vector3 feet = player.GetPosition();
                float eye = Mathf.Max(player.GetAvatarEyeHeightAsMeters(), 0.2f);
                float radius = bodyRadius * eye / 1.6f;
                Vector3 a = feet + Vector3.up * radius;
                Vector3 b = feet + Vector3.up * Mathf.Max(eye + 0.1f * eye / 1.6f - radius, radius);

                float t = RayCapsule(origin, dir, a, b, radius);
                if (t >= 0f && t < nearest)
                {
                    nearest = t;
                    hitId = player.playerId;
                    hitA = a;
                    hitB = b;
                    hitHand = false;
                }

                // 手。球は長さ 0 のカプセルとして扱い、法線の計算を共通にします。
                float hand = handRadius * eye / 1.6f;
                Vector3 left = player.GetBonePosition(HumanBodyBones.LeftHand);
                Vector3 right = player.GetBonePosition(HumanBodyBones.RightHand);
                if (left.sqrMagnitude > 1e-12f)
                {
                    float tl = RaySphere(origin, dir, left, hand);
                    if (tl >= 0f && tl < nearest)
                    {
                        nearest = tl;
                        hitId = player.playerId;
                        hitA = left;
                        hitB = left;
                        hitHand = true;
                    }
                }

                if (right.sqrMagnitude > 1e-12f)
                {
                    float tr = RaySphere(origin, dir, right, hand);
                    if (tr >= 0f && tr < nearest)
                    {
                        nearest = tr;
                        hitId = player.playerId;
                        hitA = right;
                        hitB = right;
                        hitHand = true;
                    }
                }
            }

            if (hitId < 0)
            {
                return -1;
            }

            RaycastHit blocker;
            if (Physics.Raycast(origin, dir, out blocker, nearest, occluderLayers, QueryTriggerInteraction.Ignore))
            {
                return -1;
            }

            lastHitDistance = nearest;
            lastHitPoint = origin + dir * nearest;
            lastHitNormal = CapsuleNormal(lastHitPoint, hitA, hitB);
            lastHitHand = hitHand;
            return hitId;
        }

        /// <summary>円錐内の方向。Source が放出方向のばらつきを作るのに使います。</summary>
        public Vector3 SampleCone(Vector3 axis, float angleDegrees, int sample)
        {
            return ConeDirection(axis, angleDegrees, Hash01(sample * 2), Hash01(sample * 2 + 1));
        }

        /// <summary>整数から [0, 1) の擬似乱数。</summary>
        public float Random01(int sample)
        {
            return Hash01(sample);
        }

        /// <summary>ワールドの点をプレイヤー基準の座標へ変換します。命中の同期に使います。</summary>
        public Vector3 WorldToPlayer(VRCPlayerApi player, Vector3 world)
        {
            return ToPlayerLocal(world, player.GetPosition(), player.GetRotation() * Vector3.forward);
        }

        /// <summary>WorldToPlayer の逆変換。</summary>
        public Vector3 PlayerToWorld(VRCPlayerApi player, Vector3 local)
        {
            return FromPlayerLocal(local, player.GetPosition(), player.GetRotation() * Vector3.forward);
        }

        /// <summary>方向をプレイヤー基準へ変換します。</summary>
        public Vector3 WorldToPlayerDirection(VRCPlayerApi player, Vector3 world)
        {
            return ToPlayerLocal(world, Vector3.zero, player.GetRotation() * Vector3.forward);
        }

        /// <summary>WorldToPlayerDirection の逆変換。</summary>
        public Vector3 PlayerToWorldDirection(VRCPlayerApi player, Vector3 local)
        {
            return FromPlayerLocal(local, Vector3.zero, player.GetRotation() * Vector3.forward);
        }

        public override void OnPlayerLeft(VRCPlayerApi player)
        {
            LiquidBodyCanvas canvas = FindCanvas(player);
            if (canvas != null)
            {
                canvas.Release();
            }
        }
    }
}
