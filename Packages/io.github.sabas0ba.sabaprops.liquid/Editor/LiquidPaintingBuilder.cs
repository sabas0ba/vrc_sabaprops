using System.Collections.Generic;
using UdonSharp;
using UdonSharpEditor;
using UnityEditor;
using UnityEngine;
using VRC.SDK3.Components;

namespace SabaProps.Liquid.Editors
{
    /// <summary>
    /// Builds what painting the world needs: Surface Canvases (a Body Canvas
    /// fixed to a box of the world, drawn on the walls and floors inside it),
    /// the pen, stamp and eraser, and the registration that ties them to the
    /// pool and to the paint log.
    /// </summary>
    public static class LiquidPaintingBuilder
    {
        public const string PaintLogName = "Liquid Paint Log";

        /// <summary>How far the canvas box reaches past the room it covers, so the walls' faces are well inside it.</summary>
        public const float SurfaceMargin = 0.2f;

        // VRChat's Default and Environment layers: what a Surface Canvas draws on, and what liquid stops at.
        private const int SurfaceLayers = (1 << 0) | (1 << 11);

        /// <summary>The projector material of the numbered Surface Canvas. Each canvas needs its own.</summary>
        public static string SurfaceProjectorMaterialPath(int index)
        {
            return LiquidAssets.MaterialFolder + "/LiquidSurfaceProjector_" + index.ToString("00") + ".mat";
        }

        [MenuItem("GameObject/SabaProps/Liquid/Surface Canvas (Walls and Floor)", false, 22)]
        public static void CreateSurfaceFromMenu(MenuCommand command)
        {
            LiquidCanvasPool pool = LiquidSourceBuilder.FindOrCreatePool();
            int index = 0;
            while (AssetDatabase.LoadAssetAtPath<Material>(SurfaceProjectorMaterialPath(index)) != null)
            {
                index++;
            }

            LiquidBodyCanvas canvas = CreateSurfaceCanvas(null, "Liquid Surface Canvas", new Vector3(0f, 1.5f, 0f),
                new Vector3(4f, 3f, 4f), LiquidSurfaceBuilder.GetSurface(LiquidSurfaceBuilder.PaintedWallName),
                LiquidSampleScene.CanvasUpdateMaterial(), index, 256);
            GameObject root = canvas.transform.parent.gameObject;
            GameObjectUtility.SetParentAndAlign(root, command.context as GameObject);
            Undo.RegisterCreatedObjectUndo(root, "Create Liquid Surface Canvas");
            Register(pool);
            Selection.activeGameObject = root;
        }

        [MenuItem("Tools/SabaProps/Liquid/Register Paint Tools and Surfaces", false, 21)]
        public static void RegisterFromMenu()
        {
            LiquidPaintLog log = Register(LiquidSourceBuilder.FindOrCreatePool());
            Debug.Log(log == null
                ? "[SabaProps Liquid] シーンに Surface Canvas も描画ツールもありません。"
                : "[SabaProps Liquid] Surface Canvas と描画ツールをプールと描画の履歴に登録しました。");
        }

        /// <summary>
        /// A Surface Canvas covering a box of the world: <paramref name="centre"/>
        /// and <paramref name="size"/> are the room's, and the canvas box reaches
        /// <see cref="SurfaceMargin"/> past it on every side.
        /// <para>
        /// The projector lands on the Default and Environment layers only, so it
        /// redraws the room and not the players or pickups in it. The whole box
        /// shares one receiving surface.
        /// </para>
        /// <para>
        /// Each texel of a face covers (box side / <paramref name="faceResolution"/>);
        /// the canvas holds eight render textures of 3 x 2 faces, about
        /// 240 x faceResolution^2 bytes in all.
        /// </para>
        /// </summary>
        public static LiquidBodyCanvas CreateSurfaceCanvas(Transform parent, string name, Vector3 centre, Vector3 size,
            LiquidSurfaceProfile surface, Material update, int index, int faceResolution)
        {
            var root = new GameObject(name);
            if (parent != null)
            {
                root.transform.SetParent(parent, false);
            }

            root.transform.position = centre;

            var canvasObject = new GameObject("Canvas");
            canvasObject.transform.SetParent(root.transform, false);
            LiquidBodyCanvas canvas = canvasObject.AddUdonSharpComponent<LiquidBodyCanvas>();

            Vector3 half = size * 0.5f + Vector3.one * SurfaceMargin;
            Material projectorMaterial = LiquidAssets.CreateOrLoadMaterial(
                SurfaceProjectorMaterialPath(index), LiquidAssets.BodyProjectorShader);
            // The box is rooms wide; the body's fade of a few centimetres would be decimetres here.
            projectorMaterial.SetFloat("_EdgeFade", 0.01f);
            EditorUtility.SetDirty(projectorMaterial);

            var projectorObject = new GameObject("Projector");
            projectorObject.transform.SetParent(canvasObject.transform, false);
            LiquidCanvasPoolBuilder.ConfigureProjector(projectorObject, half, projectorMaterial, 0, ~SurfaceLayers);
            projectorObject.SetActive(false);

            canvas.canvasRoot = canvasObject.transform;
            canvas.projectorObject = projectorObject;
            canvas.projectorMaterial = projectorMaterial;
            canvas.updateMaterial = update;
            canvas.anchor = root.transform;
            canvas.worldSurface = true;
            canvas.halfExtents = half;
            canvas.centerOffset = Vector3.zero;
            canvas.faceResolution = faceResolution;
            canvas.bodySurface = surface;
            canvas.estimateRegions = false;
            // Liquid has metres of wall to run down, and paint takes minutes to dry.
            canvas.flowSpeed = 0.12f;
            canvas.surfaceIdleSeconds = 300f;
            UdonSharpEditorUtility.CopyProxyToUdon(canvas);
            EditorUtility.SetDirty(canvas);
            return canvas;
        }

        /// <summary>
        /// A paint tool to carry: a synced pickup with the tool on a child, so
        /// its events do not share a GameObject with VRCObjectSync. The pickup's
        /// use button, release and drop reach the tool through a relay on the root.
        /// <para>
        /// The tip is coloured like the ink (glowing for fluorescent and luminous
        /// ink), and a disc the size of the mark shows where it would land, to
        /// the holder only.
        /// </para>
        /// </summary>
        public static GameObject CreatePaintTool(string name, int mode, LiquidProfile profile, int shape, float radius,
            string materialFolder)
        {
            var root = new GameObject(name);
            Material body = LiquidAssets.CreateOrLoadSurfaceMaterial(materialFolder + "/PaintToolBody.mat",
                new Color(0.2f, 0.21f, 0.24f), 0.5f);
            Material rubber = LiquidAssets.CreateOrLoadSurfaceMaterial(materialFolder + "/PaintToolRubber.mat",
                new Color(0.93f, 0.9f, 0.86f), 0.2f);
            Material ink = profile != null ? LiquidPrefabBuilder.TankMaterial(profile, materialFolder) : rubber;
            float reach;

            if (mode == LiquidPaintTool.ModePen)
            {
                LiquidPrefabBuilder.Part(root.transform, "Barrel", PrimitiveType.Cylinder, new Vector3(0f, 0f, 0f),
                    new Vector3(0.024f, 0.07f, 0.024f), Quaternion.Euler(90f, 0f, 0f), body, false);
                LiquidPrefabBuilder.Part(root.transform, "Cap", PrimitiveType.Cylinder, new Vector3(0f, 0f, -0.06f),
                    new Vector3(0.027f, 0.012f, 0.027f), Quaternion.Euler(90f, 0f, 0f), ink, false);
                LiquidPrefabBuilder.Part(root.transform, "Nib", PrimitiveType.Sphere, new Vector3(0f, 0f, 0.08f),
                    new Vector3(0.022f, 0.022f, 0.04f), Quaternion.identity, ink, false);
                reach = 0.1f;
            }
            else if (mode == LiquidPaintTool.ModeStamp)
            {
                LiquidPrefabBuilder.Part(root.transform, "Handle", PrimitiveType.Cylinder, new Vector3(0f, 0f, -0.02f),
                    new Vector3(0.032f, 0.045f, 0.032f), Quaternion.Euler(90f, 0f, 0f), body, false);
                LiquidPrefabBuilder.Part(root.transform, "Head", PrimitiveType.Cube, new Vector3(0f, 0f, 0.04f),
                    new Vector3(0.09f, 0.09f, 0.025f), Quaternion.identity, body, false);
                LiquidPrefabBuilder.Part(root.transform, "Pad", PrimitiveType.Cube, new Vector3(0f, 0f, 0.056f),
                    new Vector3(0.08f, 0.08f, 0.008f), Quaternion.identity, ink, false);
                reach = 0.07f;
            }
            else
            {
                LiquidPrefabBuilder.Part(root.transform, "Block", PrimitiveType.Cube, new Vector3(0f, 0f, 0f),
                    new Vector3(0.05f, 0.035f, 0.12f), Quaternion.identity, rubber, false);
                LiquidPrefabBuilder.Part(root.transform, "Sleeve", PrimitiveType.Cube, new Vector3(0f, 0f, -0.02f),
                    new Vector3(0.053f, 0.038f, 0.06f), Quaternion.identity, body, false);
                reach = 0.07f;
            }

            var collider = root.AddComponent<CapsuleCollider>();
            collider.direction = 2;
            collider.center = new Vector3(0f, 0f, 0.01f);
            collider.height = 0.2f;
            collider.radius = 0.04f;

            var tip = new GameObject("Tip");
            tip.transform.SetParent(root.transform, false);
            tip.transform.localPosition = new Vector3(0f, 0f, reach);

            // The mark's footprint, shown where the tool points. A flattened sphere, so it needs no mesh of its own.
            Color markColour = profile != null ? profile.pigmentColor : Color.white;
            string markName = profile != null ? profile.gameObject.name.Replace(" ", "") : "Eraser";
            Material markMaterial = LiquidAssets.CreateOrLoadTransparentMaterial(materialFolder + "/Mark_" + markName + ".mat",
                new Color(markColour.r, markColour.g, markColour.b, 0.55f), 0.2f);
            GameObject marker = LiquidPrefabBuilder.Part(root.transform, "Marker", PrimitiveType.Sphere, new Vector3(0f, 0f, reach),
                new Vector3(radius * 2f, radius * 2f, 0.003f), Quaternion.identity, markMaterial, false);
            var markRenderer = marker.GetComponent<Renderer>();
            markRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            markRenderer.receiveShadows = false;
            marker.SetActive(false);

            var toolObject = new GameObject("Tool");
            toolObject.transform.SetParent(root.transform, false);
            LiquidPaintTool tool = toolObject.AddUdonSharpComponent<LiquidPaintTool>();
            tool.profile = profile;
            tool.tip = tip.transform;
            tool.marker = marker.transform;
            tool.mode = mode;
            tool.shape = shape;
            tool.radius = radius;

            string useText = mode == LiquidPaintTool.ModePen ? "Draw" : mode == LiquidPaintTool.ModeStamp ? "Stamp" : "Erase";
            LiquidPrefabBuilder.MakePickup(root, useText, tool, nameof(LiquidPaintTool.Trigger));
            tool.pickup = root.GetComponent<VRCPickup>();
            UdonSharpEditorUtility.CopyProxyToUdon(tool);

            LiquidButton relay = root.GetComponent<LiquidButton>();
            relay.useUpEventName = nameof(LiquidPaintTool.Release);
            relay.dropEventName = nameof(LiquidPaintTool.Release);
            UdonSharpEditorUtility.CopyProxyToUdon(relay);

            LiquidPrefabBuilder.SetLayer(root, 13);
            return root;
        }

        /// <summary>
        /// Ties the scene's Surface Canvases and paint tools to the pool: the
        /// pool learns the canvases, every tool gets its number, and a paint log
        /// (created when missing) lists the tools in that order, so a drawing
        /// recorded on one client replays with the same tool on another.
        /// <para>
        /// Run again after adding or removing a canvas or a tool. Returns null,
        /// and changes nothing, when the scene has neither.
        /// </para>
        /// </summary>
        public static LiquidPaintLog Register(LiquidCanvasPool pool)
        {
            var surfaces = new List<LiquidBodyCanvas>();
            foreach (LiquidBodyCanvas canvas in UnityEngine.Object.FindObjectsOfType<LiquidBodyCanvas>(true))
            {
                if (canvas.worldSurface)
                {
                    surfaces.Add(canvas);
                }
            }

            var tools = new List<LiquidPaintTool>(UnityEngine.Object.FindObjectsOfType<LiquidPaintTool>(true));
            if (pool == null || (surfaces.Count == 0 && tools.Count == 0))
            {
                return null;
            }

            // FindObjectsOfType has no stable order; the hierarchy path is the same on every machine.
            surfaces.Sort((a, b) => string.CompareOrdinal(HierarchyPath(a.transform), HierarchyPath(b.transform)));
            tools.Sort((a, b) => string.CompareOrdinal(HierarchyPath(a.transform), HierarchyPath(b.transform)));
            if (surfaces.Count > LiquidPaintLog.MaxSurfaces || tools.Count > LiquidPaintLog.MaxTools)
            {
                Debug.LogWarning("[SabaProps Liquid] 描画の履歴に載せられる数（面 " + LiquidPaintLog.MaxSurfaces + "、ツール "
                    + LiquidPaintLog.MaxTools + "）を超えています。超えた分への描画は、後から入った人に伝わりません。");
            }

            LiquidPaintLog log = UnityEngine.Object.FindObjectOfType<LiquidPaintLog>(true);
            if (log == null)
            {
                var logObject = new GameObject(PaintLogName);
                logObject.transform.SetParent(pool.transform, false);
                log = logObject.AddUdonSharpComponent<LiquidPaintLog>();
                Undo.RegisterCreatedObjectUndo(logObject, "Create Liquid Paint Log");
            }

            for (int i = 0; i < tools.Count; i++)
            {
                tools[i].toolIndex = i < LiquidPaintLog.MaxTools ? i : -1;
                tools[i].pool = pool;
                Apply(tools[i]);
            }

            log.pool = pool;
            log.tools = tools.ToArray();
            Apply(log);

            pool.surfaces = surfaces.ToArray();
            pool.paintLog = log;
            Apply(pool);
            return log;
        }

        private static void Apply(UdonSharpBehaviour behaviour)
        {
            UdonSharpEditorUtility.CopyProxyToUdon(behaviour);
            EditorUtility.SetDirty(behaviour);
            UnityEngine.Object backing = UdonSharpEditorUtility.GetBackingUdonBehaviour(behaviour);
            if (backing != null)
            {
                EditorUtility.SetDirty(backing);
                if (PrefabUtility.IsPartOfPrefabInstance(backing))
                {
                    PrefabUtility.RecordPrefabInstancePropertyModifications(backing);
                }
            }
        }

        private static string HierarchyPath(Transform transform)
        {
            string path = transform.GetSiblingIndex().ToString("0000") + ":" + transform.name;
            for (Transform parent = transform.parent; parent != null; parent = parent.parent)
            {
                path = parent.GetSiblingIndex().ToString("0000") + ":" + parent.name + "/" + path;
            }

            return path;
        }
    }
}
