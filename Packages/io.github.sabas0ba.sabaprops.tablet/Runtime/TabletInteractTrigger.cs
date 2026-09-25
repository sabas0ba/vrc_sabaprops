using UdonSharp;

namespace SabaProps.Tablet
{
    /// <summary>
    /// ワールドに置いたアイテムの Interact でタブレットを出し入れします。
    /// この GameObject には Interact を受ける Collider が必要です。
    /// </summary>
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class TabletInteractTrigger : UdonSharpBehaviour
    {
        public TabletController controller;

        public override void Interact()
        {
            if (controller != null)
            {
                controller._Toggle();
            }
        }
    }
}
