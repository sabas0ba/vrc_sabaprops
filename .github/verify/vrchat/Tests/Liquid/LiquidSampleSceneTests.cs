using NUnit.Framework;
using SabaProps.Liquid.Editors;
using UdonSharp.Compiler;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using VRC.SDK3.Components;

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
            Assert.AreEqual(LiquidSourceBuilder.PresetNames.Length + 7, pool.mannequins.Length,
                "expected one mannequin per liquid and seven in the source row");

            var materials = new System.Collections.Generic.HashSet<string>();
            foreach (LiquidBodyCanvas mannequin in pool.mannequins)
            {
                Assert.IsNotNull(mannequin);
                Assert.IsNotNull(mannequin.anchor, mannequin.name + " has no anchor, so it would wait for a player");
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

            // Player canvases must not land on mannequins.
            foreach (LiquidBodyCanvas canvas in pool.canvases)
            {
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
