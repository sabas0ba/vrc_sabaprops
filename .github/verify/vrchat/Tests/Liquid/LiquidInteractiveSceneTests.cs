using NUnit.Framework;
using SabaProps.Liquid.Editors;
using UdonSharp.Compiler;
using UdonSharpEditor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using VRC.SDK3.Components;

namespace SabaProps.Liquid.WorldTests
{
    /// <summary>
    /// The prefabs and the interactive demo world built from them.
    /// <para>
    /// The prefabs are checked as a user receives them: every Source carries its
    /// own liquid, leaves the pool to be found by name, and uses only materials
    /// inside the package. The scene is checked for what fails silently: rooms
    /// that let the sky in, zones and humid areas that miss their mannequins,
    /// and panels wired to the wrong nozzle.
    /// </para>
    /// </summary>
    public class LiquidInteractiveSceneTests
    {
        [OneTimeSetUp]
        public void CreateScene()
        {
            UdonSharpCompilerV1.CompileSync(new UdonSharpCompileOptions { IsEditorBuild = true });
            LiquidPrefabBuilder.BuildAll();
            LiquidInteractiveScene.Create();
            EditorSceneManager.OpenScene(LiquidInteractiveScene.ScenePath, OpenSceneMode.Single);
            Physics.SyncTransforms();
        }

        [OneTimeTearDown]
        public void CloseScene()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        }

        [Test]
        public void Prefabs_CarryTheirLiquidAndOnlyPackageAssets()
        {
            foreach (string name in LiquidPrefabBuilder.PrefabNames)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(LiquidPrefabBuilder.PrefabPath(name));
                Assert.IsNotNull(prefab, name + " was not built");

                foreach (string dependency in AssetDatabase.GetDependencies(LiquidPrefabBuilder.PrefabPath(name), true))
                {
                    // Serialized Udon programs are compiled into every project by UdonSharp
                    // from the program assets in the package, so a user's project has its own.
                    if (dependency.EndsWith(".cs") || dependency.EndsWith(".shader") || dependency.EndsWith(".cginc")
                        || dependency.StartsWith("Assets/SerializedUdonPrograms/"))
                    {
                        continue;
                    }

                    Assert.IsFalse(dependency.StartsWith("Assets/"),
                        name + " depends on a project asset, which a user of the package would not have: " + dependency);
                }

                foreach (UdonSharp.UdonSharpBehaviour behaviour in prefab.GetComponentsInChildren<UdonSharp.UdonSharpBehaviour>(true))
                {
                    Assert.IsNotNull(UdonSharpEditorUtility.GetBackingUdonBehaviour(behaviour), name + ": " + behaviour.name + " has no backing behaviour");
                }

                foreach (LiquidNozzle nozzle in prefab.GetComponentsInChildren<LiquidNozzle>(true))
                {
                    Assert.IsNotNull(nozzle.profile, name + ": the nozzle has no liquid");
                    Assert.IsTrue(nozzle.profile.transform.IsChildOf(prefab.transform), name + ": the liquid is outside the prefab");
                    Assert.IsNull(nozzle.pool, name + ": the pool is set in the prefab, so it would point outside it");
                }

                foreach (LiquidShower shower in prefab.GetComponentsInChildren<LiquidShower>(true))
                {
                    Assert.IsNotNull(shower.profile, name + ": the shower has no liquid");
                }
            }
        }

        [Test]
        public void CarriedPrefabs_RelayTheUseButtonToTheirNozzle()
        {
            foreach (string name in new[] { LiquidPrefabBuilder.CupName, LiquidPrefabBuilder.BucketName })
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(LiquidPrefabBuilder.PrefabPath(name));
                Assert.IsNotNull(prefab.GetComponent<VRCPickup>(), name + " cannot be picked up");
                Assert.IsNotNull(prefab.GetComponent<VRCObjectSync>(), name + " is not synced");

                LiquidButton relay = prefab.GetComponent<LiquidButton>();
                Assert.IsNotNull(relay, name + " has no relay for the use button");
                Assert.IsTrue(relay.relayPickupUse);
                Assert.AreEqual(nameof(LiquidNozzle.Trigger), relay.eventName);
                LiquidNozzle nozzle = prefab.GetComponentInChildren<LiquidNozzle>();
                Assert.AreSame(nozzle, relay.target, name + ": the relay does not reach the nozzle");
                Assert.AreNotSame(prefab, nozzle.gameObject, name + ": a Manual-synced nozzle sits next to VRCObjectSync");
                Assert.AreEqual(LiquidNozzle.ModeOneShot, nozzle.mode);
            }

            var umbrella = AssetDatabase.LoadAssetAtPath<GameObject>(LiquidPrefabBuilder.PrefabPath(LiquidPrefabBuilder.UmbrellaName));
            Assert.IsNotNull(umbrella.GetComponent<VRCPickup>());
            Assert.IsNotNull(umbrella.GetComponentInChildren<LiquidUmbrella>());
        }

        [Test]
        public void EveryUdonBehaviour_LoadsItsProgram()
        {
            LiquidSampleSceneTests.AssertEveryProgramLoads();
        }

        [Test]
        public void PortableNozzles_RelayUseReleaseAndDrop()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(LiquidPrefabBuilder.PrefabPath(LiquidPrefabBuilder.SprayGunName));
            AssertPortable(prefab, LiquidNozzle.ModeHold);

            GameObject area = GameObject.Find(LiquidInteractiveScene.SprayPlayName);
            Assert.IsNotNull(area);
            var modes = new System.Collections.Generic.List<int>();
            foreach (VRCPickup pickup in area.GetComponentsInChildren<VRCPickup>())
            {
                LiquidNozzle nozzle = pickup.GetComponentInChildren<LiquidNozzle>();
                AssertPortable(pickup.gameObject, nozzle.mode);
                Assert.IsNotNull(nozzle.pool, pickup.name + " is not wired to the scene's pool");
                Assert.IsNotNull(nozzle.readout, pickup.name + " has no panel on its dock");
                modes.Add(nozzle.mode);
            }

            CollectionAssert.AreEquivalent(
                new[] { LiquidNozzle.ModeHold, LiquidNozzle.ModeOneShot, LiquidNozzle.ModeContinuous }, modes);
        }

        /// <summary>
        /// A carried nozzle: synced pickup on the root, the Manual-synced nozzle on
        /// a child, and a relay that brings use, release and drop to it.
        /// </summary>
        private static void AssertPortable(GameObject root, int mode)
        {
            Assert.IsNotNull(root.GetComponent<VRCPickup>(), root.name + " cannot be picked up");
            Assert.IsNotNull(root.GetComponent<VRCObjectSync>(), root.name + " is not synced");
            LiquidNozzle nozzle = root.GetComponentInChildren<LiquidNozzle>();
            Assert.IsNotNull(nozzle, root.name + " has no nozzle");
            Assert.AreNotSame(root, nozzle.gameObject, root.name + ": a Manual-synced nozzle sits next to VRCObjectSync");
            Assert.AreEqual(mode, nozzle.mode);
            Assert.AreSame(root.GetComponent<VRCPickup>(), nozzle.pickup, root.name + ": the nozzle does not watch its pickup");
            Assert.IsFalse(nozzle.fireOnInteract, root.name + " fires on Interact, which a pickup never gets");

            LiquidButton relay = root.GetComponent<LiquidButton>();
            Assert.IsNotNull(relay, root.name + " has no relay");
            Assert.AreSame(nozzle, relay.target);
            Assert.IsTrue(relay.relayPickupUse);
            Assert.AreEqual(nameof(LiquidNozzle.Trigger), relay.eventName);
            Assert.AreEqual(nameof(LiquidNozzle.Release), relay.useUpEventName);
            Assert.AreEqual(nameof(LiquidNozzle.StopFiring), relay.dropEventName);
            Assert.AreEqual(13, root.layer, root.name + " is not on the pickup layer");
        }

        [Test]
        public void Sauna_TreatsBodiesAsBareSkin()
        {
            GameObject sauna = GameObject.Find(LiquidInteractiveScene.SaunaName);
            LiquidHumidity humidity = sauna.GetComponentInChildren<LiquidHumidity>();
            Assert.IsTrue(humidity.assumeBareSkin);
            Assert.IsNotNull(humidity.bareSkinSurface);
            Assert.AreEqual(LiquidSurfaceBuilder.SkinName, humidity.bareSkinSurface.name);

            GameObject bathroom = GameObject.Find(LiquidInteractiveScene.BathroomName);
            Assert.IsFalse(bathroom.GetComponentInChildren<LiquidHumidity>().assumeBareSkin,
                "the bathroom keeps clothes, so the two rooms show both behaviours");
        }

        [Test]
        public void Scene_IsAWorldWithEveryMannequinRegistered()
        {
            Assert.IsNotNull(Object.FindObjectOfType<VRCSceneDescriptor>());
            LiquidCanvasPool pool = Object.FindObjectOfType<LiquidCanvasPool>();
            int expected = 3                                                  // nozzle bench
                + LiquidInteractiveScene.ViscosityPresets.Length               // viscosity row
                + 2 + 2 + 2                                                    // sauna, bathroom, damp air
                + LiquidInteractiveScene.DarkRoomPaints.Length                 // dark room
                + 1 + 2                                                        // prefab target, rain patch
                + 2;                                                           // spray each other
            Assert.AreEqual(expected, pool.mannequins.Length);

            LiquidSampleSceneTests.AssertMannequinsDrawOnTheirOwnLayer(pool);
            foreach (LiquidBodyCanvas mannequin in pool.mannequins)
            {
                StringAssert.IsMatch(@"_2\d\d\.mat$", AssetDatabase.GetAssetPath(mannequin.projectorMaterial),
                    mannequin.name + " uses a projector material numbered for another scene");
            }
        }

        [Test]
        public void NozzleBench_PanelsDriveTheirOwnNozzle()
        {
            GameObject bench = GameObject.Find(LiquidInteractiveScene.NozzleBenchName);
            LiquidNozzle[] nozzles = bench.GetComponentsInChildren<LiquidNozzle>();
            Assert.AreEqual(3, nozzles.Length);
            CollectionAssert.AreEquivalent(
                new[] { LiquidNozzle.ModeOneShot, LiquidNozzle.ModePeriodic, LiquidNozzle.ModeContinuous },
                System.Array.ConvertAll(nozzles, n => n.mode));

            foreach (LiquidNozzle nozzle in nozzles)
            {
                Assert.IsNotNull(nozzle.readout, nozzle.transform.parent.name + " has no readout");
                Transform bay = nozzle.transform.parent;
                foreach (LiquidButton button in bay.GetComponentsInChildren<LiquidButton>())
                {
                    Assert.AreSame(nozzle, button.target, bay.name + ": " + button.name + " drives another nozzle");
                    Assert.IsNotNull(typeof(LiquidNozzle).GetMethod(button.eventName), button.name + " calls a missing method");
                }

                // The nozzle reaches its mannequin, and aims up enough for the arc to arrive
                // above the knees.
                LiquidBodyCanvas target = bay.GetComponentInChildren<LiquidBodyCanvas>();
                Vector3 offset = target.anchor.position - nozzle.transform.position;
                float horizontal = new Vector2(offset.x, offset.z).magnitude;
                Assert.Less(horizontal, nozzle.range, bay.name + ": the mannequin is out of range");
                Vector3 velocity = nozzle.transform.forward * nozzle.speed;
                float t = horizontal / new Vector2(velocity.x, velocity.z).magnitude;
                float height = nozzle.transform.position.y + velocity.y * t - 0.5f * 9.81f * t * t;
                Assert.Greater(height, 0.6f, bay.name + ": the stream lands below the knees");
                Assert.Less(height, 1.7f, bay.name + ": the stream passes over the head");
            }
        }

        [Test]
        public void Rooms_AreClosedOverheadAndReachTheirMannequins()
        {
            LiquidCanvasPool pool = Object.FindObjectOfType<LiquidCanvasPool>();
            foreach (string room in new[] { LiquidInteractiveScene.SaunaName, LiquidInteractiveScene.BathroomName, LiquidInteractiveScene.DarkRoomName })
            {
                GameObject root = GameObject.Find(room);
                foreach (LiquidBodyCanvas body in root.GetComponentsInChildren<LiquidBodyCanvas>())
                {
                    Vector3 top = body.GetBodyTop() + Vector3.up * pool.bodyRadius;
                    Assert.IsTrue(Physics.Raycast(top, Vector3.up, 10f, pool.occluderLayers, QueryTriggerInteraction.Ignore),
                        room + ": " + body.transform.parent.name + " has open sky above it");
                }
            }

            foreach (LiquidHumidity humidity in Object.FindObjectsOfType<LiquidHumidity>())
            {
                Assert.AreSame(pool, humidity.pool, humidity.name);
                int inside = 0;
                foreach (LiquidBodyCanvas body in pool.mannequins)
                {
                    Vector3 local = humidity.transform.InverseTransformPoint(body.anchor.position);
                    if (Mathf.Abs(local.x) <= humidity.areaSize.x * 0.5f && Mathf.Abs(local.z) <= humidity.areaSize.z * 0.5f)
                    {
                        inside++;
                    }
                }

                Assert.Greater(inside, 0, humidity.transform.parent.name + ": the humid area holds no mannequin");
            }

            GameObject dark = GameObject.Find(LiquidInteractiveScene.DarkRoomName);
            LiquidLightZone zone = dark.GetComponentInChildren<LiquidLightZone>();
            Assert.IsNotNull(zone.lamp);
            Assert.IsNotNull(zone.blacklight);
            foreach (LiquidBodyCanvas body in dark.GetComponentsInChildren<LiquidBodyCanvas>())
            {
                Vector3 local = zone.transform.InverseTransformPoint(body.anchor.position);
                Assert.Less(Mathf.Abs(local.x), zone.areaSize.x * 0.5f, body.transform.parent.name + " is outside the light zone");
                Assert.Less(Mathf.Abs(local.z), zone.areaSize.z * 0.5f, body.transform.parent.name + " is outside the light zone");
            }
        }

        [Test]
        public void PrefabCorner_UsesThePrefabs()
        {
            GameObject corner = GameObject.Find(LiquidInteractiveScene.PrefabCornerName);
            foreach (string name in LiquidPrefabBuilder.PrefabNames)
            {
                bool placed = false;
                foreach (Transform child in corner.transform)
                {
                    if (PrefabUtility.IsAnyPrefabInstanceRoot(child.gameObject)
                        && PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(child.gameObject) == LiquidPrefabBuilder.PrefabPath(name))
                    {
                        placed = true;
                    }
                }

                Assert.IsTrue(placed, name + " is not placed as a prefab instance");
            }
        }
    }
}
