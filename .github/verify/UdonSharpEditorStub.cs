// Hand-written stand-in for the UdonSharp editor assembly.
//
// Covers component creation, proxy serialization and the backing behaviour used
// as the persistent UI event target. The real Unity tests verify these paths.
//
// Everything else the Editor assembly touches from VRChat comes from real
// shipped DLLs -- VRCSceneDescriptor and VRCPickup from VRCSDK3.dll,
// VRCPlayerApi and VRC_Pickup from VRCSDKBase.dll -- so those signatures are
// checked against the SDK. Only UdonSharp is source inside the Worlds package,
// and rebuilding that graph is not this tier's job. See UdonSharpStub.cs for
// the same argument about the runtime half.
//
// The gap this leaves is closed in .github/verify/vrchat, where the sample
// scene is built by the real editor with the real UdonSharp.
using UnityEngine;

namespace UdonSharpEditor
{
    public static class UdonSharpEditorUtility
    {
        public static VRC.Udon.UdonBehaviour GetBackingUdonBehaviour(UdonSharp.UdonSharpBehaviour proxy) { return null; }
        public static void CopyProxyToUdon(UdonSharp.UdonSharpBehaviour proxy) { }
    }

    public static class UdonSharpComponentExtensions
    {
        public static T AddUdonSharpComponent<T>(this GameObject gameObject)
            where T : UdonSharp.UdonSharpBehaviour
        {
            return gameObject.AddComponent<T>();
        }
    }
}

namespace VRC.Udon
{
    public class UdonBehaviour : MonoBehaviour
    {
        public void SendCustomEvent(string eventName) { }
    }
}
