using System.Collections.Generic;
using NUnit.Framework;
using SabaProps.Liquid.Editors;
using UnityEditor;
using UnityEngine;

namespace SabaProps.Liquid.WorldTests
{
    /// <summary>
    /// The shaders and the pool generator, in a real editor.
    /// <para>
    /// The canvas update is exercised on the GPU: a stamp is blitted into a
    /// render texture and read back. That is the only place the HLSL half of
    /// the atlas mapping is checked against what it is meant to do; the
    /// offline tier only checks that its constants agree with the C# half.
    /// </para>
    /// </summary>
    public class LiquidCanvasTests
    {
        private const int FaceResolution = 32;
        private const int Width = FaceResolution * LiquidBodyCanvas.AtlasColumns;
        private const int Height = FaceResolution * LiquidBodyCanvas.AtlasRows;
        private const float Inset = 0.02f;

        private static readonly Vector3 HalfExtents = new Vector3(0.9f, 1.1f, 0.55f);

        private readonly List<Object> _created = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (Object created in _created)
            {
                if (created is RenderTexture texture)
                {
                    texture.Release();
                }

                if (created != null)
                {
                    Object.DestroyImmediate(created);
                }
            }

            _created.Clear();
        }

        [Test]
        public void Shaders_AreFoundAndCompile()
        {
            foreach (string shaderName in new[] { LiquidAssets.BodyProjectorShader, LiquidAssets.CanvasUpdateShader })
            {
                Shader shader = Shader.Find(shaderName);
                Assert.IsNotNull(shader, $"shader '{shaderName}' was not found");

                if (!ShaderUtil.ShaderHasError(shader))
                {
                    Assert.IsTrue(shader.isSupported, $"shader '{shaderName}' is unsupported");
                    continue;
                }

                var details = new List<string>();
                foreach (ShaderMessage message in ShaderUtil.GetShaderMessages(shader))
                {
                    details.Add($"{message.file}({message.line}): {message.message} {message.messageDetails}");
                }

                Assert.Fail($"shader '{shaderName}' failed to compile:\n" + string.Join("\n", details));
            }

            // LiquidBodyCanvas blits with passes 0, 1 and 2 by index.
            Assert.AreEqual(3, Shader.Find(LiquidAssets.CanvasUpdateShader).passCount);
        }

        [Test]
        public void Builder_CreatesWiredCanvasesWithTheirOwnMaterials()
        {
            GameObject root = LiquidCanvasPoolBuilder.CreateCanvasPool(3);
            _created.Add(root);
            Assert.IsNotNull(root, "the builder could not find its shaders");

            LiquidCanvasPool pool = root.GetComponent<LiquidCanvasPool>();
            Assert.IsNotNull(pool);
            Assert.AreEqual(3, pool.canvases.Length);

            var materials = new HashSet<Material>();
            foreach (LiquidBodyCanvas canvas in pool.canvases)
            {
                Assert.IsNotNull(canvas);
                Assert.IsNotNull(canvas.updateMaterial);
                Assert.IsNotNull(canvas.projectorMaterial);
                Assert.IsTrue(materials.Add(canvas.projectorMaterial),
                    "two canvases share a projector material, so they would show each other's contents");
                Assert.AreSame(pool.canvases[0].updateMaterial, canvas.updateMaterial);

                Assert.IsNotNull(canvas.projectorObject);
                Assert.IsFalse(canvas.projectorObject.activeSelf, "an unassigned canvas must not draw");

                Projector projector = canvas.projectorObject.GetComponent<Projector>();
                Assert.IsNotNull(projector);
                Assert.IsTrue(projector.orthographic);
                Assert.AreSame(canvas.projectorMaterial, projector.material);
                Assert.AreEqual(canvas.halfExtents.y, projector.orthographicSize, 1e-5f);
                Assert.AreEqual(canvas.halfExtents.x / canvas.halfExtents.y, projector.aspectRatio, 1e-5f);
                Assert.AreEqual(canvas.halfExtents.z * 2f, projector.farClipPlane, 1e-5f);
                Assert.AreEqual(-canvas.halfExtents.z, projector.transform.localPosition.z, 1e-5f);

                foreach (int layer in new[] { 9, 10, 18 })
                {
                    Assert.AreEqual(0, projector.ignoreLayers & (1 << layer), $"avatar layer {layer} is ignored");
                }

                Assert.AreNotEqual(0, projector.ignoreLayers & 1, "the Default layer is not ignored");
            }
        }

        [Test]
        public void Stamp_WritesToTheFaceItPointsAtOnly()
        {
            Material update = UpdateMaterial();
            RenderTexture pigmentSource = Canvas(RenderTextureFormat.ARGB32);
            RenderTexture pigmentTarget = Canvas(RenderTextureFormat.ARGB32);
            RenderTexture filmSource = Canvas(RenderTextureFormat.ARGBHalf);
            RenderTexture filmTarget = Canvas(RenderTextureFormat.ARGBHalf);

            // A red stamp on the middle of the front (+Z) face.
            SetStamp(update, new Vector3(0f, 0f, HalfExtents.z), Vector3.forward, 0.3f,
                Color.red, pigment: 1f, film: 1f, dryingEncoded: 0f);
            Advance(update, pigmentSource, pigmentTarget, filmSource, filmTarget, deltaTime: 0f);

            Color[] pigment = Read(pigmentTarget, TextureFormat.RGBA32);
            Color[] film = Read(filmTarget, TextureFormat.RGBAHalf);

            Color front = TileCentre(pigment, 4);
            Assert.Greater(front.a, 0.5f, "the front face did not receive the stamp");
            Assert.Greater(front.r, 0.5f, "the stamp colour is wrong");
            Assert.Less(front.g, 0.1f, "the stamp colour is wrong");
            Assert.Greater(TileCentre(film, 4).r, 0.5f, "the front face did not get wet");

            for (int face = 0; face < LiquidBodyCanvas.FaceCount; face++)
            {
                if (face == 4)
                {
                    continue;
                }

                Assert.Less(TileCentre(pigment, face).a, 0.01f, $"face {face} received a stamp aimed at +Z");
                Assert.Less(TileCentre(film, face).r, 0.01f, $"face {face} got wet from a stamp aimed at +Z");
            }
        }

        [Test]
        public void Film_EvaporatesAndPigmentStays()
        {
            Material update = UpdateMaterial();
            RenderTexture pigmentA = Canvas(RenderTextureFormat.ARGB32);
            RenderTexture pigmentB = Canvas(RenderTextureFormat.ARGB32);
            RenderTexture filmA = Canvas(RenderTextureFormat.ARGBHalf);
            RenderTexture filmB = Canvas(RenderTextureFormat.ARGBHalf);

            // Dries in 10 s with a 0.2/s cap: encoded 1 / (10 * 0.2) = 0.5.
            SetStamp(update, new Vector3(0f, 0f, HalfExtents.z), Vector3.forward, 0.3f,
                Color.blue, pigment: 1f, film: 1f, dryingEncoded: 0.5f);
            Advance(update, pigmentA, pigmentB, filmA, filmB, deltaTime: 0f);
            float wetBefore = TileCentre(Read(filmB, TextureFormat.RGBAHalf), 4).r;

            update.SetFloat("_StampCount", 0f);
            Advance(update, pigmentB, pigmentA, filmB, filmA, deltaTime: 2f);
            float wetAfter = TileCentre(Read(filmA, TextureFormat.RGBAHalf), 4).r;
            float pigmentAfter = TileCentre(Read(pigmentA, TextureFormat.RGBA32), 4).a;

            // 2 s of a 10 s drying time removes 0.2 of the film.
            Assert.AreEqual(wetBefore - 0.2f, wetAfter, 0.03f, "the film did not evaporate at the encoded rate");
            Assert.Greater(pigmentAfter, 0.5f, "the pigment went away with the film");
        }

        private Material UpdateMaterial()
        {
            Shader shader = Shader.Find(LiquidAssets.CanvasUpdateShader);
            Assert.IsNotNull(shader);
            var material = new Material(shader);
            _created.Add(material);

            material.SetVector("_HalfExtents", new Vector4(HalfExtents.x, HalfExtents.y, HalfExtents.z, 0f));
            material.SetVector("_GravityCanvas", new Vector4(0f, -1f, 0f, 0f));
            material.SetFloat("_Inset", Inset);
            material.SetFloat("_FlowSpeed", 0f);
            material.SetFloat("_MaxEvaporationRate", 0.2f);
            return material;
        }

        private RenderTexture Canvas(RenderTextureFormat format)
        {
            var texture = new RenderTexture(Width, Height, 0, format)
            {
                useMipMap = false,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
            };
            texture.Create();
            _created.Add(texture);

            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = texture;
            GL.Clear(false, true, Color.clear);
            RenderTexture.active = previous;
            return texture;
        }

        private static void SetStamp(Material update, Vector3 local, Vector3 normal, float radius, Color color,
            float pigment, float film, float dryingEncoded)
        {
            var pos = new Vector4[LiquidBodyCanvas.MaxStampsPerUpdate];
            var nrm = new Vector4[LiquidBodyCanvas.MaxStampsPerUpdate];
            var col = new Vector4[LiquidBodyCanvas.MaxStampsPerUpdate];
            var flm = new Vector4[LiquidBodyCanvas.MaxStampsPerUpdate];
            var shp = new Vector4[LiquidBodyCanvas.MaxStampsPerUpdate];

            pos[0] = new Vector4(local.x, local.y, local.z, radius);
            nrm[0] = new Vector4(normal.x, normal.y, normal.z, pigment);
            col[0] = new Vector4(color.r, color.g, color.b, film);
            flm[0] = new Vector4(0f, 0.9f, 0f, dryingEncoded);
            shp[0] = new Vector4(0f, 0f, 0f, 0f);

            update.SetFloat("_StampCount", 1f);
            update.SetVectorArray("_StampPos", pos);
            update.SetVectorArray("_StampNormal", nrm);
            update.SetVectorArray("_StampColor", col);
            update.SetVectorArray("_StampFilm", flm);
            update.SetVectorArray("_StampShape", shp);
        }

        private static void Advance(Material update, RenderTexture pigmentSource, RenderTexture pigmentTarget,
            RenderTexture filmSource, RenderTexture filmTarget, float deltaTime)
        {
            update.SetFloat("_DeltaTime", deltaTime);
            update.SetTexture("_FilmTex", filmSource);
            Graphics.Blit(pigmentSource, pigmentTarget, update, 0);
            Graphics.Blit(filmSource, filmTarget, update, 1);
        }

        private Color[] Read(RenderTexture source, TextureFormat format)
        {
            var readback = new Texture2D(Width, Height, format, false, true);
            _created.Add(readback);

            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = source;
            readback.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
            readback.Apply();
            RenderTexture.active = previous;
            return readback.GetPixels();
        }

        private static Color TileCentre(Color[] pixels, int face)
        {
            int column = face % LiquidBodyCanvas.AtlasColumns;
            int row = face / LiquidBodyCanvas.AtlasColumns;
            int x = column * FaceResolution + FaceResolution / 2;
            int y = row * FaceResolution + FaceResolution / 2;
            return pixels[y * Width + x];
        }
    }
}
