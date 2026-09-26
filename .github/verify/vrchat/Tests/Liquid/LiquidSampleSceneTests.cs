using NUnit.Framework;
using SabaProps.Liquid.Editors;
using UdonSharp.Compiler;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UdonSharp;
using UdonSharpEditor;
using VRC.SDK3.Components;
using VRC.Udon;

namespace SabaProps.Liquid.WorldTests
{
    /// <summary>
    /// The generated demo world.
    /// <para>
    /// Most of what makes the demo work is geometry that fails silently when it
    /// is wrong: a trigger that stops short of the surface never wets anyone, a
    /// mud floor level with the mud never lets anyone sink. These checks read
    /// the saved scene rather than the builder's intent.
    /// </para>
    /// </summary>
    public class LiquidSampleSceneTests
    {
        private Scene _scene;

        [OneTimeSetUp]
        public void CreateScene()
        {
            UdonSharpCompilerV1.CompileSync(new UdonSharpCompileOptions { IsEditorBuild = true });
            LiquidSampleScene.Create();
            _scene = EditorSceneManager.OpenScene(LiquidSampleScene.ScenePath, OpenSceneMode.Single);
        }

        [OneTimeTearDown]
        public void CloseScene()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        }

        [Test]
        public void Scene_IsSavedAsAWorldWithASpawn()
        {
            Assert.IsNotNull(AssetDatabase.LoadAssetAtPath<SceneAsset>(LiquidSampleScene.ScenePath));

            VRCSceneDescriptor descriptor = Object.FindObjectOfType<VRCSceneDescriptor>();
            Assert.IsNotNull(descriptor, "no VRCSceneDescriptor");
            Assert.AreEqual(1, descriptor.spawns.Length);
            Assert.AreEqual(LiquidSampleScene.SpawnPosition, descriptor.spawns[0].position);
        }

        [Test]
        public void EverySource_IsWiredToThePoolAndAProfile()
        {
            LiquidCanvasPool pool = Object.FindObjectOfType<LiquidCanvasPool>();
            Assert.IsNotNull(pool, "no canvas pool");
            Assert.AreEqual(1, Object.FindObjectsOfType<LiquidCanvasPool>().Length, "more than one canvas pool");

            LiquidImmersionVolume[] volumes = Object.FindObjectsOfType<LiquidImmersionVolume>();
            LiquidShower[] showers = Object.FindObjectsOfType<LiquidShower>();
            LiquidWaterGun[] guns = Object.FindObjectsOfType<LiquidWaterGun>();

            Assert.AreEqual(4, volumes.Length, "expected the pool, the mud bog, the tank and the mud tub");
            Assert.AreEqual(3, showers.Length, "expected the shower, the faucet and the comparison shower");
            Assert.AreEqual(2, guns.Length, "expected two water guns");

            foreach (LiquidSprayer sprayer in Object.FindObjectsOfType<LiquidSprayer>())
            {
                Assert.AreSame(pool, sprayer.pool, sprayer.name);
                Assert.IsNotNull(sprayer.profile, sprayer.name);
            }

            foreach (LiquidImmersionVolume volume in volumes)
            {
                Assert.AreSame(pool, volume.pool, volume.name);
                Assert.IsNotNull(volume.profile, volume.name);
                Assert.IsNotNull(volume.surface, volume.name + " has no surface");
            }

            foreach (LiquidShower shower in showers)
            {
                Assert.AreSame(pool, shower.pool, shower.name);
                Assert.IsNotNull(shower.profile, shower.name);
                Assert.IsNotNull(shower.GetComponent<Collider>(), shower.name + " cannot be toggled");
            }

            foreach (LiquidWaterGun gun in guns)
            {
                Assert.AreSame(pool, gun.pool, gun.name);
                Assert.IsNotNull(gun.profile, gun.name);
            }
        }

        [Test]
        public void Triggers_ReachFromTheFloorToTheSurface()
        {
            Physics.SyncTransforms();
            foreach (LiquidImmersionVolume volume in Object.FindObjectsOfType<LiquidImmersionVolume>())
            {
                var trigger = volume.GetComponent<BoxCollider>();
                Assert.IsNotNull(trigger, volume.name);
                Assert.IsTrue(trigger.isTrigger, volume.name + " blocks players");

                Bounds bounds = trigger.bounds;
                Assert.AreEqual(volume.surface.position.y, bounds.max.y, 0.02f,
                    volume.name + ": the trigger top is not at the liquid surface");

                // Whatever a body stands on inside the volume must be within the trigger.
                Vector3 above = new Vector3(bounds.center.x, bounds.max.y + 1f, bounds.center.z);
                float floor = Physics.Raycast(above, Vector3.down, out RaycastHit hit, 10f, ~(1 << LiquidMannequinBuilder.MannequinLayer),
                    QueryTriggerInteraction.Ignore) ? hit.point.y : 0f;
                Assert.Less(bounds.min.y, floor, volume.name + ": a body standing on the floor is outside the trigger");
            }
        }

        [Test]
        public void Mannequins_AreRegisteredAndDrawOnlyOnTheirOwnLayer()
        {
            LiquidCanvasPool pool = Object.FindObjectOfType<LiquidCanvasPool>();
            Assert.IsNotNull(pool.mannequins);
            int expected = 6                                                // clothed row: three outfits, two liquids
                + 7                                                         // source row
                + 2 * 5;                                                    // two weather yards: four in the open, one sheltered
            Assert.AreEqual(expected, pool.mannequins.Length, "a row is missing mannequins");

            AssertMannequinsDrawOnTheirOwnLayer(pool);
            foreach (LiquidBodyCanvas mannequin in pool.mannequins)
            {
                StringAssert.DoesNotMatch(@"_1\d\d\.mat$", AssetDatabase.GetAssetPath(mannequin.projectorMaterial),
                    "a demo mannequin uses a projector material numbered for the comparison scene");
            }
        }

        [Test]
        public void EveryUdonBehaviour_LoadsItsProgram()
        {
            AssertEveryProgramLoads();
        }

        [Test]
        public void ResetPanel_IsBesideTheSpawn()
        {
            AssertResetPanel(LiquidSampleScene.SpawnPosition);
        }

        /// <summary>
        /// The scene has a reset panel near the spawn, and each of its three
        /// buttons calls an existing method on it.
        /// </summary>
        internal static void AssertResetPanel(Vector3 spawn)
        {
            LiquidResetPanel[] panels = Object.FindObjectsOfType<LiquidResetPanel>();
            Assert.AreEqual(1, panels.Length, "expected one reset panel");
            Assert.Less(Vector3.Distance(panels[0].transform.position, spawn), 5f, "the reset panel is far from the spawn");

            LiquidButton[] buttons = panels[0].GetComponentsInChildren<LiquidButton>();
            CollectionAssert.AreEquivalent(
                new[] { nameof(LiquidResetPanel.ClearMine), nameof(LiquidResetPanel.ClearMannequins), nameof(LiquidResetPanel.ClearEveryone) },
                System.Array.ConvertAll(buttons, b => b.eventName));
            foreach (LiquidButton button in buttons)
            {
                Assert.AreSame(panels[0], button.target, button.name);
            }
        }

        /// <summary>
        /// Every UdonBehaviour in the open scene has a serialized program that
        /// deserializes. A program asset that exists but cannot be read (a file
        /// left zero-filled by an interrupted save, for one) passes a null check
        /// on the reference and only fails in the VRChat client, which then
        /// skips the behaviour with "Could not load the program".
        /// </summary>
        internal static void AssertEveryProgramLoads()
        {
            foreach (UdonSharpBehaviour behaviour in Object.FindObjectsOfType<UdonSharpBehaviour>(true))
            {
                UdonBehaviour backing = UdonSharpEditorUtility.GetBackingUdonBehaviour(behaviour);
                Assert.IsNotNull(backing, behaviour.name + " has no backing UdonBehaviour");
                Assert.IsNotNull(backing.programSource, behaviour.name + " has no program source");
                Assert.IsNotNull(backing.programSource.SerializedProgramAsset, behaviour.name + " has no serialized program");
                Assert.IsNotNull(backing.programSource.SerializedProgramAsset.RetrieveProgram(),
                    behaviour.name + ": the serialized program " + AssetDatabase.GetAssetPath(backing.programSource.SerializedProgramAsset)
                    + " does not load");
            }
        }

        [Test]
        public void ClothedMannequins_UseTheSurfaceOfEachGarment()
        {
            GameObject row = GameObject.Find(LiquidDemoWeather.ClothedRowName);
            Assert.IsNotNull(row);
            LiquidBodyCanvas[] clothed = row.GetComponentsInChildren<LiquidBodyCanvas>();
            Assert.AreEqual(6, clothed.Length);

            foreach (LiquidBodyCanvas canvas in clothed)
            {
                Assert.IsTrue(canvas.estimateRegions, canvas.name + " does not tell hair and skin from clothing");
                Assert.IsNotNull(canvas.lowerSurface, canvas.name + " has no trouser surface");
                Assert.IsNotNull(canvas.feetSurface, canvas.name + " has no shoe surface");
                Assert.IsNotNull(canvas.leftFootAnchor, canvas.name + " has no foot anchors");
                Assert.IsNotNull(canvas.rightFootAnchor, canvas.name + " has no foot anchors");
            }
        }

        [Test]
        public void WeatherYards_FallOnTheOpenAndSpareTheSheltered()
        {
            LiquidCanvasPool pool = Object.FindObjectOfType<LiquidCanvasPool>();
            LiquidWeather[] areas = Object.FindObjectsOfType<LiquidWeather>();
            Assert.AreEqual(2, areas.Length, "expected a rain yard and a snow yard");
            Assert.AreEqual(1, System.Array.FindAll(areas, a => a.snow).Length, "expected exactly one snow yard");

            foreach (LiquidWeather weather in areas)
            {
                Assert.AreSame(pool, weather.pool, weather.name);
                Assert.IsTrue(weather.snow || weather.profile != null, weather.name + ": rain without a liquid");
                Assert.IsNotNull(weather.precipitation, weather.name + " has no particles");
                Assert.IsNotEmpty(weather.groundMaterials, weather.name + " has no ground to wet");
                foreach (Material ground in weather.groundMaterials)
                {
                    Assert.AreEqual(LiquidWeatherBuilder.WeatherSurfaceShader, ground.shader.name, ground.name);
                }

                // Each yard's mannequins stand inside its area. The ones in the open see the sky;
                // the one in the shelter has the roof above it, on a layer the area treats as cover.
                Transform yard = weather.transform.parent.parent;
                LiquidBodyCanvas[] bodies = yard.GetComponentsInChildren<LiquidBodyCanvas>();
                Assert.AreEqual(5, bodies.Length, yard.name);
                Physics.SyncTransforms();
                int sheltered = 0;
                foreach (LiquidBodyCanvas body in bodies)
                {
                    Vector3 local = weather.transform.InverseTransformPoint(body.anchor.position);
                    Assert.Less(Mathf.Abs(local.x), weather.areaSize.x * 0.5f, body.name + " stands outside the area");
                    Assert.Less(Mathf.Abs(local.z), weather.areaSize.z * 0.5f, body.name + " stands outside the area");

                    Vector3 top = body.GetBodyTop() + Vector3.up * pool.bodyRadius;
                    if (Physics.Raycast(top, Vector3.up, weather.shelterCheckDistance, pool.occluderLayers,
                        QueryTriggerInteraction.Ignore))
                    {
                        sheltered++;
                    }
                }

                Assert.AreEqual(1, sheltered, yard.name + ": expected exactly one mannequin under the roof");
            }
        }

        /// <summary>
        /// Every mannequin has its own projector material, and both it and its
        /// projector stay on the mannequin layer; player canvases never draw on them.
        /// </summary>
        internal static void AssertMannequinsDrawOnTheirOwnLayer(LiquidCanvasPool pool)
        {
            var materials = new System.Collections.Generic.HashSet<string>();
            foreach (LiquidBodyCanvas mannequin in pool.mannequins)
            {
                Assert.IsNotNull(mannequin);
                Assert.IsNotNull(mannequin.anchor, mannequin.name + " has no anchor, so it would wait for a player");
                Assert.IsNotNull(mannequin.bodySurface, mannequin.name + " has no surface profile");
                Assert.IsTrue(materials.Add(AssetDatabase.GetAssetPath(mannequin.projectorMaterial)),
                    "two mannequins share a projector material");

                Projector projector = mannequin.projectorObject.GetComponent<Projector>();
                Assert.AreEqual(LiquidMannequinBuilder.MannequinLayer, projector.gameObject.layer);
                Assert.AreEqual(~(1 << LiquidMannequinBuilder.MannequinLayer), projector.ignoreLayers,
                    "a mannequin projector lands on something other than mannequins");

                foreach (Renderer renderer in mannequin.transform.parent.GetComponentsInChildren<Renderer>())
                {
                    if (renderer.name == "Turntable Base")
                    {
                        continue;
                    }

                    Assert.AreEqual(LiquidMannequinBuilder.MannequinLayer, renderer.gameObject.layer,
                        renderer.name + " is not on the mannequin layer, so the canvas will not draw on it");
                }
            }

            // Player canvases must not land on mannequins, and carry the avatar surface defaults.
            foreach (LiquidBodyCanvas canvas in pool.canvases)
            {
                Assert.IsNotNull(canvas.bodySurface, "a player canvas has no clothing surface");
                Assert.IsNotNull(canvas.hairSurface, "a player canvas has no hair surface");
                Assert.IsNotNull(canvas.skinSurface, "a player canvas has no skin surface");
                Assert.IsTrue(canvas.estimateRegions, "player canvases do not estimate hair and skin");
                Projector projector = canvas.projectorObject.GetComponent<Projector>();
                Assert.AreNotEqual(0, projector.ignoreLayers & (1 << LiquidMannequinBuilder.MannequinLayer),
                    "player canvases draw on mannequins");
            }
        }

        [Test]
        public void Lighting_HandsTheSunToTheShader()
        {
            LiquidLighting lighting = Object.FindObjectOfType<LiquidLighting>();
            Assert.IsNotNull(lighting, "no LiquidLighting, so liquid is lit by ambient light only");
            Assert.IsNotNull(lighting.mainLight);
            Assert.AreEqual(LightType.Directional, lighting.mainLight.type);
        }

        [Test]
        public void MudBog_LetsPlayersSinkBelowItsSurface()
        {
            Assert.Less(LiquidSampleScene.MudFloorY, LiquidSampleScene.MudSurfaceY - 0.2f,
                "the mud floor is too close to the surface to sink into");

            GameObject mud = GameObject.Find(LiquidSampleScene.MudBogName);
            Assert.IsNotNull(mud);
            foreach (Renderer renderer in mud.GetComponentsInChildren<Renderer>())
            {
                Assert.IsNull(renderer.GetComponent<Collider>(),
                    renderer.name + " has a collider, so players stand on the mud instead of in it");
            }

            // Standing in the middle of the bog, the ground under the player is the floor.
            Physics.SyncTransforms();
            Vector3 above = LiquidSampleScene.MudCentre + Vector3.up * 2f;
            Assert.IsTrue(Physics.Raycast(above, Vector3.down, out RaycastHit hit, 5f, ~0, QueryTriggerInteraction.Ignore));
            Assert.AreEqual(LiquidSampleScene.MudFloorY, hit.point.y, 0.02f, "the first solid thing under the bog is not its floor");
        }

        [Test]
        public void Pool_CanBeWalkedIntoDownTheRamp()
        {
            GameObject pool = GameObject.Find(LiquidSampleScene.PoolName);
            Assert.IsNotNull(pool);
            Transform ramp = pool.transform.Find("Ramp");
            Assert.IsNotNull(ramp);

            // The ramp top is near the spawn side, at ground level; its far end reaches the floor.
            Vector3 nearTop = ramp.TransformPoint(new Vector3(0f, 0.5f, -0.5f));
            Vector3 farTop = ramp.TransformPoint(new Vector3(0f, 0.5f, 0.5f));
            Assert.Less(nearTop.z, farTop.z);
            Assert.AreEqual(0f, nearTop.y, 0.1f, "the ramp does not start at ground level");
            Assert.AreEqual(LiquidSampleScene.PoolFloorY, farTop.y, 0.15f, "the ramp does not reach the pool floor");
        }

        [Test]
        public void Mirror_ReflectsTheSpawnArea()
        {
            GameObject mirror = GameObject.Find(LiquidSampleScene.MirrorName);
            Assert.IsNotNull(mirror);
            Assert.IsNotNull(mirror.GetComponent<VRCMirrorReflection>());

            // A quad faces -Z by default; the spawn is behind that face.
            Vector3 toSpawn = LiquidSampleScene.SpawnPosition - mirror.transform.position;
            Assert.Greater(Vector3.Dot(-mirror.transform.forward, toSpawn), 0f, "the mirror faces away from the spawn");
        }
    }
}
