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

            var materials = new HashSet<string>();
            foreach (LiquidBodyCanvas canvas in pool.canvases)
            {
                Assert.IsNotNull(canvas);
                Assert.IsNotNull(canvas.updateMaterial);
                Assert.IsNotNull(canvas.projectorMaterial);
                // Compared by asset path: what matters is which asset each reference
                // resolves to, not which in-memory instance the editor handed back.
                Assert.IsTrue(materials.Add(AssetDatabase.GetAssetPath(canvas.projectorMaterial)),
                    "two canvases share a projector material, so they would show each other's contents");
                Assert.AreEqual(AssetDatabase.GetAssetPath(pool.canvases[0].updateMaterial),
                    AssetDatabase.GetAssetPath(canvas.updateMaterial));

                Assert.IsNotNull(canvas.projectorObject);
                Assert.IsFalse(canvas.projectorObject.activeSelf, "an unassigned canvas must not draw");

                Projector projector = canvas.projectorObject.GetComponent<Projector>();
                Assert.IsNotNull(projector);
                Assert.IsTrue(projector.orthographic);
                Assert.AreEqual(AssetDatabase.GetAssetPath(canvas.projectorMaterial),
                    AssetDatabase.GetAssetPath(projector.material),
                    "the projector draws with a different material from the one the canvas writes to");
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

        [Test]
        public void Wash_RemovesPigmentBelowTheLevelOnly()
        {
            Material update = UpdateMaterial();
            RenderTexture pigmentA = Canvas(RenderTextureFormat.ARGB32);
            RenderTexture pigmentB = Canvas(RenderTextureFormat.ARGB32);
            RenderTexture filmA = Canvas(RenderTextureFormat.ARGBHalf);
            RenderTexture filmB = Canvas(RenderTextureFormat.ARGBHalf);

            // Pigment over the whole front face.
            SetStamp(update, new Vector3(0f, 0f, HalfExtents.z), Vector3.forward, 3f,
                Color.red, pigment: 1f, film: 0f, dryingEncoded: 0f);
            Advance(update, pigmentA, pigmentB, filmA, filmB, deltaTime: 0f);

            // Wash everything below the middle of the body, fully.
            update.SetFloat("_StampCount", 0f);
            update.SetVector("_Wash", new Vector4(0f, 1f, 0.02f, 0f));
            Advance(update, pigmentB, pigmentA, filmB, filmA, deltaTime: 0f);
            update.SetVector("_Wash", new Vector4(-2f, 0f, 0.02f, 0f));

            Color[] pigment = Read(pigmentA, TextureFormat.RGBA32);
            Assert.Less(TilePixel(pigment, 4, 0.5f, 0.2f).a, 0.02f, "pigment below the wash level stayed");
            Assert.Greater(TilePixel(pigment, 4, 0.5f, 0.8f).a, 0.5f, "pigment above the wash level was washed");
        }

        [Test]
        public void SourceBuilder_WiresSourcesToThePoolAndProfiles()
        {
            LiquidCanvasPool pool = LiquidSourceBuilder.FindOrCreatePool();
            _created.Add(pool.gameObject);

            LiquidProfile water = LiquidSourceBuilder.GetProfile(LiquidSourceBuilder.WaterName);
            LiquidProfile mud = LiquidSourceBuilder.GetProfile(LiquidSourceBuilder.MudName);
            _created.Add(water.transform.parent.gameObject);

            Assert.AreSame(water, LiquidSourceBuilder.GetProfile(LiquidSourceBuilder.WaterName), "a second request made a second water profile");
            Assert.AreEqual(0f, water.pigmentAmount, "water carries pigment");
            Assert.Greater(water.washStrength, 0f, "water does not wash");
            Assert.Greater(mud.pigmentAmount, 0f, "mud carries no pigment");
            Assert.Greater(mud.viscosity, water.viscosity, "mud runs as freely as water");

            GameObject volume = LiquidSourceBuilder.CreateImmersionVolume("Volume", pool, water, new Vector3(2f, 1f, 2f));
            _created.Add(volume);
            Assert.IsTrue(volume.GetComponent<BoxCollider>().isTrigger, "the volume blocks players instead of detecting them");
            Assert.AreSame(pool, volume.GetComponent<LiquidImmersionVolume>().pool);

            GameObject shower = LiquidSourceBuilder.CreateShower(pool, water);
            _created.Add(shower);
            LiquidShower head = shower.GetComponentInChildren<LiquidShower>();
            Assert.IsNotNull(head);
            Assert.IsNotNull(head.GetComponent<Collider>(), "the shower cannot be toggled without a collider");
            Assert.Greater(Vector3.Dot(head.nozzle.forward, Vector3.down), 0.99f, "the shower does not point down");
            Assert.IsFalse(head.stream.main.playOnAwake, "the stream plays before the shower is turned on");

            GameObject gun = LiquidSourceBuilder.CreateWaterGun(pool, water);
            _created.Add(gun);
            Assert.IsNotNull(gun.GetComponent<VRC.SDK3.Components.VRCPickup>());
            Assert.IsNotNull(gun.GetComponent<VRC.SDK3.Components.VRCObjectSync>(), "the gun's position is not synced");
            LiquidWaterGun waterGun = gun.GetComponent<LiquidWaterGun>();
            Assert.AreSame(pool, waterGun.pool);
            Assert.AreSame(water, waterGun.profile);
            Assert.AreEqual(Vector3.forward, waterGun.muzzle.localRotation * Vector3.forward, "the muzzle does not point along the gun");
        }

        private static Color TilePixel(Color[] pixels, int face, float u, float v)
        {
            int column = face % LiquidBodyCanvas.AtlasColumns;
            int row = face / LiquidBodyCanvas.AtlasColumns;
            int x = column * FaceResolution + Mathf.Clamp(Mathf.RoundToInt(u * FaceResolution), 0, FaceResolution - 1);
            int y = row * FaceResolution + Mathf.Clamp(Mathf.RoundToInt(v * FaceResolution), 0, FaceResolution - 1);
            return pixels[y * Width + x];
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
