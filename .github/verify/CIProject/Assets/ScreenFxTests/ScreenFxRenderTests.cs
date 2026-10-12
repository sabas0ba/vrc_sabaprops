using NUnit.Framework;
using System.IO;
using SabaProps.ScreenFx.Editors;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace SabaProps.ScreenFx.CITests
{
    /// <summary>
    /// Renders a camera through every preset and compares the frame with the
    /// same frame without the effect. This is the only tier that shows a
    /// preset draws anything at all.
    /// </summary>
    public class ScreenFxRenderTests
    {
        // 出力先を指定した実行では、目視確認用に解像度を上げる。
        private static string PreviewDirectory => System.Environment.GetEnvironmentVariable("SABAPROPS_SCREENFX_CAPTURE");
        private static int Size => string.IsNullOrEmpty(PreviewDirectory) ? 128 : 512;

        private Camera _camera;
        private RenderTexture _target;
        private Texture2D _pixels;
        private RenderTexture _previousTarget;
        private Color[] _baseline;

        [SetUp]
        public void BuildScene()
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
            {
                Assert.Ignore("This rendering check requires a graphics device.");
            }

            _previousTarget = RenderTexture.active;
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            RenderSettings.fog = false;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = Color.white;

            // A backdrop with hue, brightness and edges in every part of the
            // frame, so that distortion, blur and grading all have something
            // to change.
            Color[] colours = { Color.red, Color.green, Color.blue, Color.yellow, Color.white, Color.cyan };
            for (int index = 0; index < 36; index++)
            {
                GameObject tile = GameObject.CreatePrimitive(PrimitiveType.Quad);
                tile.transform.position = new Vector3(index % 6 - 2.5f, index / 6 - 2.5f, 4f);
                tile.transform.localScale = Vector3.one * 0.8f;
                var material = new Material(Shader.Find("Unlit/Color"));
                material.color = colours[(index + index / 6) % colours.Length] * (index % 2 == 0 ? 0.9f : 0.45f);
                tile.GetComponent<MeshRenderer>().sharedMaterial = material;
            }

            _camera = new GameObject("Screen FX test camera").AddComponent<Camera>();
            _camera.clearFlags = CameraClearFlags.SolidColor;
            _camera.backgroundColor = new Color(0.2f, 0.2f, 0.2f);
            _camera.allowHDR = false;
            _camera.allowMSAA = false;
            _target = new RenderTexture(Size, Size, 24, RenderTextureFormat.ARGBFloat);
            _pixels = new Texture2D(Size, Size, TextureFormat.RGBAFloat, false);
            _camera.targetTexture = _target;
            _baseline = Capture();
        }

        [TearDown]
        public void DestroyScene()
        {
            RenderTexture.active = _previousTarget;
            if (_camera != null)
            {
                _camera.targetTexture = null;
            }

            if (_target != null)
            {
                _target.Release();
                Object.DestroyImmediate(_target);
            }

            if (_pixels != null)
            {
                Object.DestroyImmediate(_pixels);
            }

            EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
        }

        [Test]
        public void Baseline_ShowsTheBackdrop()
        {
            float spread = 0f;
            foreach (Color pixel in _baseline)
            {
                spread = Mathf.Max(spread, Mathf.Abs(pixel.r - pixel.b));
            }

            Assert.Greater(spread, 0.5f, "the backdrop is not visible, so the comparisons below prove nothing");
        }

        [Test]
        public void EveryPreset_ChangesTheFrame([Values(false, true)] bool lite)
        {
            SavePreview("Baseline", _baseline);
            foreach (ScreenFxPreset preset in ScreenFxPresets.All)
            {
                GameObject volume = CreateVolume(preset, lite, Vector3.zero);
                try
                {
                    Color[] frame = Capture();
                    SavePreview(preset.id + (lite ? "-Lite" : "-Standard"), frame);
                    AssertFinite(frame, preset.id);
                    Assert.Greater(
                        Difference(frame, _baseline), 0.004f,
                        $"{preset.id} (lite = {lite}) left the frame unchanged");
                }
                finally
                {
                    Destroy(volume);
                }
            }
        }

        [Test]
        public void NeutralMaterial_LeavesTheFrameUnchanged([Values(false, true)] bool lite)
        {
            var material = new Material(Shader.Find(ScreenFxAssetLibrary.ShaderName(lite)));
            GameObject volume = ScreenFxRigFactory.CreateVolumeObject(ScreenFxPresets.All[0], material, lite);
            try
            {
                Assert.Less(
                    Difference(Capture(), _baseline), 0.002f,
                    "a material at the shader defaults must be the identity");
            }
            finally
            {
                Destroy(volume);
            }
        }

        [Test]
        public void CameraOutsideTheVolume_SeesNoEffect([Values(false, true)] bool lite)
        {
            GameObject volume = CreateVolume(ScreenFxPresets.Find("Mud"), lite, new Vector3(0f, 0f, 12f));
            try
            {
                Assert.Less(Difference(Capture(), _baseline), 1e-4f, "the effect leaked outside its volume");
            }
            finally
            {
                Destroy(volume);
            }
        }

        [Test]
        public void ZeroWeight_SeesNoEffect([Values(false, true)] bool lite)
        {
            GameObject volume = CreateVolume(ScreenFxPresets.Find("Mud"), lite, Vector3.zero);
            try
            {
                volume.GetComponent<MeshRenderer>().sharedMaterial.SetFloat("_Weight", 0f);
                Assert.Less(Difference(Capture(), _baseline), 1e-4f, "weight 0 still drew the effect");
            }
            finally
            {
                Destroy(volume);
            }
        }

        [Test]
        public void EdgeFade_ScalesTheEffectWithDepthInsideTheVolume()
        {
            // A 6 m wide volume with a 2 m fade: 0.5 m inside the wall the
            // weight is a quarter, at the centre it is one.
            GameObject volume = CreateVolume(ScreenFxPresets.Find("Dark"), false, new Vector3(2.5f, 0f, 0f));
            try
            {
                volume.GetComponent<MeshRenderer>().sharedMaterial.SetFloat("_EdgeFade", 2f);
                float nearWall = Difference(Capture(), _baseline);
                volume.transform.position = Vector3.zero;
                float centre = Difference(Capture(), _baseline);
                Assert.Greater(nearWall, 0.004f);
                Assert.Greater(centre, nearWall * 1.5f, "the effect did not grow towards the centre");
            }
            finally
            {
                Destroy(volume);
            }
        }

        [Test]
        public void Dark_DarkensAndGlare_Brightens()
        {
            Assert.Less(MeanBrightness(Render("Dark")), MeanBrightness(_baseline) * 0.5f);
            Assert.Greater(MeanBrightness(Render("Glare")), MeanBrightness(_baseline) * 1.2f);
        }

        [Test]
        public void Faint_ClosesTheCentreLessThanTheEdge()
        {
            Color[] frame = Render("Faint");
            float centre = Brightness(frame[Size / 2 * Size + Size / 2]);
            float top = Brightness(frame[(Size - 2) * Size + Size / 2]);
            Assert.Less(top, 0.02f, "the eyelid does not cover the top edge");
            Assert.Greater(centre, top, "the centre is as dark as the eyelid");
        }

        private Color[] Render(string presetId)
        {
            GameObject volume = CreateVolume(ScreenFxPresets.Find(presetId), false, Vector3.zero);
            try
            {
                return Capture();
            }
            finally
            {
                Destroy(volume);
            }
        }

        private static GameObject CreateVolume(ScreenFxPreset preset, bool lite, Vector3 position)
        {
            GameObject volume = ScreenFxRigFactory.CreateVolumeObject(
                preset, ScreenFxAssetLibrary.CreateMaterial(preset, lite), lite);
            volume.transform.position = position;
            return volume;
        }

        private static void Destroy(GameObject volume)
        {
            Object.DestroyImmediate(volume.GetComponent<MeshRenderer>().sharedMaterial);
            Object.DestroyImmediate(volume);
        }

        private Color[] Capture()
        {
            _camera.Render();
            RenderTexture.active = _target;
            _pixels.ReadPixels(new Rect(0f, 0f, Size, Size), 0, 0);
            _pixels.Apply();
            return _pixels.GetPixels();
        }

        private static float Brightness(Color pixel) => (pixel.r + pixel.g + pixel.b) / 3f;

        private static void SavePreview(string name, Color[] frame)
        {
            if (string.IsNullOrEmpty(PreviewDirectory))
            {
                return;
            }

            Directory.CreateDirectory(PreviewDirectory);
            var image = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
            try
            {
                image.SetPixels(frame);
                image.Apply();
                File.WriteAllBytes(Path.Combine(PreviewDirectory, name + ".png"), image.EncodeToPNG());
            }
            finally
            {
                Object.DestroyImmediate(image);
            }
        }

        private static float MeanBrightness(Color[] frame)
        {
            float sum = 0f;
            foreach (Color pixel in frame)
            {
                sum += Brightness(pixel);
            }

            return sum / frame.Length;
        }

        private static float Difference(Color[] frame, Color[] reference)
        {
            float sum = 0f;
            for (int i = 0; i < frame.Length; i++)
            {
                sum += Mathf.Abs(frame[i].r - reference[i].r)
                    + Mathf.Abs(frame[i].g - reference[i].g)
                    + Mathf.Abs(frame[i].b - reference[i].b);
            }

            return sum / (frame.Length * 3f);
        }

        private static void AssertFinite(Color[] frame, string label)
        {
            foreach (Color pixel in frame)
            {
                if (float.IsNaN(pixel.r + pixel.g + pixel.b) || float.IsInfinity(pixel.r + pixel.g + pixel.b))
                {
                    Assert.Fail($"{label} wrote a non-finite pixel");
                }
            }
        }
    }
}
