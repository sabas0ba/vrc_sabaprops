using UnityEditor;
using UnityEngine;

namespace SabaProps.Foliage.Editors
{
    [CustomEditor(typeof(FoliageSpecies))]
    [CanEditMultipleObjects]
    public class FoliageSpeciesEditor : UnityEditor.Editor
    {
        private static readonly string[] AlwaysHidden =
        {
            "m_Script",
            "grass",
            "sunflower",
            "clover",
            "reed",
            "generatedMesh",
        };

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            DrawPropertiesExcluding(serializedObject, AlwaysHidden);

            EditorGUILayout.Space(6f);
            var kind = (FoliageSpeciesKind)serializedObject.FindProperty("kind").enumValueIndex;

            EditorGUILayout.PropertyField(
                serializedObject.FindProperty(FoliageAssetLibrary.ParameterProperty(kind)), true);

            serializedObject.ApplyModifiedProperties();

            EditorGUILayout.Space(8f);
            DrawMeshSection();
        }

        private void DrawMeshSection()
        {
            var species = (FoliageSpecies)target;

            if (species.material == null)
            {
                EditorGUILayout.HelpBox(
                    "Material が未設定です。SabaProps/Foliage シェーダーを使い、GPU Instancing を有効にしたマテリアルを割り当ててください。",
                    MessageType.Warning);
            }
            else if (!species.material.enableInstancing)
            {
                EditorGUILayout.HelpBox(
                    "Material の GPU Instancing が OFF です。ビルド時に自動で ON にしますが、手動で有効にしておくことを推奨します。",
                    MessageType.Warning);
            }

            if (species.generatedMesh != null)
            {
                Mesh mesh = species.generatedMesh;
                EditorGUILayout.LabelField(
                    "Generated Mesh",
                    $"{mesh.vertexCount:N0} verts / {mesh.triangles.Length / 3:N0} tris");

                using (new EditorGUI.DisabledScope(true))
                {
                    EditorGUILayout.ObjectField(mesh, typeof(Mesh), false);
                }
            }

            if (serializedObject.isEditingMultipleObjects)
            {
                return;
            }

            if (GUILayout.Button("Rebuild Mesh", GUILayout.Height(24f)))
            {
                FoliageAssetLibrary.WriteSpeciesMesh(species);
                AssetDatabase.SaveAssets();
            }

            EditorGUILayout.HelpBox(
                "メッシュは Field の Generate 時にも自動で作り直されます。Rebuild Mesh は形状パラメータを変えた直後の確認用です。\n"
                + "形状を変えたら、この Species を使っている Field は Generate し直してください。",
                MessageType.None);
        }
    }
}
