using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UdonSharp;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace SabaProps.PutItems.Editors
{
    public static class KitchenDemoSample
    {
        public const string SampleRoot = "Packages/io.github.sabas0ba.sabaprops.putitems/Samples~/KitchenDemo";
        public const string ImportRoot = "Assets/SabaProps/PutItemsKitchenDemoV2";
        private static readonly string[] ProgramNames = { "PlacementSurface", "PlacementSolver", "PlacementFollowState", "ObjectSyncPlacement" };

        [MenuItem("Tools/SabaProps/Put Items/Open Demo Scene", false, 0)]
        public static void OpenDemo()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            ImportSample();
            EditorSceneManager.OpenScene(ImportRoot + "/" + KitchenDemo.SceneName);
            if (SceneView.lastActiveSceneView != null)
                SceneView.lastActiveSceneView.LookAt(new Vector3(-0.05f, 1.05f, 1.35f),
                    Quaternion.Euler(20f, -35f, 0f), 6f);
        }

        public static void ImportSample()
        {
            ImportSampleAt(ImportRoot);
        }

        public static void ImportSampleAt(string importRoot)
        {
            if (string.IsNullOrEmpty(importRoot) || !importRoot.StartsWith("Assets/SabaProps/", StringComparison.Ordinal)
                || importRoot.Contains("..")) throw new ArgumentException("Invalid demo import destination.", nameof(importRoot));
            if (File.Exists(importRoot + "/" + KitchenDemo.SceneName)) return;
            if (Directory.Exists(importRoot) && Directory.GetFileSystemEntries(importRoot).Length > 0)
                throw new IOException("Demo import destination is not empty: " + importRoot);
            if (!File.Exists(SampleRoot + "/" + KitchenDemo.SceneName))
                throw new FileNotFoundException("Bundled kitchen scene is missing.");
            PlacementPrograms.Prepare();
            var replacements = new Dictionary<string, string>();
            foreach (string meta in Directory.GetFiles(SampleRoot, "*.meta", SearchOption.AllDirectories))
                replacements[ReadGuid(meta)] = Guid.NewGuid().ToString("N");
            // UdonSharp は1 scriptにつき1 programを使用するため、共有programへ参照を接続します。
            foreach (string name in ProgramNames)
            {
                string path = "Assets/SabaProps/PutItemsPrograms/" + name + ".asset";
                UdonSharpProgramAsset program = AssetDatabase.LoadAssetAtPath<UdonSharpProgramAsset>(path);
                replacements[ReadGuid(SampleRoot + "/Programs/" + name + ".asset.meta")] = AssetDatabase.AssetPathToGUID(path);
                string serialized = AssetDatabase.GetAssetPath(program.GetSerializedUdonProgramAsset());
                replacements[ReadGuid(SampleRoot + "/Programs/" + name + ".Serialized.asset.meta")] = AssetDatabase.AssetPathToGUID(serialized);
            }
            Directory.CreateDirectory(importRoot);
            foreach (string path in Directory.GetFiles(SampleRoot, "*", SearchOption.AllDirectories))
            {
                string relative = path.Substring(SampleRoot.Length + 1).Replace('\\', '/');
                if (relative.StartsWith("Programs/", StringComparison.Ordinal) || relative == "Programs.meta") continue;
                string destination = importRoot + "/" + relative;
                Directory.CreateDirectory(Path.GetDirectoryName(destination));
                CopyRemapped(path, destination, replacements);
            }
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        }

        // -executeMethod で独立した開発用 Unity から実行する入口です。
        public static void GenerateAndExport()
        {
            KitchenDemo.Generate();
            KitchenDemo.RenderPreviews();
            ExportSample();
        }

        public static void ExportSample()
        {
            AssetDatabase.SaveAssets();
            string scene = KitchenDemo.GeneratedRoot + "/" + KitchenDemo.SceneName;
            var paths = new Dictionary<string, string>();
            foreach (string path in AssetDatabase.GetDependencies(scene, true))
            {
                if (path.StartsWith(KitchenDemo.GeneratedRoot + "/", StringComparison.Ordinal) && !Directory.Exists(path))
                    paths[path] = SampleRoot + path.Substring(KitchenDemo.GeneratedRoot.Length);
                else if (path.StartsWith("Assets/", StringComparison.Ordinal) && !Directory.Exists(path))
                {
                    string mapped = ProgramDestination(path);
                    if (mapped == null) throw new IOException("Unexpected demo dependency: " + path);
                    paths[path] = mapped;
                }
            }
            var replacements = new Dictionary<string, string>();
            foreach (var entry in paths)
                replacements[AssetDatabase.AssetPathToGUID(entry.Key)] = File.Exists(entry.Value + ".meta")
                    ? ReadGuid(entry.Value + ".meta") : Guid.NewGuid().ToString("N");
            foreach (var entry in paths)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(entry.Value));
                CopyRemapped(entry.Key, entry.Value, replacements);
                CopyRemapped(entry.Key + ".meta", entry.Value + ".meta", replacements);
            }
            Debug.Log("[Put Items] Bundled kitchen scene exported with " + paths.Count + " assets.");
        }

        private static string ProgramDestination(string source)
        {
            foreach (string name in ProgramNames)
            {
                string path = "Assets/SabaProps/PutItemsPrograms/" + name + ".asset";
                if (source == path) return SampleRoot + "/Programs/" + name + ".asset";
                UdonSharpProgramAsset program = AssetDatabase.LoadAssetAtPath<UdonSharpProgramAsset>(path);
                if (source == AssetDatabase.GetAssetPath(program.GetSerializedUdonProgramAsset()))
                    return SampleRoot + "/Programs/" + name + ".Serialized.asset";
            }
            return null;
        }

        private static string ReadGuid(string meta)
        {
            Match match = Regex.Match(File.ReadAllText(meta), @"guid: ([a-f0-9]{32})");
            if (!match.Success) throw new IOException("Invalid asset metadata: " + meta);
            return match.Groups[1].Value;
        }

        private static void CopyRemapped(string source, string destination, Dictionary<string, string> replacements)
        {
            byte[] bytes = File.ReadAllBytes(source);
            string text = Encoding.UTF8.GetString(bytes);
            if (text.StartsWith("%YAML", StringComparison.Ordinal) || source.EndsWith(".meta", StringComparison.Ordinal))
            {
                text = Regex.Replace(text, @"\b[a-f0-9]{32}\b", match =>
                    replacements.TryGetValue(match.Value, out string value) ? value : match.Value);
                text = Regex.Replace(text.Replace("\r\n", "\n"), @"[ \t]+$", "", RegexOptions.Multiline);
                File.WriteAllText(destination, text, new UTF8Encoding(false));
            }
            else File.WriteAllBytes(destination, bytes);
        }
    }
}
