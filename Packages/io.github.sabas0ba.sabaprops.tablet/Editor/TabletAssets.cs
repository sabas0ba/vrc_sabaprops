using UnityEditor;
using UnityEngine;

namespace SabaProps.Tablet.Editors
{
    /// <summary>
    /// Build とサンプルが書き出すアセットの保存先と、その作成処理。
    /// <para>
    /// 書き出し先はすべて Assets/ 配下です。VCC は更新時にパッケージのフォルダを置き換えるため、
    /// パッケージ内に書いたものは失われます。
    /// </para>
    /// </summary>
    public static class TabletAssets
    {
        public const string RootFolder = "Assets/SabaProps/Tablet";
        public const string GeneratedFolder = RootFolder + "/Generated";
        public const string SampleFolder = RootFolder + "/Samples";

        /// <summary>"Assets/a/b/c" 形式のパスの各フォルダを作成します。</summary>
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

        /// <summary>
        /// path にアセットを保存します。既存のアセットがあれば内容だけを置き換え、GUID と参照を保ちます。
        /// </summary>
        public static T CreateOrReplace<T>(T asset, string path) where T : Object
        {
            var existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing == null)
            {
                AssetDatabase.CreateAsset(asset, path);
                return asset;
            }

            string name = existing.name;
            EditorUtility.CopySerialized(asset, existing);
            existing.name = name;
            Object.DestroyImmediate(asset);
            EditorUtility.SetDirty(existing);
            return existing;
        }

        /// <summary>シェーダーと色からマテリアルを作ります。シェーダーが未指定なら Standard を使います。</summary>
        public static Material ColorMaterial(Shader shader, Color color, float smoothness, string name)
        {
            Shader resolved = shader != null ? shader : Shader.Find("Standard");
            var material = new Material(resolved) { name = name };
            if (material.HasProperty("_Color"))
            {
                material.SetColor("_Color", color);
            }

            if (material.HasProperty("_Glossiness"))
            {
                material.SetFloat("_Glossiness", smoothness);
            }

            return material;
        }

        /// <summary>アイコン用の透過マテリアル。</summary>
        public static Material IconMaterial(Texture2D icon, string name)
        {
            var material = new Material(Shader.Find("Unlit/Transparent")) { name = name };
            material.mainTexture = icon;
            return material;
        }
    }
}
