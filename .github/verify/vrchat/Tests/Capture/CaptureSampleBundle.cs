using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using SabaProps.Capture.Editors;
using UdonSharp.Compiler;
using UnityEditor;
using UnityEditor.PackageManager.UI;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace SabaProps.Capture.WorldTests
{
    /// <summary>
    /// Writes the sample that ships in the package's Samples~ folder, and imports it the way
    /// the Package Manager does.
    /// <para>
    /// The shipped sample is the output of CaptureSampleScene.Create, not something edited by
    /// hand. Export regenerates it; the tests then import the shipped copy and hold it to the
    /// same checks as a freshly generated scene, so the two cannot drift apart unnoticed.
    /// </para>
    /// <para>
    /// Lives with the tests rather than in the package: users never need to export, and the
    /// export writes into the package folder, which a shipped tool should not do.
    /// </para>
    /// </summary>
    public static class CaptureSampleBundle
    {
        public const string PackageName = "io.github.sabas0ba.sabaprops.capture";
        public const string SampleDisplayName = "Capture Demo World";
        public const string BundleRoot = "Packages/" + PackageName + "/Samples~/CaptureDemo";
        public const string SceneFileName = "CaptureDemo.unity";

        /// <summary>
        /// Regenerates the sample and writes it to the package copy inside this project.
        /// Entry point for -executeMethod; see .github/verify/vrchat/README.md for how the
        /// result gets back into the repository.
        /// </summary>
        public static void Export()
        {
            // A batch session started right after a script change has not compiled the Udon
            // programs yet, and the generator cannot write settings into a program-less behaviour.
            UdonSharpCompilerV1.CompileSync(new UdonSharpCompileOptions { IsEditorBuild = true });

            CaptureSampleScene.Create();
            AssetDatabase.SaveAssets();

            var sources = new List<string>();
            foreach (string path in Directory.GetFiles(CaptureSampleScene.SampleFolder))
            {
                if (!path.EndsWith(".meta", StringComparison.Ordinal))
                {
                    sources.Add(path.Replace('\\', '/'));
                }
            }

            // The shipped assets get GUIDs of their own. If they kept the generated ones, a
            // project holding both the imported sample and a generated scene would have two
            // assets per GUID, and the imported scene would resolve to the generated assets.
            // An asset that is already in the bundle keeps the GUID it shipped with, so
            // re-exporting changes only what the generator changed.
            var replacements = new Dictionary<string, string>();
            foreach (string source in sources)
            {
                string shippedMeta = BundleRoot + "/" + Path.GetFileName(source) + ".meta";
                replacements[AssetDatabase.AssetPathToGUID(source)] = File.Exists(shippedMeta)
                    ? ReadGuid(shippedMeta)
                    : Guid.NewGuid().ToString("N");
            }

            if (Directory.Exists(BundleRoot))
            {
                Directory.Delete(BundleRoot, true);
            }

            Directory.CreateDirectory(BundleRoot);
            foreach (string source in sources)
            {
                string destination = BundleRoot + "/" + Path.GetFileName(source);
                CopyRemapped(source, destination, replacements);
                CopyRemapped(source + ".meta", destination + ".meta", replacements);
            }

            Debug.Log($"[SabaProps Capture] exported {sources.Count} sample asset(s) to {BundleRoot}");
        }

        /// <summary>
        /// Imports the shipped sample through the Package Manager's own API and returns the
        /// asset path of its scene.
        /// </summary>
        public static string Import()
        {
            string version = UnityEditor.PackageManager.PackageInfo.FindForAssetPath("Packages/" + PackageName).version;
            foreach (Sample sample in Sample.FindByPackage(PackageName, version))
            {
                if (sample.displayName != SampleDisplayName)
                {
                    continue;
                }

                sample.Import(Sample.ImportOptions.OverridePreviousImports | Sample.ImportOptions.HideImportWindow);
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

                string imported = sample.importPath.Replace('\\', '/');
                string assets = Application.dataPath.Replace('\\', '/');
                if (!imported.StartsWith(assets, StringComparison.Ordinal))
                {
                    throw new IOException("The sample was imported outside Assets: " + imported);
                }

                return "Assets" + imported.Substring(assets.Length) + "/" + SceneFileName;
            }

            throw new FileNotFoundException(
                $"package.json of {PackageName} declares no sample named '{SampleDisplayName}'");
        }

        /// <summary>
        /// Imports the shipped sample and opens it. Entry point for -executeMethod without
        /// -batchmode, to look at the sample in the editor.
        /// </summary>
        public static void ImportAndOpen()
        {
            EditorSceneManager.OpenScene(Import());
        }

        private static string ReadGuid(string meta)
        {
            Match match = Regex.Match(File.ReadAllText(meta), @"guid: ([a-f0-9]{32})");
            if (!match.Success)
            {
                throw new IOException("Invalid asset metadata: " + meta);
            }

            return match.Groups[1].Value;
        }

        private static void CopyRemapped(string source, string destination, Dictionary<string, string> replacements)
        {
            string text = File.ReadAllText(source, Encoding.UTF8);
            if (!text.StartsWith("%YAML", StringComparison.Ordinal) && !source.EndsWith(".meta", StringComparison.Ordinal))
            {
                throw new IOException("The sample holds an asset that is not text-serialised: " + source);
            }

            text = Regex.Replace(text, @"\b[a-f0-9]{32}\b", match =>
                replacements.TryGetValue(match.Value, out string value) ? value : match.Value);

            // The serialized program is generated per project under a GUID of that project's
            // own. Shipping the reference would point at an asset the user does not have; the
            // SDK fills it in from the program source when the scene is opened.
            text = Regex.Replace(text, @"serializedProgramAsset: \{[^}]*\}", "serializedProgramAsset: {fileID: 0}");

            text = text.Replace("\r\n", "\n");
            File.WriteAllText(destination, text, new UTF8Encoding(false));
        }
    }
}
