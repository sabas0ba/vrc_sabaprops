using TMPro;
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

namespace SabaProps.Tablet
{
    /// <summary>
    /// 登録した地点、または選択したプレイヤーの周囲の空いた場所へ自分をテレポートします。
    /// 地点のボタンは argument に destinations のインデックスを持ちます。
    /// </summary>
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public partial class TabletTeleport : UdonSharpBehaviour
    {
        [Header("地点")]
        public Transform[] destinations;

        [Header("プレイヤー")]
        [Tooltip("選択中のプレイヤーを表示するラベル。")]
        public TextMeshPro playerLabel;

        [Tooltip("プレイヤーの後方を優先して、この距離 (m) の位置へ移動します。")]
        public float playerDistance = 1.2f;

        [Tooltip("移動先の衝突確認に使用するワールドのレイヤー。Player と PlayerLocal は除外します。")]
        public LayerMask playerCollisionMask = ~(1 << 9 | 1 << 10);

        private Vector3 safePlayerPosition;

        [Header("移動後")]
        [Tooltip("移動後にタブレットを収納する場合に指定します。")]
        public TabletController stowController;

        /// <summary>TabletButton が書き込む地点のインデックス。</summary>
        [HideInInspector]
        public int tabletArgument;

        private int selectedPlayerId = -1;

        private void Start()
        {
            RefreshPlayerLabel();
        }

        public void _TeleportToDestination()
        {
            if (destinations == null || tabletArgument < 0 || tabletArgument >= destinations.Length)
            {
                return;
            }

            Transform destination = destinations[tabletArgument];
            VRCPlayerApi local = Networking.LocalPlayer;
            if (destination == null || !Utilities.IsValid(local))
            {
                return;
            }

            local.TeleportTo(destination.position, destination.rotation);
            AfterTeleport();
        }

        public void _NextPlayer()
        {
            SelectPlayer(1);
        }

        public void _PreviousPlayer()
        {
            SelectPlayer(-1);
        }

        public int GetSelectedPlayerId()
        {
            return selectedPlayerId;
        }

        public void _TeleportToSelectedPlayer()
        {
            VRCPlayerApi local = Networking.LocalPlayer;
            VRCPlayerApi target = VRCPlayerApi.GetPlayerById(selectedPlayerId);
            if (!Utilities.IsValid(local) || !Utilities.IsValid(target) || target.isLocal)
            {
                RefreshPlayerLabel();
                return;
            }

            Vector3 origin = target.GetPosition();
            float height = Mathf.Max(1.8f, local.GetTrackingData(VRCPlayerApi.TrackingDataType.Head).position.y - local.GetPosition().y + 0.2f);
            if (!TryFindPlayerDestination(origin, target.GetRotation() * Vector3.forward, height))
            {
                if (playerLabel != null) playerLabel.text = "No safe position";
                return;
            }

            local.TeleportTo(safePlayerPosition, FacingRotation(safePlayerPosition, origin));
            AfterTeleport();
        }

        // 後方、後方斜め、左右、前方斜め、前方の順で、床と身体の空間を確認します。
        private bool TryFindPlayerDestination(Vector3 origin, Vector3 forward, float height)
        {
            const float radius = 0.3f;
            for (int i = 0; i < 8; i++)
            {
                Vector3 candidate = AroundPlayer(origin, forward, Mathf.Max(0.7f, playerDistance), i);
                RaycastHit floor;
                if (!Physics.Raycast(candidate + Vector3.up * 0.5f, Vector3.down, out floor,
                    2f, playerCollisionMask, QueryTriggerInteraction.Ignore)) continue;
                if (Vector3.Dot(floor.normal, Vector3.up) < 0.7071f) continue;

                Vector3 feet = floor.point + Vector3.up * 0.02f;
                if (Physics.CheckCapsule(feet + Vector3.up * radius,
                    feet + Vector3.up * (Mathf.Max(1.8f, height) - radius), radius,
                    playerCollisionMask, QueryTriggerInteraction.Ignore)) continue;
                if (Physics.Linecast(origin + Vector3.up * 0.9f, feet + Vector3.up * 0.9f,
                    playerCollisionMask, QueryTriggerInteraction.Ignore)) continue;

                safePlayerPosition = feet;
                return true;
            }

            return false;
        }

        public override void OnPlayerLeft(VRCPlayerApi player)
        {
            if (Utilities.IsValid(player) && player.playerId == selectedPlayerId)
            {
                selectedPlayerId = -1;
            }

            RefreshPlayerLabel();
        }

        private void SelectPlayer(int direction)
        {
            int count = VRCPlayerApi.GetPlayerCount();
            VRCPlayerApi[] players = new VRCPlayerApi[count];
            VRCPlayerApi.GetPlayers(players);

            int[] ids = new int[count];
            for (int i = 0; i < count; i++)
            {
                VRCPlayerApi player = players[i];
                // 自分自身は移動先の候補にしません。
                ids[i] = Utilities.IsValid(player) && !player.isLocal ? player.playerId : -1;
            }

            selectedPlayerId = NextPlayerId(ids, count, selectedPlayerId, direction);
            RefreshPlayerLabel();
        }

        private void RefreshPlayerLabel()
        {
            if (playerLabel == null)
            {
                return;
            }

            VRCPlayerApi player = selectedPlayerId < 0 ? null : VRCPlayerApi.GetPlayerById(selectedPlayerId);
            playerLabel.text = Utilities.IsValid(player) ? player.displayName : "-";
        }

        private void AfterTeleport()
        {
            if (stowController != null)
            {
                stowController._Stow();
            }
        }
    }
}
