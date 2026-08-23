using System.Collections;
using NUnit.Framework;
using SabaProps.Foliage;
using SabaProps.Foliage.Editors;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using VRC.SDK3.ClientSim;
using VRC.SDKBase;

namespace SabaProps.Foliage.WorldTests
{
    /// <summary>
    /// Runs the demo scene as a VRChat world.
    /// <para>
    /// The EditMode tests only prove the scene is authored correctly. This one
    /// enters play mode under ClientSim, the VRChat client's own in-editor
    /// runtime: it reads the VRCSceneDescriptor, spawns a local player and
    /// drives the world the way the client does. A misconfigured descriptor
    /// leaves no player to find.
    /// </para>
    /// <para>
    /// Lives here rather than in the CI project because it references the
    /// Worlds SDK, which that project deliberately does not have.
    /// </para>
    /// <para>
    /// One test, not several: entering and leaving play mode is per-fixture
    /// state, and a second test's setup would run before the first had left it.
    /// </para>
    /// </summary>
    public class FoliageDemoWorldTests
    {
        private bool _restoreOptionsEnabled;
        private EnterPlayModeOptions _restoreOptions;

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            // ClientSim logs the same startup exception on the way down.
            LogAssert.ignoreFailingMessages = true;

            yield return new ExitPlayMode();

            ClientSimRuntimeLoader.EndUnityTesting();
            EditorSettings.enterPlayModeOptions = _restoreOptions;
            EditorSettings.enterPlayModeOptionsEnabled = _restoreOptionsEnabled;

            if (AssetDatabase.IsValidFolder("Assets/SabaProps"))
            {
                AssetDatabase.DeleteAsset("Assets/SabaProps");
            }
        }

        [UnityTest]
        public IEnumerator Demo_RunsAsAVrchatWorldWithAPlayerInTheFoliage()
        {
            FoliageSampleScene.Create();

            // ClientSim's test hooks are static, and the domain reload that
            // normally happens on entering play mode would clear them before
            // the world starts.
            _restoreOptionsEnabled = EditorSettings.enterPlayModeOptionsEnabled;
            _restoreOptions = EditorSettings.enterPlayModeOptions;
            EditorSettings.enterPlayModeOptionsEnabled = true;
            EditorSettings.enterPlayModeOptions = EnterPlayModeOptions.DisableDomainReload;

            ClientSimRuntimeLoader.BeginUnityTesting(new ClientSimSettings
            {
                enableClientSim = true,
                initializationDelay = 0f,
                spawnPlayer = true,
                localPlayerIsMaster = true,
                displayLogs = false,
            });

            // ClientSim's player controller starts polling input a frame or two
            // before its input system is injected and throws a
            // NullReferenceException on the way up, every run, before it logs
            // "ClientSim Initialized". That is the SDK's own startup race, not
            // something this world can fix, so it is tolerated until the world
            // is up. Everything this test actually checks is asserted below.
            LogAssert.ignoreFailingMessages = true;

            yield return new EnterPlayMode();

            VRCPlayerApi player = null;
            for (int frame = 0; frame < 600 && player == null; frame++)
            {
                player = Networking.LocalPlayer;
                yield return null;
            }

            Assert.IsNotNull(player, "ClientSim did not spawn a local player");
            Assert.IsTrue(player.IsValid(), "the local player is not valid");

            // Let the character controller settle onto the ground collider.
            for (int frame = 0; frame < 120; frame++)
            {
                yield return null;
            }

            LogAssert.ignoreFailingMessages = false;

            Vector3 position = player.GetPosition();

            // Compared on the ground plane only: the controller settles to its
            // own capsule height, which is not what this asserts.
            float drift = Vector2.Distance(
                new Vector2(position.x, position.z),
                new Vector2(FoliageSampleScene.SpawnPosition.x, FoliageSampleScene.SpawnPosition.z));

            Assert.Less(drift, 2f,
                $"the player spawned at {position}, not at the demo's spawn point");

            // Below the ground means the player fell through the collider the
            // foliage is scattered onto.
            Assert.Greater(position.y, -1f, $"the player fell out of the world to y={position.y}");

            int visible = 0;
            int placed = 0;
            int plots = 0;

            foreach (FoliageField field in Object.FindObjectsOfType<FoliageField>())
            {
                plots++;
                if (field.lastBuildStats != null)
                {
                    placed += field.lastBuildStats.instanceCount;
                }

                foreach (MeshFilter filter in field.GetComponentsInChildren<MeshFilter>())
                {
                    var renderer = filter.GetComponent<MeshRenderer>();
                    if (renderer != null && renderer.enabled && renderer.gameObject.activeInHierarchy
                        && filter.sharedMesh != null && renderer.sharedMaterial != null)
                    {
                        visible++;
                    }
                }
            }

            // Counted separately on purpose. Renderers are not instances: most
            // of the demo is merged, where thousands of instances collapse into
            // a handful of renderers, so a renderer threshold would say nothing
            // about how much foliage is actually there.
            Assert.Greater(plots, 8, "the demo lost most of its plots on the way into play mode");
            Assert.Greater(placed, 2000, "the demo placed far less foliage than it should have");

            // Exact counts are the EditMode tests' job; this only has to show
            // the baked hierarchy survived into play mode, which is what will
            // happen in the uploaded world.
            Assert.Greater(visible, 50, "the foliage did not survive into play mode");
        }
    }
}
