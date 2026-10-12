using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace SabaProps.Llama
{
    /// <summary>World Runnerを1台置いた、Build &amp; Test用の最小シーンを生成します。</summary>
    public static class LlamaSampleScene
    {
        public const string ScenePath = "Assets/SabaPropsLlama/SampleScene/LlamaSample.unity";
        private const string FloorMaterialPath = "Assets/SabaPropsLlama/SampleScene/Floor.mat";

        [MenuItem("Tools/SabaProps/Llama/Create Sample Scene")]
        public static void CreateFromSelection()
        {
            var model = Selection.activeObject as LlamaModelAsset;
            if (model == null)
            {
                EditorUtility.DisplayDialog("SabaProps Llama",
                    "ProjectウィンドウでModel.asset（変換済みモデル）を選択してから実行してください。", "OK");
                return;
            }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Create(model);
        }

        /// <summary>
        /// 現在のシーンを置き換えて生成し、<see cref="ScenePath"/>へ保存します。
        /// 先にruntimeをimportし、コンパイルを終えておく必要があります。
        /// </summary>
        public static GameObject Create(LlamaModelAsset model)
        {
            if (model == null) throw new ArgumentNullException(nameof(model));
            // Loading a scene unloads unreferenced assets; resolve the model again afterwards.
            string modelPath = AssetDatabase.GetAssetPath(model);
            if (string.IsNullOrEmpty(modelPath)) throw new InvalidOperationException("保存済みの変換済みモデルを指定してください。");

            EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            model = AssetDatabase.LoadAssetAtPath<LlamaModelAsset>(modelPath);
            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));

            var floorMaterial = AssetDatabase.LoadAssetAtPath<Material>(FloorMaterialPath);
            if (floorMaterial == null)
            {
                floorMaterial = new Material(Shader.Find("Standard")) { color = new Color(0.35f, 0.36f, 0.38f) };
                AssetDatabase.CreateAsset(floorMaterial, FloorMaterialPath);
            }
            GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.name = "Floor";
            floor.GetComponent<Renderer>().sharedMaterial = floorMaterial;

            var world = new GameObject("VRCWorld");
            var spawn = new GameObject("Spawn");
            spawn.transform.SetParent(world.transform, false);
            // The canvas faces -Z; spawn in front of it, within reach of the cube after one step.
            spawn.transform.position = new Vector3(0f, 0f, -3f);
            Type descriptorType = FindType("VRC.SDK3.Components.VRCSceneDescriptor");
            if (descriptorType == null) throw new InvalidOperationException("VRChat Worlds SDKが必要です。");
            Component descriptor = world.AddComponent(descriptorType);
            FieldInfo spawns = descriptorType.GetField("spawns", BindingFlags.Public | BindingFlags.Instance);
            if (spawns == null) throw new InvalidOperationException("VRCSceneDescriptorのspawnsを設定できません。");
            spawns.SetValue(descriptor, new[] { spawn.transform });

            GameObject runner = LlamaWorldBuilder.Create(model);
            runner.transform.position = new Vector3(0f, 0.5f, 0f);

            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(), ScenePath);
            Debug.Log("[SabaProps Llama] サンプルシーンを保存しました: " + ScenePath);
            return runner;
        }

        private static Type FindType(string name)
        {
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type type = assembly.GetType(name, false);
                if (type != null) return type;
            }
            return null;
        }
    }
}
