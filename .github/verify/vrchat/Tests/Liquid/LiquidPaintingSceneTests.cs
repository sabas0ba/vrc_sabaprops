using System.Collections.Generic;
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
    /// The painting demo world and the paint tool prefabs.
    /// <para>
    /// Checked for what fails silently: a Surface Canvas the pool does not know
    /// (liquid passes through its walls), two canvases sharing a projector
    /// material (one overwrites the other), a projector that lands on avatars,
    /// a tool whose number does not match its place in the paint log (a late
    /// joiner sees the drawing in another colour), and a tool that shares its
    /// GameObject with VRCObjectSync.
    /// </para>
    /// </summary>
    public class LiquidPaintingSceneTests
    {
        [OneTimeSetUp]
        public void CreateScene()
        {
            UdonSharpCompilerV1.CompileSync(new UdonSharpCompileOptions { IsEditorBuild = true });
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            LiquidSampleScene.EnsurePrefabs();
            LiquidPaintingScene.Create();
            EditorSceneManager.OpenScene(LiquidPaintingScene.ScenePath, OpenSceneMode.Single);
            Physics.SyncTransforms();
        }

        [OneTimeTearDown]
        public void CloseScene()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        }

        [Test]
        public void EveryUdonBehaviour_LoadsItsProgram()
        {
            LiquidSampleSceneTests.AssertEveryProgramLoads();
        }

        [Test]
        public void ResetPanel_IsBesideTheSpawn()
        {
            LiquidSampleSceneTests.AssertResetPanel(LiquidPaintingScene.SpawnPosition);
        }

        [Test]
        public void Scene_IsAWorld()
        {
            Assert.IsNotNull(Object.FindObjectOfType<VRCSceneDescriptor>(), "the scene has no scene descriptor");
            LiquidSampleSceneTests.AssertMovement();
            LiquidCanvasPool pool = Object.FindObjectOfType<LiquidCanvasPool>();
            Assert.IsNotNull(pool);
            Assert.AreEqual(1, pool.mannequins.Length, "the studio mannequin is not registered");
        }

        [Test]
        public void SurfaceCanvases_AreRegisteredAndCoverTheirArea()
        {
            LiquidCanvasPool pool = Object.FindObjectOfType<LiquidCanvasPool>();
            var inScene = new List<LiquidBodyCanvas>();
            foreach (LiquidBodyCanvas canvas in Object.FindObjectsOfType<LiquidBodyCanvas>(true))
            {
                if (canvas.worldSurface)
                {
                    inScene.Add(canvas);
                }
            }

            Assert.AreEqual(5, inScene.Count, "studio, dark studio, two wall bays and the rain patio");
            CollectionAssert.AreEquivalent(inScene, pool.surfaces, "the pool does not know every Surface Canvas");
            Assert.LessOrEqual(pool.surfaces.Length, LiquidPaintLog.MaxSurfaces);

            var materials = new HashSet<Material>();
            long bytes = 0;
            foreach (LiquidBodyCanvas canvas in pool.surfaces)
            {
                string name = canvas.transform.parent.parent.name;
                Assert.IsNotNull(canvas.anchor, name + ": a Surface Canvas needs an anchor");
                Assert.IsNotNull(canvas.bodySurface, name + ": no receiving surface");
                Assert.IsFalse(canvas.estimateRegions, name + ": body regions on a wall");
                Assert.IsTrue(materials.Add(canvas.projectorMaterial), name + ": shares its projector material with another canvas");
                Assert.AreEqual("SabaProps/Liquid/Body Projector", canvas.projectorMaterial.shader.name);

                var projector = canvas.projectorObject.GetComponent<Projector>();
                Assert.IsTrue(projector.orthographic);
                Assert.AreEqual(0, projector.ignoreLayers & ((1 << 0) | (1 << 11)), name + ": does not land on Default and Environment");
                Assert.AreNotEqual(0, projector.ignoreLayers & (1 << LiquidCanvasPoolBuilder.PlayerLayer), name + ": lands on avatars");
                Assert.AreNotEqual(0, projector.ignoreLayers & (1 << 13), name + ": lands on pickups");
                Assert.AreNotEqual(0, projector.ignoreLayers & (1 << LiquidMannequinBuilder.MannequinLayer), name + ": lands on mannequins");
                Assert.AreEqual(canvas.halfExtents.y, projector.orthographicSize, 1e-4f, name);
                Assert.AreEqual(canvas.halfExtents.z * 2f, projector.farClipPlane, 1e-4f, name);

                // The ground under the canvas is inside the box, clear of the fade at its edge.
                Vector3 centre = canvas.anchor.position;
                Assert.Less(centre.y - canvas.halfExtents.y, -0.1f, name + ": the box does not reach below the floor");
                Assert.IsTrue(Physics.Raycast(centre, Vector3.down, out RaycastHit floor, canvas.halfExtents.y,
                    pool.occluderLayers, QueryTriggerInteraction.Ignore), name + ": no floor under the canvas");

                bytes += 240L * canvas.faceResolution * canvas.faceResolution;
            }

            TestContext.WriteLine("Surface Canvas render textures: " + (bytes / (1024 * 1024)) + " MiB");
            Assert.Less(bytes, 120L * 1024 * 1024, "the Surface Canvases take more texture memory than the demo should");
        }

        [Test]
        public void Studio_WallsFaceTheToolsInsideTheCanvas()
        {
            LiquidCanvasPool pool = Object.FindObjectOfType<LiquidCanvasPool>();
            LiquidBodyCanvas studio = Surface(LiquidPaintingScene.StudioName);
            Vector3 from = LiquidPaintingScene.StudioCentre + new Vector3(0f, 1.5f, 0f);

            foreach (Vector3 direction in new[] { Vector3.forward, Vector3.left, Vector3.right, Vector3.down })
            {
                Assert.IsTrue(Physics.Raycast(from, direction, out RaycastHit hit, 10f, pool.occluderLayers,
                    QueryTriggerInteraction.Ignore), "nothing to draw on towards " + direction);
                Vector3 local = studio.anchor.InverseTransformPoint(hit.point);
                Assert.Less(Mathf.Abs(local.x), studio.halfExtents.x - 0.1f, "the wall towards " + direction + " is at the edge of the box");
                Assert.Less(Mathf.Abs(local.y), studio.halfExtents.y - 0.1f, "the floor is at the edge of the box");
                Assert.Less(Mathf.Abs(local.z), studio.halfExtents.z - 0.1f, "the wall towards " + direction + " is at the edge of the box");
            }
        }

        [Test]
        public void PaintTools_AreNumberedAsThePaintLogListsThem()
        {
            LiquidCanvasPool pool = Object.FindObjectOfType<LiquidCanvasPool>();
            LiquidPaintLog[] logs = Object.FindObjectsOfType<LiquidPaintLog>(true);
            Assert.AreEqual(1, logs.Length, "expected one paint log");
            Assert.AreSame(logs[0], pool.paintLog, "the pool does not know the paint log");
            Assert.AreSame(pool, logs[0].pool);

            LiquidPaintTool[] tools = Object.FindObjectsOfType<LiquidPaintTool>(true);
            int expected = LiquidPaintingScene.PenInks.Length + LiquidPaintingScene.StampShapes.Length + 2
                + LiquidPaintingScene.GlowInks.Length + 1;
            Assert.AreEqual(expected, tools.Length);
            Assert.AreEqual(tools.Length, logs[0].tools.Length);
            Assert.LessOrEqual(tools.Length, LiquidPaintLog.MaxTools);

            for (int i = 0; i < logs[0].tools.Length; i++)
            {
                Assert.IsNotNull(logs[0].tools[i], "the paint log has an empty slot at " + i);
                Assert.AreEqual(i, logs[0].tools[i].toolIndex, logs[0].tools[i].transform.parent.name + " has another number than its slot");
            }
        }

        [Test]
        public void PaintTools_AreWiredAsPickups()
        {
            LiquidCanvasPool pool = Object.FindObjectOfType<LiquidCanvasPool>();
            foreach (LiquidPaintTool tool in Object.FindObjectsOfType<LiquidPaintTool>(true))
            {
                AssertTool(tool.transform.parent.gameObject, tool.mode);
                Assert.AreSame(pool, tool.pool, tool.transform.parent.name);
            }

            GameObject table = GameObject.Find(LiquidPaintingScene.ToolTableName);
            var stamps = new List<int>();
            int pens = 0;
            int erasers = 0;
            foreach (LiquidPaintTool tool in table.GetComponentsInChildren<LiquidPaintTool>())
            {
                if (tool.mode == LiquidPaintTool.ModeStamp)
                {
                    stamps.Add(tool.shape);
                }
                else if (tool.mode == LiquidPaintTool.ModePen)
                {
                    pens++;
                }
                else
                {
                    erasers++;
                }
            }

            CollectionAssert.AreEquivalent(LiquidPaintingScene.StampShapes, stamps, "the table does not hold one stamp of each shape");
            Assert.AreEqual(LiquidPaintingScene.PenInks.Length, pens);
            Assert.AreEqual(2, erasers);

            // The dark studio's pens glow, apart from the white one that is there to compare.
            int glowing = 0;
            foreach (LiquidPaintTool tool in GameObject.Find(LiquidPaintingScene.DarkStudioName).GetComponentsInChildren<LiquidPaintTool>())
            {
                if (tool.profile != null && tool.profile.fluorescence + tool.profile.luminescence > 0f)
                {
                    glowing++;
                }
            }

            Assert.AreEqual(3, glowing, "fluorescent pink, fluorescent green and luminous");
        }

        [Test]
        public void PaintToolPrefabs_CarryTheirInkAndLeaveThePoolToBeFound()
        {
            int[] modes = { LiquidPaintTool.ModePen, LiquidPaintTool.ModeStamp, LiquidPaintTool.ModeEraser };
            string[] names = { LiquidPrefabBuilder.PenName, LiquidPrefabBuilder.StampName, LiquidPrefabBuilder.EraserName };
            for (int i = 0; i < names.Length; i++)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(LiquidPrefabBuilder.PrefabPath(names[i]));
                Assert.IsNotNull(prefab, names[i] + " was not built");
                AssertTool(prefab, modes[i]);

                LiquidPaintTool tool = prefab.GetComponentInChildren<LiquidPaintTool>(true);
                Assert.IsNull(tool.pool, names[i] + ": the pool is set in the prefab, so it would point outside it");
                Assert.AreEqual(-1, tool.toolIndex, names[i] + ": numbered before it is registered in a scene");
                if (tool.profile != null)
                {
                    Assert.IsTrue(tool.profile.transform.IsChildOf(prefab.transform), names[i] + ": the ink is outside the prefab");
                }

                foreach (string dependency in AssetDatabase.GetDependencies(LiquidPrefabBuilder.PrefabPath(names[i]), true))
                {
                    if (dependency.EndsWith(".cs") || dependency.EndsWith(".shader") || dependency.EndsWith(".cginc")
                        || dependency.StartsWith("Assets/SerializedUdonPrograms/"))
                    {
                        continue;
                    }

                    Assert.IsFalse(dependency.StartsWith("Assets/"), names[i] + " depends on a project asset: " + dependency);
                }
            }
        }

        [Test]
        public void DarkStudio_LightZoneReachesItsSurfaceCanvas()
        {
            GameObject room = GameObject.Find(LiquidPaintingScene.DarkStudioName);
            LiquidLightZone zone = room.GetComponentInChildren<LiquidLightZone>();
            LiquidBodyCanvas surface = Surface(LiquidPaintingScene.DarkStudioName);
            Vector3 local = zone.transform.InverseTransformPoint(surface.anchor.position);
            Assert.Less(Mathf.Abs(local.x), zone.areaSize.x * 0.5f);
            Assert.Less(Mathf.Abs(local.y), zone.areaSize.y * 0.5f);
            Assert.Less(Mathf.Abs(local.z), zone.areaSize.z * 0.5f);
            Assert.IsNotNull(zone.lamp);
            Assert.IsNotNull(zone.blacklight);

            // Closed overhead, so the sun does not light the wall the ink is on.
            LiquidCanvasPool pool = Object.FindObjectOfType<LiquidCanvasPool>();
            Assert.IsTrue(Physics.Raycast(LiquidPaintingScene.DarkStudioCentre + Vector3.up, Vector3.up, 5f, pool.occluderLayers),
                "the dark studio has no roof");
        }

        [Test]
        public void RainPatio_HasOpenSkyAndACanopy()
        {
            LiquidCanvasPool pool = Object.FindObjectOfType<LiquidCanvasPool>();
            LiquidWeather rain = GameObject.Find(LiquidPaintingScene.RainPatioName).GetComponentInChildren<LiquidWeather>();
            Assert.IsNotNull(rain);
            Assert.IsFalse(rain.snow);
            Assert.Greater(rain.surfaceDropsPerSecond, 0f);

            Vector3 centre = LiquidPaintingScene.RainPatioCentre;
            Vector3 half = LiquidPaintingScene.RainPatioSize * 0.5f;
            Assert.IsFalse(Physics.Raycast(centre + new Vector3(0.8f, 0.3f, -0.5f), Vector3.up, 10f, pool.occluderLayers),
                "the open part of the patio is covered");
            Assert.IsTrue(Physics.Raycast(centre + new Vector3(-half.x + 0.8f, 0.3f, -half.z + 0.8f), Vector3.up, 10f, pool.occluderLayers),
                "the canopy does not cover its corner");
        }

        private static void AssertTool(GameObject root, int mode)
        {
            string name = root.name;
            Assert.IsNotNull(root.GetComponent<VRCPickup>(), name + " cannot be picked up");
            Assert.IsNotNull(root.GetComponent<VRCObjectSync>(), name + " is not synced");
            Assert.AreEqual(13, root.layer, name + " is not on the Pickup layer");

            LiquidPaintTool tool = root.GetComponentInChildren<LiquidPaintTool>(true);
            Assert.IsNotNull(tool, name + " has no paint tool");
            Assert.AreEqual(mode, tool.mode, name);
            Assert.AreNotSame(root, tool.gameObject, name + ": the tool sits next to VRCObjectSync");
            Assert.AreSame(root.GetComponent<VRCPickup>(), tool.pickup, name);
            Assert.IsNotNull(tool.tip, name);
            Assert.IsNotNull(tool.marker, name);
            Assert.IsFalse(tool.marker.gameObject.activeSelf, name + ": the marker shows before anyone holds the tool");
            Assert.IsNull(tool.marker.GetComponent<Collider>(), name + ": the marker would block the tool's own ray");
            Assert.Greater(tool.radius, 0f);
            Assert.AreEqual(mode == LiquidPaintTool.ModeEraser, tool.profile == null, name + ": pens and stamps need ink, erasers none");

            LiquidButton relay = root.GetComponent<LiquidButton>();
            Assert.IsNotNull(relay, name + " has no relay for the use button");
            Assert.IsTrue(relay.relayPickupUse);
            Assert.AreSame(tool, relay.target, name + ": the relay does not reach the tool");
            Assert.AreEqual(nameof(LiquidPaintTool.Trigger), relay.eventName);
            Assert.AreEqual(nameof(LiquidPaintTool.Release), relay.useUpEventName);
            Assert.AreEqual(nameof(LiquidPaintTool.Release), relay.dropEventName);
        }

        /// <summary>The Surface Canvas under the named area.</summary>
        internal static LiquidBodyCanvas Surface(string areaName)
        {
            GameObject area = GameObject.Find(areaName);
            Assert.IsNotNull(area, areaName + " is not in the scene");
            foreach (LiquidBodyCanvas canvas in area.GetComponentsInChildren<LiquidBodyCanvas>(true))
            {
                if (canvas.worldSurface)
                {
                    return canvas;
                }
            }

            Assert.Fail(areaName + " has no Surface Canvas");
            return null;
        }
    }
}
