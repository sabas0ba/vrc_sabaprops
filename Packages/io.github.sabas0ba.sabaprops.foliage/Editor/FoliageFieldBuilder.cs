using System.Collections.Generic;
using System.Diagnostics;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Debug = UnityEngine.Debug;

namespace SabaProps.Foliage.Editors
{
    /// <summary>
    /// Turns a <see cref="FoliageField"/> into an actual renderer hierarchy.
    /// Everything happens at edit time; the result is plain MeshRenderers that
    /// need no scripts to run inside VRChat.
    /// </summary>
    public static class FoliageFieldBuilder
    {
        /// <summary>
        /// Merged chunks above this vertex count are still built, but warned
        /// about: past roughly this size a single chunk stops being a useful
        /// culling unit.
        /// </summary>
        private const int MergedChunkVertexWarning = 250000;

        /// <summary>
        /// Beyond this many individual renderers, the transform and culling cost
        /// usually outweighs what GPU instancing saves.
        /// </summary>
        private const int InstancedRendererWarning = 10000;

        public static FoliageBuildStats Build(
            FoliageField field,
            bool recordUndo = true)
        {
            if (field == null)
            {
                return null;
            }

            var stopwatch = Stopwatch.StartNew();

            // Clearing and rebuilding are separate operations internally; collapse
            // them so a single Ctrl+Z undoes the whole build.
            int undoGroup = recordUndo ? Undo.GetCurrentGroup() : -1;
            if (recordUndo)
            {
                Undo.SetCurrentGroupName("Build Foliage");
            }

            try
            {
                EditorUtility.DisplayProgressBar("SabaProps Foliage", "配置を計算中...", 0.05f);

                List<FoliageSpecies> species = FoliageScatterer.CollectValidSpecies(field, out string speciesError);
                if (species.Count == 0)
                {
                    Debug.LogWarning($"[SabaProps Foliage] {speciesError}", field);
                    return null;
                }

                List<FoliageInstance> instances = FoliageScatterer.Scatter(field, out string scatterError);
                if (!string.IsNullOrEmpty(scatterError))
                {
                    Debug.LogWarning($"[SabaProps Foliage] {scatterError}", field);
                }

                if (instances.Count == 0)
                {
                    return null;
                }

                EditorUtility.DisplayProgressBar("SabaProps Foliage", "メッシュを生成中...", 0.2f);

                // Folders must exist before the batch: inside StartAssetEditing
                // the database does not see freshly created directories, so
                // CreateAsset would fail on a project that has never built.
                FoliageAssetLibrary.EnsureFolder(FoliageAssetLibrary.GeneratedMeshFolder);

                var meshes = new Mesh[species.Count];
                try
                {
                    AssetDatabase.StartAssetEditing();

                    for (int i = 0; i < species.Count; i++)
                    {
                        meshes[i] = FoliageAssetLibrary.WriteSpeciesMesh(species[i]);
                        WarnIfInstancingDisabled(species[i]);
                    }
                }
                finally
                {
                    AssetDatabase.StopAssetEditing();
                }

                for (int i = 0; i < meshes.Length; i++)
                {
                    if (meshes[i] == null)
                    {
                        Debug.LogError($"[SabaProps Foliage] Species '{species[i].name}' のメッシュ生成に失敗しました。", species[i]);
                        return null;
                    }
                }

                EditorUtility.DisplayProgressBar("SabaProps Foliage", "既存の生成物を削除中...", 0.3f);
                Clear(field, recordUndo);

                var root = new GameObject(FoliageField.GeneratedRootName);
                root.transform.SetParent(field.transform, false);
                root.transform.localPosition = Vector3.zero;
                root.transform.localRotation = Quaternion.identity;
                root.transform.localScale = Vector3.one;

                Dictionary<Vector2Int, List<int>> chunks = GroupIntoChunks(field, instances);

                var stats = new FoliageBuildStats
                {
                    instanceCount = instances.Count,
                    chunkCount = chunks.Count,
                    mode = field.outputMode,
                };

                if (field.outputMode == FoliageOutputMode.MergedChunks)
                {
                    BuildMerged(field, root.transform, instances, species, meshes, chunks, stats);
                }
                else
                {
                    BuildInstanced(field, root.transform, instances, species, meshes, chunks, stats);
                }

                field.generatedRoot = root.transform;

                stopwatch.Stop();
                stats.buildSeconds = stopwatch.ElapsedMilliseconds / 1000f;
                field.lastBuildStats = stats;

                if (recordUndo)
                {
                    Undo.RegisterCreatedObjectUndo(root, "Build Foliage");
                }
                EditorUtility.SetDirty(field);
                MarkSceneDirty(field);

                if (recordUndo)
                {
                    Undo.CollapseUndoOperations(undoGroup);
                }

                ReportScaleWarnings(stats);
                return stats;
            }
            finally
            {
                EditorUtility.ClearProgressBar();
                AssetDatabase.SaveAssets();
            }
        }

        /// <summary>
        /// Removes the generated hierarchy and any merged mesh assets that
        /// belonged to it. Objects a user parented under the field by hand are
        /// left alone: only the container this builder created is touched.
        /// </summary>
        public static void Clear(FoliageField field, bool recordUndo = true)
        {
            if (field == null)
            {
                return;
            }

            Transform existing = field.generatedRoot;
            if (existing == null || existing.parent != field.transform)
            {
                existing = field.transform.Find(FoliageField.GeneratedRootName);
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

            FoliageAssetLibrary.DeleteMergedFolder(field);

            EditorUtility.SetDirty(field);
            MarkSceneDirty(field);
        }

        // ------------------------------------------------------------------

        private static Dictionary<Vector2Int, List<int>> GroupIntoChunks(
            FoliageField field, List<FoliageInstance> instances)
        {
            var chunks = new Dictionary<Vector2Int, List<int>>();
            float chunkSize = Mathf.Max(1f, field.chunkSize);
            Transform fieldTransform = field.transform;

            for (int i = 0; i < instances.Count; i++)
            {
                Vector3 local = fieldTransform.InverseTransformPoint(instances[i].Position);
                var coordinate = new Vector2Int(
                    Mathf.FloorToInt(local.x / chunkSize),
                    Mathf.FloorToInt(local.z / chunkSize));

                if (!chunks.TryGetValue(coordinate, out List<int> bucket))
                {
                    bucket = new List<int>();
                    chunks[coordinate] = bucket;
                }

                bucket.Add(i);
            }

            return chunks;
        }

        private static Transform CreateChunkObject(
            FoliageField field, Transform root, Vector2Int coordinate, int instanceCount)
        {
            float chunkSize = Mathf.Max(1f, field.chunkSize);

            var chunkObject = new GameObject($"Chunk_{coordinate.x}_{coordinate.y}");
            chunkObject.transform.SetParent(root, false);
            chunkObject.transform.localRotation = Quaternion.identity;
            chunkObject.transform.localScale = Vector3.one;
            chunkObject.transform.localPosition = new Vector3(
                (coordinate.x + 0.5f) * chunkSize,
                0f,
                (coordinate.y + 0.5f) * chunkSize);

            var marker = chunkObject.AddComponent<FoliageChunk>();
            marker.coordinate = coordinate;
            marker.instanceCount = instanceCount;
            marker.ownerBuildId = field.BuildId;

            return chunkObject.transform;
        }

        private static void BuildInstanced(
            FoliageField field, Transform root,
            List<FoliageInstance> instances, List<FoliageSpecies> species, Mesh[] meshes,
            Dictionary<Vector2Int, List<int>> chunks, FoliageBuildStats stats)
        {
            // Mesh.triangles allocates a fresh array on every access, so the
            // per-instance loop below must never touch it.
            var triangleCounts = new int[meshes.Length];
            for (int i = 0; i < meshes.Length; i++)
            {
                triangleCounts[i] = (int)(meshes[i].GetIndexCount(0) / 3);
            }

            int processedChunks = 0;

            foreach (KeyValuePair<Vector2Int, List<int>> chunk in chunks)
            {
                EditorUtility.DisplayProgressBar(
                    "SabaProps Foliage",
                    $"チャンクを生成中... ({processedChunks + 1}/{chunks.Count})",
                    0.35f + 0.6f * (processedChunks / (float)chunks.Count));

                Transform chunkTransform = CreateChunkObject(field, root, chunk.Key, chunk.Value.Count);
                Matrix4x4 worldToChunk = chunkTransform.worldToLocalMatrix;

                var chunkMarker = chunkTransform.GetComponent<FoliageChunk>();
                int chunkTriangles = 0;

                foreach (int instanceIndex in chunk.Value)
                {
                    FoliageInstance instance = instances[instanceIndex];
                    FoliageSpecies speciesEntry = species[instance.SpeciesIndex];
                    Mesh mesh = meshes[instance.SpeciesIndex];

                    var instanceObject = new GameObject(speciesEntry.name);
                    Transform t = instanceObject.transform;
                    t.SetParent(chunkTransform, false);

                    ApplyLocalMatrix(t, worldToChunk * instance.ToMatrix());

                    instanceObject.AddComponent<MeshFilter>().sharedMesh = mesh;
                    var renderer = instanceObject.AddComponent<MeshRenderer>();
                    renderer.sharedMaterial = speciesEntry.material;
                    ConfigureRenderer(renderer, speciesEntry);

                    chunkTriangles += triangleCounts[instance.SpeciesIndex];
                    stats.vertexCount += mesh.vertexCount;
                    stats.rendererCount++;
                }

                chunkMarker.triangleCount = chunkTriangles;
                stats.triangleCount += chunkTriangles;
                processedChunks++;
            }
        }

        private static void BuildMerged(
            FoliageField field, Transform root,
            List<FoliageInstance> instances, List<FoliageSpecies> species, Mesh[] meshes,
            Dictionary<Vector2Int, List<int>> chunks, FoliageBuildStats stats)
        {
            var sources = new FoliageSourceMesh[meshes.Length];
            for (int i = 0; i < meshes.Length; i++)
            {
                sources[i] = FoliageSourceMesh.From(meshes[i]);
            }

            string folder = FoliageAssetLibrary.MergedFolderFor(field);
            FoliageAssetLibrary.EnsureFolder(folder);

            int processedChunks = 0;
            var createdMeshes = new List<(Mesh mesh, string path)>();

            foreach (KeyValuePair<Vector2Int, List<int>> chunk in chunks)
            {
                EditorUtility.DisplayProgressBar(
                    "SabaProps Foliage",
                    $"チャンクを結合中... ({processedChunks + 1}/{chunks.Count})",
                    0.35f + 0.5f * (processedChunks / (float)chunks.Count));

                Transform chunkTransform = CreateChunkObject(field, root, chunk.Key, chunk.Value.Count);
                Matrix4x4 worldToChunk = chunkTransform.worldToLocalMatrix;
                var chunkMarker = chunkTransform.GetComponent<FoliageChunk>();

                // One buffer per species: different species use different
                // materials, so they cannot share a submesh-less merged mesh.
                var buffers = new Dictionary<int, FoliageMeshBuffer>();

                foreach (int instanceIndex in chunk.Value)
                {
                    FoliageInstance instance = instances[instanceIndex];

                    if (!buffers.TryGetValue(instance.SpeciesIndex, out FoliageMeshBuffer buffer))
                    {
                        buffer = new FoliageMeshBuffer();
                        buffers[instance.SpeciesIndex] = buffer;
                    }

                    buffer.Append(sources[instance.SpeciesIndex], worldToChunk * instance.ToMatrix());
                }

                int chunkTriangles = 0;

                foreach (KeyValuePair<int, FoliageMeshBuffer> entry in buffers)
                {
                    FoliageSpecies speciesEntry = species[entry.Key];
                    FoliageMeshBuffer buffer = entry.Value;

                    if (buffer.VertexCount > MergedChunkVertexWarning)
                    {
                        Debug.LogWarning(
                            $"[SabaProps Foliage] チャンク {chunk.Key} の '{speciesEntry.name}' が {buffer.VertexCount:N0} 頂点あります。" +
                            "Chunk Size を小さくするとカリングが効きやすくなります。",
                            field);
                    }

                    // Chunk-sized padding: merged bounds already cover the whole
                    // chunk, so wind only needs a modest extra margin.
                    Mesh merged = buffer.ToMesh(
                        $"{speciesEntry.name}_Chunk_{chunk.Key.x}_{chunk.Key.y}",
                        0.5f);

                    string path = $"{folder}/{merged.name}.asset";
                    createdMeshes.Add((merged, path));

                    var meshObject = new GameObject(speciesEntry.name);
                    meshObject.transform.SetParent(chunkTransform, false);

                    meshObject.AddComponent<MeshFilter>().sharedMesh = merged;
                    var renderer = meshObject.AddComponent<MeshRenderer>();
                    renderer.sharedMaterial = speciesEntry.material;
                    ConfigureRenderer(renderer, speciesEntry);

                    // Safe here (and only here): the mesh is already one object,
                    // so occlusion culling has something worth culling.
                    GameObjectUtility.SetStaticEditorFlags(meshObject, StaticEditorFlags.OccludeeStatic);

                    chunkTriangles += buffer.TriangleCount;
                    stats.vertexCount += buffer.VertexCount;
                    stats.rendererCount++;
                }

                chunkMarker.triangleCount = chunkTriangles;
                stats.triangleCount += chunkTriangles;
                processedChunks++;
            }

            EditorUtility.DisplayProgressBar("SabaProps Foliage", "メッシュを保存中...", 0.9f);

            try
            {
                AssetDatabase.StartAssetEditing();
                foreach ((Mesh mesh, string path) in createdMeshes)
                {
                    AssetDatabase.CreateAsset(mesh, path);
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }
        }

        // ------------------------------------------------------------------

        /// <summary>
        /// Decomposes a local matrix onto a transform. Foliage only ever uses
        /// uniform scale, so a straight TRS decomposition is exact.
        /// </summary>
        private static void ApplyLocalMatrix(Transform target, Matrix4x4 local)
        {
            target.localPosition = local.GetPosition();
            target.localRotation = local.rotation;
            target.localScale = local.lossyScale;
        }

        private static void ConfigureRenderer(MeshRenderer renderer, FoliageSpecies species)
        {
            renderer.shadowCastingMode = species.castShadows
                ? ShadowCastingMode.On
                : ShadowCastingMode.Off;

            renderer.receiveShadows = species.receiveShadows;

            // Lightmapping requires batching-static, which would defeat GPU
            // instancing, so lighting comes from probes instead.
            renderer.lightProbeUsage = species.useLightProbes
                ? LightProbeUsage.BlendProbes
                : LightProbeUsage.Off;

            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            renderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
            renderer.allowOcclusionWhenDynamic = true;
        }

        private static void WarnIfInstancingDisabled(FoliageSpecies species)
        {
            if (species.material == null || species.material.enableInstancing)
            {
                return;
            }

            species.material.enableInstancing = true;
            EditorUtility.SetDirty(species.material);

            Debug.LogWarning(
                $"[SabaProps Foliage] Material '{species.material.name}' の GPU Instancing が OFF だったため有効化しました。",
                species.material);
        }

        private static void ReportScaleWarnings(FoliageBuildStats stats)
        {
            if (stats.mode == FoliageOutputMode.GpuInstanced && stats.rendererCount > InstancedRendererWarning)
            {
                Debug.LogWarning(
                    $"[SabaProps Foliage] GPU Instanced モードで {stats.rendererCount:N0} 個の Renderer を生成しました。" +
                    "この規模では Transform とカリングの CPU コストが支配的になるため、Merged Chunks モードを検討してください。");
            }
        }

        private static void MarkSceneDirty(FoliageField field)
        {
            if (field.gameObject.scene.IsValid())
            {
                EditorSceneManager.MarkSceneDirty(field.gameObject.scene);
            }
        }
    }
}
