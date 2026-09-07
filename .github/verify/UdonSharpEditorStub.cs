// Hand-written stand-in for the UdonSharp editor assembly.
//
// The stage camera's Editor assembly needs exactly one thing from it: the
// extension that adds an UdonSharpBehaviour together with the UdonBehaviour
// that actually runs it. Adding the component on its own leaves an inert proxy.
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
    public static class UdonSharpComponentExtensions
    {
        public static T AddUdonSharpComponent<T>(this GameObject gameObject)
            where T : UdonSharp.UdonSharpBehaviour
        {
            return gameObject.AddComponent<T>();
        }
    }
}
