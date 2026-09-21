using UdonSharp;
using UnityEngine;
using UnityEngine.UI;
using VRC.SDKBase;

namespace SabaProps.StageCam
{
    /// <summary>複数のカメラリグを各プレイヤーが独立して操作するパネル。</summary>
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class StageCamControlPanel : UdonSharpBehaviour
    {
        [Tooltip("パネルから操作するリグ。順番がカメラ切替順になります。")]
        public StageCamRig[] rigs;
        public RawImage preview;
        public Text cameraLabel;
        public Text playerLabel;
        public Text targetLabel;
        public Text settingsLabel;

        private int selectedCamera;
        private int selectedPlayerId = -1;
        private float nextRefresh;

        private void Start()
        {
            if (Utilities.IsValid(Networking.LocalPlayer))
            {
                selectedPlayerId = Networking.LocalPlayer.playerId;
            }
            RefreshView();
        }

        private void Update()
        {
            // Pickup や別パネルからの変更も表示へ反映します。
            if (Time.time < nextRefresh) return;
            nextRefresh = Time.time + 0.5f;
            RefreshView();
        }

        public StageCamRig GetSelectedRig()
        {
            if (rigs == null || rigs.Length == 0) return null;
            selectedCamera = Mathf.Clamp(selectedCamera, 0, rigs.Length - 1);
            return rigs[selectedCamera];
        }

        public void _PreviousCamera() { SelectCamera(-1); }
        public void _NextCamera() { SelectCamera(1); }

        private void SelectCamera(int direction)
        {
            if (rigs == null || rigs.Length == 0) return;
            selectedCamera = (selectedCamera + direction + rigs.Length) % rigs.Length;
            RefreshView();
        }

        public void _PreviousPlayer() { SelectPlayer(-1); }
        public void _NextPlayer() { SelectPlayer(1); }

        private void SelectPlayer(int direction)
        {
            int count = VRCPlayerApi.GetPlayerCount();
            if (count == 0) return;
            VRCPlayerApi[] players = new VRCPlayerApi[count];
            VRCPlayerApi.GetPlayers(players);

            // GetPlayers の配列順に依存せず ID 順で選びます。
            int candidate = -1;
            int wrap = -1;
            for (int i = 0; i < players.Length; i++)
            {
                VRCPlayerApi player = players[i];
                if (!Utilities.IsValid(player)) continue;
                int id = player.playerId;
                if (direction > 0)
                {
                    if (wrap < 0 || id < wrap) wrap = id;
                    if (id > selectedPlayerId && (candidate < 0 || id < candidate)) candidate = id;
                }
                else
                {
                    if (wrap < 0 || id > wrap) wrap = id;
                    if (id < selectedPlayerId && (candidate < 0 || id > candidate)) candidate = id;
                }
            }
            selectedPlayerId = candidate >= 0 ? candidate : wrap;
            RefreshView();
        }

        public void _ApplyPlayer()
        {
            StageCamRig rig = GetSelectedRig();
            if (rig != null) rig.SetTargetPlayer(selectedPlayerId);
            RefreshView();
        }

        public void _TargetSelf()
        {
            StageCamRig rig = GetSelectedRig();
            if (rig != null) rig._TargetLocalPlayer();
            RefreshView();
        }

        public void _StopFollowing()
        {
            StageCamRig rig = GetSelectedRig();
            if (rig != null) rig._ClearTarget();
            RefreshView();
        }

        public void _PreviousSubject() { ChangeSubject(-1); }
        public void _NextSubject() { ChangeSubject(1); }

        private void ChangeSubject(int direction)
        {
            StageCamRig rig = GetSelectedRig();
            if (rig != null) rig.subject = (Mathf.Clamp(rig.subject, 0, 7) + direction + 8) % 8;
            RefreshView();
        }

        public void _ToggleCameraWork()
        {
            StageCamRig rig = GetSelectedRig();
            if (rig != null) rig.cameraWork = !rig.cameraWork;
            RefreshView();
        }

        public void _ToggleAutoFraming()
        {
            StageCamRig rig = GetSelectedRig();
            if (rig != null) rig.autoFraming = !rig.autoFraming;
            RefreshView();
        }

        public void _ToggleFollowMode()
        {
            StageCamRig rig = GetSelectedRig();
            if (rig != null) rig.followMode = rig.followMode == StageCamRig.FollowBodyOrbit
                ? StageCamRig.FollowWorldOrbit : StageCamRig.FollowBodyOrbit;
            RefreshView();
        }

        public void _YawLeft() { AdjustOrbit(-5f, 0f, 0f); }
        public void _YawRight() { AdjustOrbit(5f, 0f, 0f); }
        public void _PitchDown() { AdjustOrbit(0f, -5f, 0f); }
        public void _PitchUp() { AdjustOrbit(0f, 5f, 0f); }
        public void _Closer() { AdjustOrbit(0f, 0f, -0.25f); }
        public void _Further() { AdjustOrbit(0f, 0f, 0.25f); }

        private void AdjustOrbit(float yaw, float pitch, float distance)
        {
            StageCamRig rig = GetSelectedRig();
            if (rig == null) return;
            rig.orbitYaw = Mathf.DeltaAngle(0f, rig.orbitYaw + yaw);
            rig.orbitPitch = Mathf.Clamp(rig.orbitPitch + pitch, -85f, 85f);
            if (rig.autoFraming)
            {
                rig.screenFraction = Mathf.Clamp(rig.screenFraction - distance * 0.2f, 0.1f, 0.95f);
            }
            else
            {
                rig.orbitDistance = Mathf.Clamp(rig.orbitDistance + distance, 0.25f, 50f);
            }
            RefreshView();
        }

        public void RefreshView()
        {
            StageCamRig rig = GetSelectedRig();
            if (preview != null)
            {
                preview.texture = rig != null && rig.framingCamera != null ? rig.framingCamera.targetTexture : null;
                preview.enabled = preview.texture != null;
            }
            if (cameraLabel != null) cameraLabel.text = rig == null ? "No camera configured"
                : "CAM " + (selectedCamera + 1) + " / " + rigs.Length + "   " + rig.gameObject.name;
            if (playerLabel != null) playerLabel.text = "Select player: " + PlayerName(selectedPlayerId);
            if (targetLabel != null) targetLabel.text = "Tracking: " + (rig == null ? "Stopped" : PlayerName(rig.GetTargetPlayerId()));
            if (settingsLabel != null) settingsLabel.text = rig == null ? "Assign rigs in the Inspector."
                : "Subject: " + SubjectName(rig.subject)
                + "    Reference: " + (rig.followMode == StageCamRig.FollowBodyOrbit ? "Body" : "World")
                + "\nYaw: " + rig.orbitYaw.ToString("F0") + " deg    Pitch: " + rig.orbitPitch.ToString("F0")
                + " deg    " + (rig.autoFraming ? "Frame: " + (rig.screenFraction * 100f).ToString("F0") + "%"
                    : "Distance: " + rig.orbitDistance.ToString("F2") + " m")
                + "\nAuto framing: " + (rig.autoFraming ? "ON" : "OFF")
                + "    Camera work: " + (rig.cameraWork ? "ON" : "OFF");
        }

        private string PlayerName(int id)
        {
            if (id < 0) return "None";
            VRCPlayerApi player = VRCPlayerApi.GetPlayerById(id);
            return Utilities.IsValid(player) ? player.displayName + " (#" + id + ")" : "Player left";
        }

        private string SubjectName(int value)
        {
            if (value == 1) return "Neck";
            if (value == 2) return "Chest";
            if (value == 3) return "Body";
            if (value == 4) return "Left hand";
            if (value == 5) return "Right hand";
            if (value == 6) return "Left foot";
            if (value == 7) return "Right foot";
            return "Face";
        }
    }
}
