using System.Collections.Generic;
using NUnit.Framework;
using SabaProps.Foliage.Editors;
using UnityEditor;
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
        public void DirectionJitterCreatesSeededPersistentWander()
        {
            var settings = new SurfaceGrowthSettings
            {
                mode = SurfaceGrowthMode.ProjectedSpline,
                pathCount = 1,
                coverage = 1f,
                stepLength = 0.08f,
                maxPathLength = 3f,
                branchesPerMetre = 0f,
                rootSpread = 0f,
                pathLengthVariance = 0f,
                minimumSpacing = 0f,
                directionJitter = 0.62f,
                directionPersistence = 0.88f,
                guideAttraction = 0.52f,
                nodeBudget = 128,
                seed = 7321,
            };
            var guides = new List<Vector3>
            {
                Vector3.zero,
                new Vector3(0f, 3f, 0f),
            };

            SurfaceGrowthGraph first = SurfaceGrowthGraphBuilder.Build(
                settings,
                guides,
                ProjectWall);
            float minimumX = float.MaxValue;
            float maximumX = float.MinValue;
            foreach (SurfaceGrowthNode node in first.Nodes)
            {
                minimumX = Mathf.Min(minimumX, node.position.x);
                maximumX = Mathf.Max(maximumX, node.position.x);
            }
            Assert.Greater(maximumX - minimumX, 0.12f,
                "direction jitter should produce visible lateral wander");
            Assert.Less(maximumX - minimumX, 1.5f,
                "guide attraction should keep the path in a usable corridor");

            settings.seed = 7322;
            SurfaceGrowthGraph second = SurfaceGrowthGraphBuilder.Build(
                settings,
                guides,
                ProjectWall);
            int compared = Mathf.Min(first.Nodes.Count, second.Nodes.Count);
            float seedDifference = 0f;
            for (int i = 0; i < compared; i++)
            {
                seedDifference += Vector3.Distance(
                    first.Nodes[i].position,
                    second.Nodes[i].position);
            }
            Assert.Greater(seedDifference, 0.2f,
                "different seeds should produce different growth directions");

            settings.directionJitter = 0f;
            SurfaceGrowthGraph straight = SurfaceGrowthGraphBuilder.Build(
                settings,
                guides,
                ProjectWall);
            foreach (SurfaceGrowthNode node in straight.Nodes)
            {
                Assert.Less(Mathf.Abs(node.position.x), 1e-4f,
                    "zero jitter should preserve the guide direction");
            }
        }

        [Test]
        public void BranchAngleControlsLateralGrowth()
        {
            var settings = new SurfaceGrowthSettings
            {
                mode = SurfaceGrowthMode.ProjectedSpline,
                pathCount = 1,
                coverage = 1f,
                stepLength = 0.2f,
                maxPathLength = 1.2f,
                branchesPerMetre = 8f,
                maxBranchDepth = 1,
                branchLength = 0.4f,
                branchAngle = 35f,
                branchAngleJitter = 0f,
                branchLengthVariance = 0f,
                directionJitter = 0f,
                gravityBias = 0f,
                rootSpread = 0f,
                minimumSpacing = 0f,
                seed = 7311,
            };
            SurfaceGrowthGraph graph = SurfaceGrowthGraphBuilder.Build(
                settings,
                new List<Vector3>
                {
                    Vector3.zero,
                    new Vector3(0f, 1.2f, 0f),
                },
                ProjectWall);

            int firstBranch = -1;
            for (int i = 0; i < graph.Nodes.Count; i++)
            {
                if (graph.Nodes[i].branchDepth == 1)
                {
                    firstBranch = i;
                    break;
                }
            }
            Assert.Greater(firstBranch, 0, "a lateral branch should be generated");

            SurfaceGrowthNode branch = graph.Nodes[firstBranch];
            SurfaceGrowthNode parent = graph.Nodes[branch.parentIndex];
            SurfaceGrowthNode grandParent = graph.Nodes[parent.parentIndex];
            Vector3 incoming = (parent.position - grandParent.position).normalized;
            Vector3 outgoing = (branch.position - parent.position).normalized;
            Assert.AreEqual(
                settings.branchAngle,
                Vector3.Angle(incoming, outgoing),
                0.05f);
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

        [Test]
        public void AuthoringVineCrossesAdjacentFloorAndWallColliders()
        {
            GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            var vineObject = new GameObject("Multi Surface Vine Test");
            Material material = null;
            string generatedPath = string.Empty;
            try
            {
                floor.transform.position = new Vector3(0f, -0.05f, 0f);
                floor.transform.localScale = new Vector3(3f, 0.1f, 3f);
                wall.transform.position = new Vector3(0f, 1f, 1.5f);
                wall.transform.localScale = new Vector3(3f, 2f, 0.1f);
                Physics.SyncTransforms();

                SurfaceVine vine = vineObject.AddComponent<SurfaceVine>();
                vine.targetSurface = floor.GetComponent<Collider>();
                vine.additionalSurfaces.Add(wall.GetComponent<Collider>());
                vine.growth.pathCount = 1;
                vine.growth.coverage = 1f;
                vine.growth.stepLength = 0.075f;
                vine.growth.maxPathLength = 3.4f;
                vine.growth.branchesPerMetre = 0f;
                vine.growth.rootSpread = 0f;
                vine.growth.pathLengthVariance = 0f;
                vine.growth.guideAttraction = 0.88f;
                vine.growth.directionJitter = 0f;
                vine.growth.projectionDistance = 0.35f;
                vine.guidePoints = new List<Vector3>
                {
                    new Vector3(0f, 0f, 0.2f),
                    new Vector3(0f, 0f, 1.42f),
                    new Vector3(0f, 0.75f, 1.44f),
                    new Vector3(0f, 1.85f, 1.44f),
                };
                material = new Material(Shader.Find("Standard"));
                vine.material = material;

                Assert.IsTrue(SurfaceGrowthAuthoringBuilder.Build(vine));
                generatedPath = AssetDatabase.GetAssetPath(vine.generatedMesh);

                bool foundFloor = false;
                bool foundWall = false;
                for (int i = 0; i < vine.generatedGraph.Nodes.Count; i++)
                {
                    SurfaceGrowthNode node = vine.generatedGraph.Nodes[i];
                    foundFloor |= node.normal.y > 0.9f;
                    foundWall |= node.normal.z < -0.9f;
                    if (node.parentIndex >= 0)
                    {
                        float edgeLength = Vector3.Distance(
                            node.position,
                            vine.generatedGraph.Nodes[node.parentIndex].position);
                        Assert.Less(edgeLength, 0.3f,
                            "surface transition must remain a connected stem");
                    }
                }
                Assert.IsTrue(foundFloor, "floor nodes were not generated");
                Assert.IsTrue(foundWall, "wall nodes were not generated");
                Assert.Greater(vine.generatedMesh.vertexCount, 0);
            }
            finally
            {
                if (!string.IsNullOrEmpty(generatedPath))
                {
                    AssetDatabase.DeleteAsset(generatedPath);
                }
                Object.DestroyImmediate(material);
                Object.DestroyImmediate(vineObject);
                Object.DestroyImmediate(wall);
                Object.DestroyImmediate(floor);
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
            Assert.IsTrue(uv0.Exists(value => value.y <= -1f),
                "surface-grown mesh should enable one-sided wind clipping");
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
