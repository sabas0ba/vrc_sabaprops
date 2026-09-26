using UnityEditor;
using UnityEngine;

namespace SabaProps.Flock.Editors
{
    /// <summary>Owns the project assets the flock tools generate.</summary>
    public static class FlockAssetLibrary
    {
        public const string RootFolder = "Assets/SabaProps/Flock";
        public const string MaterialsFolder = RootFolder + "/Materials";
        public const string MeshesFolder = RootFolder + "/Meshes";

        public const string SkyMaterialName = "Flock_Sky";
        public const string WaterMaterialName = "Flock_Water";
        public const string AquariumMaterialName = "Flock_Aquarium";

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

        /// <summary>The material a new swarm of this habitat uses unless one is assigned.</summary>
        public static Material DefaultMaterial(FlockHabitat habitat)
        {
            switch (habitat)
            {
                case FlockHabitat.Sea:
                case FlockHabitat.Reef:
                    return GetOrCreateMaterial(WaterMaterialName, habitat);
                case FlockHabitat.Aquarium:
                    return GetOrCreateMaterial(AquariumMaterialName, habitat);
                default:
                    return GetOrCreateMaterial(SkyMaterialName, habitat);
            }
        }

        public static FlockHabitat HabitatOf(FlockSpecies species)
        {
            foreach (FlockPreset preset in FlockSpeciesCatalog.All)
            {
                if (preset.Species.id == species.id)
                {
                    return preset.Habitat;
                }
            }

            return species.category == FlockCategory.Bird ? FlockHabitat.Sky : FlockHabitat.Sea;
        }

        private static Material GetOrCreateMaterial(string name, FlockHabitat habitat)
        {
            string path = MaterialsFolder + "/" + name + ".mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null)
            {
                return existing;
            }

            Shader shader = Shader.Find(FlockShaderContract.ShaderName);
            if (shader == null)
            {
                Debug.LogError($"[SabaProps Flock] Shader '{FlockShaderContract.ShaderName}' が見つかりません。");
                return null;
            }

            EnsureFolder(MaterialsFolder);
            var material = new Material(shader) { name = name };
            ApplyHabitatDefaults(material, habitat);
            AssetDatabase.CreateAsset(material, path);
            AssetDatabase.SaveAssets();
            return material;
        }

        /// <summary>
        /// Distance settings per habitat. The sky material turns far birds
        /// into a dark silhouette; the water materials fade fish into the
        /// water colour instead, and the aquarium keeps them fully coloured.
        /// </summary>
        public static void ApplyHabitatDefaults(Material material, FlockHabitat habitat)
        {
            switch (habitat)
            {
                case FlockHabitat.Sea:
                case FlockHabitat.Reef:
                    material.SetColor(FlockShaderContract.SilhouetteColorProperty, new Color(0.05f, 0.18f, 0.24f, 0f));
                    material.SetColor(FlockShaderContract.MediumColorProperty, new Color(0.08f, 0.32f, 0.42f, 1f));
                    material.SetFloat(FlockShaderContract.MediumDensityProperty, 0.04f);
                    break;
                case FlockHabitat.Aquarium:
                    material.SetColor(FlockShaderContract.SilhouetteColorProperty, new Color(0f, 0f, 0f, 0f));
                    material.SetFloat(FlockShaderContract.MediumDensityProperty, 0f);
                    break;
                default:
                    material.SetColor(FlockShaderContract.SilhouetteColorProperty, new Color(0.12f, 0.12f, 0.14f, 1f));
                    material.SetFloat(FlockShaderContract.SilhouetteStartProperty, 80f);
                    material.SetFloat(FlockShaderContract.SilhouetteEndProperty, 300f);
                    material.SetFloat(FlockShaderContract.MediumDensityProperty, 0f);
                    break;
            }
        }
    }
}
