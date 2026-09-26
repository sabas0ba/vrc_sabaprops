using System.Collections.Generic;
using NUnit.Framework;
using SabaProps.Liquid.Editors;
using UdonSharp.Compiler;
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

        /// <summary>
        /// The builders add UdonSharp behaviours, which serialise through the
        /// compiled programs. Batch mode does not necessarily compile before the
        /// first test, so this class must not depend on another having run.
        /// </summary>
        [OneTimeSetUp]
        public void CompileUdonSharpPrograms()
        {
            UdonSharpCompilerV1.CompileSync(new UdonSharpCompileOptions { IsEditorBuild = true });
        }

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
            foreach (string shaderName in new[]
            {
                LiquidAssets.BodyProjectorShader, LiquidAssets.CanvasUpdateShader, LiquidWeatherBuilder.WeatherSurfaceShader,
                "SabaProps/Liquid/Fog Volume", "SabaProps/Liquid/Fogged Glass",
            })
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

            // LiquidBodyCanvas blits with passes 0 to 4 by index.
            Assert.AreEqual(5, Shader.Find(LiquidAssets.CanvasUpdateShader).passCount);
        }

        [Test]
        public void Glow_IsCarriedByPigmentAndPaintedOver()
        {
            Material update = UpdateMaterial();
            RenderTexture pigmentSource = Canvas(RenderTextureFormat.ARGB32);
            RenderTexture pigmentTarget = Canvas(RenderTextureFormat.ARGB32);
            RenderTexture filmSource = Canvas(RenderTextureFormat.ARGBHalf);
            RenderTexture filmTarget = Canvas(RenderTextureFormat.ARGBHalf);
            RenderTexture glowSource = Canvas(RenderTextureFormat.RGHalf);
            RenderTexture glowTarget = Canvas(RenderTextureFormat.RGHalf);

            // Fluorescent paint on the front face: the glow canvas records it as fluorescent, not luminous.
            SetStamp(update, new Vector3(0f, 0f, HalfExtents.z), Vector3.forward, 0.3f,
                Color.magenta, pigment: 1f, film: 0.5f, dryingEncoded: 0f);
            SetGlow(update, 1f, 0f);
            update.SetFloat("_DeltaTime", 0f);
            update.SetTexture("_FilmTex", filmSource);
            Graphics.Blit(glowSource, glowTarget, update, 4);

            Color glow = TileCentre(Read(glowTarget, TextureFormat.RGBAHalf), 4);
            Assert.Greater(glow.r, 0.9f, "fluorescent paint left no fluorescence");
            Assert.Less(glow.g, 0.01f, "fluorescent paint was recorded as luminous");
            Assert.Less(TileCentre(Read(glowTarget, TextureFormat.RGBAHalf), 5).r, 0.01f, "the back face glows");

            // Ordinary paint over it covers the glow, as it covers the colour.
            SetStamp(update, new Vector3(0f, 0f, HalfExtents.z), Vector3.forward, 0.3f,
                Color.red, pigment: 1f, film: 0.5f, dryingEncoded: 0f);
            SetGlow(update, 0f, 0f);
            Graphics.Blit(glowTarget, glowSource, update, 4);
            Assert.Less(TileCentre(Read(glowSource, TextureFormat.RGBAHalf), 4).r, 0.05f,
                "ordinary paint over fluorescent paint still glows");
        }

        private static void SetGlow(Material update, float fluorescence, float luminescence)
        {
            var glow = new Vector4[LiquidBodyCanvas.MaxStampsPerUpdate];
            glow[0] = new Vector4(fluorescence, luminescence, 0f, 0f);
            update.SetVectorArray("_StampGlow", glow);
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

                // Cameras cull the projector by its own layer. Avatar-only mirrors
                // render Player but not Default.
                Assert.AreEqual(LiquidCanvasPoolBuilder.PlayerLayer, projector.gameObject.layer,
                    "the projector is not on the Player layer, so avatar-only mirrors will not show it");
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
        public void Depth_RecordsTheSurfaceTheStampLandedOn()
        {
            Material update = UpdateMaterial();
            RenderTexture pigmentA = Canvas(RenderTextureFormat.ARGB32);
            RenderTexture pigmentB = Canvas(RenderTextureFormat.ARGB32);
            RenderTexture filmA = Canvas(RenderTextureFormat.ARGBHalf);
            RenderTexture filmB = Canvas(RenderTextureFormat.ARGBHalf);
            RenderTexture depthA = Canvas(RenderTextureFormat.RGHalf);
            RenderTexture depthB = Canvas(RenderTextureFormat.RGHalf);

            // Paint on the outer side of a right arm, 0.49 m out along +X.
            SetStamp(update, new Vector3(0.49f, 0f, 0f), Vector3.right, 0.3f,
                Color.red, pigment: 1f, film: 1f, dryingEncoded: 0f);
            Advance(update, pigmentA, pigmentB, filmA, filmB, depthA, depthB, deltaTime: 0f);

            Color[] depth = Read(depthB, TextureFormat.RGBAHalf);
            Color landed = TileCentre(depth, 0);
            Assert.AreEqual(0.49f, landed.r, 0.01f, "the +X tile does not hold the depth of the arm");
            Assert.Greater(landed.g, 0.9f, "the depth is recorded without confidence");
            Assert.Less(TileCentre(depth, 1).g, 0.01f, "the -X tile recorded a depth for a stamp facing +X");

            // A wash-only stamp does not move the recorded depth.
            SetStamp(update, new Vector3(0.1f, 0f, 0f), Vector3.right, 0.3f,
                Color.white, pigment: 0f, film: 0f, dryingEncoded: 0f);
            Advance(update, pigmentB, pigmentA, filmB, filmA, depthB, depthA, deltaTime: 0f);
            Assert.AreEqual(0.49f, TileCentre(Read(depthA, TextureFormat.RGBAHalf), 0).r, 0.01f,
                "a stamp that leaves nothing behind moved the recorded depth");
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

        /// <summary>
        /// Draws a stand-in body through the real projector and writes
        /// TestResults/liquid-preview.png for a person to look at.
        /// <para>
        /// Asserts only that the projector changed the picture. Whether the wet
        /// and muddy parts look right is a judgement the image exists for.
        /// </para>
        /// </summary>
        [Test]
        public void RenderPreviewForVisualReview()
        {
            const int layer = 9;
            Vector3 origin = new Vector3(0f, 1.05f, 0f);

            Primitive(PrimitiveType.Capsule, new Vector3(0f, 0.85f, 0f), new Vector3(0.55f, 0.85f, 0.4f), layer);
            Primitive(PrimitiveType.Sphere, new Vector3(0f, 1.88f, 0f), Vector3.one * 0.34f, layer);
            Primitive(PrimitiveType.Capsule, new Vector3(-0.42f, 1.15f, 0f), new Vector3(0.14f, 0.35f, 0.14f), layer);
            Primitive(PrimitiveType.Capsule, new Vector3(0.42f, 1.15f, 0f), new Vector3(0.14f, 0.35f, 0.14f), layer);
            var skin = new Material(Shader.Find("Standard")) { color = new Color(0.86f, 0.78f, 0.72f) };
            _created.Add(skin);
            foreach (Renderer renderer in Object.FindObjectsOfType<Renderer>())
            {
                if (renderer.gameObject.layer == layer)
                {
                    renderer.sharedMaterial = skin;
                }
            }

            // Canvas contents: mud on the chest, paint on the right arm, water on the back.
            Material update = UpdateMaterial();
            // The package default resolution, so the picture shows what a world gets.
            const int previewResolution = 160;
            RenderTexture pigmentA = Canvas(RenderTextureFormat.ARGB32, previewResolution);
            RenderTexture pigmentB = Canvas(RenderTextureFormat.ARGB32, previewResolution);
            RenderTexture filmA = Canvas(RenderTextureFormat.ARGBHalf, previewResolution);
            RenderTexture filmB = Canvas(RenderTextureFormat.ARGBHalf, previewResolution);
            RenderTexture depthA = Canvas(RenderTextureFormat.RGHalf, previewResolution);
            RenderTexture depthB = Canvas(RenderTextureFormat.RGHalf, previewResolution);

            // Stamp centres sit on the stand-in's surfaces: the chest front at z = 0.2,
            // the right arm's outer side at x = 0.49 and the back at z = -0.2.
            SetStamps(update,
                (new Vector3(0.05f, 0.2f, 0.2f), Vector3.forward, 0.22f, new Color(0.32f, 0.22f, 0.13f), 0.9f, 0.8f, 0.85f),
                (new Vector3(0.49f, 0.1f, 0.02f), new Vector3(1f, 0f, 0.2f).normalized, 0.16f, new Color(0.85f, 0.1f, 0.12f), 1f, 0.6f, 0.3f),
                (new Vector3(0f, 0.2f, -0.2f), Vector3.back, 0.4f, Color.white, 0f, 1f, 0.1f));
            Advance(update, pigmentA, pigmentB, filmA, filmB, depthA, depthB, deltaTime: 0f);

            // Let the water run for a few seconds so the drips show.
            update.SetFloat("_StampCount", 0f);
            update.SetFloat("_FlowSpeed", 0.08f);
            for (int i = 0; i < 20; i++)
            {
                bool even = i % 2 == 0;
                Advance(update, even ? pigmentB : pigmentA, even ? pigmentA : pigmentB,
                    even ? filmB : filmA, even ? filmA : filmB,
                    even ? depthB : depthA, even ? depthA : depthB, deltaTime: 0.1f);
            }

            // The atlas as the projector will read it, for reviewing the canvas on its own.
            WritePng(pigmentB, "liquid-preview-pigment.png");
            WritePng(filmB, "liquid-preview-film.png");

            Shader projectorShader = Shader.Find(LiquidAssets.BodyProjectorShader);
            var projectorMaterial = new Material(projectorShader);
            _created.Add(projectorMaterial);
            projectorMaterial.SetTexture("_PigmentTex", pigmentB);
            projectorMaterial.SetTexture("_FilmTex", filmB);
            projectorMaterial.SetTexture("_DepthTex", depthB);
            projectorMaterial.SetFloat("_CanvasInset", Inset);
            projectorMaterial.SetVector("_CanvasRowX", Row(Vector3.right, origin, HalfExtents.x));
            projectorMaterial.SetVector("_CanvasRowY", Row(Vector3.up, origin, HalfExtents.y));
            projectorMaterial.SetVector("_CanvasRowZ", Row(Vector3.forward, origin, HalfExtents.z));
            // Standing in mud to the shins, and wet to the waist from a pool.
            projectorMaterial.SetVector("_ImmersionLevels", new Vector4(-0.1f, -0.65f, 0.02f, 1f));
            projectorMaterial.SetVector("_ImmersionAmounts", new Vector4(0.9f, 0.85f, 0.9f, 0f));
            projectorMaterial.SetColor("_ImmersionColor", new Color(0.32f, 0.22f, 0.13f));

            var projectorRoot = new GameObject("Canvas");
            _created.Add(projectorRoot);
            projectorRoot.transform.position = origin;
            var projectorObject = new GameObject("Projector");
            projectorObject.transform.SetParent(projectorRoot.transform, false);
            LiquidCanvasPoolBuilder.ConfigureProjector(projectorObject, HalfExtents, projectorMaterial);

            var lightObject = new GameObject("Key Light");
            _created.Add(lightObject);
            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.transform.rotation = Quaternion.Euler(35f, 150f, 0f);
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.45f, 0.47f, 0.5f);

            Texture2D withLiquid = RenderViews(layer, "liquid-preview.png");
            projectorObject.SetActive(false);
            Texture2D without = RenderViews(layer, null);

            int changed = 0;
            Color[] a = withLiquid.GetPixels();
            Color[] b = without.GetPixels();
            for (int i = 0; i < a.Length; i++)
            {
                if (Mathf.Abs(a[i].r - b[i].r) + Mathf.Abs(a[i].g - b[i].g) + Mathf.Abs(a[i].b - b[i].b) > 0.05f)
                {
                    changed++;
                }
            }

            Assert.Greater(changed, a.Length / 50, "the projector left the body looking the same");
        }

        /// <summary>Front and back views side by side.</summary>
        private Texture2D RenderViews(int layer, string fileName)
        {
            const int size = 512;
            var target = new RenderTexture(size * 2, size, 24, RenderTextureFormat.ARGB32);
            target.Create();
            _created.Add(target);

            var cameraObject = new GameObject("Preview Camera");
            _created.Add(cameraObject);
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.targetTexture = target;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.2f, 0.21f, 0.23f);
            camera.cullingMask = 1 << layer;
            camera.fieldOfView = 30f;
            camera.aspect = 1f;

            Vector3 look = new Vector3(0f, 1.05f, 0f);
            camera.rect = new Rect(0f, 0f, 0.5f, 1f);
            camera.transform.position = look + new Vector3(1.4f, 0.4f, 4.2f);
            camera.transform.LookAt(look);
            camera.Render();

            camera.rect = new Rect(0.5f, 0f, 0.5f, 1f);
            camera.transform.position = look + new Vector3(-1.4f, 0.4f, -4.2f);
            camera.transform.LookAt(look);
            camera.Render();

            var image = new Texture2D(size * 2, size, TextureFormat.RGB24, false);
            _created.Add(image);
            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = target;
            image.ReadPixels(new Rect(0, 0, size * 2, size), 0, 0);
            image.Apply();
            RenderTexture.active = previous;
            camera.targetTexture = null;

            if (fileName != null)
            {
                string directory = System.IO.Path.Combine(System.IO.Directory.GetCurrentDirectory(), "TestResults");
                System.IO.Directory.CreateDirectory(directory);
                System.IO.File.WriteAllBytes(System.IO.Path.Combine(directory, fileName), image.EncodeToPNG());
            }

            return image;
        }

        /// <summary>
        /// Writes a canvas texture as a PNG. Premultiplied pigment is shown over
        /// grey so uncovered texels are distinguishable from black pigment; film
        /// shows its amount in red.
        /// </summary>
        private void WritePng(RenderTexture source, string fileName)
        {
            var readback = new Texture2D(source.width, source.height, TextureFormat.RGBAHalf, false, true);
            _created.Add(readback);
            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = source;
            readback.ReadPixels(new Rect(0, 0, source.width, source.height), 0, 0);
            readback.Apply();
            RenderTexture.active = previous;

            Color[] pixels = readback.GetPixels();
            for (int i = 0; i < pixels.Length; i++)
            {
                Color c = pixels[i];
                pixels[i] = new Color(c.r + 0.5f * (1f - c.a), c.g + 0.5f * (1f - c.a), c.b + 0.5f * (1f - c.a), 1f);
            }

            var image = new Texture2D(source.width, source.height, TextureFormat.RGB24, false);
            _created.Add(image);
            image.SetPixels(pixels);
            image.Apply();

            string directory = System.IO.Path.Combine(System.IO.Directory.GetCurrentDirectory(), "TestResults");
            System.IO.Directory.CreateDirectory(directory);
            System.IO.File.WriteAllBytes(System.IO.Path.Combine(directory, fileName), image.EncodeToPNG());
        }

        private GameObject Primitive(PrimitiveType type, Vector3 position, Vector3 scale, int layer)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            _created.Add(go);
            go.layer = layer;
            go.transform.position = position;
            go.transform.localScale = scale;
            return go;
        }

        private static Vector4 Row(Vector3 axis, Vector3 origin, float half)
        {
            return new Vector4(axis.x / half, axis.y / half, axis.z / half, -Vector3.Dot(axis, origin) / half);
        }

        private static void SetStamps(Material update,
            params (Vector3 local, Vector3 normal, float radius, Color color, float pigment, float film, float viscosity)[] stamps)
        {
            var pos = new Vector4[LiquidBodyCanvas.MaxStampsPerUpdate];
            var nrm = new Vector4[LiquidBodyCanvas.MaxStampsPerUpdate];
            var col = new Vector4[LiquidBodyCanvas.MaxStampsPerUpdate];
            var flm = new Vector4[LiquidBodyCanvas.MaxStampsPerUpdate];
            var shp = new Vector4[LiquidBodyCanvas.MaxStampsPerUpdate];

            for (int i = 0; i < stamps.Length; i++)
            {
                var s = stamps[i];
                pos[i] = new Vector4(s.local.x, s.local.y, s.local.z, s.radius);
                nrm[i] = new Vector4(s.normal.x, s.normal.y, s.normal.z, s.pigment);
                col[i] = new Vector4(s.color.r, s.color.g, s.color.b, s.film);
                flm[i] = new Vector4(0f, 0.9f, s.viscosity, 0f);
                shp[i] = new Vector4(i * 17.3f, 0.6f, 0f, 0f);
            }

            update.SetFloat("_StampCount", stamps.Length);
            update.SetVectorArray("_StampPos", pos);
            update.SetVectorArray("_StampNormal", nrm);
            update.SetVectorArray("_StampColor", col);
            update.SetVectorArray("_StampFilm", flm);
            update.SetVectorArray("_StampShape", shp);
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
            return Canvas(format, FaceResolution);
        }

        private RenderTexture Canvas(RenderTextureFormat format, int faceResolution)
        {
            var texture = new RenderTexture(
                faceResolution * LiquidBodyCanvas.AtlasColumns, faceResolution * LiquidBodyCanvas.AtlasRows, 0, format)
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
            Advance(update, pigmentSource, pigmentTarget, filmSource, filmTarget, null, null, deltaTime);
        }

        /// <summary>The same pass order as LiquidBodyCanvas.AdvanceCanvas.</summary>
        private static void Advance(Material update, RenderTexture pigmentSource, RenderTexture pigmentTarget,
            RenderTexture filmSource, RenderTexture filmTarget, RenderTexture depthSource, RenderTexture depthTarget,
            float deltaTime)
        {
            update.SetFloat("_DeltaTime", deltaTime);
            update.SetTexture("_FilmTex", filmSource);
            Graphics.Blit(pigmentSource, pigmentTarget, update, 0);
            if (depthSource != null)
            {
                Graphics.Blit(depthSource, depthTarget, update, 3);
            }

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
