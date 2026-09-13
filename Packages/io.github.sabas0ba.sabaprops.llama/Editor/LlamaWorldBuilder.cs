using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEngine;
using UnityEngine.UI;

namespace SabaProps.Llama
{
    public static class LlamaWorldBuilder
    {
        public const string RuntimePath = "Assets/SabaPropsLlama/Runtime/SabaLlamaRunner.cs";
        private const string PackagePath = "Packages/io.github.sabas0ba.sabaprops.llama";

        private static Type FindType(string name)
        {
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type type = assembly.GetType(name, false);
                if (type != null) return type;
            }
            return null;
        }

        [MenuItem("Tools/SabaProps/Llama/Import VRChat Runtime")]
        public static void ImportRuntime()
        {
            Type programType = FindType("UdonSharp.UdonSharpProgramAsset");
            if (programType == null) throw new InvalidOperationException("VRChat Worlds SDK / UdonSharpが必要です。");
            PackageInfo package = PackageInfo.FindForAssetPath(PackagePath + "/package.json");
            string source = Path.Combine(package == null ? PackagePath : package.resolvedPath, "Samples~/VRChat/SabaLlamaRunner.cs");
            string contents = File.ReadAllText(source);
            if (File.Exists(RuntimePath) && File.ReadAllText(RuntimePath) != contents)
                throw new InvalidOperationException("既存runtimeと内容が異なります。変更を退避し、既存ファイルを更新してから再実行してください。");
            Directory.CreateDirectory(Path.GetDirectoryName(RuntimePath));
            if (!File.Exists(RuntimePath)) File.WriteAllText(RuntimePath, contents);
            AssetDatabase.Refresh();
            string programPath = Path.ChangeExtension(RuntimePath, ".asset");
            if (AssetDatabase.LoadAssetAtPath<ScriptableObject>(programPath) == null)
            {
                var script = AssetDatabase.LoadAssetAtPath<MonoScript>(RuntimePath);
                var program = ScriptableObject.CreateInstance(programType);
                FieldInfo field = programType.GetField("sourceCsScript", BindingFlags.Public | BindingFlags.Instance);
                if (script == null || field == null)
                {
                    UnityEngine.Object.DestroyImmediate(program);
                    throw new InvalidOperationException("Udon program assetを作成できません。コンパイル後に再実行してください。");
                }
                field.SetValue(program, script);
                AssetDatabase.CreateAsset(program, programPath);
                AssetDatabase.SaveAssets();
            }
        }

        public static GameObject Create(LlamaModelAsset model)
        {
            if (model == null) throw new ArgumentNullException(nameof(model));
            Type runnerType = FindType("SabaLlamaRunner");
            Type extensions = FindType("UdonSharpEditor.UdonSharpComponentExtensions");
            MethodInfo add = extensions?.GetMethod("AddUdonSharpComponent", BindingFlags.Public | BindingFlags.Static,
                null, new[] { typeof(GameObject), typeof(Type) }, null);
            if (runnerType == null || add == null) throw new InvalidOperationException("runtimeをimportし、コンパイル完了後に再実行してください。");
            var root = GameObject.CreatePrimitive(PrimitiveType.Cube);
            root.name = "SabaLlama - Interact to generate or stop";
            try
            {
                var runner = (Component)add.Invoke(null, new object[] { root, runnerType });
                void Set(string field, object value) { runnerType.GetField(field).SetValue(runner, value); }
                Set("weights", model.weights); Set("templates", model.materials);
                var materialObject = new GameObject("Llama Material Instances", typeof(MeshRenderer));
                materialObject.transform.SetParent(root.transform, false);
                var materialSource = materialObject.GetComponent<MeshRenderer>();
                materialSource.enabled = false;
                materialSource.sharedMaterials = model.materials;
                Set("materialSource", materialSource);
                Set("inputA", model.inputA); Set("inputB", model.inputB); Set("targets", model.targets);
                Set("widths", model.widths); Set("heights", model.heights);
                Set("context", model.context); Set("vocabulary", model.config.vocabulary); Set("resultTarget", model.resultTarget);
                Set("pieces", model.tokenizer.pieces); Set("scores", model.tokenizer.scores);
                Set("sortedPieces", model.tokenizer.sortedPieces); Set("sortedIds", model.tokenizer.sortedIds);
                Set("byteTokens", model.tokenizer.byteTokens);
                string prompt = model.config.vocabulary == 8 ? "ab" : "Once upon a time";
                Set("prompt", prompt);

                var canvasObject = new GameObject("Llama Display", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster));
                canvasObject.transform.SetParent(root.transform, false);
                var canvas = canvasObject.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.WorldSpace;
                var rect = canvasObject.GetComponent<RectTransform>();
                rect.sizeDelta = new Vector2(800, 600);
                rect.localScale = Vector3.one * 0.002f;
                rect.localPosition = new Vector3(0, 1.4f, -0.6f);
                // The world template owns the EventSystem; don't replace its VRChat input modules.
                Type uiShape = FindType("VRC.SDK3.Components.VRCUiShape") ?? FindType("VRC.SDKBase.VRC_UiShape");
                if (uiShape != null) canvasObject.AddComponent(uiShape);

                Text MakeText(string name, Vector2 position, Vector2 size)
                {
                    var go = new GameObject(name, typeof(RectTransform), typeof(Text));
                    go.transform.SetParent(canvasObject.transform, false);
                    var label = go.GetComponent<Text>();
                    label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                    label.fontSize = 26; label.color = Color.white; label.supportRichText = false;
                    label.alignment = TextAnchor.UpperLeft;
                    label.rectTransform.anchoredPosition = position; label.rectTransform.sizeDelta = size;
                    return label;
                }
                Text result = MakeText("Output", new Vector2(0, -65), new Vector2(780, 440));
                result.raycastTarget = false;
                result.text = "入力後、CubeをInteractすると生成・停止します。\n結果は各プレイヤーのローカルです。";
                Text inputText = MakeText("Prompt", new Vector2(0, 230), new Vector2(780, 100));
                inputText.color = Color.black;
                var inputObject = new GameObject("Input", typeof(RectTransform), typeof(Image), typeof(InputField));
                inputObject.transform.SetParent(canvasObject.transform, false);
                var inputRect = inputObject.GetComponent<RectTransform>();
                inputRect.anchoredPosition = new Vector2(0, 230); inputRect.sizeDelta = new Vector2(800, 110);
                inputText.transform.SetParent(inputObject.transform, false);
                inputText.rectTransform.anchoredPosition = Vector2.zero;
                var input = inputObject.GetComponent<InputField>();
                input.textComponent = inputText; input.targetGraphic = inputObject.GetComponent<Image>();
                input.characterLimit = 256; input.lineType = InputField.LineType.MultiLineNewline; input.text = prompt;
                Set("input", input); Set("output", result);
                EditorUtility.SetDirty(runner);
                Type utility = FindType("UdonSharpEditor.UdonSharpEditorUtility");
                Type proxyType = FindType("UdonSharp.UdonSharpBehaviour");
                MethodInfo copy = utility?.GetMethod("CopyProxyToUdon", BindingFlags.Public | BindingFlags.Static,
                    null, new[] { proxyType }, null);
                if (copy == null) throw new InvalidOperationException("UdonSharpのproxy転記APIが見つかりません。");
                copy.Invoke(null, new object[] { runner });
                Undo.RegisterCreatedObjectUndo(root, "Create SabaLlama runner");
                Selection.activeGameObject = root;
                return root;
            }
            catch { UnityEngine.Object.DestroyImmediate(root); throw; }
        }
    }
}
