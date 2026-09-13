using System;
using System.IO;
using System.Security.Cryptography;
using UnityEditor;
using UnityEngine;

namespace SabaProps.Llama
{
    public sealed class LlamaImportWindow : EditorWindow
    {
        private string checkpointPath = "", tokenizerPath = "", provenance = "";
        private int context = 128;
        private LlamaModelAsset selected;
        private string message = "";

        [MenuItem("Tools/SabaProps/Llama/Import Model")]
        public static void Open() { GetWindow<LlamaImportWindow>(false, "SabaProps Llama", true); }

        private void OnGUI()
        {
            EditorGUILayout.HelpBox("PCワールド向け実験実装。FP32 llama2.c v0 checkpointとtokenizer.binに対応します。GGUFは未対応です。", MessageType.Info);
            if (GUILayout.Button("checkpointを選択")) checkpointPath = EditorUtility.OpenFilePanel("FP32 v0 checkpoint", "", "bin");
            EditorGUILayout.LabelField(Path.GetFileName(checkpointPath));
            if (GUILayout.Button("tokenizerを選択")) tokenizerPath = EditorUtility.OpenFilePanel("tokenizer.bin", "", "bin");
            EditorGUILayout.LabelField(Path.GetFileName(tokenizerPath));
            context = EditorGUILayout.IntField("Context（最大256）", context);
            provenance = EditorGUILayout.TextField("重みの出典・ライセンス", provenance);
            EditorGUILayout.HelpBox("変換した重みはワールドの配布物に含まれます。出典と再配布条件を記録してください。外部ファイルの取得は行いません。", MessageType.Info);
            if (GUILayout.Button("検査してtextureへ変換"))
            {
                try
                {
                    if (string.IsNullOrWhiteSpace(provenance)) throw new InvalidOperationException("出典・ライセンスを入力してください。");
                    var checkpoint = LlamaCheckpoint.Read(checkpointPath);
                    var tokenizer = LlamaTokenizer.Read(tokenizerPath, checkpoint.config.vocabulary);
                    selected = LlamaProgramBuilder.Build(checkpoint, tokenizer, context);
                    using (var sha = SHA256.Create())
                    using (var file = File.OpenRead(tokenizerPath))
                        selected.tokenizerSha256 = BitConverter.ToString(sha.ComputeHash(file)).Replace("-", "").ToLowerInvariant();
                    selected.sourceAndLicense = provenance;
                    Save(selected, "ImportedModel");
                    message = "変換しました。次にruntimeをimportし、コンパイル後にrunnerを作成してください。";
                }
                catch (Exception error) { message = error.Message; Debug.LogException(error); }
            }
            if (GUILayout.Button("検証用モデルを作成（未学習）"))
            {
                try
                {
                    selected = LlamaProgramBuilder.Build(LlamaCheckpoint.Fixture(), LlamaTokenizer.Fixture(), 8);
                    selected.sourceAndLicense = "SabaProps numerical fixture / MIT / untrained";
                    selected.tokenizerSha256 = "generated";
                    Save(selected, "NumericalFixture");
                    message = "未学習の数値検証モデルを作成しました。入力例は ab です。会話品質は評価できません。";
                }
                catch (Exception error) { message = error.Message; Debug.LogException(error); }
            }
            selected = (LlamaModelAsset)EditorGUILayout.ObjectField("変換済みモデル", selected, typeof(LlamaModelAsset), false);
            if (selected != null)
            {
                EditorGUILayout.LabelField("Passes / token", selected.materials.Length.ToString());
                EditorGUILayout.LabelField("作業VRAM（重みを除く）", (selected.WorkingBytes / 1048576.0).ToString("F2") + " MiB");
            }
            if (GUILayout.Button("VRChat runtimeをimport"))
            {
                try { LlamaWorldBuilder.ImportRuntime(); message = "コンパイル完了後にrunnerを作成してください。"; }
                catch (Exception error) { message = error.Message; Debug.LogException(error); }
            }
            if (GUILayout.Button("選択モデルのWorld Runnerを作成"))
            {
                try { LlamaWorldBuilder.Create(selected); }
                catch (Exception error) { message = error.Message; Debug.LogException(error); }
            }
            EditorGUILayout.HelpBox(message, MessageType.None);
        }

        public static string Save(LlamaModelAsset model, string name)
        {
            if (!AssetDatabase.IsValidFolder("Assets/SabaPropsLlama")) AssetDatabase.CreateFolder("Assets", "SabaPropsLlama");
            string folder = AssetDatabase.GenerateUniqueAssetPath("Assets/SabaPropsLlama/" + name);
            string guid = AssetDatabase.CreateFolder("Assets/SabaPropsLlama", Path.GetFileName(folder));
            if (string.IsNullOrEmpty(guid)) throw new IOException("出力フォルダーを作成できません。");
            try
            {
                AssetDatabase.CreateAsset(model.weights, folder + "/Weights.asset");
                foreach (Material material in model.materials) AssetDatabase.CreateAsset(material, folder + "/" + material.name + ".mat");
                AssetDatabase.CreateAsset(model, folder + "/Model.asset");
                File.WriteAllText(folder + "/Provenance.json", JsonUtility.ToJson(new Provenance
                {
                    checkpointSha256 = model.checkpointSha256, tokenizerSha256 = model.tokenizerSha256,
                    sourceAndLicense = model.sourceAndLicense, context = model.context
                }, true) + "\n");
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                Selection.activeObject = model;
                return folder;
            }
            catch
            {
                AssetDatabase.DeleteAsset(folder);
                throw;
            }
        }

        [Serializable]
        private sealed class Provenance
        {
            public string format = "llama2.c-fp32-v0", packageVersion = "0.1.0";
            public string checkpointSha256, tokenizerSha256, sourceAndLicense;
            public int context;
        }
    }
}
