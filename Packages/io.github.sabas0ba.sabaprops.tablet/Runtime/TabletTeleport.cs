using TMPro;
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

namespace SabaProps.Tablet
{
    /// <summary>
    /// 登録した地点、または選択したプレイヤーの正面へ自分をテレポートします。
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

        [Tooltip("プレイヤーの正面、この距離 (m) の位置へ移動します。")]
        public float playerDistance = 1.2f;

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
            Vector3 position = FrontOf(origin, target.GetRotation() * Vector3.forward, playerDistance);
            local.TeleportTo(position, FacingRotation(position, origin));
            AfterTeleport();
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
