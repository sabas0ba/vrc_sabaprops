using System.Collections.Generic;
using NUnit.Framework;
using SabaProps.Foliage.Editors;
using UnityEngine;

namespace SabaProps.Foliage.CITests
{
    public sealed class SurfaceGrowthEditorTests
    {
        [Test]
        public void ProjectedSplineIsDeterministicAndStaysOnSurface()
        {
            var settings = new SurfaceGrowthSettings
            {
                mode = SurfaceGrowthMode.ProjectedSpline,
                pathCount = 6,
                coverage = 0.75f,
                stepLength = 0.1f,
                maxPathLength = 2f,
                branchesPerMetre = 1f,
                maxBranchDepth = 2,
                nodeBudget = 200,
                seed = 7301,
            };
            var guides = new List<Vector3>
            {
                Vector3.zero,
                new Vector3(0.25f, 0.8f, 0f),
                new Vector3(-0.15f, 1.5f, 0f),
                new Vector3(0.3f, 2.1f, 0f),
            };

            SurfaceGrowthGraph first = SurfaceGrowthGraphBuilder.Build(
                settings,
                guides,
                ProjectWall);
            SurfaceGrowthGraph second = SurfaceGrowthGraphBuilder.Build(
                settings,
                guides,
                ProjectWall);

            Assert.Greater(first.Nodes.Count, settings.pathCount);
            Assert.LessOrEqual(first.Nodes.Count, settings.nodeBudget);
            Assert.AreEqual(first.Nodes.Count, second.Nodes.Count);
            for (int i = 0; i < first.Nodes.Count; i++)
            {
                Assert.AreEqual(first.Nodes[i].position, second.Nodes[i].position);
                Assert.AreEqual(settings.surfaceOffset, first.Nodes[i].position.z, 1e-5f);
                Assert.Less(first.Nodes[i].parentIndex, i);
            }
        }

        [Test]
        public void SurfaceMorphologyControlsGeometryAndKeepsShaderChannels()
        {
            var settings = new SurfaceGrowthSettings
            {
                mode = SurfaceGrowthMode.ProjectedSpline,
                pathCount = 3,
                coverage = 0.7f,
                stepLength = 0.1f,
                maxPathLength = 1.4f,
                seed = 7302,
            };
            SurfaceGrowthGraph graph = SurfaceGrowthGraphBuilder.Build(
                settings,
                new List<Vector3>
                {
                    Vector3.zero,
                    new Vector3(0.15f, 0.7f, 0f),
                    new Vector3(-0.1f, 1.4f, 0f),
                },
                ProjectWall);

            var sparse = new SurfaceVineParams
            {
                leafShape = SurfaceLeafShape.Cordate,
                leavesPerMetre = 2f,
            };
            var dense = new SurfaceVineParams
            {
                leafShape = SurfaceLeafShape.Lobed,
                leavesPerMetre = 12f,
                autumnAmount = 0.7f,
            };
            Mesh sparseMesh = SurfaceGrowthMeshBuilder.BuildVine(graph, settings, sparse);
            Mesh denseMesh = SurfaceGrowthMeshBuilder.BuildVine(graph, settings, dense);
            try
            {
                Assert.Greater(sparseMesh.vertexCount, 0);
                Assert.Greater(denseMesh.vertexCount, sparseMesh.vertexCount);
                AssertChannels(sparseMesh);
                AssertChannels(denseMesh);
            }
            finally
            {
                Object.DestroyImmediate(sparseMesh);
                Object.DestroyImmediate(denseMesh);
            }
        }

        [Test]
        public void SurfaceVineScattersRootsAndKeepsPigmentLocal()
        {
            var settings = new SurfaceGrowthSettings
            {
                mode = SurfaceGrowthMode.ProjectedSpline,
                pathCount = 6,
                coverage = 1f,
                stepLength = 0.09f,
                maxPathLength = 1.8f,
                rootSpread = 0.42f,
                guideAttraction = 0.4f,
                pathLengthVariance = 0.35f,
                branchesPerMetre = 0f,
                seed = 7310,
            };
            SurfaceGrowthGraph graph = SurfaceGrowthGraphBuilder.Build(
                settings,
                new List<Vector3>
                {
                    Vector3.zero,
                    new Vector3(0.1f, 0.9f, 0f),
                    new Vector3(-0.1f, 1.8f, 0f),
                },
                ProjectWall);

            float minimumRootY = float.MaxValue;
            float maximumRootY = float.MinValue;
            int roots = 0;
            foreach (SurfaceGrowthNode node in graph.Nodes)
            {
                if (node.parentIndex >= 0)
                {
                    continue;
                }
                roots++;
                minimumRootY = Mathf.Min(minimumRootY, node.position.y);
                maximumRootY = Mathf.Max(maximumRootY, node.position.y);
            }
            Assert.AreEqual(settings.pathCount, roots);
            Assert.Greater(maximumRootY - minimumRootY, 0.05f,
                "roots should occupy an area instead of one fixed baseline");

            var morphology = new SurfaceVineParams();
            morphology.ApplyBostonIvyPreset();
            Assert.Less(morphology.autumnAmount, 0.2f);
            Assert.AreEqual(
                SurfaceLeafPigmentPattern.EdgeAndVein,
                morphology.pigmentPattern);

            morphology.autumnAmount = 0f;
            morphology.dryAmount = 0f;
            morphology.pigmentAmount = 1f;
            morphology.youngColor = new Color(0.05f, 0.62f, 0.05f, 1f);
            morphology.matureColor = morphology.youngColor;
            morphology.edgeColor = new Color(0.34f, 0.02f, 0.12f, 1f);
            morphology.veinColor = morphology.edgeColor;
            morphology.petioleColor = morphology.edgeColor;
            Mesh mesh = SurfaceGrowthMeshBuilder.BuildVine(
                graph,
                settings,
                morphology);
            try
            {
                int green = 0;
                int pigment = 0;
                foreach (Color color in mesh.colors)
                {
                    if (color.g > 0.5f && color.r < 0.15f) green++;
                    if (color.r > 0.25f && color.g < 0.1f) pigment++;
                }
                Assert.Greater(green, 0, "leaf interiors should remain green");
                Assert.Greater(pigment, 0, "edge, vein, and petiole pigment should be present");
                AssertChannels(mesh);
            }
            finally
            {
                Object.DestroyImmediate(mesh);
            }
        }

        [Test]
        public void RhizomePatchProducesConnectedShoots()
        {
            var settings = new SurfaceGrowthSettings
            {
                mode = SurfaceGrowthMode.SurfaceCrawl,
                pathCount = 5,
                coverage = 0.75f,
                maxPathLength = 1.2f,
                branchesPerMetre = 1f,
                maxBranchDepth = 2,
                seed = 7303,
            };
            SurfaceGrowthGraph graph = SurfaceGrowthGraphBuilder.Build(
                settings,
                new List<Vector3> { Vector3.zero },
                ProjectGround);
            Mesh mesh = SurfaceGrowthMeshBuilder.BuildRhizomePatch(
                graph,
                settings,
                new RhizomePatchParams { flowerChance = 0.5f });
            try
            {
                Assert.Greater(graph.Nodes.Count, 1);
                Assert.Greater(mesh.vertexCount, 0);
                AssertChannels(mesh);
                foreach (SurfaceGrowthNode node in graph.Nodes)
                {
                    Assert.AreEqual(settings.surfaceOffset, node.position.y, 1e-5f);
                }
            }
            finally
            {
                Object.DestroyImmediate(mesh);
            }
        }

        private static void AssertChannels(Mesh mesh)
        {
            Assert.AreEqual(mesh.vertexCount, mesh.normals.Length);
            Assert.AreEqual(mesh.vertexCount, mesh.colors.Length);
            var uv0 = new List<Vector2>();
            var uv3 = new List<Vector4>();
            mesh.GetUVs(0, uv0);
            mesh.GetUVs(FoliageShaderContract.WindDataUvChannel, uv3);
            Assert.AreEqual(mesh.vertexCount, uv0.Count);
            Assert.AreEqual(mesh.vertexCount, uv3.Count);
        }

        private static bool ProjectWall(
            Vector3 candidate,
            Vector3 normalHint,
            float maximumDistance,
            out SurfacePoint point)
        {
            candidate.z = 0f;
            point = new SurfacePoint(candidate, Vector3.forward);
            return true;
        }

        private static bool ProjectGround(
            Vector3 candidate,
            Vector3 normalHint,
            float maximumDistance,
            out SurfacePoint point)
        {
            candidate.y = 0f;
            point = new SurfacePoint(candidate, Vector3.up);
            return true;
        }
    }
}
