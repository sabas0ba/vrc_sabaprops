using SabaProps.StageCam;
using TMPro;
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

namespace SabaProps.Tablet.Samples
{
    /// <summary>
    /// タブレットのページに Stage Cam の状態を表示します。
    /// 操作はタブレットのボタンが StageCamControlPanel のイベントを直接呼び、
    /// このコンポーネントは選択中のカメラの映像と追従対象を表示するだけです。
    /// ページが非表示の間は GameObject ごと無効になるため、更新も止まります。
    /// </summary>
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class TabletStageCamDisplay : UdonSharpBehaviour
    {
        public StageCamControlPanel panel;
        public Renderer preview;
        public TextMeshPro cameraLabel;
        public TextMeshPro targetLabel;
        public float refreshSeconds = 0.25f;

        private float nextRefresh;

        private void OnEnable()
        {
            nextRefresh = 0f;
        }

        private void Update()
        {
            if (Time.time < nextRefresh)
            {
                return;
            }

            nextRefresh = Time.time + refreshSeconds;
            Refresh();
        }

        public void Refresh()
        {
            StageCamRig rig = panel == null ? null : panel.GetSelectedRig();
            if (preview != null)
            {
                Texture texture = rig != null && rig.framingCamera != null ? rig.framingCamera.targetTexture : null;
                preview.enabled = texture != null;
                if (texture != null)
                {
                    preview.material.mainTexture = texture;
                }
            }

            if (cameraLabel != null)
            {
                cameraLabel.text = rig == null ? "No camera" : rig.gameObject.name;
            }

            if (targetLabel != null)
            {
                int id = rig == null ? -1 : rig.GetTargetPlayerId();
                VRCPlayerApi player = id < 0 ? null : VRCPlayerApi.GetPlayerById(id);
                targetLabel.text = Utilities.IsValid(player) ? "Tracking: " + player.displayName : "Tracking: -";
            }
        }
    }
}
