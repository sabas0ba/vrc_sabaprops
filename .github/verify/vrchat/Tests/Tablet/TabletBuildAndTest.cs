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
        public static async void Run()
        {
            try
            {
                UdonSharpCompilerV1.CompileSync(new UdonSharpCompileOptions { IsEditorBuild = true });
                TabletSampleScene.Create();
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
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorApplication.Exit(1);
            }
        }
    }
}
