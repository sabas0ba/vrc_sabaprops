using UdonSharpEditor;
using UnityEditor;
using UnityEngine;

namespace SabaProps.Liquid.Editors
{
    /// <summary>
    /// Builds a Canvas Pool: the pool behaviour, its Body Canvases, and one
    /// projector per canvas.
    /// <para>
    /// The projector settings are fixed here because Udon has no access to the
    /// Projector component at runtime. Everything the runtime needs to change
    /// goes through the projector's material instead.
    /// </para>
    /// </summary>
    public static class LiquidCanvasPoolBuilder
    {
        public const int DefaultCanvasCount = 12;

        // VRChat's built-in layers. Avatars render on Player (remote) and
        // PlayerLocal (the local avatar); MirrorReflection carries the local
        // avatar's copy that mirrors see.
        public const int PlayerLayer = 9;
        public const int PlayerLocalLayer = 10;
        public const int MirrorReflectionLayer = 18;

        /// <summary>Every layer except the avatar ones.</summary>
        public const int IgnoreNonAvatarLayers =
            ~((1 << PlayerLayer) | (1 << PlayerLocalLayer) | (1 << MirrorReflectionLayer));

        [MenuItem("GameObject/SabaProps/Liquid/Canvas Pool", false, 20)]
        public static void CreateFromMenu(MenuCommand command)
        {
            GameObject root = CreateCanvasPool(DefaultCanvasCount);
            if (root == null)
            {
                return;
            }

            GameObjectUtility.SetParentAndAlign(root, command.context as GameObject);
            Undo.RegisterCreatedObjectUndo(root, "Create Liquid Canvas Pool");
            Selection.activeGameObject = root;
        }

        /// <summary>
        /// Creates the pool in the open scene. Returns null when the shaders are
        /// missing, which only happens when the package failed to import.
        /// </summary>
        public static GameObject CreateCanvasPool(int canvasCount)
        {
            Material update = LiquidAssets.CreateOrLoadMaterial(
                LiquidAssets.CanvasUpdateMaterialPath, LiquidAssets.CanvasUpdateShader);
            if (update == null)
            {
                return null;
            }

            var root = new GameObject("Liquid Canvas Pool");
            LiquidCanvasPool pool = root.AddUdonSharpComponent<LiquidCanvasPool>();
            var canvases = new LiquidBodyCanvas[canvasCount];

            for (int i = 0; i < canvasCount; i++)
            {
                Material projectorMaterial = LiquidAssets.CreateOrLoadMaterial(
                    LiquidAssets.BodyProjectorMaterialPath(i), LiquidAssets.BodyProjectorShader);
                if (projectorMaterial == null)
                {
                    Object.DestroyImmediate(root);
                    return null;
                }

                canvases[i] = CreateBodyCanvas(root.transform, i, projectorMaterial, update);
            }

            pool.canvases = canvases;
            UdonSharpEditorUtility.CopyProxyToUdon(pool);
            EditorUtility.SetDirty(pool);
            AssetDatabase.SaveAssets();
            return root;
        }

        private static LiquidBodyCanvas CreateBodyCanvas(Transform parent, int index, Material projectorMaterial, Material update)
        {
            var canvasObject = new GameObject("Body Canvas " + index.ToString("00"));
            canvasObject.transform.SetParent(parent, false);

            LiquidBodyCanvas canvas = canvasObject.AddUdonSharpComponent<LiquidBodyCanvas>();

            var projectorObject = new GameObject("Projector");
            projectorObject.transform.SetParent(canvasObject.transform, false);
            ConfigureProjector(projectorObject, canvas.halfExtents, projectorMaterial);
            projectorObject.SetActive(false);

            canvas.canvasRoot = canvasObject.transform;
            canvas.projectorObject = projectorObject;
            canvas.projectorMaterial = projectorMaterial;
            canvas.updateMaterial = update;
            UdonSharpEditorUtility.CopyProxyToUdon(canvas);
            EditorUtility.SetDirty(canvas);
            return canvas;
        }

        /// <summary>
        /// Sizes an orthographic projector to exactly cover the canvas box.
        /// <para>
        /// A projector looks down its local +Z, from the near plane to the far
        /// plane. Moving it back by the box's half depth and setting the far
        /// plane at the full depth makes the frustum the box itself. The shader
        /// clips to the box again, so the frustum only has to be no smaller.
        /// </para>
        /// </summary>
        public static void ConfigureProjector(GameObject projectorObject, Vector3 halfExtents, Material material)
        {
            projectorObject.transform.localPosition = new Vector3(0f, 0f, -halfExtents.z);
            projectorObject.transform.localRotation = Quaternion.identity;

            var projector = projectorObject.GetComponent<Projector>();
            if (projector == null)
            {
                projector = projectorObject.AddComponent<Projector>();
            }

            projector.orthographic = true;
            projector.orthographicSize = halfExtents.y;
            projector.aspectRatio = halfExtents.x / Mathf.Max(halfExtents.y, 1e-4f);
            projector.nearClipPlane = 0.001f;
            projector.farClipPlane = halfExtents.z * 2f;
            projector.ignoreLayers = IgnoreNonAvatarLayers;
            projector.material = material;
        }
    }
}
