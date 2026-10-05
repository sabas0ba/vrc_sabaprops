using System;
using UdonSharp;
using UnityEditor;
using UnityEngine;

namespace SabaProps.PutItems.Editors
{
    public static class PlacementPrograms
    {
        private const string Output = "Assets/SabaProps/PutItemsPrograms";
        private const string Runtime = "Packages/io.github.sabas0ba.sabaprops.putitems/Runtime/";

        [MenuItem("Tools/SabaProps/Put Items/Prepare Udon Programs")]
        public static void Prepare()
        {
            if (!AssetDatabase.IsValidFolder("Assets/SabaProps"))
                AssetDatabase.CreateFolder("Assets", "SabaProps");
            if (!AssetDatabase.IsValidFolder(Output))
                AssetDatabase.CreateFolder("Assets/SabaProps", "PutItemsPrograms");
            string[] names = { "PlacementSurface", "PlacementSolver", "PlacementFollowState", "ObjectSyncPlacement" };

            foreach (string name in names)
            {
                string path = Output + "/" + name + ".asset";
                MonoScript source = AssetDatabase.LoadAssetAtPath<MonoScript>(Runtime + name + ".cs");
                if (source == null) throw new InvalidOperationException("Missing placement script: " + name);
                UdonSharpProgramAsset existing = AssetDatabase.LoadAssetAtPath<UdonSharpProgramAsset>(path);
                if (existing != null)
                {
                    if (existing.sourceCsScript != source)
                        throw new InvalidOperationException("Unexpected program source: " + path);
                    continue;
                }
                UdonSharpProgramAsset program = ScriptableObject.CreateInstance<UdonSharpProgramAsset>();
                program.sourceCsScript = source;
                // 新規生成には現行形式を指定し、旧形式向けの遅延 upgrade を待たないようにします。
                program.ScriptVersion = UdonSharpProgramVersion.CurrentVersion;
                AssetDatabase.CreateAsset(program, path);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

            }
            AssetDatabase.SaveAssets();
            // 初回生成の直後にも proxy を配置できるよう、完了まで待つ API を使います。
            UdonSharp.Compiler.UdonSharpCompilerV1.CompileSync();
            if (UdonSharpProgramAsset.AnyUdonSharpScriptHasError())
                throw new InvalidOperationException("Placement Udon compilation failed; see Console.");
            foreach (string name in names)
            {
                UdonSharpProgramAsset program = AssetDatabase.LoadAssetAtPath<UdonSharpProgramAsset>(Output + "/" + name + ".asset");
                program.UpdateProgram();
                if (program.GetSerializedUdonProgramAsset() == null)
                    throw new InvalidOperationException("Missing serialized Udon program: " + name);
            }
        }
    }
}
