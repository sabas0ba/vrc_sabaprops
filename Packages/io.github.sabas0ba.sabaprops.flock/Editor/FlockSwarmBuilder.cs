using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace SabaProps.Flock.Editors
{
    /// <summary>
    /// Creates and rebuilds the renderers of a <see cref="FlockSwarm"/>. The
    /// generated meshes are stored as one asset per swarm under
    /// <see cref="FlockAssetLibrary.MeshesFolder"/>, and are rewritten in place
    /// on rebuild so references and GUIDs stay stable.
    /// </summary>
    public static class FlockSwarmBuilder
    {
        /// <summary>Child object names. Children with these names are owned by the builder.</summary>
        public static readonly string[] RendererNames = { "Flock High", "Flock Low", "Flock Silhouette" };
        public const string SingleRendererName = "Flock Renderer";

        private static readonly FlockDetail[] LodDetails = { FlockDetail.High, FlockDetail.Low, FlockDetail.Silhouette };

        /// <summary>Creates a swarm GameObject from a preset and builds it.</summary>
        public static FlockSwarm Create(string presetId, GameObject parent, Vector3 localPosition)
        {
            FlockSpecies species = FlockSpeciesCatalog.Create(presetId) ?? new FlockSpecies();
            var go = new GameObject($"Flock {species.displayName}");
            Undo.RegisterCreatedObjectUndo(go, "Create Flock Swarm");
            if (parent != null)
            {
                GameObjectUtility.SetParentAndAlign(go, parent);
            }

            go.transform.localPosition = localPosition;
            var swarm = go.AddComponent<FlockSwarm>();
            ApplyPreset(swarm, presetId);
            Rebuild(swarm);
            return swarm;
        }

        /// <summary>Copies a preset's species and default swarm settings into the swarm.</summary>
        public static void ApplyPreset(FlockSwarm swarm, string presetId)
        {
            FlockSpecies species = FlockSpeciesCatalog.Create(presetId);
            if (species == null)
            {
                return;
            }

            Undo.RecordObject(swarm, "Apply Flock Preset");
            swarm.presetId = presetId;
            swarm.species = species;
            swarm.settings.pattern = species.defaultPattern;
            swarm.settings.count = species.defaultCount;
            swarm.settings.area = species.defaultArea;
            swarm.settings.clusterRadius = 0f;
            swarm.gameObject.name = $"Flock {species.displayName}";
            EditorUtility.SetDirty(swarm);
        }

        public static void Rebuild(FlockSwarm swarm)
        {
            if (swarm == null)
            {
                return;
            }

            Undo.RecordObject(swarm, "Rebuild Flock Swarm");
            bool lod = swarm.settings.lodMode == FlockLodMode.LodGroup;
            FlockDetail[] details = lod ? LodDetails : new[] { swarm.settings.detail };
            Mesh[] meshes = PrepareMeshes(swarm, details.Length);

            for (int i = 0; i < details.Length; i++)
            {
                FlockSwarmMeshBuilder.Fill(meshes[i], swarm.species, swarm.settings, details[i]);
                meshes[i].name = $"{swarm.species.id}_{details[i]}";
                EditorUtility.SetDirty(meshes[i]);
            }

            swarm.generatedMeshes = meshes;
            AssetDatabase.SaveAssets();

            Material material = swarm.material != null
                ? swarm.material
                : FlockAssetLibrary.DefaultMaterial(FlockAssetLibrary.HabitatOf(swarm.species));

            RemoveRenderers(swarm);
            var renderers = new List<Renderer>();
            for (int i = 0; i < details.Length; i++)
            {
                string name = lod ? RendererNames[i] : SingleRendererName;
                renderers.Add(CreateRenderer(swarm, name, meshes[i], material));
            }

            var group = swarm.GetComponent<LODGroup>();
            if (lod)
            {
                if (group == null)
                {
                    group = Undo.AddComponent<LODGroup>(swarm.gameObject);
                }

                Vector3 t = swarm.settings.lodTransitions;
                float high = Mathf.Clamp01(t.x);
                float low = Mathf.Clamp(t.y, 0f, high);
                float silhouette = Mathf.Clamp(t.z, 0f, low);
                group.SetLODs(new[]
                {
                    new LOD(high, new[] { renderers[0] }),
                    new LOD(low, new[] { renderers[1] }),
                    new LOD(silhouette, new[] { renderers[2] }),
                });
                group.RecalculateBounds();

                // Measured against the whole swarm, the tiers would follow the
                // area (tens of metres) and a distant flock of specks would
                // still draw at High. Sizing the group as one individual makes
                // the transitions the screen height of a single body, seen
                // from the swarm's centre.
                group.size = IndividualSize(swarm.species);
            }
            else if (group != null)
            {
                Undo.DestroyObjectImmediate(group);
            }

            EditorUtility.SetDirty(swarm);
        }

        /// <summary>Size the LODGroup is given: the larger of the wingspan and the body length.</summary>
        public static float IndividualSize(FlockSpecies species)
        {
            return Mathf.Max(species.Span, species.bodyLength);
        }

        /// <summary>
        /// Returns the swarm's mesh assets, creating or replacing the asset when
        /// the tier count changed. A duplicated GameObject still references the
        /// original's meshes; such a swarm gets a new asset of its own, and the
        /// shared asset is left untouched.
        /// </summary>
        private static Mesh[] PrepareMeshes(FlockSwarm swarm, int count)
        {
            Mesh[] existing = swarm.generatedMeshes ?? new Mesh[0];
            bool shared = IsSharedWithAnotherSwarm(swarm, existing);
            bool reusable = existing.Length == count && !shared;
            foreach (Mesh mesh in existing)
            {
                reusable &= mesh != null && IsGeneratedMeshPath(AssetDatabase.GetAssetPath(mesh));
            }

            if (reusable)
            {
                return existing;
            }

            string path = existing.Length > 0 && existing[0] != null
                ? AssetDatabase.GetAssetPath(existing[0])
                : string.Empty;
            // A copied sample scene keeps references to its bundled mesh assets.
            // Rebuilding the copy must leave those source assets intact even when
            // the original scene is closed and no other swarm is loaded.
            if (shared || !IsGeneratedMeshPath(path))
            {
                path = string.Empty;
            }
            if (!string.IsNullOrEmpty(path))
            {
                AssetDatabase.DeleteAsset(path);
            }
            else
            {
                FlockAssetLibrary.EnsureFolder(FlockAssetLibrary.MeshesFolder);
                path = AssetDatabase.GenerateUniqueAssetPath(
                    $"{FlockAssetLibrary.MeshesFolder}/{swarm.species.id}_swarm.asset");
            }

            var meshes = new Mesh[count];
            for (int i = 0; i < count; i++)
            {
                meshes[i] = new Mesh { name = $"{swarm.species.id}_{i}" };
            }

            AssetDatabase.CreateAsset(meshes[0], path);
            for (int i = 1; i < count; i++)
            {
                AssetDatabase.AddObjectToAsset(meshes[i], meshes[0]);
            }

            return meshes;
        }

        private static bool IsGeneratedMeshPath(string path)
        {
            return path.StartsWith(
                FlockAssetLibrary.MeshesFolder + "/", System.StringComparison.Ordinal);
        }

        /// <summary>True when another swarm in the open scenes references any of <paramref name="meshes"/>.</summary>
        private static bool IsSharedWithAnotherSwarm(FlockSwarm swarm, Mesh[] meshes)
        {
            foreach (FlockSwarm other in Object.FindObjectsOfType<FlockSwarm>())
            {
                if (other == swarm || other.generatedMeshes == null)
                {
                    continue;
                }

                foreach (Mesh mesh in meshes)
                {
                    if (mesh != null && System.Array.IndexOf(other.generatedMeshes, mesh) >= 0)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static Renderer CreateRenderer(FlockSwarm swarm, string name, Mesh mesh, Material material)
        {
            var go = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(go, "Create Flock Renderer");
            go.transform.SetParent(swarm.transform, false);
            // Static batching would bake the vertices into world space and
            // break the object-space motion in the shader.
            GameObjectUtility.SetStaticEditorFlags(go, 0);

            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = true;
            renderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
            renderer.lightProbeUsage = LightProbeUsage.BlendProbes;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            return renderer;
        }

        private static void RemoveRenderers(FlockSwarm swarm)
        {
            var doomed = new List<GameObject>();
            foreach (Transform child in swarm.transform)
            {
                if (child.name == SingleRendererName || System.Array.IndexOf(RendererNames, child.name) >= 0)
                {
                    doomed.Add(child.gameObject);
                }
            }

            foreach (GameObject go in doomed)
            {
                Undo.DestroyObjectImmediate(go);
            }
        }

        /// <summary>Vertex and triangle totals of the swarm at each generated tier.</summary>
        public static string Describe(FlockSwarm swarm)
        {
            int count = FlockSwarmMeshBuilder.Count(swarm.settings);
            bool lod = swarm.settings.lodMode == FlockLodMode.LodGroup;
            FlockDetail[] details = lod ? LodDetails : new[] { swarm.settings.detail };
            var lines = new List<string>();
            foreach (FlockDetail detail in details)
            {
                FlockSwarmMeshBuilder.BodyCost(swarm.species, detail, out int vertices, out int triangles);
                lines.Add($"{detail}: {count} 体 x {triangles} tris = {count * triangles:N0} tris, {count * vertices:N0} verts");
            }

            float radius = FlockSwarmMeshBuilder.ClusterRadius(swarm.species, swarm.settings);
            lines.Add($"群れの半径: {radius:0.0} m");
            return string.Join("\n", lines);
        }
    }
}
