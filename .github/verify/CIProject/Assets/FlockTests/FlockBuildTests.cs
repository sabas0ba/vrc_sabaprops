#if UNITY_EDITOR
using System.IO;
using NUnit.Framework;
using SabaProps.Flock;
using SabaProps.Flock.Editors;
using UnityEditor;
using UnityEngine;

namespace SabaProps.Flock.CITests
{
    public class FlockBuildTests
    {
        [Test]
        public void BuiltAssetBundle_ExcludesAuthoringScriptAndKeepsRenderers()
        {
            const string root = "Assets/FlockBuildTest";
            const string prefabPath = root + "/FlockRuntime.prefab";
            const string output = "Temp/FlockBuildTest";
            FlockSwarm swarm = null;
            AssetBundle bundle = null;
            try
            {
                FlockAssetLibrary.EnsureFolder(root);
                swarm = FlockSwarmBuilder.Create("neon-tetra", null, Vector3.zero);
                Assert.IsTrue((swarm.hideFlags & HideFlags.DontSaveInBuild) != 0);
                PrefabUtility.SaveAsPrefabAsset(swarm.gameObject, prefabPath);
                Directory.CreateDirectory(output);
                AssetBundleManifest manifest = BuildPipeline.BuildAssetBundles(output,
                    new[] { new AssetBundleBuild { assetBundleName = "flock-runtime", assetNames = new[] { prefabPath } } },
                    BuildAssetBundleOptions.ForceRebuildAssetBundle, BuildTarget.StandaloneWindows64);
                Assert.IsNotNull(manifest, "Unity AssetBundle build failed");
                bundle = AssetBundle.LoadFromFile(output + "/flock-runtime");
                Assert.IsNotNull(bundle);
                GameObject built = bundle.LoadAsset<GameObject>("FlockRuntime");
                Assert.IsNotNull(built);
                Assert.AreEqual(0, built.GetComponentsInChildren<MonoBehaviour>(true).Length,
                    "an authoring or missing Script component survived the runtime build");
                MeshRenderer[] renderers = built.GetComponentsInChildren<MeshRenderer>(true);
                Assert.AreEqual(3, renderers.Length);
                Assert.IsNotNull(built.GetComponent<LODGroup>());
                foreach (MeshRenderer renderer in renderers)
                {
                    Assert.IsNotNull(renderer.sharedMaterial);
                    Assert.AreEqual(FlockShaderContract.ShaderName, renderer.sharedMaterial.shader.name);
                    Assert.IsNotNull(renderer.GetComponent<MeshFilter>().sharedMesh);
                }
                Assert.IsNotNull(swarm, "building changed the source authoring object");
            }
            finally
            {
                if (bundle != null) bundle.Unload(true);
                if (swarm != null) Object.DestroyImmediate(swarm.gameObject);
                AssetDatabase.DeleteAsset(root);
                AssetDatabase.DeleteAsset(FlockAssetLibrary.RootFolder);
            }
        }
    }
}
#endif
