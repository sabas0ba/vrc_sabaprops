using UdonSharpEditor;
using UnityEditor;
using UnityEngine;

namespace SabaProps.Liquid.Editors
{
    public static class LiquidContactBuilder
    {
        [MenuItem("Tools/SabaProps/Liquid/Enable Contact in Scene")]
        public static void EnableContact()
        {
            LiquidCanvasPool pool = Object.FindObjectOfType<LiquidCanvasPool>();
            if (pool == null)
            {
                GameObject created = LiquidCanvasPoolBuilder.CreateCanvasPool(12);
                if (created == null) return;
                Undo.RegisterCreatedObjectUndo(created, "Create Liquid Canvas Pool");
                pool = created.GetComponent<LiquidCanvasPool>();
            }
            // Existing worlds retain their pool. Add only missing limb slots.
            if (pool.limbs == null || pool.limbs.Length == 0)
            {
                int bodies = pool.canvases == null ? 12 : Mathf.Max(1, pool.canvases.Length);
                pool.limbs = CreateLimbs(pool, Mathf.Min(48, bodies * 4));
            }
            LiquidContact contact = pool.GetComponent<LiquidContact>();
            if (contact == null) contact = pool.gameObject.AddUdonSharpComponent<LiquidContact>();
            contact.pool = pool;
            var tools = Object.FindObjectsOfType<LiquidWipeTool>(true);
            var demos = Object.FindObjectsOfType<LiquidContactDemo>(true);
            pool.contactTools = new LiquidBodyCanvas[tools.Length + demos.Length];
            for (int i = 0; i < tools.Length; i++) pool.contactTools[i] = tools[i].clothCanvas;
            for (int i = 0; i < demos.Length; i++) pool.contactTools[tools.Length + i] = demos[i].padCanvas;
            Material material = LiquidAssets.CreateOrLoadMaterial(
                LiquidAssets.MaterialFolder + "/LiquidContact.mat", "Hidden/SabaProps/Liquid/Contact");
            foreach (LiquidBodyCanvas canvas in Object.FindObjectsOfType<LiquidBodyCanvas>(true))
            {
                Undo.RecordObject(canvas, "Enable Liquid Contact");
                canvas.contactMaterial = material;
                UdonSharpEditorUtility.CopyProxyToUdon(canvas);
                EditorUtility.SetDirty(canvas);
            }
            UdonSharpEditorUtility.CopyProxyToUdon(contact);
            UdonSharpEditorUtility.CopyProxyToUdon(pool);
            EditorUtility.SetDirty(pool);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(pool.gameObject.scene);
            AssetDatabase.SaveAssets();
        }

        private static LiquidBodyCanvas[] CreateLimbs(LiquidCanvasPool pool, int count)
        {
            var result = new LiquidBodyCanvas[count];
            for (int i = 0; i < count; i++)
            {
                var go = new GameObject("Limb Canvas " + i.ToString("00"));
                go.transform.SetParent(pool.transform, false);
                Undo.RegisterCreatedObjectUndo(go, "Create Limb Canvas");
                LiquidBodyCanvas canvas = go.AddUdonSharpComponent<LiquidBodyCanvas>();
                canvas.limbCanvas = true;
                canvas.canvasRoot = go.transform;
                canvas.halfExtents = Vector3.one * 0.18f;
                canvas.centerOffset = Vector3.zero;
                canvas.faceResolution = 64;
                canvas.simulationDistance = 12f;
                canvas.displayDistance = 20f;
                canvas.updateMaterial = LiquidAssets.CreateOrLoadMaterial(LiquidAssets.CanvasUpdateMaterialPath, LiquidAssets.CanvasUpdateShader);
                canvas.contactMaterial = LiquidAssets.CreateOrLoadMaterial(LiquidAssets.MaterialFolder + "/LiquidContact.mat", "Hidden/SabaProps/Liquid/Contact");
                canvas.projectorMaterial = LiquidAssets.CreateOrLoadMaterial(
                    LiquidAssets.MaterialFolder + "/LiquidLimbProjector_" + i.ToString("00") + ".mat", LiquidAssets.BodyProjectorShader);
                canvas.projectorObject = new GameObject("Projector");
                canvas.projectorObject.transform.SetParent(go.transform, false);
                LiquidCanvasPoolBuilder.ConfigureProjector(canvas.projectorObject, canvas.halfExtents, canvas.projectorMaterial);
                canvas.projectorObject.SetActive(false);
                LiquidSurfaceBuilder.AssignAvatarDefaults(canvas);
                result[i] = canvas;
            }
            return result;
        }
    }
}
