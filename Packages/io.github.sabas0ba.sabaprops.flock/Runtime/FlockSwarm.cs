using UnityEngine;

namespace SabaProps.Flock
{
    /// <summary>
    /// Authoring component of one swarm. It holds the parameters and the
    /// renderers the editor generated from them; nothing runs at play time.
    /// The individuals move in the vertex shader, so moving, rotating or
    /// animating this transform moves the whole swarm.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("SabaProps/Flock/Flock Swarm")]
    public sealed class FlockSwarm : MonoBehaviour
    {
        [Tooltip("プリセットの ID。インスペクタでプリセットを選ぶと species が上書きされる。")]
        public string presetId = string.Empty;

        public FlockSpecies species = new FlockSpecies();

        public FlockSwarmSettings settings = new FlockSwarmSettings();

        [Tooltip("空欄なら既定の Material を使う")]
        public Material material;

        [HideInInspector]
        public Mesh[] generatedMeshes = new Mesh[0];

        private void OnValidate()
        {
            ExcludeAuthoringComponentFromBuild();
        }

        private void Reset()
        {
            ExcludeAuthoringComponentFromBuild();
        }

        /// <summary>
        /// The renderers carry everything the swarm needs at run time. Keeping
        /// this component out of the build means a VRChat world uploads with
        /// no custom script attached.
        /// </summary>
        private void ExcludeAuthoringComponentFromBuild()
        {
            hideFlags |= HideFlags.DontSaveInBuild;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.color = new Color(0.3f, 0.8f, 1f, 0.8f);
            Gizmos.DrawWireCube(Vector3.zero, settings.area * 2f);
        }
    }
}
