// Hand-written stand-in for the UdonSharp runtime assembly.
//
// The rest of the VRChat API that io.github.sabas0ba.sabaprops.stagecam uses --
// VRCPlayerApi, Networking, Utilities, VRC_Pickup -- comes from the real
// VRCSDKBase.dll that packages.lock pins, so those signatures are checked
// against the shipping SDK. UdonSharpBehaviour itself cannot be: it is source
// inside the Worlds package and pulls in VRC.Udon, OdinSerializer and the
// UdonSharp compiler libraries, which is a graph this offline tier has no
// business rebuilding.
//
// So this file stands in for it, the same way UnityEditorStub.cs stands in for
// UnityEditor.dll. It carries only the members the package actually overrides
// or calls, and it shares that file's weakness: if the stub and the package
// hold the same wrong belief, this check passes anyway.
//
// What closes the gap is .github/verify/vrchat, where UdonSharp compiles the
// behaviour for real. Anything specific to UdonSharp -- unsupported syntax, a
// type Udon cannot expose, a synced field Udon will not carry -- is only ever
// caught there, never here.
using System;
using UnityEngine;

namespace UdonSharp
{
    // The order matters: it is what lands in the serialized UdonSharpProgramAsset.
    // Kept in the same order as UdonSharpAttributes.cs in the pinned SDK.
    public enum BehaviourSyncMode
    {
        Any,
        None,
        NoVariableSync,
        Continuous,
        Manual,
    }

    public enum UdonSyncMode
    {
        NotSynced,
        None,
        Linear,
        Smooth,
    }

    [AttributeUsage(AttributeTargets.Class)]
    public class UdonBehaviourSyncModeAttribute : Attribute
    {
        public BehaviourSyncMode behaviourSyncMode = BehaviourSyncMode.Any;

        public UdonBehaviourSyncModeAttribute(BehaviourSyncMode behaviourSyncMode)
        {
            this.behaviourSyncMode = behaviourSyncMode;
        }
    }

    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false, Inherited = false)]
    public class UdonSyncedAttribute : Attribute
    {
        public UdonSyncMode NetworkSyncType { get; }

        public UdonSyncedAttribute(UdonSyncMode networkSyncTypeIn = UdonSyncMode.None)
        {
            NetworkSyncType = networkSyncTypeIn;
        }
    }

    public abstract class UdonSharpBehaviour : MonoBehaviour
    {
        public void RequestSerialization() { }

        public void SendCustomEvent(string eventName) { }

        public virtual void Interact() { }

        public virtual void PostLateUpdate() { }

        public virtual void OnPickup() { }

        public virtual void OnDrop() { }

        public virtual void OnDeserialization() { }

        public virtual void OnPreSerialization() { }

        public virtual void OnPlayerJoined(VRC.SDKBase.VRCPlayerApi player) { }

        public virtual void OnPlayerLeft(VRC.SDKBase.VRCPlayerApi player) { }

        public virtual void OnOwnershipTransferred(VRC.SDKBase.VRCPlayerApi player) { }

        public virtual void OnAvatarEyeHeightChanged(VRC.SDKBase.VRCPlayerApi player, float prevEyeHeightAsMeters) { }
    }
}
