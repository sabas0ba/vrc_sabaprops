using UnityEditor;
using UnityEngine;

namespace SabaProps.Water.Editors
{
    [CustomEditor(typeof(WaterSurfaceProfile))]
    [CanEditMultipleObjects]
    public sealed class WaterSurfaceProfileEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            EditorGUILayout.Space();
            if (GUILayout.Button("Apply to Material"))
            {
                foreach (UnityEngine.Object selected in targets)
                {
                    var profile = selected as WaterSurfaceProfile;
                    if (profile == null)
                    {
                        continue;
                    }

                    profile.ApplyToMaterial();
                    EditorUtility.SetDirty(profile);
                    if (profile.material != null)
                    {
                        EditorUtility.SetDirty(profile.material);
                    }
                }

                AssetDatabase.SaveAssets();
            }

            EditorGUILayout.HelpBox(
                "値はMaterialへbakeされます。VRChat buildでこのprofileが除去されても水面表示は維持されます。",
                MessageType.Info);
        }
    }
}
