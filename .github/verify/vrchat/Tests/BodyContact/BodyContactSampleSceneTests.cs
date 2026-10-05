using NUnit.Framework;
using SabaProps.BodyContact.Editors;
using UdonSharpEditor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using VRC.SDK3.Components;
using VRC.Udon;

namespace SabaProps.BodyContact.WorldTests
{
    /// <summary>
    /// The generated scene is what a person opens to try the package in the client, so
    /// what matters is the state it is in after a save and a reload, not the state the
    /// generator left in memory.
    /// </summary>
    public class BodyContactSampleSceneTests
    {
        [Test]
        public void SavedDemo_KeepsTheSystemWiredAndTheDummiesRegistered()
        {
            BodyContactSampleScene.Create();
            EditorSceneManager.OpenScene(BodyContactSampleScene.ScenePath);

            BodyContactSystem[] systems = Object.FindObjectsOfType<BodyContactSystem>();
            Assert.That(systems.Length, Is.EqualTo(1), "the demo must hold exactly one system");
            BodyContactSystem system = systems[0];

            UdonBehaviour systemUdon = UdonSharpEditorUtility.GetBackingUdonBehaviour(system);
            Assert.That(systemUdon, Is.Not.Null);
            Assert.That(systemUdon.programSource, Is.Not.Null, "the system lost its Udon program");

            BodyContactDebugView view = system.debugView;
            Assert.That(view, Is.Not.Null, "the system lost its debug view");
            Assert.That(view.source, Is.SameAs(system));
            Assert.That(view.lineFilter, Is.Not.Null);
            Assert.That(view.lineRenderer, Is.Not.Null);
            Assert.That(view.lineRenderer.sharedMaterial, Is.Not.Null, "the line material was not saved as an asset");
            Assert.That(view.statusLabel, Is.Not.Null);
            Assert.That(view.hudRoot, Is.Not.Null);
            Assert.That(view.lineFilter.transform.position, Is.EqualTo(Vector3.zero));
            Assert.That(view.lineFilter.transform.lossyScale, Is.EqualTo(Vector3.one));

            UdonBehaviour viewUdon = UdonSharpEditorUtility.GetBackingUdonBehaviour(view);
            Assert.That(viewUdon, Is.Not.Null);
            Assert.That(viewUdon.programSource, Is.Not.Null, "the debug view lost its Udon program");

            Assert.That(system.dummies.Length, Is.EqualTo(5));
            AssertDummy(system, 0, BodyContactSampleScene.OpenDummyName, BodyContactSampleScene.OpenDummyPosition, 1f);
            AssertDummy(system, 1, BodyContactSampleScene.WallDummyName, BodyContactSampleScene.WallDummyPosition, 1f);
            AssertDummy(
                system, 2, BodyContactSampleScene.SmallDummyName, BodyContactSampleScene.SmallDummyPosition,
                BodyContactSampleScene.SmallDummyScale);
            AssertDummy(system, 3, BodyContactSampleScene.MovingDummyName, BodyContactSampleScene.MovingDummyPosition, 1f);
            AssertDummy(system, 4, BodyContactSampleScene.TurningDummyName, BodyContactSampleScene.TurningDummyPosition, 1f);
        }

        [Test]
        public void SavedMannequins_MatchContactGeometryWithoutBlockingWorldRaycasts()
        {
            BodyContactSampleScene.Create();
            EditorSceneManager.OpenScene(BodyContactSampleScene.ScenePath);
            BodyContactSystem system = Object.FindObjectOfType<BodyContactSystem>();
            foreach (Transform dummy in system.dummies)
            {
                Assert.That(dummy.GetComponentsInChildren<Collider>().Length, Is.Zero,
                    "visual colliders must not block the system's world collision raycasts");
                for (int part = 0; part < BodyContactSystem.PartCount; part++)
                {
                    Transform endpoint = dummy.Find("Part " + part + " A");
                    Assert.That(endpoint, Is.Not.Null);
                    Vector3 expected = dummy.position + dummy.rotation *
                        (system.StandardJoint(system.PartJointA(part)) * system.dummyEyeHeight * dummy.lossyScale.y);
                    Assert.That(Vector3.Distance(endpoint.position, expected), Is.LessThan(1e-4f));
                    float diameter = system.PartUnitRadius(part) * system.dummyEyeHeight * system.radiusScale * dummy.lossyScale.y * 2f;
                    Assert.That(endpoint.lossyScale.x, Is.EqualTo(diameter).Within(1e-4f));
                }
            }
        }

        [Test]
        public void SavedMotion_MovesTheRegisteredTransformAndLoopsContinuously()
        {
            BodyContactSampleScene.Create();
            EditorSceneManager.OpenScene(BodyContactSampleScene.ScenePath);
            BodyContactSystem system = Object.FindObjectOfType<BodyContactSystem>();
            foreach (int index in new[] { 3, 4 })
            {
                Transform dummy = system.dummies[index];
                Animator animator = dummy.GetComponent<Animator>();
                Assert.That(animator, Is.Not.Null);
                Assert.That(animator.cullingMode, Is.EqualTo(AnimatorCullingMode.AlwaysAnimate));
                AnimationClip clip = animator.runtimeAnimatorController.animationClips[0];
                Assert.That(clip, Is.Not.Null);
                Assert.That(clip.legacy, Is.False);
                Assert.That(clip.wrapMode, Is.EqualTo(WrapMode.Loop));
                clip.SampleAnimation(dummy.gameObject, 0f);
                Vector3 start = dummy.position;
                Quaternion rotation = dummy.rotation;
                clip.SampleAnimation(dummy.gameObject, 2f);
                if (index == 3) Assert.That(Vector3.Distance(start, dummy.position), Is.EqualTo(0.8f).Within(0.01f));
                else Assert.That(Quaternion.Angle(rotation, dummy.rotation), Is.EqualTo(60f).Within(0.1f));
                // 表示用メッシュと判定が参照するTransformが一緒に動きます。
                Transform head = dummy.Find("Part 0 A");
                Vector3 expected = dummy.TransformPoint(system.StandardJoint(BodyContactSystem.JointHead) * system.dummyEyeHeight);
                Assert.That(Vector3.Distance(head.position, expected), Is.LessThan(1e-4f));
                clip.SampleAnimation(dummy.gameObject, clip.length);
                Assert.That(Vector3.Distance(start, dummy.position), Is.LessThan(1e-4f));
                Assert.That(Quaternion.Angle(rotation, dummy.rotation), Is.LessThan(0.01f));
            }
        }

        [Test]
        public void DebugLines_RemainVisibleBehindAnOpaqueSurface()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var cameraObject = new GameObject("Occlusion test camera");
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.transform.position = new Vector3(0f, 0f, -3f);
            camera.orthographic = true;
            camera.orthographicSize = 1f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;
            GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            var surface = new Material(Shader.Find("Unlit/Color"));
            surface.color = Color.black;
            cube.GetComponent<Renderer>().sharedMaterial = surface;
            var line = new GameObject("Occluded line");
            var mesh = new Mesh();
            mesh.vertices = new[] { new Vector3(-0.3f, 0f, 0.8f), new Vector3(0.3f, 0f, 0.8f) };
            mesh.colors = new[] { Color.red, Color.red };
            mesh.SetIndices(new[] { 0, 1 }, MeshTopology.Lines, 0);
            line.AddComponent<MeshFilter>().sharedMesh = mesh;
            var material = new Material(Shader.Find("SabaProps/Body Contact/Debug Lines"));
            line.AddComponent<MeshRenderer>().sharedMaterial = material;
            var target = new RenderTexture(128, 128, 24);
            var pixels = new Texture2D(128, 128, TextureFormat.RGB24, false);
            RenderTexture previous = RenderTexture.active;
            try
            {
                camera.targetTexture = target;
                camera.Render();
                RenderTexture.active = target;
                pixels.ReadPixels(new Rect(0, 0, 128, 128), 0, 0);
                pixels.Apply();
                int redPixels = 0;
                for (int y = 60; y < 68; y++)
                for (int x = 50; x < 78; x++)
                {
                    Color pixel = pixels.GetPixel(x, y);
                    if (pixel.r > 0.15f && pixel.g < 0.05f) redPixels++;
                }
                Assert.That(redPixels, Is.GreaterThan(10), "the opaque cube hid the diagnostic line");
            }
            finally
            {
                camera.targetTexture = null;
                RenderTexture.active = previous;
                Object.DestroyImmediate(pixels);
                Object.DestroyImmediate(target);
                Object.DestroyImmediate(material);
                Object.DestroyImmediate(surface);
                Object.DestroyImmediate(mesh);
            }
        }

        [Test]
        public void DemoShaders_CompileAndRenderTheScene()
        {
            BodyContactSampleScene.Create();
            Shader lines = Shader.Find("SabaProps/Body Contact/Debug Lines");
            Shader floor = Shader.Find("SabaProps/Body Contact/Metric Floor");
            Assert.That(lines, Is.Not.Null);
            Assert.That(floor, Is.Not.Null);
            Assert.That(ShaderUtil.ShaderHasError(lines), Is.False);
            Assert.That(ShaderUtil.ShaderHasError(floor), Is.False);
            BodyContactSystem system = Object.FindObjectOfType<BodyContactSystem>();
            Assert.That(system.debugView.lineRenderer.sharedMaterial.shader, Is.SameAs(lines));
            Camera camera = Camera.main;
            camera.transform.position = new Vector3(0f, 5f, -8f);
            camera.transform.LookAt(new Vector3(0f, 1f, 3f));
            var target = new RenderTexture(1280, 720, 24);
            var pixels = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            RenderTexture previous = RenderTexture.active;
            try
            {
                camera.targetTexture = target;
                camera.Render();
                RenderTexture.active = target;
                pixels.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
                pixels.Apply();
                System.IO.Directory.CreateDirectory("TestResults");
                System.IO.File.WriteAllBytes("TestResults/bodycontact-scene.png", pixels.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture = null;
                RenderTexture.active = previous;
                Object.DestroyImmediate(pixels);
                Object.DestroyImmediate(target);
            }
        }

        [Test]
        public void SavedDemo_HasAWallTheSystemWillStopAt()
        {
            BodyContactSampleScene.Create();
            EditorSceneManager.OpenScene(BodyContactSampleScene.ScenePath);

            BodyContactSystem system = Object.FindObjectOfType<BodyContactSystem>();
            GameObject wall = GameObject.Find(BodyContactSampleScene.WallName);
            Assert.That(wall, Is.Not.Null);
            Assert.That(wall.GetComponent<Collider>(), Is.Not.Null, "the wall has nothing to raycast against");
            Assert.That(
                system.worldCollisionMask.value & (1 << wall.layer), Is.Not.Zero,
                "the wall is on a layer the system does not test, so a player would be pushed through it");

            // The wall has to stand between the spawn and its dummy, with room for a
            // player in the gap. Otherwise the wall check in the README cannot be done.
            float wallZ = wall.transform.position.z;
            float dummyZ = BodyContactSampleScene.WallDummyPosition.z;
            Assert.That(wallZ, Is.LessThan(dummyZ));
            Assert.That(wallZ, Is.GreaterThan(BodyContactSampleScene.SpawnPosition.z));
            Assert.That(dummyZ - wallZ, Is.InRange(0.8f, 1.5f), "the gap between the wall and the dummy");
        }

        [Test]
        public void SavedDemo_IsAVrchatWorldWithAFloorUnderTheSpawn()
        {
            BodyContactSampleScene.Create();
            EditorSceneManager.OpenScene(BodyContactSampleScene.ScenePath);

            VRCSceneDescriptor world = Object.FindObjectOfType<VRCSceneDescriptor>();
            Assert.That(world, Is.Not.Null);
            Assert.That(world.spawns.Length, Is.EqualTo(1));
            Assert.That(world.spawns[0].position, Is.EqualTo(BodyContactSampleScene.SpawnPosition));

            GameObject floor = GameObject.Find(BodyContactSampleScene.FloorName);
            Assert.That(floor, Is.Not.Null);
            Bounds bounds = floor.GetComponent<Collider>().bounds;
            Vector3 spawn = BodyContactSampleScene.SpawnPosition;
            Assert.That(bounds.max.y, Is.EqualTo(0f).Within(1e-4f), "the floor surface must be at height 0");
            Assert.That(spawn.x, Is.InRange(bounds.min.x, bounds.max.x));
            Assert.That(spawn.z, Is.InRange(bounds.min.z, bounds.max.z));
        }

        private static void AssertDummy(BodyContactSystem system, int index, string name, Vector3 position, float scale)
        {
            Transform dummy = system.dummies[index];
            Assert.That(dummy, Is.Not.Null, name + " is missing from the system");
            Assert.That(dummy.name, Is.EqualTo(name));
            Assert.That(Vector3.Distance(dummy.position, position), Is.LessThan(1e-4f), name + " position");
            Assert.That(dummy.lossyScale.y, Is.EqualTo(scale).Within(1e-4f), name + " scale");
        }
    }
}
