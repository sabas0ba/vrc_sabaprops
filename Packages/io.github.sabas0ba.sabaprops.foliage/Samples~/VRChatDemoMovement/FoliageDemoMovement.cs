using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

/// <summary>
/// Raises the local player's movement speeds and enables jumping.
/// <para>
/// Walking a garden of 28 plots at VRChat's default 2 m/s is slow, and
/// its default jump impulse of zero means the mound and the terraces cannot be
/// climbed at all. Both are worth changing while looking at foliage; neither
/// belongs in a world you actually ship without thinking about it.
/// </para>
/// <para>
/// This has to be Udon rather than a setting on the scene descriptor:
/// VRCSceneDescriptor carries no movement fields, and VRChat applies speeds
/// through VRCPlayerApi at runtime. Which is also why it lives in Samples~
/// rather than in the package proper — importing it is what pulls UdonSharp
/// into the picture, and a foliage package should not do that on its own.
/// </para>
/// </summary>
[AddComponentMenu("SabaProps/Foliage Demo Movement")]
public class FoliageDemoMovement : UdonSharpBehaviour
{
    [Tooltip("歩行速度 (m/s)。VRChat の既定は 2 です。")]
    public float walkSpeed = 4f;

    [Tooltip("走行速度 (m/s)。VRChat の既定は 4 です。")]
    public float runSpeed = 9f;

    [Tooltip("横移動の速度 (m/s)。VRChat の既定は 2 です。")]
    public float strafeSpeed = 4f;

    [Tooltip("ジャンプの初速。VRChat の既定は 0 で、0 のままだとジャンプできません。")]
    public float jumpImpulse = 4f;

    private void Start()
    {
        VRCPlayerApi player = Networking.LocalPlayer;
        if (player == null)
        {
            return;
        }

        player.SetWalkSpeed(walkSpeed);
        player.SetRunSpeed(runSpeed);
        player.SetStrafeSpeed(strafeSpeed);
        player.SetJumpImpulse(jumpImpulse);
    }
}
