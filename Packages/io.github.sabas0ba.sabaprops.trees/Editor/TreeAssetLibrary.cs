using System;
using System.Collections.Generic;
using System.IO;
using SabaProps.Foliage;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace SabaProps.Trees.Editors
{
    /// <summary>Creates tree source assets, generated LOD meshes and scene LOD groups.</summary>
    public static class TreeAssetLibrary
    {
        public const string RootFolder = "Assets/SabaProps/Trees";
        public const string MaterialsFolder = RootFolder + "/Materials";
        public const string SpeciesFolder = RootFolder + "/Species";
        public const string GeneratedFolder = RootFolder + "/Generated";

        public static readonly TreeArchetype[] AllArchetypes =
        {
            TreeArchetype.Broadleaf,
            TreeArchetype.Conifer,
            TreeArchetype.Deadwood,
            TreeArchetype.DesertScrub,
        };

        public static readonly TreeBotanicalPreset[] AllBotanicalPresets =
        {
            TreeBotanicalPreset.JapaneseZelkova,
            TreeBotanicalPreset.JapaneseMaple,
            TreeBotanicalPreset.JapaneseCedar,
            TreeBotanicalPreset.JapaneseWhiteBirch,
            TreeBotanicalPreset.JapaneseRedPine,
            TreeBotanicalPreset.HinokiCypress,
            TreeBotanicalPreset.SomeiYoshinoSpring,
            TreeBotanicalPreset.SomeiYoshinoSummer,
            TreeBotanicalPreset.GinkgoSummer,
            TreeBotanicalPreset.GinkgoAutumn,
        };

        public static void EnsureFolder(string folderPath)
        {
            if (AssetDatabase.IsValidFolder(folderPath))
            {
                return;
            }

            string[] parts = folderPath.Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, parts[i]);
                }
                current = next;
            }
        }

        public static string DisplayName(TreeArchetype archetype)
        {
            switch (archetype)
            {
                case TreeArchetype.Conifer: return "Conifer";
                case TreeArchetype.Deadwood: return "Deadwood";
                case TreeArchetype.DesertScrub: return "DesertScrub";
                case TreeArchetype.Broadleaf:
                default: return "Broadleaf";
            }
        }

        public static string DisplayName(TreeBotanicalPreset preset)
        {
            switch (preset)
            {
                case TreeBotanicalPreset.JapaneseZelkova: return "JapaneseZelkova";
                case TreeBotanicalPreset.JapaneseMaple: return "JapaneseMaple";
                case TreeBotanicalPreset.JapaneseCedar: return "JapaneseCedar";
                case TreeBotanicalPreset.JapaneseWhiteBirch: return "JapaneseWhiteBirch";
                case TreeBotanicalPreset.JapaneseRedPine: return "JapaneseRedPine";
                case TreeBotanicalPreset.HinokiCypress: return "HinokiCypress";
                case TreeBotanicalPreset.SomeiYoshinoSpring: return "SomeiYoshinoSpring";
                case TreeBotanicalPreset.SomeiYoshinoSummer: return "SomeiYoshinoSummer";
                case TreeBotanicalPreset.GinkgoSummer: return "GinkgoSummer";
                case TreeBotanicalPreset.GinkgoAutumn: return "GinkgoAutumn";
                default: return "Custom";
            }
        }

        public static Material CreateOrLoadDefaultMaterial()
        {
            EnsureFolder(MaterialsFolder);
            const string path = MaterialsFolder + "/SabaProps_Trees.mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                Shader shader = Shader.Find(FoliageShaderContract.ShaderName);
                if (shader == null)
                {
                    Debug.LogError(
                        $"[SabaProps Trees] Shader '{FoliageShaderContract.ShaderName}' が見つかりません。" +
                        " SabaProps Foliage がインストールされているか確認してください。");
                    return null;
                }

                material = new Material(shader)
                {
                    name = "SabaProps_Trees",
                    enableInstancing = true,
                };
                AssetDatabase.CreateAsset(material, path);
            }

            material.enableInstancing = true;
            material.SetFloat(FoliageShaderContract.DistanceFadeProperty, 0f);
            material.DisableKeyword(FoliageShaderContract.DistanceFadeKeyword);
            material.SetFloat("_Cull", (float)CullMode.Back);
            material.SetFloat("_WindStrength", 0.12f);
            EditorUtility.SetDirty(material);
            return material;
        }

        public static TreeSpecies CreateOrLoadSpecies(TreeArchetype archetype, Material material = null)
        {
            EnsureFolder(SpeciesFolder);
            string name = DisplayName(archetype);
            string path = $"{SpeciesFolder}/{name}.asset";
            TreeSpecies species = AssetDatabase.LoadAssetAtPath<TreeSpecies>(path);
            if (species == null)
            {
                species = ScriptableObject.CreateInstance<TreeSpecies>();
                species.name = name;
                species.ApplyArchetypePreset(archetype);
                species.material = material ?? CreateOrLoadDefaultMaterial();
                AssetDatabase.CreateAsset(species, path);
            }
            else if (species.material == null)
            {
                species.material = material ?? CreateOrLoadDefaultMaterial();
                EditorUtility.SetDirty(species);
            }

            WriteLodMeshes(species);
            return species;
        }

        public static TreeSpecies CreateOrLoadSpecies(
            TreeBotanicalPreset preset,
            Material material = null)
        {
            EnsureFolder(SpeciesFolder);
            string name = DisplayName(preset);
            string path = $"{SpeciesFolder}/{name}.asset";
            TreeSpecies species = AssetDatabase.LoadAssetAtPath<TreeSpecies>(path);
            if (species == null)
            {
                species = ScriptableObject.CreateInstance<TreeSpecies>();
                species.name = name;
                species.ApplyBotanicalPreset(preset);
                species.material = material ?? CreateOrLoadDefaultMaterial();
                AssetDatabase.CreateAsset(species, path);
            }
            else if (species.material == null)
            {
                species.material = material ?? CreateOrLoadDefaultMaterial();
                EditorUtility.SetDirty(species);
            }

            WriteLodMeshes(species);
            return species;
        }

        public static List<TreeSpecies> CreateOrLoadDefaults(out Material material)
        {
            material = CreateOrLoadDefaultMaterial();
            var result = new List<TreeSpecies>(AllBotanicalPresets.Length + 2);
            foreach (TreeBotanicalPreset preset in AllBotanicalPresets)
            {
                result.Add(CreateOrLoadSpecies(preset, material));
            }
            result.Add(CreateOrLoadSpecies(TreeArchetype.Deadwood, material));
            result.Add(CreateOrLoadSpecies(TreeArchetype.DesertScrub, material));
            return result;
        }

        /// <summary>
        /// Regenerates all three LODs while preserving existing mesh GUIDs.
        /// Asset writes are intentionally outside Unity Undo.
        /// </summary>
        public static Mesh[] WriteLodMeshes(TreeSpecies species)
        {
            if (species == null)
            {
                return Array.Empty<Mesh>();
            }

            EnsureFolder(GeneratedFolder);
            var result = new Mesh[3];
            for (int lod = 0; lod < result.Length; lod++)
            {
                Mesh generated = TreeMeshBuilder.Build(species, lod);
                string path = $"{GeneratedFolder}/{SanitizeFileName(species.name)}_{StableSuffix(species)}_LOD{lod}.asset";
                Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);

                if (existing != null)
                {
                    EditorUtility.CopySerialized(generated, existing);
                    UnityEngine.Object.DestroyImmediate(generated);
                    existing.name = Path.GetFileNameWithoutExtension(path);
                    EditorUtility.SetDirty(existing);
                    result[lod] = existing;
                }
                else
                {
                    generated.name = Path.GetFileNameWithoutExtension(path);
                    AssetDatabase.CreateAsset(generated, path);
                    result[lod] = generated;
                }
            }

            species.lod0Mesh = result[0];
            species.lod1Mesh = result[1];
            species.lod2Mesh = result[2];
            if (species.material == null)
            {
                species.material = CreateOrLoadDefaultMaterial();
            }
            EditorUtility.SetDirty(species);
            return result;
        }

        public static GameObject CreateLodGroup(TreeSpecies species, Transform parent = null)
        {
            if (species == null)
            {
                return null;
            }

            Mesh[] meshes = WriteLodMeshes(species);
            if (meshes.Length != 3 || species.material == null)
            {
                return null;
            }

            GameObject root = CreateLodGroupInstance(species, parent);
            if (root == null)
            {
                return null;
            }

            Undo.RegisterCreatedObjectUndo(root, "Create SabaProps Tree");
            Selection.activeGameObject = root;
            return root;
        }

        /// <summary>
        /// Creates only a scene LOD hierarchy from meshes already assigned to
        /// the species. Bulk builders use this after generating each species
        /// once, so asset writes do not scale with the number of instances.
        /// </summary>
        public static GameObject CreateLodGroupInstance(
            TreeSpecies species, Transform parent = null)
        {
            if (species == null || species.material == null)
            {
                return null;
            }

            var meshes = new[]
            {
                species.lod0Mesh,
                species.lod1Mesh,
                species.lod2Mesh,
            };
            for (int i = 0; i < meshes.Length; i++)
            {
                if (meshes[i] == null)
                {
                    return null;
                }
            }

            var root = new GameObject(species.name + " Tree");
            if (parent != null)
            {
                root.transform.SetParent(parent, false);
            }

            var renderers = new Renderer[3];
            for (int lod = 0; lod < meshes.Length; lod++)
            {
                var child = new GameObject("LOD" + lod);
                child.transform.SetParent(root.transform, false);

                MeshFilter filter = child.AddComponent<MeshFilter>();
                filter.sharedMesh = meshes[lod];

                MeshRenderer renderer = child.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = species.material;
                renderer.shadowCastingMode = ShadowCastingMode.On;
                renderer.receiveShadows = true;
                renderers[lod] = renderer;
            }

            LODGroup group = root.AddComponent<LODGroup>();
            group.SetLODs(new[]
            {
                new LOD(species.lod.lod0ScreenHeight, new[] { renderers[0] }),
                new LOD(species.lod.lod1ScreenHeight, new[] { renderers[1] }),
                new LOD(species.lod.lod2ScreenHeight, new[] { renderers[2] }),
            });
            group.RecalculateBounds();

            return root;
        }

        private static string StableSuffix(UnityEngine.Object asset)
        {
            string path = AssetDatabase.GetAssetPath(asset);
            if (string.IsNullOrEmpty(path))
            {
                return "mem";
            }
            string guid = AssetDatabase.AssetPathToGUID(path);
            return string.IsNullOrEmpty(guid) ? "mem" : guid.Substring(0, 8);
        }

        private static string SanitizeFileName(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return "Unnamed";
            }
            foreach (char invalid in Path.GetInvalidFileNameChars())
            {
                value = value.Replace(invalid, '_');
            }
            return value.Replace(' ', '_');
        }
    }
}
