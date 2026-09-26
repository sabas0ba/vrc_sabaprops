using System.Text;
using SabaProps.Tablet.Authoring;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using VRC.SDK3.Components;

namespace SabaProps.Tablet.Editors
{
    /// <summary>
    /// ミラー、コライダー、エフェクト、ライト、テレポート地点と、それらを操作するタブレットを含む
    /// VRChat World のサンプルシーンを生成します。
    /// </summary>
    public static class TabletSampleScene
    {
        public const string ScenePath = TabletAssets.SampleFolder + "/TabletDemo.unity";
        public const string TabletName = "Tablet";
        public const string StandName = "Tablet Stand";

        /// <summary>低画質ミラーが映すレイヤー。PlayerLocal (10) と MirrorReflection (18) です。</summary>
        private const int LowQualityMirrorLayers = (1 << 10) | (1 << 18);

        [MenuItem("Tools/SabaProps/Tablet/Create Sample Scene", false, 1)]
        public static void CreateAndOpen()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            Create();
        }

        /// <summary>
        /// 開いているシーンをサンプルで置き換え、ScenePath に保存します。
        /// 確認ダイアログを出さないため、テストとバッチモードから直接呼べます。
        /// </summary>
        public static Scene Create()
        {
            TabletAssets.EnsureFolder(TabletAssets.SampleFolder);
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            Material ground = SaveMaterial(new Color(0.42f, 0.45f, 0.43f), "SampleGround");
            Material wall = SaveMaterial(new Color(0.55f, 0.36f, 0.30f), "SampleWall");
            Material marker = SaveMaterial(new Color(0.25f, 0.55f, 0.85f), "SampleMarker");

            Primitive(PrimitiveType.Plane, "Ground", new Vector3(0f, 0f, 0f), new Vector3(3f, 1f, 3f), ground);

            GameObject mirrorHigh = Mirror("Mirror HQ", new Vector3(-1.6f, 1.3f, 4f), ~0);
            GameObject mirrorLow = Mirror("Mirror LQ", new Vector3(1.6f, 1.3f, 4f), LowQualityMirrorLayers);
            mirrorLow.SetActive(false);

            GameObject barrier = Primitive(PrimitiveType.Cube, "Barrier", new Vector3(0f, 1f, 2f), new Vector3(4f, 2f, 0.1f), wall);

            var effect = new GameObject("Heavy Effect");
            effect.transform.position = new Vector3(-3f, 0.5f, 0f);
            ParticleSystem particles = effect.AddComponent<ParticleSystem>();
            ParticleSystem.MainModule main = particles.main;
            main.maxParticles = 2000;

            var lampObject = new GameObject("Lamp");
            lampObject.transform.position = new Vector3(3f, 2.5f, 0f);
            Light lamp = lampObject.AddComponent<Light>();
            lamp.type = LightType.Point;
            lamp.range = 6f;
            lamp.color = new Color(1f, 0.8f, 0.55f);
            Material glowSource = SaveMaterial(Color.white, "SampleGlowSource");
            glowSource.EnableKeyword("_EMISSION");
            glowSource.SetColor("_EmissionColor", new Color(4f, 2f, 0.5f));
            RemoveCollider(Primitive(PrimitiveType.Sphere, "Glow Source", new Vector3(3f, 1.5f, 0f),
                Vector3.one * 0.35f, glowSource));

            Transform pointA = Primitive(PrimitiveType.Cylinder, "Point A", new Vector3(-6f, 0.02f, -6f), new Vector3(0.8f, 0.02f, 0.8f), marker).transform;
            Transform pointB = Primitive(PrimitiveType.Cylinder, "Point B", new Vector3(6f, 0.02f, -6f), new Vector3(0.8f, 0.02f, 0.8f), marker).transform;
            Transform stage = Primitive(PrimitiveType.Cylinder, "Mirror Front", new Vector3(0f, 0.02f, 3f), new Vector3(0.8f, 0.02f, 0.8f), marker).transform;
            RemoveCollider(pointA.gameObject);
            RemoveCollider(pointB.gameObject);
            RemoveCollider(stage.gameObject);

            GameObject stand = Primitive(PrimitiveType.Cube, StandName, new Vector3(0f, 0.9f, -1.2f), new Vector3(0.3f, 0.08f, 0.3f), marker);

            TabletDefinition definition = TabletMenu.CreateDefinition(null);
            definition.gameObject.name = TabletName;
            definition.transform.position = new Vector3(0f, 1.2f, -1.2f);
            definition.startVisible = true;
            definition.interactItems.Add(stand);
            TabletSetupWindow.AddMirrors(definition, new[] { mirrorHigh, mirrorLow });
            TabletSetupWindow.AddColliders(definition, new[] { barrier });
            TabletSetupWindow.AddObjects(definition, new[] { effect });
            definition.FindOrAddPage(TabletSetupWindow.ObjectPage).entries.Add(new TabletEntry
            {
                label = "Lamp",
                kind = TabletEntryKind.Toggle,
                behaviours = new Behaviour[] { lamp },
                startOn = true,
            });
            TabletSetupWindow.AddDestinations(definition, new[] { pointA.gameObject, pointB.gameObject, stage.gameObject });

            AddBedMirrors(definition, marker, ground);
            TabletPostEffectsSample.Create(definition, Camera.main);

            TabletBuilder.Build(definition);
            BuildWorld();

            AssetDatabase.SaveAssets();
            EditorSceneManager.SaveScene(scene, ScenePath);
            Debug.Log(Summarise());
            return scene;
        }

        private static GameObject Mirror(string name, Vector3 position, int layers)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name = name;
            RemoveCollider(go);
            go.transform.position = position;
            go.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
            go.transform.localScale = new Vector3(3f, 2.4f, 1f);
            Material material = AssetDatabase.LoadAssetAtPath<Material>(
                "Packages/com.vrchat.base/Runtime/VRCSDK/Sample Assets/Materials/MirrorReflection.mat");
            if (material == null) throw new System.InvalidOperationException("VRChat mirror material was not found.");
            go.GetComponent<MeshRenderer>().sharedMaterial = material;
            VRCMirrorReflection mirror = go.AddComponent<VRCMirrorReflection>();
            mirror.m_ReflectLayers = layers;
            return go;
        }

        private static void AddBedMirrors(TabletDefinition definition, Material frame, Material mattress)
        {
            Vector3 center = new Vector3(6f, 0f, 3f);
            GameObject bed = Primitive(PrimitiveType.Cube, "Bed", center + Vector3.up * 0.35f,
                new Vector3(1.6f, 0.7f, 2.2f), frame);
            GameObject pillow = Primitive(PrimitiveType.Cube, "Pillow", center + new Vector3(0f, 0.76f, 0.7f),
                new Vector3(1.2f, 0.12f, 0.45f), mattress);
            pillow.transform.SetParent(bed.transform, true);
            Collider[] colliders = bed.GetComponentsInChildren<Collider>();
            TabletPage page = definition.FindOrAddPage("Bed Mirrors");
            page.bedDiagram = true;
            string[] names = { "Head", "Feet", "Left", "Right", "Ceiling" };
            Vector3[] positions =
            {
                center + new Vector3(0, 1.3f, 1.4f), center + new Vector3(0, 1.3f, -1.4f),
                center + new Vector3(-1.1f, 1.3f, 0), center + new Vector3(1.1f, 1.3f, 0),
                center + new Vector3(0, 2.5f, 0),
            };
            Vector2[] ui = { new Vector2(0, 0.4f), new Vector2(0, -0.4f), new Vector2(-0.33f, 0),
                new Vector2(0.33f, 0), new Vector2(0.33f, 0.4f) };
            for (int i = 0; i < names.Length; i++)
            {
                GameObject mirror = Mirror("Bed Mirror " + names[i], positions[i], LowQualityMirrorLayers);
                // Quad の可視面 (-Z) をベッド中心の観察位置に向けます。
                mirror.transform.rotation = Quaternion.LookRotation(positions[i] - (center + Vector3.up * 0.8f),
                    i == 4 ? Vector3.forward : Vector3.up);
                mirror.transform.localScale = i == 4 ? new Vector3(1.8f, 2.4f, 1) :
                    new Vector3(i < 2 ? 1.8f : 2.4f, 1.8f, 1);
                mirror.SetActive(false);
                page.entries.Add(new TabletEntry
                {
                    label = names[i], kind = TabletEntryKind.Toggle, objects = new[] { mirror }, startOn = false,
                    customPlacement = true, normalizedCenter = ui[i], normalizedSize = new Vector2(0.25f, 0.19f),
                });
            }
            page.entries.Add(new TabletEntry
            {
                label = "Bed Collider", kind = TabletEntryKind.Toggle, colliders = colliders, startOn = true,
                customPlacement = true, normalizedCenter = new Vector2(-0.33f, -0.4f), normalizedSize = new Vector2(0.27f, 0.19f),
            });
        }

        private static GameObject Primitive(PrimitiveType type, string name, Vector3 position, Vector3 scale, Material material)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.position = position;
            go.transform.localScale = scale;
            go.GetComponent<MeshRenderer>().sharedMaterial = material;
            return go;
        }

        private static void RemoveCollider(GameObject go)
        {
            Collider collider = go.GetComponent<Collider>();
            if (collider != null)
            {
                Object.DestroyImmediate(collider);
            }
        }

        private static Material SaveMaterial(Color color, string name)
        {
            return TabletAssets.CreateOrReplace(
                TabletAssets.ColorMaterial(null, color, 0.2f, name), TabletAssets.SampleFolder + "/" + name + ".mat");
        }

        private static void BuildWorld()
        {
            var world = new GameObject("VRCWorld");
            var spawn = new GameObject("Spawn");
            spawn.transform.SetParent(world.transform, false);
            spawn.transform.position = new Vector3(0f, 0f, -2f);

            var descriptor = world.AddComponent<VRCSceneDescriptor>();
            descriptor.spawns = new[] { spawn.transform };
            descriptor.RespawnHeightY = -50f;
            if (Camera.main != null)
            {
                descriptor.ReferenceCamera = Camera.main.gameObject;
            }
        }

        private static string Summarise()
        {
            var text = new StringBuilder();
            text.AppendLine("[SabaProps Tablet] サンプルシーンを " + ScenePath + " に作成しました。");
            text.AppendLine("・Desktop: B キー、または台の上の箱を Interact するとタブレットを出し入れします。");
            text.AppendLine("・VR: 頭上に手を伸ばして Grab するとその手の前に出ます。ボタンは指先で押せます。");
            text.AppendLine("・ミラー 2 枚は同じグループのため、片方を ON にするともう片方は OFF になります。");
            text.AppendLine("・Bed Mirrors: 頭側・足側・左右・天井を個別に切替。Bed Collider はベッドの表示を保持して接触だけを切替えます。");
            text.AppendLine("・Post Effects: 明るさ・色相・Glow を各自で調整。Desktop はスライダーを狙って Interact、または − / ＋、VR は指で横に動かします。");
            text.AppendLine("構成を変えるときは Tablet の Inspector または Tools > SabaProps > Tablet > Setup Window から編集し、Build してください。");
            return text.ToString();
        }
    }
}
