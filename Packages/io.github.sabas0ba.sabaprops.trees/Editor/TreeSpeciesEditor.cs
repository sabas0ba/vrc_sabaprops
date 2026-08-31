using UnityEditor;
using UnityEngine;

namespace SabaProps.Trees.Editors
{
    [CustomEditor(typeof(TreeSpecies))]
    public sealed class TreeSpeciesEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            DrawDefaultInspector();
            serializedObject.ApplyModifiedProperties();

            EditorGUILayout.Space();
            EditorGUILayout.HelpBox(
                "LOD Mesh の書き出しは AssetDatabase 操作のため Undo 対象外です。" +
                "シーンに生成した LODGroup は Undo できます。",
                MessageType.Info);

            TreeSpecies species = (TreeSpecies)target;
            if (GUILayout.Button("Apply Archetype Preset"))
            {
                Undo.RecordObject(species, "Apply Tree Archetype Preset");
                species.ApplyArchetypePreset(species.archetype);
                EditorUtility.SetDirty(species);
            }

            using (new EditorGUI.DisabledScope(
                species.botanicalPreset == TreeBotanicalPreset.Custom))
            {
                if (GUILayout.Button("Apply Botanical Preset"))
                {
                    Undo.RecordObject(species, "Apply Tree Botanical Preset");
                    species.ApplyBotanicalPreset(species.botanicalPreset);
                    EditorUtility.SetDirty(species);
                }
            }

            if (GUILayout.Button("Rebuild LOD Meshes"))
            {
                TreeAssetLibrary.WriteLodMeshes(species);
                AssetDatabase.SaveAssets();
            }

            if (GUILayout.Button("Create LOD Group in Scene"))
            {
                TreeAssetLibrary.CreateLodGroup(species);
            }
        }
    }
}
