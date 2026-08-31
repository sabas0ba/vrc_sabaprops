using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace SabaProps.Foliage.Editors
{
    /// <summary>Collider projection, mesh persistence, and scene binding for surface growth.</summary>
    public static class SurfaceGrowthAuthoringBuilder
    {
        private const string GeneratedFolder =
            FoliageAssetLibrary.GeneratedFolder + "/SurfaceGrowth";

        public static bool Build(SurfaceVine vine, bool recordUndo = true)
        {
            if (vine == null || vine.targetSurface == null)
            {
                Debug.LogWarning(
                    "[SabaProps Foliage] Surface Vine requires a target Collider.",
                    vine);
                return false;
            }

            var projector = new ColliderProjector(
                vine.transform,
                vine.targetSurface,
                vine.additionalSurfaces);
            SurfaceGrowthGraph graph = SurfaceGrowthGraphBuilder.Build(
                vine.growth,
                vine.guidePoints,
                projector.Project);
            if (graph.Nodes.Count < 2)
            {
                Debug.LogWarning(
                    "[SabaProps Foliage] Surface projection produced fewer than two nodes. "
                    + "Move the guide points closer to the Collider or increase Projection Distance.",
                    vine);
                return false;
            }

            Mesh mesh = SurfaceGrowthMeshBuilder.BuildVine(
                graph,
                vine.growth,
                vine.morphology);
            if (recordUndo)
            {
                Undo.RecordObject(vine, "Build Surface Vine");
            }
            vine.generatedGraph = graph;
            vine.generatedMesh = WriteMesh(vine.generatedMesh, mesh, vine.name + "_SurfaceVine");
            Bind(
                vine.gameObject,
                vine.generatedMesh,
                vine.material,
                recordUndo,
                out Material material);
            vine.material = material;
            EditorUtility.SetDirty(vine);
            MarkSceneDirty(vine.gameObject);
            AssetDatabase.SaveAssets();
            return true;
        }

        public static bool Build(RhizomePatch patch, bool recordUndo = true)
        {
            if (patch == null || patch.targetSurface == null)
            {
                Debug.LogWarning(
                    "[SabaProps Foliage] Rhizome Patch requires a target Collider.",
                    patch);
                return false;
            }

            var projector = new ColliderProjector(
                patch.transform,
                patch.targetSurface,
                patch.additionalSurfaces);
            SurfaceGrowthGraph graph = SurfaceGrowthGraphBuilder.Build(
                patch.growth,
                patch.guidePoints,
                projector.Project);
            if (graph.Nodes.Count == 0)
            {
                Debug.LogWarning(
                    "[SabaProps Foliage] Rhizome projection produced no nodes. "
                    + "Move the seed point closer to the Collider or increase Projection Distance.",
                    patch);
                return false;
            }

            Mesh mesh = SurfaceGrowthMeshBuilder.BuildRhizomePatch(
                graph,
                patch.growth,
                patch.morphology);
            if (recordUndo)
            {
                Undo.RecordObject(patch, "Build Rhizome Patch");
            }
            patch.generatedGraph = graph;
            patch.generatedMesh = WriteMesh(
                patch.generatedMesh,
                mesh,
                patch.name + "_RhizomePatch");
            Bind(
                patch.gameObject,
                patch.generatedMesh,
                patch.material,
                recordUndo,
                out Material material);
            patch.material = material;
            EditorUtility.SetDirty(patch);
            MarkSceneDirty(patch.gameObject);
            AssetDatabase.SaveAssets();
            return true;
        }

        public static void Clear(SurfaceVine vine)
        {
            if (vine == null)
            {
                return;
            }
            Undo.RecordObject(vine, "Clear Surface Vine");
            vine.generatedGraph.Clear();
            MeshFilter filter = vine.GetComponent<MeshFilter>();
            if (filter != null)
            {
                Undo.RecordObject(filter, "Clear Surface Vine");
                filter.sharedMesh = null;
            }
            EditorUtility.SetDirty(vine);
            MarkSceneDirty(vine.gameObject);
        }

        public static void Clear(RhizomePatch patch)
        {
            if (patch == null)
            {
                return;
            }
            Undo.RecordObject(patch, "Clear Rhizome Patch");
            patch.generatedGraph.Clear();
            MeshFilter filter = patch.GetComponent<MeshFilter>();
            if (filter != null)
            {
                Undo.RecordObject(filter, "Clear Rhizome Patch");
                filter.sharedMesh = null;
            }
            EditorUtility.SetDirty(patch);
            MarkSceneDirty(patch.gameObject);
        }

        private static Mesh WriteMesh(Mesh existing, Mesh generated, string name)
        {
            if (generated == null)
            {
                return existing;
            }

            string existingPath = existing != null
                ? AssetDatabase.GetAssetPath(existing)
                : string.Empty;
            if (!string.IsNullOrEmpty(existingPath))
            {
                EditorUtility.CopySerialized(generated, existing);
                Object.DestroyImmediate(generated);
                existing.name = Path.GetFileNameWithoutExtension(existingPath);
                EditorUtility.SetDirty(existing);
                return existing;
            }

            FoliageAssetLibrary.EnsureFolder(GeneratedFolder);
            string fileName = SanitizeFileName(name) + ".asset";
            string path = AssetDatabase.GenerateUniqueAssetPath(
                GeneratedFolder + "/" + fileName);
            generated.name = Path.GetFileNameWithoutExtension(path);
            AssetDatabase.CreateAsset(generated, path);
            return generated;
        }

        private static void Bind(
            GameObject gameObject,
            Mesh mesh,
            Material requestedMaterial,
            bool recordUndo,
            out Material material)
        {
            MeshFilter filter = gameObject.GetComponent<MeshFilter>();
            MeshRenderer renderer = gameObject.GetComponent<MeshRenderer>();
            if (recordUndo)
            {
                Undo.RecordObject(filter, "Bind Surface Growth Mesh");
                Undo.RecordObject(renderer, "Bind Surface Growth Material");
            }
            filter.sharedMesh = mesh;
            material = requestedMaterial != null
                ? requestedMaterial
                : FoliageAssetLibrary.CreateOrLoadDefaultMaterial();
            renderer.sharedMaterial = material;
        }

        private static string SanitizeFileName(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return "SurfaceGrowth";
            }
            foreach (char invalid in Path.GetInvalidFileNameChars())
            {
                value = value.Replace(invalid, '_');
            }
            return value.Replace(' ', '_');
        }

        private static void MarkSceneDirty(GameObject gameObject)
        {
            if (gameObject.scene.IsValid())
            {
                EditorSceneManager.MarkSceneDirty(gameObject.scene);
            }
        }

        internal sealed class ColliderProjector
        {
            private readonly Transform authoringTransform;
            private readonly Collider[] colliders;

            public ColliderProjector(
                Transform authoringTransform,
                Collider primary,
                IReadOnlyList<Collider> additional)
            {
                this.authoringTransform = authoringTransform;
                var unique = new List<Collider>();
                if (primary != null)
                {
                    unique.Add(primary);
                }
                if (additional != null)
                {
                    for (int i = 0; i < additional.Count; i++)
                    {
                        Collider candidate = additional[i];
                        if (candidate != null && !unique.Contains(candidate))
                        {
                            unique.Add(candidate);
                        }
                    }
                }
                colliders = unique.ToArray();
            }

            public bool Project(
                Vector3 candidate,
                Vector3 normalHint,
                float maximumDistance,
                out SurfacePoint point)
            {
                point = default;
                if (authoringTransform == null || colliders.Length == 0)
                {
                    return false;
                }

                float distance = Mathf.Max(0.01f, maximumDistance);
                Vector3 worldCandidate = authoringTransform.TransformPoint(candidate);
                Vector3 worldHint = authoringTransform.TransformDirection(normalHint).normalized;
                if (worldHint.sqrMagnitude < 1e-6f)
                {
                    worldHint = Vector3.up;
                }

                Vector3[] directions =
                {
                    worldHint,
                    -worldHint,
                    Vector3.up,
                    Vector3.down,
                    Vector3.right,
                    Vector3.left,
                    Vector3.forward,
                    Vector3.back,
                };

                bool found = false;
                float bestDistance = float.MaxValue;
                RaycastHit bestHit = default;
                for (int colliderIndex = 0;
                     colliderIndex < colliders.Length;
                     colliderIndex++)
                {
                    Collider collider = colliders[colliderIndex];
                    if (collider == null || !collider.enabled)
                    {
                        continue;
                    }

                    for (int directionIndex = 0;
                         directionIndex < directions.Length;
                         directionIndex++)
                    {
                        Vector3 direction = directions[directionIndex].normalized;
                        Vector3 origin = worldCandidate + direction * distance;
                        RaycastHit hit;
                        if (!collider.Raycast(
                                new Ray(origin, -direction),
                                out hit,
                                distance * 2f + 0.001f))
                        {
                            continue;
                        }

                        float candidateDistance = Vector3.Distance(
                            worldCandidate,
                            hit.point);
                        if (candidateDistance < bestDistance)
                        {
                            bestDistance = candidateDistance;
                            bestHit = hit;
                            found = true;
                        }
                    }
                }

                if (!found)
                {
                    return false;
                }

                Vector3 localNormal = authoringTransform.localToWorldMatrix.transpose
                    .MultiplyVector(bestHit.normal).normalized;
                point = new SurfacePoint(
                    authoringTransform.InverseTransformPoint(bestHit.point),
                    localNormal);
                return true;
            }
        }
    }
}
