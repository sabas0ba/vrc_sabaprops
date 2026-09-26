using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

namespace SabaProps.Liquid
{
    /// <summary>
    /// Body Canvas をプレイヤーへ割り当てるプールであり、Source が液体をかける相手（ターゲット）を探す窓口です。
    /// <para>
    /// Canvas は RenderTexture を 6 枚持つため、全員分を常に確保するとメモリが足りません。
    /// 付着の入力を受けたプレイヤーにだけ割り当て、足りなくなったら最も長く入力の無い
    /// Canvas を取り上げます。ローカルプレイヤーの Canvas は取り上げの対象から外します。
    /// 自分の体に付いた液体が他人の都合で消えるのは、見え方として最も不自然なためです。
    /// </para>
    /// <para>
    /// ターゲットはプレイヤーとマネキンです。マネキンは固定の Transform に追従する
    /// Body Canvas で、割り当ての対象にならず常に有効です。ターゲットは整数で表し、
    /// 0 以上はプレイヤー ID、-2 以下はマネキンの番号、-1 は無しです。
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
        /// <summary>
        /// プールの GameObject の名前。Prefab の Source は、プールが設定されていなければ
        /// この名前でシーンから探します。
        /// </summary>
        public const string DefaultName = "Liquid Canvas Pool";

        [Tooltip("プレイヤーへの割り当てに使う Canvas。数がそのまま同時に付着を表示できる人数の上限です。")]
        public LiquidBodyCanvas[] canvases;

        [Tooltip("マネキンの Canvas。Source の命中判定の対象になります。")]
        public LiquidBodyCanvas[] mannequins;

        [Header("命中判定")]
        [Tooltip("液体を遮る環境のレイヤ。既定は Default と Environment です。プレイヤーのレイヤは含めません。")]
        public LayerMask occluderLayers = (1 << 0) | (1 << 11);

        [Tooltip("目の高さ 1.6 m のアバターでの体のカプセル半径（m）。体格に比例させます。")]
        public float bodyRadius = 0.2f;

        [Tooltip("目の高さ 1.6 m のアバターでの手の球の半径（m）。体格に比例させます。")]
        public float handRadius = 0.07f;

        [Tooltip("雨や雪を遮る傘。天候の Source が、傘の下の相手には降らせません。")]
        public LiquidUmbrella[] umbrellas;

        /// <summary>直前の CastTargets で当たった点。</summary>
        [HideInInspector] public Vector3 lastHitPoint;

        /// <summary>直前の CastTargets で当たった点の外向き法線。</summary>
        [HideInInspector] public Vector3 lastHitNormal;

        /// <summary>直前の CastTargets で当たった点までの距離。</summary>
        [HideInInspector] public float lastHitDistance;

        /// <summary>直前の CastTargets で当たったのが手なら true、体なら false。</summary>
        [HideInInspector] public bool lastHitHand;

        private VRCPlayerApi[] _players = new VRCPlayerApi[100];

        // CastTargets の途中経過。Udon ではメソッドから複数の値を返せないため、フィールドで受け渡します。
        private int _castTarget;
        private float _castNearest;
        private Vector3 _castA;
        private Vector3 _castB;
        private bool _castHand;

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

        /// <summary>ターゲットの Canvas。プレイヤーなら割り当て、マネキンならその Canvas を返します。</summary>
        public LiquidBodyCanvas CanvasForTarget(int target)
        {
            if (target >= 0)
            {
                VRCPlayerApi player = VRCPlayerApi.GetPlayerById(target);
                return Utilities.IsValid(player) ? AcquireCanvas(player) : null;
            }

            int index = MannequinIndex(target);
            if (mannequins == null || index < 0 || index >= mannequins.Length)
            {
                return null;
            }

            return mannequins[index];
        }

        /// <summary>マネキンの数。</summary>
        public int GetMannequinCount()
        {
            return mannequins == null ? 0 : mannequins.Length;
        }

        /// <summary>マネキンのターゲット番号。</summary>
        public int GetMannequinTarget(int index)
        {
            return MannequinTarget(index);
        }

        /// <summary>
        /// 光線が最初に当たるターゲットを返します。当たらない、または環境に遮られた場合は -1。
        /// <para>
        /// プレイヤーの体は足元から頭頂までのカプセルで、手は手首のボーンを中心とする球で近似します。
        /// 手を別に扱うのは、体から離して差し出した手（蛇口の下など）が体のカプセルに入らないためです。
        /// マネキンは Canvas に設定したカプセルで判定します。
        /// Collider の構成に依存しないため、ローカルとリモートのプレイヤーとマネキンを同じ規則で判定できます。
        /// 当たった点と法線は lastHitPoint と lastHitNormal に、手かどうかは lastHitHand に入ります。
        /// </para>
        /// </summary>
        public int CastTargets(Vector3 origin, Vector3 direction, float maxDistance, bool includeLocal)
        {
            Vector3 dir = direction.normalized;
            _castTarget = -1;
            _castNearest = maxDistance;
            _castHand = false;

            CastAgainstPlayers(origin, dir, includeLocal);
            CastAgainstMannequins(origin, dir);

            if (_castTarget == -1)
            {
                return -1;
            }

            RaycastHit blocker;
            if (Physics.Raycast(origin, dir, out blocker, _castNearest, occluderLayers, QueryTriggerInteraction.Ignore))
            {
                return -1;
            }

            lastHitDistance = _castNearest;
            lastHitPoint = origin + dir * _castNearest;
            lastHitNormal = CapsuleNormal(lastHitPoint, _castA, _castB);
            lastHitHand = _castHand;
            return _castTarget;
        }

        private void CastAgainstPlayers(Vector3 origin, Vector3 dir, bool includeLocal)
        {
            int count = VRCPlayerApi.GetPlayerCount();
            VRCPlayerApi.GetPlayers(_players);

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
                ConsiderHit(RayCapsule(origin, dir, a, b, radius), player.playerId, a, b, false);

                // 手。球は長さ 0 のカプセルとして扱い、法線の計算を共通にします。
                float hand = handRadius * eye / 1.6f;
                Vector3 left = player.GetBonePosition(HumanBodyBones.LeftHand);
                Vector3 right = player.GetBonePosition(HumanBodyBones.RightHand);
                if (left.sqrMagnitude > 1e-12f)
                {
                    ConsiderHit(RaySphere(origin, dir, left, hand), player.playerId, left, left, true);
                }

                if (right.sqrMagnitude > 1e-12f)
                {
                    ConsiderHit(RaySphere(origin, dir, right, hand), player.playerId, right, right, true);
                }
            }
        }

        private void CastAgainstMannequins(Vector3 origin, Vector3 dir)
        {
            if (mannequins == null)
            {
                return;
            }

            for (int i = 0; i < mannequins.Length; i++)
            {
                LiquidBodyCanvas mannequin = mannequins[i];
                if (mannequin == null || !mannequin.IsMannequin())
                {
                    continue;
                }

                Vector3 a = mannequin.GetBodyBottom();
                Vector3 b = mannequin.GetBodyTop();
                ConsiderHit(RayCapsule(origin, dir, a, b, mannequin.anchorBodyRadius), MannequinTarget(i), a, b, false);
            }
        }

        private void ConsiderHit(float distance, int target, Vector3 a, Vector3 b, bool hand)
        {
            if (distance < 0f || distance >= _castNearest)
            {
                return;
            }

            _castNearest = distance;
            _castTarget = target;
            _castA = a;
            _castB = b;
            _castHand = hand;
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

        /// <summary>
        /// ワールドの点をターゲット基準の座標へ変換します。命中の同期に使います。
        /// プレイヤーは足元と水平の向き、マネキンは anchor が基準です。
        /// </summary>
        public Vector3 WorldToTarget(int target, Vector3 world)
        {
            if (target < 0)
            {
                LiquidBodyCanvas mannequin = CanvasForTarget(target);
                return mannequin != null ? mannequin.anchor.InverseTransformPoint(world) : world;
            }

            VRCPlayerApi player = VRCPlayerApi.GetPlayerById(target);
            if (!Utilities.IsValid(player))
            {
                return world;
            }

            return ToPlayerLocal(world, player.GetPosition(), player.GetRotation() * Vector3.forward);
        }

        /// <summary>WorldToTarget の逆変換。</summary>
        public Vector3 TargetToWorld(int target, Vector3 local)
        {
            if (target < 0)
            {
                LiquidBodyCanvas mannequin = CanvasForTarget(target);
                return mannequin != null ? mannequin.anchor.TransformPoint(local) : local;
            }

            VRCPlayerApi player = VRCPlayerApi.GetPlayerById(target);
            if (!Utilities.IsValid(player))
            {
                return local;
            }

            return FromPlayerLocal(local, player.GetPosition(), player.GetRotation() * Vector3.forward);
        }

        /// <summary>方向をターゲット基準へ変換します。</summary>
        public Vector3 WorldToTargetDirection(int target, Vector3 world)
        {
            if (target < 0)
            {
                LiquidBodyCanvas mannequin = CanvasForTarget(target);
                return mannequin != null ? mannequin.anchor.InverseTransformDirection(world) : world;
            }

            VRCPlayerApi player = VRCPlayerApi.GetPlayerById(target);
            if (!Utilities.IsValid(player))
            {
                return world;
            }

            return ToPlayerLocal(world, Vector3.zero, player.GetRotation() * Vector3.forward);
        }

        /// <summary>WorldToTargetDirection の逆変換。</summary>
        public Vector3 TargetToWorldDirection(int target, Vector3 local)
        {
            if (target < 0)
            {
                LiquidBodyCanvas mannequin = CanvasForTarget(target);
                return mannequin != null ? mannequin.anchor.TransformDirection(local) : local;
            }

            VRCPlayerApi player = VRCPlayerApi.GetPlayerById(target);
            if (!Utilities.IsValid(player))
            {
                return local;
            }

            return FromPlayerLocal(local, Vector3.zero, player.GetRotation() * Vector3.forward);
        }

        /// <summary>
        /// 傘を登録します。Prefab の傘がシーンに置かれたとき、自分から呼びます。登録済みなら何もしません。
        /// </summary>
        public void RegisterUmbrella(LiquidUmbrella umbrella)
        {
            if (umbrella == null)
            {
                return;
            }

            int count = umbrellas == null ? 0 : umbrellas.Length;
            for (int i = 0; i < count; i++)
            {
                if (umbrellas[i] == umbrella)
                {
                    return;
                }
            }

            var grown = new LiquidUmbrella[count + 1];
            for (int i = 0; i < count; i++)
            {
                grown[i] = umbrellas[i];
            }

            grown[count] = umbrella;
            umbrellas = grown;
        }

        /// <summary>
        /// 点が、いずれかの傘の下にあるか。傘の面から下へ depth までの、少し裾広がりの円柱で判定します。
        /// </summary>
        public bool IsUnderUmbrella(Vector3 point)
        {
            if (umbrellas == null)
            {
                return false;
            }

            for (int i = 0; i < umbrellas.Length; i++)
            {
                LiquidUmbrella umbrella = umbrellas[i];
                if (umbrella == null || umbrella.canopy == null || !umbrella.gameObject.activeInHierarchy)
                {
                    continue;
                }

                if (UnderCanopy(point, umbrella.canopy.position, umbrella.canopy.up, umbrella.radius, umbrella.depth))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 箱の中にいるターゲットを集めます。箱は box を中心とする大きさ size の直方体です。
        /// targets にターゲット番号、centres に体の中心、tops に頭頂のすぐ上の点を入れ、数を返します。
        /// 配列が足りない分は数えません。
        /// </summary>
        public int CollectTargetsInBox(Transform box, Vector3 size, bool includePlayers, int[] targets,
            Vector3[] centres, Vector3[] tops)
        {
            int count = 0;
            Vector3 half = size * 0.5f;
            int capacity = Mathf.Min(targets.Length, Mathf.Min(centres.Length, tops.Length));

            int mannequinCount = GetMannequinCount();
            for (int i = 0; i < mannequinCount && count < capacity; i++)
            {
                LiquidBodyCanvas canvas = mannequins[i];
                if (canvas == null || canvas.anchor == null)
                {
                    continue;
                }

                Vector3 bottom = canvas.GetBodyBottom();
                Vector3 top = canvas.GetBodyTop() + Vector3.up * bodyRadius;
                Vector3 centre = (bottom + top) * 0.5f;
                if (!InsideBox(box.InverseTransformPoint(centre), half))
                {
                    continue;
                }

                targets[count] = MannequinTarget(i);
                centres[count] = centre;
                tops[count] = top;
                count++;
            }

            if (!includePlayers)
            {
                return count;
            }

            int playerCount = VRCPlayerApi.GetPlayerCount();
            if (_players.Length < playerCount)
            {
                _players = new VRCPlayerApi[playerCount];
            }

            VRCPlayerApi.GetPlayers(_players);
            for (int i = 0; i < playerCount && count < capacity; i++)
            {
                VRCPlayerApi player = _players[i];
                if (!Utilities.IsValid(player))
                {
                    continue;
                }

                Vector3 feet = player.GetPosition();
                float height = Mathf.Max(player.GetAvatarEyeHeightAsMeters(), 0.2f);
                Vector3 top = player.GetBonePosition(HumanBodyBones.Head);
                if (top == Vector3.zero)
                {
                    top = feet + Vector3.up * height;
                }

                top += Vector3.up * bodyRadius;
                Vector3 centre = (feet + top) * 0.5f;
                if (!InsideBox(box.InverseTransformPoint(centre), half))
                {
                    continue;
                }

                targets[count] = player.playerId;
                centres[count] = centre;
                tops[count] = top;
                count++;
            }

            return count;
        }

        /// <summary>プレイヤーの付着を消します。Canvas が割り当たっていなければ何もしません。</summary>
        public void ClearPlayer(int playerId)
        {
            LiquidBodyCanvas canvas = FindCanvas(VRCPlayerApi.GetPlayerById(playerId));
            if (canvas != null)
            {
                canvas.Clear();
            }
        }

        /// <summary>マネキンの付着を消します。</summary>
        public void ClearMannequins()
        {
            int count = GetMannequinCount();
            for (int i = 0; i < count; i++)
            {
                if (mannequins[i] != null)
                {
                    mannequins[i].Clear();
                }
            }
        }

        /// <summary>全員とマネキンの付着を消します。</summary>
        public void ClearEveryone()
        {
            if (canvases != null)
            {
                for (int i = 0; i < canvases.Length; i++)
                {
                    if (canvases[i] != null)
                    {
                        canvases[i].Clear();
                    }
                }
            }

            ClearMannequins();
        }

        /// <summary>
        /// アバターが変わったら、そのプレイヤーの付着を消します。付着は前のアバターの形に沿って付いており、
        /// 新しいアバターでは体から浮いた位置に描かれるためです。各クライアントで同じ事象を受けるため、同期しません。
        /// </summary>
        public override void OnAvatarChanged(VRCPlayerApi player)
        {
            LiquidBodyCanvas canvas = FindCanvas(player);
            if (canvas != null)
            {
                canvas.Clear();
            }
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
