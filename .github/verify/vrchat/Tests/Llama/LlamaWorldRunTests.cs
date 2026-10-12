using System;
using System.Collections;
using System.IO;
using System.Text;
using NUnit.Framework;
using UdonSharp;
using UdonSharp.Compiler;
using UdonSharpEditor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.TestTools;
using UnityEngine.UI;
using VRC.SDK3.ClientSim;
using VRC.SDK3.Components;
using VRC.SDKBase;
using VRC.Udon;
using Object = UnityEngine.Object;

namespace SabaProps.Llama.WorldTests
{
    /// <summary>
    /// Runs the generated world runner under ClientSim and compares the text it
    /// displays with greedy generation on the scalar CPU reference.
    /// <para>
    /// The model goes through the same steps as the import window: conversion,
    /// asset save, runner creation. Generation is driven only through the Udon
    /// interact event, so the tokenizer, the pass schedule, the readback and the
    /// text assembly all run as Udon.
    /// </para>
    /// <para>
    /// Each run saves the rendered world to <c>TestResults/</c>. This is the
    /// editor's ClientSim, not the VRChat client.
    /// </para>
    /// </summary>
    public sealed class LlamaWorldRunTests
    {
        /// <summary>Path of a GGUF file for the trained-model run.</summary>
        public const string GgufVariable = "SABAPROPS_LLAMA_GGUF";

        // Wall-clock, not frames: a batch-mode editor runs thousands of frames
        // per second, while a readback completes on the GPU's schedule.
        private const double TimeLimitSeconds = 180.0;
        private const string Generating = "\n[生成中]";
        private const string Tokenizing = "入力をtokenizeしています。";

        private bool restoreOptionsEnabled;
        private EnterPlayModeOptions restoreOptions;
        private string modelFolder;

        [OneTimeSetUp]
        public void CompilePrograms()
        {
            UdonSharpCompilerV1.CompileSync(new UdonSharpCompileOptions { IsEditorBuild = true });
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            LogAssert.ignoreFailingMessages = true;
            if (Application.isPlaying)
            {
                yield return new ExitPlayMode();
            }

            ClientSimRuntimeLoader.EndUnityTesting();
            EditorSettings.enterPlayModeOptions = restoreOptions;
            EditorSettings.enterPlayModeOptionsEnabled = restoreOptionsEnabled;
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            if (modelFolder != null) AssetDatabase.DeleteAsset(modelFolder);
            modelFolder = null;
            LogAssert.ignoreFailingMessages = false;
        }

        [UnityTest]
        public IEnumerator Fixture_GeneratesTheCpuReferenceTextThroughUdon()
        {
            return RunWorld(LlamaCheckpoint.Fixture(), LlamaTokenizer.Fixture(), 8, "ab", 8, "llama-world-fixture.png");
        }

        [UnityTest]
        public IEnumerator Gguf_GeneratesTheCpuReferenceTextThroughUdon()
        {
            string path = Environment.GetEnvironmentVariable(GgufVariable);
            if (string.IsNullOrEmpty(path))
            {
                Assert.Ignore("Set " + GgufVariable + " to a GGUF file to run a trained model.");
            }

            LlamaGguf gguf = LlamaGguf.Read(path);
            return RunWorld(gguf.checkpoint, gguf.tokenizer, 64, "Once upon a time", 24, "llama-world-gguf.png");
        }

        private IEnumerator RunWorld(LlamaCheckpoint checkpoint, LlamaTokenizer tokenizer, int context,
            string prompt, int maxNewTokens, string image)
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
            {
                Assert.Ignore("A real graphics device is required.");
            }

            restoreOptionsEnabled = EditorSettings.enterPlayModeOptionsEnabled;
            restoreOptions = EditorSettings.enterPlayModeOptions;

            EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            LlamaModelAsset model = LlamaProgramBuilder.Build(checkpoint, tokenizer, context);
            model.sourceAndLicense = "world run test";
            model.tokenizerSha256 = checkpoint.sha256;
            modelFolder = LlamaImportWindow.Save(model, "WorldRunTest");
            // Use the saved asset, as the import window's object field does.
            model = AssetDatabase.LoadAssetAtPath<LlamaModelAsset>(modelFolder + "/Model.asset");
            string expected = Expected(checkpoint, tokenizer, context, prompt, maxNewTokens);
            GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.name = "Floor";
            floor.GetComponent<Renderer>().sharedMaterial =
                new Material(Shader.Find("Standard")) { color = new Color(0.35f, 0.36f, 0.38f) };
            var world = new GameObject("VRCWorld");
            var spawn = new GameObject("Spawn");
            spawn.transform.SetParent(world.transform, false);
            // Behind the capture camera, so the player model stays out of the image.
            spawn.transform.position = new Vector3(0f, 0f, -8f);
            world.AddComponent<VRCSceneDescriptor>().spawns = new[] { spawn.transform };
            GameObject root = LlamaWorldBuilder.Create(model);
            root.transform.position = new Vector3(0f, 0.5f, 0f);

            EditorSettings.enterPlayModeOptionsEnabled = true;
            EditorSettings.enterPlayModeOptions = EnterPlayModeOptions.DisableDomainReload;
            ClientSimRuntimeLoader.BeginUnityTesting(new ClientSimSettings
            {
                enableClientSim = true,
                initializationDelay = 0f,
                spawnPlayer = true,
                localPlayerIsMaster = true,
                displayLogs = false,
            });

            // SDK 3.10.4 can poll input before ClientSim injects its input system.
            LogAssert.ignoreFailingMessages = true;
            yield return new EnterPlayMode();
            for (int frame = 0; frame < 600 && Networking.LocalPlayer == null; frame++)
            {
                yield return null;
            }

            Assert.That(Networking.LocalPlayer, Is.Not.Null, "ClientSim did not spawn a player");
            for (int frame = 0; frame < 120; frame++)
            {
                yield return null;
            }

            foreach (ClientSimMenu menu in Resources.FindObjectsOfTypeAll<ClientSimMenu>())
            {
                if (menu.gameObject.scene.IsValid())
                {
                    menu.CloseMenu();
                }
            }

            LogAssert.ignoreFailingMessages = false;

            UdonBehaviour runner = UdonSharpEditorUtility.GetBackingUdonBehaviour(
                GameObject.Find(LlamaWorldBuilder.RunnerName).GetComponent<UdonSharpBehaviour>());
            var output = (Text)runner.GetProgramVariable("output");
            var input = (InputField)runner.GetProgramVariable("input");
            Assert.That(output, Is.Not.Null);
            Assert.That(input, Is.Not.Null);
            input.text = prompt;
            runner.SetProgramVariable("maxNewTokens", maxNewTokens);
            runner.SetProgramVariable("passesPerFrame", 8);

            // A complete generation.
            runner.SendCustomEvent("_interact");
            yield return WaitUntilIdle(output);
            Assert.That(output.text, Is.EqualTo(expected), "The Udon runner and the CPU reference disagree.");
            Capture(image);

            // Stop during generation, then generate again.
            runner.SendCustomEvent("_interact");
            yield return WaitUntilGenerating(output);
            runner.SendCustomEvent("_interact");
            Assert.That(output.text, Does.EndWith("\n[停止]"));
            yield return Frames(30);
            runner.SendCustomEvent("_interact");
            yield return WaitUntilIdle(output);
            Assert.That(output.text, Is.EqualTo(expected), "Generation after a stop differs.");

            // Disable during generation, enable, then generate again.
            runner.SendCustomEvent("_interact");
            yield return WaitUntilGenerating(output);
            runner.gameObject.SetActive(false);
            yield return Frames(10);
            runner.gameObject.SetActive(true);
            yield return Frames(30);
            runner.SendCustomEvent("_interact");
            yield return WaitUntilIdle(output);
            Assert.That(output.text, Is.EqualTo(expected), "Generation after disable and enable differs.");
        }

        private static IEnumerator Frames(int count)
        {
            for (int frame = 0; frame < count; frame++)
            {
                yield return null;
            }
        }

        private static IEnumerator WaitUntilGenerating(Text output)
        {
            double deadline = EditorApplication.timeSinceStartup + TimeLimitSeconds;
            while (EditorApplication.timeSinceStartup < deadline && !output.text.EndsWith(Generating))
            {
                yield return null;
            }

            Assert.That(output.text, Does.EndWith(Generating), "Generation did not start.");
        }

        private static IEnumerator WaitUntilIdle(Text output)
        {
            // Let the interact event replace the previous run's final text first.
            yield return null;
            double deadline = EditorApplication.timeSinceStartup + TimeLimitSeconds;
            while (EditorApplication.timeSinceStartup < deadline && (output.text == Tokenizing || output.text.EndsWith(Generating)))
            {
                yield return null;
            }

            Assert.That(output.text, Is.Not.EqualTo(Tokenizing), "Generation did not finish.");
            Assert.That(output.text, Does.Not.EndWith(Generating), "Generation did not finish.");
        }

        /// <summary>The text the runner displays after greedy generation, computed on the CPU.</summary>
        private static string Expected(LlamaCheckpoint checkpoint, LlamaTokenizer tokenizer, int context,
            string prompt, int budget)
        {
            int[] tokens = tokenizer.Encode(prompt);
            Assert.That(tokens.Length, Is.LessThanOrEqualTo(context));
            var cpu = new LlamaReference(checkpoint);
            float[] logits = null;
            foreach (int token in tokens)
            {
                logits = cpu.Forward(token);
            }

            var bytes = new MemoryStream();
            int position = tokens.Length - 1, previous = tokens[position], generated = 0;
            while (true)
            {
                int next = 0;
                for (int i = 1; i < logits.Length; i++)
                {
                    if (logits[i] > logits[next]) next = i;
                }

                string text = prompt + Encoding.UTF8.GetString(bytes.ToArray());
                if (next == 1 || next == 2) return text;
                int byteValue = Array.IndexOf(tokenizer.byteTokens, next);
                if (byteValue >= 0) bytes.WriteByte((byte)byteValue);
                else
                {
                    string piece = tokenizer.pieces[next];
                    if (previous == 1 && piece.StartsWith(" ")) piece = piece.Substring(1);
                    byte[] encoded = Encoding.UTF8.GetBytes(piece);
                    bytes.Write(encoded, 0, encoded.Length);
                }

                generated++;
                position++;
                previous = next;
                if (generated >= budget || position >= context)
                {
                    return prompt + Encoding.UTF8.GetString(bytes.ToArray()) + "\n[生成上限]";
                }

                logits = cpu.Forward(next);
            }
        }

        private static void Capture(string file)
        {
            const int width = 1600, height = 1000;
            var go = new GameObject("Capture camera");
            Camera camera = go.AddComponent<Camera>();
            camera.transform.position = new Vector3(-0.9f, 1.7f, -4.1f);
            camera.transform.rotation = Quaternion.LookRotation(new Vector3(0f, 1.5f, -0.3f) - camera.transform.position);
            camera.fieldOfView = 40f;
            RenderTexture previous = RenderTexture.active;
            RenderTexture target = RenderTexture.GetTemporary(width, height, 24);
            Texture2D image = null;
            try
            {
                Canvas.ForceUpdateCanvases();
                camera.targetTexture = target;
                camera.Render();
                RenderTexture.active = target;
                image = new Texture2D(width, height, TextureFormat.RGB24, false);
                image.ReadPixels(new Rect(0f, 0f, width, height), 0, 0);
                image.Apply();
                string folder = Path.GetFullPath(Path.Combine(Application.dataPath, "../TestResults"));
                Directory.CreateDirectory(folder);
                File.WriteAllBytes(Path.Combine(folder, file), image.EncodeToPNG());
            }
            finally
            {
                RenderTexture.active = previous;
                camera.targetTexture = null;
                RenderTexture.ReleaseTemporary(target);
                if (image != null) Object.DestroyImmediate(image);
                Object.Destroy(go);
            }
        }
    }
}
