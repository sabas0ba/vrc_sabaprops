using System.IO;
using UnityEditor;
using UnityEngine;

namespace SabaProps.Water.Editors
{
    // Undo restores object state, but does not manage asset files.
    // Keep the bookkeeping in an editor-only object so it survives assembly reloads.
    [InitializeOnLoad]
    public sealed class WaterMeshAssetUndo : ScriptableObject
    {
        [SerializeField] private Mesh mesh;
        [SerializeField] private bool present;
        [SerializeField] private string assetPath;
        [SerializeField] private string assetGuid;
        [SerializeField] private string metaContents;

        static WaterMeshAssetUndo()
        {
            Undo.undoRedoPerformed += SynchronizeAssets;
        }

        public static void Track(Mesh mesh, string path)
        {
            var record = CreateInstance<WaterMeshAssetUndo>();
            record.hideFlags = HideFlags.HideAndDontSave;
            record.mesh = mesh;
            record.assetPath = path;
            record.assetGuid = AssetDatabase.AssetPathToGUID(path);
            record.metaContents = File.ReadAllText(path + ".meta");
            Undo.RegisterCompleteObjectUndo(record, "Create Water Mesh");
            record.present = true;
        }

        private static void SynchronizeAssets()
        {
            foreach (WaterMeshAssetUndo record in Resources.FindObjectsOfTypeAll<WaterMeshAssetUndo>())
            {
                record.Synchronize();
            }
        }

        private void Synchronize()
        {
            if (!present && mesh != null && AssetDatabase.GetAssetPath(mesh) == assetPath)
            {
                // Never delete a replacement asset or one containing a different mesh.
                if (AssetDatabase.AssetPathToGUID(assetPath) == assetGuid
                    && AssetDatabase.LoadAssetAtPath<Mesh>(assetPath) == mesh)
                {
                    // Detach before deleting so redo keeps the same native Mesh identity.
                    AssetDatabase.RemoveObjectFromAsset(mesh);
                    AssetDatabase.DeleteAsset(assetPath);
                }
            }
            else if (present && mesh != null && string.IsNullOrEmpty(AssetDatabase.GetAssetPath(mesh))
                && !File.Exists(assetPath))
            {
                File.WriteAllText(assetPath + ".meta", metaContents);
                AssetDatabase.CreateAsset(mesh, assetPath);
            }
        }
    }
}
