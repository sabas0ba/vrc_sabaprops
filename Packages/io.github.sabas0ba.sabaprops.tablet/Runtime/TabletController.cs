using TMPro;
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

namespace SabaProps.Tablet
{
    /// <summary>
    /// タブレットの召喚・収納、ページ切り替え、VR の指先による押下を扱います。
    /// <para>
    /// 状態は各プレイヤーのローカルです。同じオブジェクトを全員が持ちますが、位置と表示は
    /// 各クライアントが自分で決めるため、他のプレイヤーのタブレットは見えません。
    /// </para>
    /// </summary>
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public partial class TabletController : UdonSharpBehaviour
    {
        [Header("構成")]
        [Tooltip("召喚時に移動・表示するタブレット本体。このコンポーネントとは別の GameObject にします。")]
        public Transform body;

        [Tooltip("本体を掴んで動かすためのハンドル。本体の子に置き、local 回転は単位回転にします。")]
        public VRC_Pickup handle;

        [Tooltip("指先による押下の対象。Editor の Build で登録されます。")]
        public TabletButton[] buttons;

        public GameObject[] pages;
        public string[] pageTitles;
        public TextMeshPro titleLabel;
        public TextMeshPro pageLabel;

        [Header("召喚")]
        [Tooltip("頭からの水平距離 (m)。")]
        public float summonDistance = 0.42f;

        [Tooltip("頭からの高さ (m)。負で目線より下に出します。")]
        public float summonHeight = -0.22f;

        [Tooltip("表面を上向きに傾ける角度 (度)。")]
        public float summonTilt = 30f;

        [Tooltip("手元に出すときの、手から前方へのずらし量 (m)。")]
        public float handOffset = 0.1f;

        [Tooltip("本体からこの距離 (m) 以上離れると自動で収納します。0 で無効です。")]
        public float autoStowDistance = 4f;

        public bool startVisible;

        [Header("指先操作 (VR)")]
        public bool fingerPress = true;

        [Tooltip("押下領域の奥行きに対する、押下が成立する深さの比率。")]
        [Range(0.1f, 1f)]
        public float pressDepthRatio = 0.7f;

        [Tooltip("押下後、この比率より浅く戻ると再び押せる状態になります。pressDepthRatio より小さくします。")]
        [Range(0f, 1f)]
        public float releaseDepthRatio = 0.35f;

        [Tooltip("指先の推定位置。末節ボーンの位置から、中節→末節の向きへこの比率だけ延長します。0 で末節ボーンの位置です。")]
        [Range(0f, 1.5f)]
        public float fingertipExtension = 0.8f;

        [Tooltip("指先が本体からこの距離 (m) より遠い間は判定しません。")]
        public float interactionRadius = 0.5f;

        public bool haptics = true;

        /// <summary>TabletButton が引数付きで呼ぶときの受け渡し用。_ShowPage が読みます。</summary>
        [HideInInspector]
        public int tabletArgument;

        private bool shown;
        private int currentPage;
        private Vector3 handleLocalPosition;
        private bool handleCaptured;

        private int leftState;
        private int rightState;
        private int leftButton = -1;
        private int rightButton = -1;
        private Vector3 pokeLocal;

        private void Start()
        {
            if (handle != null)
            {
                handleLocalPosition = handle.transform.localPosition;
                handleCaptured = true;
            }

            SetShown(startVisible);
            ShowPage(0);
        }

        private void Update()
        {
            if (!shown || body == null)
            {
                return;
            }

            FollowHandle();

            VRCPlayerApi player = Networking.LocalPlayer;
            if (!Utilities.IsValid(player))
            {
                return;
            }

            if (ShouldAutoStow(body.position, player.GetPosition(), autoStowDistance))
            {
                _Stow();
                return;
            }

            if (fingerPress && player.IsUserInVR())
            {
                PollFingers(player);
            }
        }

        // ------------------------------------------------------------------
        // 召喚と収納
        // ------------------------------------------------------------------

        public bool IsShown()
        {
            return shown;
        }

        public void _Toggle()
        {
            if (shown)
            {
                _Stow();
            }
            else
            {
                _Summon();
            }
        }

        /// <summary>頭の前方に出します。</summary>
        public void _Summon()
        {
            VRCPlayerApi player = Networking.LocalPlayer;
            if (!Utilities.IsValid(player))
            {
                SetShown(true);
                return;
            }

            VRCPlayerApi.TrackingData head = player.GetTrackingData(VRCPlayerApi.TrackingDataType.Head);
            float yaw = HorizontalYaw(head.rotation * Vector3.forward, 0f);
            SummonAt(SummonPosition(head.position, yaw, summonDistance, summonHeight), SummonRotation(yaw, summonTilt));
        }

        /// <summary>指定した手の前に出します。0 が左手、1 が右手です。</summary>
        public void SummonNearHand(int hand)
        {
            VRCPlayerApi player = Networking.LocalPlayer;
            if (!Utilities.IsValid(player))
            {
                return;
            }

            VRCPlayerApi.TrackingData head = player.GetTrackingData(VRCPlayerApi.TrackingDataType.Head);
            VRCPlayerApi.TrackingData palm = player.GetTrackingData(
                hand == 0 ? VRCPlayerApi.TrackingDataType.LeftHand : VRCPlayerApi.TrackingDataType.RightHand);
            float yaw = HorizontalYaw(palm.position - head.position, HorizontalYaw(head.rotation * Vector3.forward, 0f));
            SummonAt(palm.position + YawForward(yaw) * handOffset, SummonRotation(yaw, summonTilt));
        }

        public void SummonAt(Vector3 position, Quaternion rotation)
        {
            if (body != null)
            {
                body.position = position;
                body.rotation = rotation;
            }

            SetShown(true);
        }

        public void _Stow()
        {
            if (handle != null && handle.IsHeld)
            {
                handle.Drop();
            }

            SetShown(false);
        }

        private void SetShown(bool value)
        {
            shown = value;
            ResetPokes();
            if (body != null)
            {
                body.gameObject.SetActive(value);
            }
        }

        private void FollowHandle()
        {
            if (handle == null || !handleCaptured)
            {
                return;
            }

            Transform grip = handle.transform;
            if (handle.IsHeld)
            {
                // Pickup が決めたハンドルの姿勢に本体を合わせ、ハンドルは本体に対する定位置へ戻します。
                Quaternion rotation = grip.rotation;
                body.position = ParentPositionFromChild(grip.position, rotation, handleLocalPosition);
                body.rotation = rotation;
            }

            grip.localPosition = handleLocalPosition;
            grip.localRotation = Quaternion.identity;
        }

        // ------------------------------------------------------------------
        // ページ
        // ------------------------------------------------------------------

        public int GetCurrentPage()
        {
            return currentPage;
        }

        public void _NextPage()
        {
            ShowPage(WrapIndex(currentPage, 1, PageCount()));
        }

        public void _PreviousPage()
        {
            ShowPage(WrapIndex(currentPage, -1, PageCount()));
        }

        /// <summary>tabletArgument のページを表示します。</summary>
        public void _ShowPage()
        {
            ShowPage(tabletArgument);
        }

        public void ShowPage(int index)
        {
            int count = PageCount();
            currentPage = count == 0 ? 0 : Mathf.Clamp(index, 0, count - 1);
            for (int i = 0; i < count; i++)
            {
                if (pages[i] != null)
                {
                    pages[i].SetActive(i == currentPage);
                }
            }

            ResetPokes();
            if (titleLabel != null)
            {
                titleLabel.text = pageTitles != null && currentPage < pageTitles.Length ? pageTitles[currentPage] : "";
            }

            if (pageLabel != null)
            {
                pageLabel.text = count <= 1 ? "" : (currentPage + 1) + " / " + count;
            }
        }

        private int PageCount()
        {
            return pages == null ? 0 : pages.Length;
        }

        // ------------------------------------------------------------------
        // 指先による押下
        // ------------------------------------------------------------------

        private void PollFingers(VRCPlayerApi player)
        {
            Vector3 left = player.GetBonePosition(HumanBodyBones.LeftIndexDistal);
            Vector3 right = player.GetBonePosition(HumanBodyBones.RightIndexDistal);
            if (left.sqrMagnitude > 1e-8f)
            {
                left = FingertipFromBones(player.GetBonePosition(HumanBodyBones.LeftIndexIntermediate), left, fingertipExtension);
            }

            if (right.sqrMagnitude > 1e-8f)
            {
                right = FingertipFromBones(player.GetBonePosition(HumanBodyBones.RightIndexIntermediate), right, fingertipExtension);
            }

            float limit = interactionRadius * interactionRadius;

            // 存在しないボーンは原点が返るため、その手は判定しません。
            bool leftValid = left.sqrMagnitude > 1e-8f && (left - body.position).sqrMagnitude <= limit;
            bool rightValid = right.sqrMagnitude > 1e-8f && (right - body.position).sqrMagnitude <= limit;

            int found = leftValid ? FindButton(left) : -1;
            leftState = AdvancePoke(player, 0, leftState, leftButton, found);
            leftButton = found;

            found = rightValid ? FindButton(right) : -1;
            rightState = AdvancePoke(player, 1, rightState, rightButton, found);
            rightButton = found;
        }

        /// <summary>指先を正面から見た矩形に含むボタン。見つかった場合は pokeLocal にボタン座標を残します。</summary>
        private int FindButton(Vector3 tip)
        {
            if (buttons == null)
            {
                return -1;
            }

            for (int i = 0; i < buttons.Length; i++)
            {
                TabletButton button = buttons[i];
                if (button == null || !button.gameObject.activeInHierarchy || button.pressZone == null)
                {
                    continue;
                }

                BoxCollider zone = button.pressZone;
                Vector3 local = zone.transform.InverseTransformPoint(tip);
                if (PokeInsideRect(local, zone.center, zone.size))
                {
                    pokeLocal = local;
                    return i;
                }
            }

            return -1;
        }

        private int AdvancePoke(VRCPlayerApi player, int hand, int state, int previous, int found)
        {
            if (previous >= 0 && previous != found && state == PokePressed)
            {
                buttons[previous].SetPressed(false);
            }

            if (found < 0)
            {
                return PokeIdle;
            }

            if (found != previous)
            {
                state = PokeIdle;
            }

            TabletButton button = buttons[found];
            BoxCollider zone = button.pressZone;
            float depth = PokeDepth(pokeLocal, zone.center, zone.size);
            int next = NextPokeState(state, true, depth, zone.size.z * pressDepthRatio, zone.size.z * releaseDepthRatio);

            if (next == PokePressed && state == PokeArmed)
            {
                button.SetPressed(true);
                if (haptics)
                {
                    player.PlayHapticEventInHand(
                        hand == 0 ? VRC_Pickup.PickupHand.Left : VRC_Pickup.PickupHand.Right, 0.04f, 0.35f, 180f);
                }

                button._Press();
            }
            else if (next != PokePressed && state == PokePressed)
            {
                button.SetPressed(false);
            }

            return next;
        }

        private void ResetPokes()
        {
            if (buttons != null)
            {
                if (leftButton >= 0 && leftButton < buttons.Length && leftState == PokePressed && buttons[leftButton] != null)
                {
                    buttons[leftButton].SetPressed(false);
                }

                if (rightButton >= 0 && rightButton < buttons.Length && rightState == PokePressed && buttons[rightButton] != null)
                {
                    buttons[rightButton].SetPressed(false);
                }
            }

            leftState = PokeIdle;
            rightState = PokeIdle;
            leftButton = -1;
            rightButton = -1;
        }
    }
}
