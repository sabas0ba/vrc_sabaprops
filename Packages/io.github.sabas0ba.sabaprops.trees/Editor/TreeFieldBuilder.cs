using System.Collections.Generic;
using System.Diagnostics;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace SabaProps.Trees.Editors
{
    /// <summary>
    /// Bakes a TreeField into ordinary LODGroups at edit time. Generated scene
    /// objects have no runtime dependency on this editor assembly.
    /// </summary>
    public static class TreeFieldBuilder
    {
        public static TreeBuildStats Build(
            TreeField field,
            bool recordUndo = true)
        {
            if (field == null)
            {
                return null;
            }

            var stopwatch = Stopwatch.StartNew();
            int undoGroup = recordUndo ? Undo.GetCurrentGroup() : -1;
            if (recordUndo)
            {
                Undo.SetCurrentGroupName("Build Tree Field");
            }

            try
            {
                EditorUtility.DisplayProgressBar(
                    "SabaProps Trees", "Preparing species...", 0.05f);

                List<TreeSpecies> species =
                    TreeScatterer.CollectValidSpecies(field, out string speciesError);
                if (species.Count == 0)
                {
                    Debug.LogWarning($"[SabaProps Trees] {speciesError}", field);
                    return null;
                }

                List<TreeInstance> instances =
                    TreeScatterer.Scatter(field, out string scatterError);
                if (!string.IsNullOrEmpty(scatterError))
                {
                    Debug.LogWarning($"[SabaProps Trees] {scatterError}", field);
                }
                if (instances.Count == 0)
                {
                    return null;
                }

                EditorUtility.DisplayProgressBar(
                    "SabaProps Trees", "Generating shared LOD meshes...", 0.15f);
                TreeAssetLibrary.EnsureFolder(TreeAssetLibrary.GeneratedFolder);
                Material defaultMaterial = null;
                foreach (TreeSpecies entry in species)
                {
                    if (entry.material != null)
                    {
                        continue;
                    }
                    defaultMaterial = defaultMaterial ??
                        TreeAssetLibrary.CreateOrLoadDefaultMaterial();
                    entry.material = defaultMaterial;
                    EditorUtility.SetDirty(entry);
                }
                try
                {
                    AssetDatabase.StartAssetEditing();
                    foreach (TreeSpecies entry in species)
                    {
                        Mesh[] meshes = TreeAssetLibrary.WriteLodMeshes(entry);
                        if (meshes.Length != 3 || entry.material == null)
                        {
                            Debug.LogError(
                                $"[SabaProps Trees] Failed to generate LOD meshes for '{entry.name}'.",
                                entry);
                            return null;
                        }
                    }
                }
                finally
                {
                    AssetDatabase.StopAssetEditing();
                }

                Clear(field, recordUndo);

                var generated = new GameObject(TreeField.GeneratedRootName);
                generated.transform.SetParent(field.transform, false);
                generated.transform.localPosition = Vector3.zero;
                generated.transform.localRotation = Quaternion.identity;
                generated.transform.localScale = Vector3.one;

                var stats = new TreeBuildStats
                {
                    instanceCount = instances.Count,
                };
                var lod0VertexCounts = new int[species.Count];
                var lod0TriangleCounts = new int[species.Count];
                for (int i = 0; i < species.Count; i++)
                {
                    lod0VertexCounts[i] = species[i].lod0Mesh.vertexCount;
                    lod0TriangleCounts[i] =
                        (int)(species[i].lod0Mesh.GetIndexCount(0) / 3);
                }

                for (int i = 0; i < instances.Count; i++)
                {
                    if ((i & 31) == 0)
                    {
                        EditorUtility.DisplayProgressBar(
                            "SabaProps Trees",
                            $"Creating LODGroups... ({i + 1}/{instances.Count})",
                            0.25f + 0.7f * (i / (float)instances.Count));
                    }

                    TreeInstance instance = instances[i];
                    TreeSpecies entry = species[instance.SpeciesIndex];
                    GameObject tree = TreeAssetLibrary.CreateLodGroupInstance(
                        entry, generated.transform);
                    if (tree == null)
                    {
                        Object.DestroyImmediate(generated);
                        Debug.LogError(
                            $"[SabaProps Trees] Failed to create an LODGroup for '{entry.name}'.",
                            entry);
                        return null;
                    }

                    tree.name = $"{entry.name} Tree {i:D4}";
                    ApplyLocalMatrix(
                        tree.transform,
                        generated.transform.worldToLocalMatrix * instance.ToMatrix());

                    stats.rendererCount += 3;
                    stats.lod0VertexCount +=
                        lod0VertexCounts[instance.SpeciesIndex];
                    stats.lod0TriangleCount +=
                        lod0TriangleCounts[instance.SpeciesIndex];
                }

                field.generatedRoot = generated.transform;
                stopwatch.Stop();
                stats.buildSeconds = stopwatch.ElapsedMilliseconds / 1000f;
                field.lastBuildStats = stats;

                if (recordUndo)
                {
                    Undo.RegisterCreatedObjectUndo(generated, "Build Tree Field");
                }
                EditorUtility.SetDirty(field);
                MarkSceneDirty(field);
                if (recordUndo)
                {
                    Undo.CollapseUndoOperations(undoGroup);
                }
                return stats;
            }
            finally
            {
                EditorUtility.ClearProgressBar();
                AssetDatabase.SaveAssets();
            }
        }

        public static void Clear(TreeField field, bool recordUndo = true)
        {
            if (field == null)
            {
                return;
            }

            Transform existing = field.generatedRoot;
            if (existing == null || existing.parent != field.transform)
            {
                existing = field.transform.Find(TreeField.GeneratedRootName);
            }
            if (existing != null)
            {
                if (recordUndo)
                {
                    Undo.DestroyObjectImmediate(existing.gameObject);
                }
                else
                {
                    Object.DestroyImmediate(existing.gameObject);
                }
            }

            field.generatedRoot = null;
            field.lastBuildStats = null;
            EditorUtility.SetDirty(field);
            MarkSceneDirty(field);
        }

        private static void ApplyLocalMatrix(Transform target, Matrix4x4 local)
        {
            target.localPosition = local.GetPosition();
            target.localRotation = local.rotation;
            target.localScale = local.lossyScale;
        }

        private static void MarkSceneDirty(TreeField field)
        {
            if (field.gameObject.scene.IsValid())
            {
                EditorSceneManager.MarkSceneDirty(field.gameObject.scene);
            }
        }
    }
}
