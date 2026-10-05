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
        public override void Interact()
        {
            if (pull == null) return;
            if (releaseOnly) pull._ReleasePull(); else pull._TogglePullPermission();
        }
        private void Update()
        {
            if (label == null || pull == null) return;
            label.text = releaseOnly ? "RELEASE PULL\nJump also releases"
                : "ALLOW BEING PULLED: " + (pull.allowBeingPulled ? "ON" : "OFF") + "\nInteract to toggle";
        }
    }
}
