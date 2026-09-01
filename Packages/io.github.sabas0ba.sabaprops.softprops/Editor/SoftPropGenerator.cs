using System;
using UnityEditor;
using UnityEngine;

namespace SabaProps.SoftProps.Editors
{
    /// <summary>配布用の4種のpropをAssets配下へ生成する。</summary>
    public static class SoftPropGenerator
    {
        public const string OutputRoot = "Assets/SabaProps/SoftPropsGenerated";
        public const string MeshFolder = OutputRoot + "/Meshes";
        public const string MaterialFolder = OutputRoot + "/Materials";
        public const string PrefabFolder = OutputRoot + "/Prefabs";
        public const string ProgramAssetPath = OutputRoot + "/SoftSurfaceContactController.asset";

        private static readonly string[] ContactTags =
        {
            "Head",
            "Torso",
            "Hand",
            "HandL",
            "HandR",
            "Foot",
            "FootL",
            "FootR",
            "Finger",
            "FingerL",
            "FingerR",
            "SoftProbeFinger",
            "SoftProbeRod",
            "SoftProbePlate",
        };

        public static void GenerateAll()
        {
            if (!SoftPropsVrcBridge.IsAvailable(out string reason))
            {
                throw new InvalidOperationException(reason);
            }

            EnsureFolders();

            Shader softShader = Shader.Find("SabaProps/Soft Surface");
            Shader standardShader = Shader.Find("Standard");
            if (softShader == null || standardShader == null)
            {
                throw new InvalidOperationException("必要なshaderを解決できません。");
            }

            Material futonMaterial = CreateSoftMaterial(
                "Futon", softShader, new Color(0.78f, 0.82f, 0.88f), 0.22f, 58f);
            Material mattressMaterial = CreateSoftMaterial(
                "Mattress", softShader, new Color(0.91f, 0.91f, 0.87f), 0.14f, 86f);
            Material sofaMaterial = CreateSoftMaterial(
                "Sofa", softShader, new Color(0.34f, 0.48f, 0.50f), 0.18f, 92f);
            Material cushionMaterial = CreateSoftMaterial(
                "Cushion", softShader, new Color(0.72f, 0.38f, 0.32f), 0.16f, 74f);
            Material skinMaterial = CreateSkinMaterial("DefaultSkinMatte", softShader);
            Material woodMaterial = CreateStandardMaterial(
                "Wood", standardShader, new Color(0.25f, 0.12f, 0.06f), 0.24f);
            Material sofaFrameMaterial = CreateStandardMaterial(
                "SofaFrame", standardShader, new Color(0.13f, 0.18f, 0.18f), 0.12f);
            Material probeMaterial = CreateStandardMaterial(
                "ContactProbe", standardShader, new Color(0.20f, 0.22f, 0.24f), 0.06f);

            CreateFuton(futonMaterial);
            CreateBed(mattressMaterial, woodMaterial);
            CreateSofa(sofaMaterial, sofaFrameMaterial);
            CreateCushion(cushionMaterial);
            CreateContactProbeTest(skinMaterial, probeMaterial);

            SoftPropsVrcBridge.CompileControllerProgram(ProgramAssetPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        public static GameObject CreateShowcase()
        {
            GameObject futon = LoadPrefab("Futon");
            GameObject bed = LoadPrefab("Bed");
            GameObject sofa = LoadPrefab("Sofa");
            GameObject cushion = LoadPrefab("Cushion");

            if (futon == null || bed == null || sofa == null || cushion == null)
            {
                throw new InvalidOperationException("先にGenerate All Prefabsを実行してください。");
            }

            var root = new GameObject("SabaProps Soft Props Showcase");
            InstantiatePrefab(futon, root.transform, new Vector3(-1.7f, 0f, 0f), Quaternion.identity);
            InstantiatePrefab(bed, root.transform, new Vector3(1.4f, 0f, 0f), Quaternion.identity);
            InstantiatePrefab(sofa, root.transform, new Vector3(-1.3f, 0f, 2.6f), Quaternion.identity);
            InstantiatePrefab(cushion, root.transform, new Vector3(1.1f, 0f, 2.5f), Quaternion.Euler(0f, 18f, 0f));
            Undo.RegisterCreatedObjectUndo(root, "Create Soft Props Showcase");
            Selection.activeGameObject = root;
            return root;
        }

        public static GameObject CreateContactProbeTestInScene()
        {
            GameObject prefab = LoadPrefab("ContactProbeTest");
            if (prefab == null)
            {
                throw new InvalidOperationException("先にGenerate All Prefabsを実行してください。");
            }

            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            Undo.RegisterCreatedObjectUndo(instance, "Create Contact Probe Test");
            Selection.activeGameObject = instance;
            return instance;
        }

        private static void CreateFuton(Material material)
        {
            var preset = new SurfacePreset
            {
                hardness = 0.20f,
                maximumIndent = 0.105f,
                radius = 0.26f,
                rimLift = 0.007f,
                wrinkleStrength = 0.010f,
                wrinkleFrequency = 21f,
                responseSeconds = 0.09f,
                recoverySeconds = 0.62f,
                activationDistance = 0.012f,
            };

            GameObject root = new GameObject("Futon");
            try
            {
                CreateSoftSurface(root.transform, "FutonSurface", "Futon", new Vector3(1.05f, 0.12f, 2.05f),
                    new Vector3(0f, 0.07f, 0f), Quaternion.identity, 0.055f, 40, 76, material, preset);
                SavePrefab(root, "Futon");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static void CreateBed(Material mattress, Material wood)
        {
            var preset = new SurfacePreset
            {
                hardness = 0.54f,
                maximumIndent = 0.085f,
                radius = 0.31f,
                rimLift = 0.005f,
                wrinkleStrength = 0.0035f,
                wrinkleFrequency = 16f,
                responseSeconds = 0.055f,
                recoverySeconds = 0.28f,
                activationDistance = 0.012f,
            };

            GameObject root = new GameObject("Bed");
            try
            {
                CreateStructuralBox(root.transform, "Frame", new Vector3(0f, 0.23f, 0f),
                    new Vector3(1.52f, 0.20f, 2.12f), wood);
                CreateStructuralBox(root.transform, "Headboard", new Vector3(0f, 0.74f, 1.02f),
                    new Vector3(1.58f, 1.22f, 0.10f), wood);
                CreateStructuralBox(root.transform, "LegFL", new Vector3(-0.68f, 0.12f, -0.92f),
                    new Vector3(0.12f, 0.24f, 0.12f), wood);
                CreateStructuralBox(root.transform, "LegFR", new Vector3(0.68f, 0.12f, -0.92f),
                    new Vector3(0.12f, 0.24f, 0.12f), wood);
                CreateStructuralBox(root.transform, "LegBL", new Vector3(-0.68f, 0.12f, 0.92f),
                    new Vector3(0.12f, 0.24f, 0.12f), wood);
                CreateStructuralBox(root.transform, "LegBR", new Vector3(0.68f, 0.12f, 0.92f),
                    new Vector3(0.12f, 0.24f, 0.12f), wood);

                CreateSoftSurface(root.transform, "Mattress", "BedMattress", new Vector3(1.42f, 0.24f, 2.02f),
                    new Vector3(0f, 0.43f, -0.01f), Quaternion.identity, 0.09f, 54, 76, mattress, preset);
                SavePrefab(root, "Bed");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static void CreateSofa(Material fabric, Material frameMaterial)
        {
            var seatPreset = new SurfacePreset
            {
                hardness = 0.43f,
                maximumIndent = 0.075f,
                radius = 0.23f,
                rimLift = 0.006f,
                wrinkleStrength = 0.005f,
                wrinkleFrequency = 17f,
                responseSeconds = 0.065f,
                recoverySeconds = 0.34f,
                activationDistance = 0.012f,
            };

            var backPreset = seatPreset;
            backPreset.hardness = 0.34f;
            backPreset.maximumIndent = 0.065f;
            backPreset.recoverySeconds = 0.42f;
            backPreset.activationDistance = 0.012f;

            GameObject root = new GameObject("Sofa");
            try
            {
                CreateStructuralBox(root.transform, "Base", new Vector3(0f, 0.34f, 0.10f),
                    new Vector3(2.08f, 0.36f, 0.83f), frameMaterial);
                CreateStructuralBox(root.transform, "BackFrame", new Vector3(0f, 0.88f, 0.42f),
                    new Vector3(2.08f, 0.90f, 0.20f), frameMaterial);
                CreateStructuralBox(root.transform, "ArmL", new Vector3(-1.02f, 0.66f, 0f),
                    new Vector3(0.20f, 0.68f, 0.93f), frameMaterial);
                CreateStructuralBox(root.transform, "ArmR", new Vector3(1.02f, 0.66f, 0f),
                    new Vector3(0.20f, 0.68f, 0.93f), frameMaterial);

                for (int i = 0; i < 3; i++)
                {
                    float x = (i - 1) * 0.64f;
                    CreateSoftSurface(root.transform, "Seat" + (i + 1), "SofaSeat" + (i + 1),
                        new Vector3(0.61f, 0.17f, 0.66f), new Vector3(x, 0.59f, -0.05f),
                        Quaternion.identity, 0.065f, 24, 26, fabric, seatPreset);

                    CreateSoftSurface(root.transform, "BackCushion" + (i + 1), "SofaBack" + (i + 1),
                        new Vector3(0.61f, 0.16f, 0.64f), new Vector3(x, 0.94f, 0.25f),
                        Quaternion.Euler(-74f, 0f, 0f), 0.06f, 24, 26, fabric, backPreset);
                }

                SavePrefab(root, "Sofa");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static void CreateCushion(Material material)
        {
            var preset = new SurfacePreset
            {
                hardness = 0.12f,
                maximumIndent = 0.095f,
                radius = 0.19f,
                rimLift = 0.009f,
                wrinkleStrength = 0.011f,
                wrinkleFrequency = 23f,
                responseSeconds = 0.11f,
                recoverySeconds = 0.78f,
                activationDistance = 0.012f,
            };

            GameObject root = new GameObject("Cushion");
            try
            {
                CreateSoftSurface(root.transform, "CushionSurface", "Cushion", new Vector3(0.56f, 0.16f, 0.56f),
                    new Vector3(0f, 0.09f, 0f), Quaternion.identity, 0.075f, 34, 34, material, preset);
                SavePrefab(root, "Cushion");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static void CreateContactProbeTest(Material skinMaterial, Material probeMaterial)
        {
            var preset = new SurfacePreset
            {
                hardness = 0.30f,
                maximumIndent = 0.085f,
                radius = 0.20f,
                rimLift = 0.004f,
                wrinkleStrength = 0.0035f,
                wrinkleFrequency = 18f,
                responseSeconds = 0.055f,
                recoverySeconds = 0.38f,
                activationDistance = 0.012f,
            };

            GameObject root = new GameObject("Contact Probe Test");
            try
            {
                CreateSoftSurface(root.transform, "SkinSurface", "ContactProbeSurface",
                    new Vector3(1.75f, 0.18f, 0.78f), new Vector3(0f, 0.09f, 0f),
                    Quaternion.identity, 0.07f, 72, 34, skinMaterial, preset);

                CreateProbe(root.transform, "Finger Probe", PrimitiveType.Sphere,
                    new Vector3(-0.58f, 0.43f, -0.04f), Quaternion.identity,
                    new Vector3(0.07f, 0.07f, 0.07f), probeMaterial,
                    "Sphere", 0.035f, 0f, Vector3.zero, "SoftProbeFinger", 0.08f);
                CreateProbe(root.transform, "Rod Probe", PrimitiveType.Capsule,
                    new Vector3(0f, 0.43f, -0.04f), Quaternion.Euler(0f, 0f, 90f),
                    new Vector3(0.05f, 0.25f, 0.05f), probeMaterial,
                    "Capsule", 0.025f, 0.50f, Vector3.zero, "SoftProbeRod", 0.22f);
                CreateProbe(root.transform, "Plate Probe", PrimitiveType.Cube,
                    new Vector3(0.58f, 0.43f, -0.04f), Quaternion.identity,
                    new Vector3(0.40f, 0.035f, 0.24f), probeMaterial,
                    "Box", 0f, 0f, new Vector3(0.40f, 0.035f, 0.24f),
                    "SoftProbePlate", 0.45f);

                CreateProbeLabel(root.transform, "Finger", new Vector3(-0.58f, 0.49f, 0.33f));
                CreateProbeLabel(root.transform, "Rod", new Vector3(0f, 0.49f, 0.33f));
                CreateProbeLabel(root.transform, "Plate", new Vector3(0.58f, 0.49f, 0.33f));
                SavePrefab(root, "ContactProbeTest");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static void CreateProbe(
            Transform parent,
            string name,
            PrimitiveType primitiveType,
            Vector3 localPosition,
            Quaternion localRotation,
            Vector3 visualScale,
            Material material,
            string contactShape,
            float contactRadius,
            float contactHeight,
            Vector3 contactSize,
            string contactTag,
            float mass)
        {
            var probe = new GameObject(name);
            probe.transform.SetParent(parent, false);
            probe.transform.localPosition = localPosition;
            probe.transform.localRotation = localRotation;

            GameObject visual = GameObject.CreatePrimitive(primitiveType);
            visual.name = "Visual";
            visual.transform.SetParent(probe.transform, false);
            visual.transform.localScale = visualScale;
            visual.GetComponent<MeshRenderer>().sharedMaterial = material;

            var rigidbody = probe.AddComponent<Rigidbody>();
            rigidbody.mass = mass;
            rigidbody.drag = 0.25f;
            rigidbody.angularDrag = 0.35f;
            rigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

            SoftPropsVrcBridge.AddContactSender(
                probe, contactShape, contactRadius, contactHeight, contactSize, contactTag);
            SoftPropsVrcBridge.AddPickup(probe);
        }

        private static void CreateProbeLabel(Transform parent, string text, Vector3 localPosition)
        {
            var label = new GameObject(text + " Label");
            label.transform.SetParent(parent, false);
            label.transform.localPosition = localPosition;
            label.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            var textMesh = label.AddComponent<TextMesh>();
            textMesh.text = text;
            textMesh.anchor = TextAnchor.MiddleCenter;
            textMesh.alignment = TextAlignment.Center;
            textMesh.characterSize = 0.055f;
            textMesh.fontSize = 48;
            textMesh.color = new Color(0.18f, 0.18f, 0.18f, 1f);
        }

        private static GameObject CreateSoftSurface(
            Transform parent,
            string objectName,
            string assetName,
            Vector3 size,
            Vector3 localPosition,
            Quaternion localRotation,
            float cornerRadius,
            int xSegments,
            int zSegments,
            Material material,
            SurfacePreset preset)
        {
            string meshPath = MeshFolder + "/" + assetName + ".asset";
            ReplaceAsset(meshPath);
            Mesh mesh = SoftPropMeshBuilder.BuildRoundedBox(assetName, size, cornerRadius, xSegments, zSegments);
            AssetDatabase.CreateAsset(mesh, meshPath);

            var surface = new GameObject(objectName);
            surface.transform.SetParent(parent, false);
            surface.transform.localPosition = localPosition;
            surface.transform.localRotation = localRotation;

            var filter = surface.AddComponent<MeshFilter>();
            filter.sharedMesh = mesh;
            var renderer = surface.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;

            var collider = surface.AddComponent<BoxCollider>();
            collider.size = size;

            float planeY = size.y * 0.5f;
            float below = Mathf.Min(0.018f, size.y * 0.20f);
            Vector3 receiverSize = new Vector3(
                size.x * 0.96f,
                preset.activationDistance + below,
                size.z * 0.96f);
            Vector3 receiverPosition = new Vector3(
                0f,
                planeY + (preset.activationDistance - below) * 0.5f,
                0f);

            SoftPropsVrcBridge.AddBoxReceiver(surface, receiverSize, receiverPosition, ContactTags);
            SoftSurfaceContactController controller =
                SoftPropsVrcBridge.AddController(surface, ProgramAssetPath);
            controller.targetRenderer = renderer;
            controller.surfaceTransform = surface.transform;
            controller.hardness = preset.hardness;
            controller.maximumIndent = preset.maximumIndent;
            controller.contactRadius = preset.radius;
            controller.rimLift = preset.rimLift;
            controller.wrinkleStrength = preset.wrinkleStrength;
            controller.wrinkleFrequency = preset.wrinkleFrequency;
            controller.responseSeconds = preset.responseSeconds;
            controller.recoverySeconds = preset.recoverySeconds;
            controller.surfacePlaneY = planeY;
            return surface;
        }

        private static GameObject CreateStructuralBox(
            Transform parent,
            string name,
            Vector3 localPosition,
            Vector3 localScale,
            Material material)
        {
            GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = name;
            box.transform.SetParent(parent, false);
            box.transform.localPosition = localPosition;
            box.transform.localScale = localScale;
            box.GetComponent<MeshRenderer>().sharedMaterial = material;
            return box;
        }

        private static Material CreateSoftMaterial(
            string name,
            Shader shader,
            Color color,
            float smoothness,
            float weaveScale)
        {
            string path = MaterialFolder + "/" + name + ".mat";
            ReplaceAsset(path);
            var material = new Material(shader) { name = name };
            material.SetColor("_Color", color);
            material.SetFloat("_Smoothness", smoothness);
            material.SetFloat("_WeaveScale", weaveScale);
            material.SetFloat("_WeaveContrast", 0.055f);
            material.SetFloat("_SurfaceGrainStrength", 0.010f);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        private static Material CreateSkinMaterial(string name, Shader shader)
        {
            string path = MaterialFolder + "/" + name + ".mat";
            ReplaceAsset(path);
            var material = new Material(shader) { name = name };
            material.SetColor("_Color", new Color(0.86f, 0.67f, 0.55f, 1f));
            material.SetFloat("_Smoothness", 0.045f);
            material.SetFloat("_WeaveContrast", 0f);
            material.SetFloat("_SurfaceGrainScale", 145f);
            material.SetFloat("_SurfaceGrainStrength", 0.032f);
            material.SetFloat("_WrinkleStrength", 0.0035f);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        private static Material CreateStandardMaterial(
            string name,
            Shader shader,
            Color color,
            float smoothness)
        {
            string path = MaterialFolder + "/" + name + ".mat";
            ReplaceAsset(path);
            var material = new Material(shader) { name = name };
            material.SetColor("_Color", color);
            material.SetFloat("_Glossiness", smoothness);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        private static void SavePrefab(GameObject root, string name)
        {
            string path = PrefabFolder + "/" + name + ".prefab";
            ReplaceAsset(path);
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }

        private static void EnsureFolders()
        {
            EnsureFolder("Assets", "SabaProps");
            EnsureFolder("Assets/SabaProps", "SoftPropsGenerated");
            EnsureFolder(OutputRoot, "Meshes");
            EnsureFolder(OutputRoot, "Materials");
            EnsureFolder(OutputRoot, "Prefabs");
        }

        private static void EnsureFolder(string parent, string name)
        {
            string path = parent + "/" + name;
            if (!AssetDatabase.IsValidFolder(path))
            {
                AssetDatabase.CreateFolder(parent, name);
            }
        }

        private static void ReplaceAsset(string path)
        {
            if (AssetDatabase.LoadMainAssetAtPath(path) != null)
            {
                AssetDatabase.DeleteAsset(path);
            }
        }

        private static GameObject LoadPrefab(string name)
        {
            return AssetDatabase.LoadAssetAtPath<GameObject>(PrefabFolder + "/" + name + ".prefab");
        }

        private static void InstantiatePrefab(
            GameObject prefab,
            Transform parent,
            Vector3 localPosition,
            Quaternion localRotation)
        {
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            instance.transform.localPosition = localPosition;
            instance.transform.localRotation = localRotation;
        }

        private struct SurfacePreset
        {
            public float hardness;
            public float maximumIndent;
            public float radius;
            public float rimLift;
            public float wrinkleStrength;
            public float wrinkleFrequency;
            public float responseSeconds;
            public float recoverySeconds;
            public float activationDistance;
        }
    }
}
