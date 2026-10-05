using UdonSharp;
using VRC.SDKBase;

namespace SabaProps.BodyContact
{
    /// <summary>VRCStationと同じGameObjectに付け、着席中の移動補正を止めます。</summary>
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class BodyContactStationRelay : UdonSharpBehaviour
    {
        public BodyContactSystem source;
        public bool interactToSit;
        private bool _localSeated;

        public override void Interact()
        {
            if (interactToSit && Utilities.IsValid(Networking.LocalPlayer)) Networking.LocalPlayer.UseAttachedStation();
        }

        public override void OnPlayerRespawn(VRCPlayerApi player)
        {
            if (Utilities.IsValid(player) && player.isLocal) _localSeated = false;
        }

        public override void OnStationEntered(VRCPlayerApi player)
        {
            if (!Utilities.IsValid(player) || !player.isLocal || _localSeated) return;
            _localSeated = true;
            if (source != null) source._StationEntered();
        }

        public override void OnStationExited(VRCPlayerApi player)
        {
            if (!Utilities.IsValid(player) || !player.isLocal || !_localSeated) return;
            _localSeated = false;
            if (source != null) source._StationExited();
        }
    }
}
