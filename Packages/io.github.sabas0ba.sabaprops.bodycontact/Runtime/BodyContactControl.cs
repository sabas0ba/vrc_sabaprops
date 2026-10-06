using UdonSharp;
using UnityEngine;
using UnityEngine.UI;

namespace SabaProps.BodyContact
{
    /// <summary>接触処理と診断表示のローカル切替。</summary>
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class BodyContactControl : UdonSharpBehaviour
    {
        public BodyContactSystem source;
        public BodyContactDebugView debugView;
        public Text label;
        // 1: Gizmo、2: HUD、3: 接触処理。
        public int controlMode;
        private float _nextLabelTime;
        private string _lastLabel;

        private void Start()
        {
            if (source != null && source.startupDiagnostics) Debug.Log("[BodyContact] Control Start; mode=" + controlMode);
        }

        public override void Interact()
        {
            if (source != null && source.startupDiagnostics) Debug.Log("[BodyContact] Control Interact; mode=" + controlMode);
            if (controlMode == 1 && debugView != null) debugView._ToggleVisible();
            else if (controlMode == 2 && debugView != null) debugView._ToggleHud();
            else if (controlMode == 3 && source != null) source._ToggleEnabled();
        }

        private void Update()
        {
            if (label == null || Time.time < _nextLabelTime) return;
            _nextLabelTime = Time.time + 0.2f;
            string text = "";
            if (controlMode == 1 && debugView != null) text = "GIZMO: " + (debugView.visible ? "ON" : "OFF");
            else if (controlMode == 2 && debugView != null) text = "HUD: " + (debugView.hudVisible ? "ON" : "OFF");
            else if (controlMode == 3 && source != null) text = "CONTACT: " + (source.contactEnabled ? "ON" : "OFF");
            if (text == _lastLabel) return;
            if (_lastLabel == null && source != null && source.startupDiagnostics)
                Debug.Log("[BodyContact] Control first Update; mode=" + controlMode);
            _lastLabel = text;
            label.text = text;
        }
    }
}
