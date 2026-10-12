using NUnit.Framework;
using UnityEngine;

namespace SabaProps.ScreenFx.CITests
{
    public class ScreenFxSamplingTests
    {
        [Test]
        public void PackedEyeOffsetsStayInsideEye([Values(0, 1)] int eye, [Values(-2f, 0.2f, 2f)] float offset)
        {
            var source = new Texture2D(16, 4, TextureFormat.RGBA32, false, true);
            source.filterMode = FilterMode.Bilinear;
            source.wrapMode = TextureWrapMode.Clamp;
            var material = new Material(Shader.Find("Hidden/SabaProps/ScreenFxSamplingTest"));
            var target = new RenderTexture(4, 4, 0, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);
            var pixels = new Texture2D(4, 4, TextureFormat.RGBAFloat, false, true);
            var previous = RenderTexture.active;
            try
            {
                // The right eye is blue; any blue in the left eye reveals seam leakage.
                for (int y = 0; y < 4; y++)
                    for (int x = 0; x < 16; x++) source.SetPixel(x, y, x < 8 ? Color.red : Color.blue);
                source.Apply();
                material.SetVector("_Viewport", new Vector4(0.5f, 1f, eye * 0.5f, 0f));
                material.SetVector("_Delta", new Vector4(offset, 0f, 0f, 0f));
                Graphics.Blit(source, target, material);
                RenderTexture.active = target;
                pixels.ReadPixels(new Rect(0, 0, 4, 4), 0, 0);
                pixels.Apply();
                float expectedX = Mathf.Clamp(eye * 0.5f + 0.25f + offset * 0.5f,
                    eye * 0.5f + 1f / 32f, eye * 0.5f + 0.5f - 1f / 32f);
                foreach (Color pixel in pixels.GetPixels())
                {
                    Assert.AreEqual(expectedX, pixel.r, 0.001f, "offset must use the eye width");
                    Assert.AreEqual(0.5f, pixel.g, 0.001f);
                    Assert.AreEqual((float)eye, pixel.b, 0.001f, "bilinear sampling crossed the eye boundary");
                }
            }
            finally
            {
                RenderTexture.active = previous;
                target.Release();
                Object.DestroyImmediate(target);
                Object.DestroyImmediate(source);
                Object.DestroyImmediate(material);
                Object.DestroyImmediate(pixels);
            }
        }
    }
}
