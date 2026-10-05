using UdonSharp;
using UnityEngine;

namespace SabaProps.Tablet
{
    /// <summary>キー入力でタブレットを出し入れします。主に Desktop 向けです。</summary>
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class TabletKeyTrigger : UdonSharpBehaviour
    {
        public TabletController controller;

        [Tooltip("VRChat が既に使っているキーは避けてください。")]
        public KeyCode key = KeyCode.B;

        private void Update()
        {
            if (controller != null && Input.GetKeyDown(key))
            {
                controller._Toggle();
            }
        }
    }
}
