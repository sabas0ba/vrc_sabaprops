using UnityEditor;
using UnityEngine;

namespace SabaProps.Foliage.Editors
{
    /// <summary>
    /// Material inspector for <c>SabaProps/Foliage</c>. The property list itself
    /// is already grouped by the [Header] attributes in the shader, so this only
    /// adds the checks a user is likely to get wrong.
    /// </summary>
    public class SabaFoliageShaderGUI : ShaderGUI
    {
        public override void OnGUI(MaterialEditor materialEditor, MaterialProperty[] properties)
        {
            var material = materialEditor.target as Material;

            if (material != null && !material.enableInstancing)
            {
                EditorGUILayout.HelpBox(
                    "GPU Instancing が OFF です。大量配置の効果が出ないので有効にしてください。",
                    MessageType.Warning);

                if (GUILayout.Button("Enable GPU Instancing"))
                {
                    foreach (Object each in materialEditor.targets)
                    {
                        var target = (Material)each;
                        target.enableInstancing = true;
                        EditorUtility.SetDirty(target);
                    }
                }

                EditorGUILayout.Space(4f);
            }

            base.OnGUI(materialEditor, properties);

            EditorGUILayout.Space(8f);
            EditorGUILayout.HelpBox(
                "このシェーダーは頂点カラー駆動です。テクスチャは任意で、未設定でも動作します。\n"
                + "Distance Shrink は遠くの個体を根元へ縮退させ、実質的な密度 LOD として働きます。",
                MessageType.None);
        }
    }
}
