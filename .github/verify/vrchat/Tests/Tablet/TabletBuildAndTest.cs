using System;
using SabaProps.Tablet.Editors;
using UdonSharp.Compiler;
using UnityEditor;
using UnityEngine;
using VRC.SDK3.Editor;

namespace SabaProps.Tablet.WorldTests
{
    /// <summary>Batch entry point for the SDK's local Build &amp; Test workflow.</summary>
    public static class TabletBuildAndTest
    {
        public static void Run() { Run(false); }

        public static void RunGallery() { Run(true); }

        private static async void Run(bool gallery)
        {
            try
            {
#if UDON
                UdonSharpCompilerV1.CompileSync(new UdonSharpCompileOptions { IsEditorBuild = true });
                if (gallery) TabletThemeGallery.Create();
                else TabletSampleScene.Create();
                EditorWindow.GetWindow<VRCSdkControlPanel>();
                if (!VRCSdkControlPanel.TryGetBuilder<IVRCSdkWorldBuilderApi>(out var builder))
                {
                    throw new InvalidOperationException("VRChat Worlds SDK builder is unavailable.");
                }

                if (!builder.IsValidBuilder(out string message))
                {
                    throw new InvalidOperationException(message);
                }

                builder.OnSdkBuildProgress += (_, status) => Debug.Log("[SabaProps Tablet] " + status);
                builder.OnSdkBuildSuccess += (_, path) => Debug.Log("[SabaProps Tablet] Built world: " + path);
                await builder.BuildAndTest();
                Debug.Log("[SabaProps Tablet] Build & Test completed.");
                EditorApplication.Exit(0);
#else
                throw new InvalidOperationException("The Worlds SDK has not initialized its UDON define. Restart the editor after import.");
#endif
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorApplication.Exit(1);
            }
        }
    }
}
