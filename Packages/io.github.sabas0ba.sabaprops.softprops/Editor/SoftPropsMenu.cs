using System;
using UnityEditor;
using UnityEngine;

namespace SabaProps.SoftProps.Editors
{
    public static class SoftPropsMenu
    {
        [MenuItem("Tools/SabaProps/Soft Props/Generate All Prefabs", false, 10)]
        public static void GenerateAllPrefabs()
        {
            try
            {
                SoftPropGenerator.GenerateAll();
                EditorUtility.DisplayDialog(
                    "SabaProps Soft Props",
                    "ふとん、ベッド、ソファー、クッション、接触形状テストを\n"
                    + SoftPropGenerator.PrefabFolder
                    + " に生成しました。",
                    "OK");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorUtility.DisplayDialog("SabaProps Soft Props", exception.Message, "OK");
            }
        }

        [MenuItem("Tools/SabaProps/Soft Props/Create Showcase In Scene", false, 11)]
        public static void CreateShowcase()
        {
            try
            {
                SoftPropGenerator.CreateShowcase();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorUtility.DisplayDialog("SabaProps Soft Props", exception.Message, "OK");
            }
        }

        [MenuItem("Tools/SabaProps/Soft Props/Create Contact Probe Test In Scene", false, 12)]
        public static void CreateContactProbeTest()
        {
            try
            {
                SoftPropGenerator.CreateContactProbeTestInScene();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorUtility.DisplayDialog("SabaProps Soft Props", exception.Message, "OK");
            }
        }

        [MenuItem("Tools/SabaProps/Soft Props/Documentation", false, 100)]
        public static void OpenDocumentation()
        {
            Application.OpenURL(
                "https://github.com/sabas0ba/vrc_sabaprops/blob/main/"
                + "Packages/io.github.sabas0ba.sabaprops.softprops/README.md");
        }
    }
}
