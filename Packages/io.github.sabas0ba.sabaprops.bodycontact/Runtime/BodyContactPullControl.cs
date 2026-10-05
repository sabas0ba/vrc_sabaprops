using UdonSharp;
using UnityEngine;
using UnityEngine.UI;

namespace SabaProps.BodyContact
{
    /// <summary>許可と解除をローカル操作するワールド内ボタン。</summary>
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class BodyContactPullControl : UdonSharpBehaviour
    {
        public BodyContactPull pull;
        public bool releaseOnly;
        public Text label;
        // 0: 引っ張り操作、1: Gizmo、2: HUD、3: 接触処理。既存シーンは0のまま動作します。
        public int controlMode;
        public BodyContactDebugView debugView;
        private float _nextLabelTime;
        private string _lastLabel;
        public override void Interact()
        {
            if (pull == null) return;
            if (controlMode == 1 && debugView != null) { debugView._ToggleVisible(); return; }
            if (controlMode == 2 && debugView != null) { debugView._ToggleHud(); return; }
            if (controlMode == 3 && pull.source != null) { pull.source._ToggleEnabled(); return; }
            if (releaseOnly) pull._ReleasePull(); else pull._TogglePullPermission();
        }
        private void Update()
        {
            if (label == null || pull == null || Time.time < _nextLabelTime) return;
            _nextLabelTime = Time.time + 0.2f;
            string text;
            if (controlMode == 1 && debugView != null) text = "GIZMO: " + (debugView.visible ? "ON" : "OFF");
            else if (controlMode == 2 && debugView != null) text = "HUD: " + (debugView.hudVisible ? "ON" : "OFF");
            else if (controlMode == 3 && pull.source != null) text = "CONTACT + PULL: " + (pull.source.contactEnabled ? "ON" : "OFF");
            else text = releaseOnly ? "RELEASE PULL\nJump also releases"
                : "ALLOW BEING PULLED: " + (pull.allowBeingPulled ? "ON" : "OFF") + "\nInteract to toggle";
            if (text == _lastLabel) return;
            _lastLabel = text;
            label.text = text;
        }
    }
}
