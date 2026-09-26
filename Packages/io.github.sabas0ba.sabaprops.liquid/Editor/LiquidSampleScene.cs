using System.Text;
using UdonSharpEditor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using VRC.SDK3.Components;

namespace SabaProps.Liquid.Editors
{
    /// <summary>
    /// Generates a demo world with one of each Source and a mirror to watch
    /// the result on your own avatar.
    /// <para>
    /// The pits are real: the pool and the mud bog are holes in the ground, so
    /// the liquid line rises on the avatar as the player walks down into them.
    /// The mud's visible surface sits level with the ground while its floor
    /// collider is lower, so a player standing in it sinks below the surface,
    /// and the mud clings as high as they sank.
    /// </para>
    /// <para>
    /// Generated rather than shipped inside the package: VCC replaces the
    /// package folder on upgrade and would take the user's edits with it.
    /// </para>
    /// </summary>
    public static class LiquidSampleScene
    {
        public const string SampleFolder = LiquidAssets.RootFolder + "/Samples";
        public const string ScenePath = SampleFolder + "/LiquidDemo.unity";

        public const string GroundMaterialPath = SampleFolder + "/Ground.mat";
        public const string TileMaterialPath = SampleFolder + "/Tile.mat";
        public const string WaterSurfaceMaterialPath = SampleFolder + "/WaterSurface.mat";
        public const string MudSurfaceMaterialPath = SampleFolder + "/MudSurface.mat";
        public const string FurnitureMaterialPath = SampleFolder + "/Furniture.mat";

        public const string MirrorMaterialPath = "Packages/com.vrchat.base/Runtime/VRCSDK/Sample Assets/Materials/MirrorReflection.mat";

        // Named as constants because the world tests navigate the scene by them.
        public const string GroundRootName = "Ground";
        public const string PoolName = "Pool";
        public const string MudBogName = "Mud Bog";
        public const string ShowerName = "Shower";
        public const string FaucetName = "Faucet";
        public const string WaterGunsName = "Water Guns";
        public const string MirrorName = "Mirror";

        public static readonly Vector3 SpawnPosition = new Vector3(0f, 0.05f, -7f);

        /// <summary>Pool pit: centre, footprint, depth of the floor and height of the water surface.</summary>
        public static readonly Vector3 PoolCentre = new Vector3(-5f, 0f, 2f);
        public static readonly Vector2 PoolSize = new Vector2(5f, 5f);
        public const float PoolFloorY = -1.3f;
        public const float PoolSurfaceY = -0.15f;

        /// <summary>
        /// Mud pit. The visible surface is at ground level and the floor
        /// collider below it by the sinking depth.
        /// </summary>
        public static readonly Vector3 MudCentre = new Vector3(5f, 0f, 2f);
        public static readonly Vector2 MudSize = new Vector2(3f, 3f);
        public const float MudFloorY = -0.45f;
        public const float MudSurfaceY = 0f;

        private const float GroundHalfSize = 12f;
        private const float GroundThickness = 2f;

        [MenuItem("Tools/SabaProps/Liquid/Create Sample Scene", false, 1)]
        public static void CreateAndOpen()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            Scene scene = Create();
            if (!scene.IsValid())
            {
                return;
            }

            SceneView view = SceneView.lastActiveSceneView;
            if (view != null)
            {
                view.LookAt(new Vector3(0f, 0.5f, 1f), Quaternion.Euler(35f, 0f, 0f), 14f);
            }
        }

        /// <summary>
        /// Replaces the open scene with the demo and saves it to
        /// <see cref="ScenePath"/>. Prompt free, so tests and batch mode can call
        /// it directly.
        /// </summary>
        public static Scene Create()
        {
            LiquidAssets.EnsureFolder(SampleFolder);

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            ConfigureLight();

            Material ground = LiquidAssets.CreateOrLoadSurfaceMaterial(GroundMaterialPath, new Color(0.55f, 0.56f, 0.52f), 0.1f);
            Material tile = LiquidAssets.CreateOrLoadSurfaceMaterial(TileMaterialPath, new Color(0.78f, 0.84f, 0.86f), 0.5f);
            Material furniture = LiquidAssets.CreateOrLoadSurfaceMaterial(FurnitureMaterialPath, new Color(0.42f, 0.33f, 0.25f), 0.2f);

            BuildGround(ground, tile);

            LiquidCanvasPool pool = LiquidSourceBuilder.FindOrCreatePool();
            LiquidProfile water = LiquidSourceBuilder.GetProfile(LiquidSourceBuilder.WaterName);
            LiquidProfile mud = LiquidSourceBuilder.GetProfile(LiquidSourceBuilder.MudName);

            BuildPool(pool, water, tile);
            BuildMudBog(pool, mud);
            BuildShower(pool, water, tile);
            BuildFaucet(pool, water, furniture);
            BuildWaterGuns(pool, water, furniture);
            BuildMirror();
            BuildWorld();

            AssetDatabase.SaveAssets();
            EditorSceneManager.SaveScene(scene, ScenePath);

            Debug.Log(Summarise());
            return scene;
        }

        // ------------------------------------------------------------------
        // Ground with two pits
        // ------------------------------------------------------------------

        /// <summary>
        /// The ground as slabs around the two pits. The slabs are thick, so
        /// their sides are the pits' walls.
        /// </summary>
        private static void BuildGround(Material ground, Material tile)
        {
            var root = new GameObject(GroundRootName);
            float edge = GroundHalfSize;

            Rect poolRect = RectAround(PoolCentre, PoolSize);
            Rect mudRect = RectAround(MudCentre, MudSize);
            float bandMin = Mathf.Min(poolRect.yMin, mudRect.yMin);
            float bandMax = Mathf.Max(poolRect.yMax, mudRect.yMax);

            // In front of and behind the band that holds the pits.
            Slab(root.transform, "Front", -edge, edge, -edge, bandMin, 0f, GroundThickness, ground);
            Slab(root.transform, "Back", -edge, edge, bandMax, edge, 0f, GroundThickness, ground);

            // The band itself, left to right, skipping the pits.
            Slab(root.transform, "Band Left", -edge, poolRect.xMin, bandMin, bandMax, 0f, GroundThickness, ground);
            Slab(root.transform, "Band Middle", poolRect.xMax, mudRect.xMin, bandMin, bandMax, 0f, GroundThickness, ground);
            Slab(root.transform, "Band Right", mudRect.xMax, edge, bandMin, bandMax, 0f, GroundThickness, ground);
            Slab(root.transform, "Pool Front Fill", poolRect.xMin, poolRect.xMax, bandMin, poolRect.yMin, 0f, GroundThickness, ground);
            Slab(root.transform, "Pool Back Fill", poolRect.xMin, poolRect.xMax, poolRect.yMax, bandMax, 0f, GroundThickness, ground);
            Slab(root.transform, "Mud Front Fill", mudRect.xMin, mudRect.xMax, bandMin, mudRect.yMin, 0f, GroundThickness, ground);
            Slab(root.transform, "Mud Back Fill", mudRect.xMin, mudRect.xMax, mudRect.yMax, bandMax, 0f, GroundThickness, ground);

            // Pit floors.
            Slab(root.transform, "Pool Floor", poolRect.xMin, poolRect.xMax, poolRect.yMin, poolRect.yMax,
                PoolFloorY, 0.5f, tile);
            Slab(root.transform, "Mud Floor", mudRect.xMin, mudRect.xMax, mudRect.yMin, mudRect.yMax,
                MudFloorY, 0.5f, ground);
        }

        private static Rect RectAround(Vector3 centre, Vector2 size)
        {
            return new Rect(centre.x - size.x * 0.5f, centre.z - size.y * 0.5f, size.x, size.y);
        }

        /// <summary>A box spanning x and z, with its top face at <paramref name="top"/>.</summary>
        private static GameObject Slab(Transform parent, string name, float xMin, float xMax, float zMin, float zMax,
            float top, float thickness, Material material)
        {
            if (xMax - xMin < 1e-3f || zMax - zMin < 1e-3f)
            {
                return null;
            }

            GameObject slab = GameObject.CreatePrimitive(PrimitiveType.Cube);
            slab.name = name;
            slab.transform.SetParent(parent, false);
            slab.transform.localPosition = new Vector3((xMin + xMax) * 0.5f, top - thickness * 0.5f, (zMin + zMax) * 0.5f);
            slab.transform.localScale = new Vector3(xMax - xMin, thickness, zMax - zMin);
            slab.GetComponent<Renderer>().sharedMaterial = material;
            GameObjectUtility.SetStaticEditorFlags(slab, StaticEditorFlags.BatchingStatic | StaticEditorFlags.ContributeGI);
            return slab;
        }

        // ------------------------------------------------------------------
        // Sources
        // ------------------------------------------------------------------

        /// <summary>
        /// The pool: a ramp down one side, a translucent surface and a trigger
        /// from the surface to the floor.
        /// </summary>
        private static void BuildPool(LiquidCanvasPool pool, LiquidProfile water, Material tile)
        {
            var root = new GameObject(PoolName);
            root.transform.position = new Vector3(PoolCentre.x, 0f, PoolCentre.z);

            // Ramp along the near edge, from the ground down to the floor.
            float rampLength = PoolSize.y - 0.5f;
            float drop = -PoolFloorY;
            float angle = Mathf.Atan2(drop, rampLength) * Mathf.Rad2Deg;
            GameObject ramp = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ramp.name = "Ramp";
            ramp.transform.SetParent(root.transform, false);
            ramp.transform.localPosition = new Vector3(-PoolSize.x * 0.5f + 0.6f, PoolFloorY * 0.5f - 0.05f, 0f);
            // 正の X 回転で +Z 側が下がります。スポーン側（-Z）が地面、奥が底になります。
            ramp.transform.localRotation = Quaternion.Euler(angle, 0f, 0f);
            ramp.transform.localScale = new Vector3(1.2f, 0.1f, Mathf.Sqrt(rampLength * rampLength + drop * drop));
            ramp.GetComponent<Renderer>().sharedMaterial = tile;

            GameObject surface = CreateSurface(root.transform, "Water Surface", PoolSize, PoolSurfaceY,
                LiquidAssets.CreateOrLoadTransparentMaterial(WaterSurfaceMaterialPath, new Color(0.35f, 0.62f, 0.78f, 0.35f), 0.95f));

            GameObject volume = LiquidSourceBuilder.CreateImmersionVolume("Water Volume", pool, water,
                new Vector3(PoolSize.x, PoolSurfaceY - PoolFloorY + 0.2f, PoolSize.y));
            volume.transform.SetParent(root.transform, false);
            volume.transform.localPosition = new Vector3(0f, PoolSurfaceY, 0f);
            SetSurface(volume, surface.transform);
        }

        /// <summary>
        /// The mud bog: an opaque surface at ground level with no collider, over
        /// a floor lower down, so players sink into it.
        /// </summary>
        private static void BuildMudBog(LiquidCanvasPool pool, LiquidProfile mud)
        {
            var root = new GameObject(MudBogName);
            root.transform.position = new Vector3(MudCentre.x, 0f, MudCentre.z);

            GameObject surface = CreateSurface(root.transform, "Mud Surface", MudSize, MudSurfaceY - 0.01f,
                LiquidAssets.CreateOrLoadSurfaceMaterial(MudSurfaceMaterialPath, new Color(0.3f, 0.21f, 0.13f), 0.45f));

            GameObject volume = LiquidSourceBuilder.CreateImmersionVolume("Mud Volume", pool, mud,
                new Vector3(MudSize.x, MudSurfaceY - MudFloorY + 0.2f, MudSize.y));
            volume.transform.SetParent(root.transform, false);
            volume.transform.localPosition = new Vector3(0f, MudSurfaceY, 0f);
            SetSurface(volume, surface.transform);
        }

        private static void BuildShower(LiquidCanvasPool pool, LiquidProfile water, Material tile)
        {
            GameObject shower = LiquidSourceBuilder.CreateShower(pool, water);
            shower.name = ShowerName;
            shower.transform.position = new Vector3(0f, 0f, 3.5f);

            Slab(shower.transform, "Shower Tray", -0.8f, 0.8f, -0.8f, 0.8f, 0.03f, 0.03f, tile);
        }

        /// <summary>
        /// A faucet over a sink: the same Source as the shower, narrowed and
        /// shortened so it reaches hands held under it rather than the whole body.
        /// </summary>
        private static void BuildFaucet(LiquidCanvasPool pool, LiquidProfile water, Material furniture)
        {
            GameObject faucet = LiquidSourceBuilder.CreateShower(pool, water);
            faucet.name = FaucetName;
            faucet.transform.position = new Vector3(3f, 0f, -3.5f);

            LiquidShower source = faucet.GetComponentInChildren<LiquidShower>();
            source.transform.localPosition = new Vector3(0f, 1.15f, 0f);
            source.transform.localScale = new Vector3(0.05f, 0.05f, 0.05f);
            source.nozzle.localPosition = new Vector3(0f, 1.12f, 0f);
            source.coneAngle = 4f;
            source.range = 0.8f;
            source.raysPerEvaluation = 3;
            source.startRunning = false;
            UdonSharpEditorUtility.CopyProxyToUdon(source);
            EditorUtility.SetDirty(source);

            Slab(faucet.transform, "Sink", -0.4f, 0.4f, -0.3f, 0.3f, 0.85f, 0.85f, furniture);
        }

        private static void BuildWaterGuns(LiquidCanvasPool pool, LiquidProfile water, Material furniture)
        {
            var root = new GameObject(WaterGunsName);
            root.transform.position = new Vector3(-2.5f, 0f, -5f);

            Slab(root.transform, "Table", -0.6f, 0.6f, -0.4f, 0.4f, 0.8f, 0.8f, furniture);

            for (int i = 0; i < 2; i++)
            {
                GameObject gun = LiquidSourceBuilder.CreateWaterGun(pool, water);
                gun.name = "Water Gun " + (i + 1);
                gun.transform.SetParent(root.transform, false);
                gun.transform.localPosition = new Vector3(-0.25f + i * 0.5f, 0.86f, 0f);
                gun.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);
            }
        }

        private static GameObject CreateSurface(Transform parent, string name, Vector2 size, float y, Material material)
        {
            GameObject surface = GameObject.CreatePrimitive(PrimitiveType.Quad);
            surface.name = name;
            Object.DestroyImmediate(surface.GetComponent<Collider>());
            surface.transform.SetParent(parent, false);
            surface.transform.localPosition = new Vector3(0f, y, 0f);
            surface.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            surface.transform.localScale = new Vector3(size.x, size.y, 1f);
            surface.GetComponent<Renderer>().sharedMaterial = material;
            return surface;
        }

        private static void SetSurface(GameObject volumeObject, Transform surface)
        {
            LiquidImmersionVolume volume = volumeObject.GetComponent<LiquidImmersionVolume>();
            volume.surface = surface;
            UdonSharpEditorUtility.CopyProxyToUdon(volume);
            EditorUtility.SetDirty(volume);
        }

        // ------------------------------------------------------------------
        // Mirror, light and world
        // ------------------------------------------------------------------

        /// <summary>
        /// A mirror behind the shower, facing the spawn, so the result shows on
        /// the local avatar without a second player.
        /// </summary>
        private static void BuildMirror()
        {
            GameObject mirror = GameObject.CreatePrimitive(PrimitiveType.Quad);
            mirror.name = MirrorName;
            Object.DestroyImmediate(mirror.GetComponent<Collider>());
            mirror.transform.position = new Vector3(0f, 1.6f, 7f);
            mirror.transform.localScale = new Vector3(8f, 3.2f, 1f);

            Material material = AssetDatabase.LoadAssetAtPath<Material>(MirrorMaterialPath);
            if (material != null)
            {
                mirror.GetComponent<Renderer>().sharedMaterial = material;
            }

            mirror.AddComponent<VRCMirrorReflection>();
        }

        private static void ConfigureLight()
        {
            Light light = Object.FindObjectOfType<Light>();
            if (light == null)
            {
                var go = new GameObject("Directional Light");
                light = go.AddComponent<Light>();
                light.type = LightType.Directional;
            }

            light.transform.rotation = Quaternion.Euler(48f, 150f, 0f);
            light.color = new Color(1f, 0.97f, 0.92f);
            light.intensity = 1.1f;
            light.shadows = LightShadows.Soft;

            Camera camera = Camera.main;
            if (camera != null)
            {
                camera.transform.SetPositionAndRotation(new Vector3(0f, 2f, -9f), Quaternion.Euler(8f, 0f, 0f));
                camera.farClipPlane = 200f;
            }
        }

        private static void BuildWorld()
        {
            var world = new GameObject("VRCWorld");

            var spawn = new GameObject("Spawn");
            spawn.transform.SetParent(world.transform, false);
            spawn.transform.SetPositionAndRotation(SpawnPosition, Quaternion.identity);

            var descriptor = world.AddComponent<VRCSceneDescriptor>();
            descriptor.spawns = new[] { spawn.transform };
            descriptor.RespawnHeightY = -50f;

            if (Camera.main != null)
            {
                descriptor.ReferenceCamera = Camera.main.gameObject;
            }
        }

        private static string Summarise()
        {
            var text = new StringBuilder();
            text.AppendLine($"[SabaProps Liquid] サンプルシーンを {ScenePath} に作成しました。");
            text.AppendLine("正面の鏡で、自分のアバターへの付着を確認できます。");
            text.AppendLine($"・{PoolName}（左）: スロープから水に入ると、浸かった高さまで濡れます。出ると上から乾いていきます。");
            text.AppendLine($"・{MudBogName}（右）: 泥の表面より床が低く、足が沈みます。沈んだ高さまで泥が付き、水でしか落ちません。");
            text.AppendLine($"・{ShowerName}（奥）: Interact で放水を切り替えます。当たった所から下が濡れ、泥が洗い流されます。");
            text.AppendLine($"・{FaucetName}（右手前）: 手を差し出すと濡れます。Interact で開閉します。");
            text.AppendLine($"・{WaterGunsName}（左手前）: 持って使用ボタンを押している間、放水します。命中は全員に同期されます。");
            return text.ToString();
        }
    }
}
