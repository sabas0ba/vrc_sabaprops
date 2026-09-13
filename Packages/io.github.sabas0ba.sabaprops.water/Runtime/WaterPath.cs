using System.Collections.Generic;
using UnityEngine;

namespace SabaProps.Water
{
    /// <summary>
    /// Editor authoring data for a river strip. Rebuilding writes a regular MeshFilter,
    /// so removing this component or having VRChat strip it does not remove the river.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class WaterPath : MonoBehaviour
    {
        public List<Vector3> controlPoints = new List<Vector3>
        {
            new Vector3(0f, 0f, -5f),
            new Vector3(0f, 0f, 0f),
            new Vector3(1f, 0f, 5f),
        };

        [Min(0.05f)] public float width = 2f;
        [Range(1, 16)] public int subdivisions = 4;
        [Min(0.01f)] public float uvMetersPerTile = 2f;
        public WaterSurfaceProfile profile;
        [HideInInspector] public Mesh generatedMesh;

        public void Normalize()
        {
            width = Mathf.Max(0.05f, width);
            subdivisions = Mathf.Clamp(subdivisions, 1, 16);
            uvMetersPerTile = Mathf.Max(0.01f, uvMetersPerTile);

            if (controlPoints == null)
            {
                controlPoints = new List<Vector3>();
            }

            while (controlPoints.Count < 2)
            {
                float z = controlPoints.Count * 5f;
                controlPoints.Add(new Vector3(0f, 0f, z));
            }
        }

        public void ApplyProfile()
        {
            if (profile == null || profile.material == null)
            {
                return;
            }

            profile.ApplyToMaterial();
            GetComponent<MeshRenderer>().sharedMaterial = profile.material;
        }

        private void Reset()
        {
            Normalize();
            ApplyProfile();
        }

        private void OnValidate()
        {
            Normalize();
            ApplyProfile();
        }
    }
}
