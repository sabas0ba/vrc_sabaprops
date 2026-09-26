// Behavioural checks on the liquid Body Canvas solver, executed without Unity.
//
// LiquidCanvasSolver.cs is a partial of LiquidBodyCanvas that carries no base
// type and no VRChat references, so it compiles on its own against the shim in
// UnityEngineShim.cs. This file is another partial of the same class, which is
// how it reaches the solver's private members without widening their
// visibility in the shipped package. It is never part of the package.
//
// What this can assert: the body frame, the mapping from the body to the
// six-face atlas, the stamp and immersion arithmetic, and that the constants
// the HLSL side restates still agree with the C# ones.
//
// What it cannot: that the shaders compile or draw what is intended, that
// UdonSharp accepts the behaviour, or that VRChat returns bones the way the
// collection layer expects. Those stay with the VRChat world tests.
//
// Usage: OfflineLiquidTests <package directory>
using System;
using System.IO;
using System.Text.RegularExpressions;
using UnityEngine;

namespace SabaProps.Liquid
{
    public partial class LiquidBodyCanvas
    {
        private static int _failures;
        private static string _packageDirectory;

        private const float Tolerance = 1e-4f;

        private static int Main(string[] args)
        {
            if (args.Length != 1 || !Directory.Exists(args[0]))
            {
                Console.Error.WriteLine("usage: OfflineLiquidTests <package directory>");
                return 2;
            }

            _packageDirectory = args[0];

            Run("an upright body gives the world axes", UprightBodyGivesWorldAxes);
            Run("the frame is orthonormal and left-handed in any pose", FrameIsOrthonormalInAnyPose);
            Run("missing bones fall back to the player orientation", MissingBonesFallBack);
            Run("a player facing sideways without legs keeps its facing", SidewaysPlayerKeepsItsFacing);

            Run("canvas rows map the box to [-1, 1]", CanvasRowsMapTheBox);
            Run("local conversion round trips", LocalConversionRoundTrips);

            Run("every face lands in its own tile", EveryFaceLandsInItsOwnTile);
            Run("atlas tiles do not overlap and keep their inset", AtlasTilesDoNotOverlap);
            Run("tile axes follow the documented mapping", TileAxesFollowTheMapping);
            Run("front and back never share a tile", FrontAndBackNeverShareATile);
            Run("face weights are non-negative and sum to one", FaceWeightsSumToOne);

            Run("stamp falloff is one at the centre and zero at the radius", StampFalloffShape);
            Run("the depth mask keeps nearby surfaces and drops distant ones", DepthMaskSeparatesSurfaces);
            Run("body regions put hair on the crown, skin on the face and hands", RegionsFollowTheBody);
            Run("evaporation encoding matches the drying time", EvaporationEncodingMatchesDryingTime);
            Run("immersion level follows the body axis", ImmersionLevelFollowsTheBodyAxis);
            Run("the film line drains down and stops at the bottom", FilmLineDrains);

            Run("shader constants agree with the solver", ShaderConstantsAgree);
            Run("degenerate input stays finite", DegenerateInputStaysFinite);

            Run("a ray hits the capsule side, caps and misses correctly", LiquidCanvasPool.RayCapsuleCases);
            Run("capsule hits lie on the surface with outward normals", LiquidCanvasPool.CapsuleHitsLieOnTheSurface);
            Run("cone samples stay inside the cone", LiquidCanvasPool.ConeSamplesStayInsideTheCone);
            Run("the hash is in [0, 1) and repeatable", LiquidCanvasPool.HashIsBoundedAndRepeatable);
            Run("player-relative coordinates round trip and follow the player", LiquidCanvasPool.PlayerLocalRoundTrips);
            Run("mannequin targets never collide with players or none", LiquidCanvasPool.MannequinTargetsAreDistinct);

            if (_failures > 0)
            {
                Console.Error.WriteLine($"\n{_failures} liquid canvas check(s) failed");
                return 1;
            }

            Console.WriteLine("\nall liquid canvas checks passed");
            return 0;
        }

        // ------------------------------------------------------------------
        // 体の座標系
        // ------------------------------------------------------------------

        private static void UprightBodyGivesWorldAxes()
        {
            var canvas = new LiquidBodyCanvas();
            Vector3 hips = new Vector3(2f, 1f, -3f);
            Vector3 up = canvas.SolveFrameUp(hips, hips + new Vector3(0f, 0.3f, 0f), Vector3.up);
            Vector3 right = canvas.SolveFrameRight(up,
                hips + new Vector3(-0.1f, -0.05f, 0f), hips + new Vector3(0.1f, -0.05f, 0f), Vector3.forward);
            Vector3 forward = canvas.SolveFrameForward(right, up);

            Require(Near(up, Vector3.up), $"up {up}");
            Require(Near(right, Vector3.right), $"right {right}");
            Require(Near(forward, Vector3.forward), $"forward {forward}");
        }

        private static void FrameIsOrthonormalInAnyPose()
        {
            var canvas = new LiquidBodyCanvas();
            var random = new System.Random(7);

            for (int i = 0; i < 500; i++)
            {
                Vector3 hips = RandomVector(random) * 5f;
                Vector3 spine = RandomVector(random);
                Vector3 lateral = RandomVector(random);
                if (spine.sqrMagnitude < 1e-3f || lateral.sqrMagnitude < 1e-3f)
                {
                    continue;
                }

                Vector3 up = canvas.SolveFrameUp(hips, hips + spine * 0.3f, Vector3.up);
                Vector3 right = canvas.SolveFrameRight(up, hips - lateral * 0.1f, hips + lateral * 0.1f, Vector3.forward);
                Vector3 forward = canvas.SolveFrameForward(right, up);

                Require(Mathf.Abs(up.magnitude - 1f) < Tolerance, $"|up| {up.magnitude}");
                Require(Mathf.Abs(right.magnitude - 1f) < Tolerance, $"|right| {right.magnitude}");
                Require(Mathf.Abs(forward.magnitude - 1f) < Tolerance, $"|forward| {forward.magnitude}");
                Require(Mathf.Abs(Vector3.Dot(up, right)) < Tolerance, "up and right are not orthogonal");
                Require(Mathf.Abs(Vector3.Dot(up, forward)) < Tolerance, "up and forward are not orthogonal");
                Require(Mathf.Abs(Vector3.Dot(right, forward)) < Tolerance, "right and forward are not orthogonal");

                // Unity の左手系: right x up = forward。符号が逆だと鏡像の座標系になり、
                // シェーダの面の向きと Canvas の中身が左右反転します。
                Require(Near(Vector3.Cross(right, up), forward), "the frame is mirrored");

                // 右方向は太腿を結ぶ向きと同じ側を向きます。
                if (Mathf.Abs(Vector3.Dot(lateral.normalized, spine.normalized)) < 0.95f)
                {
                    Require(Vector3.Dot(right, lateral) > 0f, "right points away from the right leg");
                }
            }
        }

        private static void MissingBonesFallBack()
        {
            var canvas = new LiquidBodyCanvas();
            Vector3 hips = new Vector3(0f, 1f, 0f);

            Vector3 up = canvas.SolveFrameUp(hips, Vector3.zero, Vector3.up);
            Require(Near(up, Vector3.up), $"missing chest: up {up}");

            Vector3 tilted = new Vector3(0f, 1f, 1f).normalized;
            up = canvas.SolveFrameUp(Vector3.zero, new Vector3(0f, 2f, 0f), tilted);
            Require(Near(up, tilted), $"missing hips: up {up}");

            up = canvas.SolveFrameUp(hips, hips, Vector3.zero);
            Require(Near(up, Vector3.up), $"coincident bones and no fallback: up {up}");

            Vector3 right = canvas.SolveFrameRight(Vector3.up, Vector3.zero, new Vector3(0.1f, 1f, 0f), Vector3.forward);
            Require(Near(right, Vector3.right), $"missing left leg: right {right}");

            right = canvas.SolveFrameRight(Vector3.up, new Vector3(0f, 1f, 0f), new Vector3(0f, 1.2f, 0f), Vector3.forward);
            Require(Near(right, Vector3.right), $"legs along the spine: right {right}");

            right = canvas.SolveFrameRight(Vector3.up, Vector3.zero, Vector3.zero, Vector3.up);
            Require(Mathf.Abs(right.magnitude - 1f) < Tolerance && Mathf.Abs(Vector3.Dot(right, Vector3.up)) < Tolerance,
                $"facing parallel to up: right {right}");
        }

        private static void SidewaysPlayerKeepsItsFacing()
        {
            var canvas = new LiquidBodyCanvas();
            Vector3 facing = Vector3.right;
            Vector3 up = canvas.SolveFrameUp(Vector3.zero, Vector3.zero, Vector3.up);
            Vector3 right = canvas.SolveFrameRight(up, Vector3.zero, Vector3.zero, facing);
            Vector3 forward = canvas.SolveFrameForward(right, up);
            Require(Near(forward, facing), $"forward {forward} does not follow the player facing {facing}");
        }

        // ------------------------------------------------------------------
        // Canvas 空間
        // ------------------------------------------------------------------

        private static void CanvasRowsMapTheBox()
        {
            var canvas = new LiquidBodyCanvas();
            Vector3 origin = new Vector3(1f, 2f, 3f);
            Vector3 axis = new Vector3(1f, 1f, 0f).normalized;
            Vector3 other = new Vector3(-1f, 1f, 0f).normalized;
            const float half = 0.7f;
            Vector4 row = canvas.CanvasRow(axis, origin, half);

            Require(Mathf.Abs(Apply(row, origin)) < Tolerance, "the origin is not at 0");
            Require(Mathf.Abs(Apply(row, origin + axis * half) - 1f) < Tolerance, "the positive face is not at 1");
            Require(Mathf.Abs(Apply(row, origin - axis * half) + 1f) < Tolerance, "the negative face is not at -1");
            Require(Mathf.Abs(Apply(row, origin + other * 5f)) < Tolerance, "a perpendicular offset changed the value");
        }

        private static void LocalConversionRoundTrips()
        {
            var canvas = new LiquidBodyCanvas();
            Vector3 up = new Vector3(0.2f, 1f, 0.1f).normalized;
            Vector3 right = canvas.SolveFrameRight(up, Vector3.zero, Vector3.zero, Vector3.forward);
            Vector3 forward = canvas.SolveFrameForward(right, up);
            Vector3 origin = new Vector3(-4f, 0.5f, 9f);
            Vector3 expected = new Vector3(0.3f, -0.6f, 0.25f);
            Vector3 world = origin + right * expected.x + up * expected.y + forward * expected.z;

            Vector3 local = canvas.ToCanvasLocalPoint(world, origin, right, up, forward);
            Require(Near(local, expected), $"point {local} != {expected}");

            Vector3 direction = canvas.ToCanvasLocalDirection(forward * 2f, right, up, forward);
            Require(Near(direction, new Vector3(0f, 0f, 2f)), $"direction {direction}");
        }

        // ------------------------------------------------------------------
        // アトラス
        // ------------------------------------------------------------------

        private static void EveryFaceLandsInItsOwnTile()
        {
            var canvas = new LiquidBodyCanvas();
            const float inset = 0.02f;
            var random = new System.Random(11);

            for (int face = 0; face < FaceCount; face++)
            {
                int axis = face / 2;
                float sign = face % 2 == 0 ? 1f : -1f;

                for (int i = 0; i < 200; i++)
                {
                    // 箱の面上の点。面の軸は ±1、他の 2 軸は [-1, 1] の乱数です。
                    Vector3 q = RandomVector(random);
                    q = SetAxis(q, axis, sign);
                    Vector3 normal = SetAxis(Vector3.zero, axis, sign);

                    Require(canvas.DominantAxis(normal) == axis, $"face {face}: dominant axis");
                    Require(canvas.FaceOf(axis, sign) == face, $"face {face}: FaceOf");

                    Vector2 tile = canvas.TileUv(q, axis);
                    Require(tile.x >= 0f && tile.x <= 1f && tile.y >= 0f && tile.y <= 1f, $"face {face}: tile uv {tile}");

                    Vector2 atlas = canvas.AtlasUv(face, tile, inset);
                    Require(canvas.FaceAtAtlasUv(atlas) == face, $"face {face}: atlas uv {atlas} reads back as face {canvas.FaceAtAtlasUv(atlas)}");

                    Vector2 back = canvas.TileUvAtAtlasUv(atlas, inset);
                    Require(Mathf.Abs(back.x - tile.x) < Tolerance && Mathf.Abs(back.y - tile.y) < Tolerance,
                        $"face {face}: tile uv {tile} round trips to {back}");
                }
            }
        }

        private static void AtlasTilesDoNotOverlap()
        {
            var canvas = new LiquidBodyCanvas();
            const float inset = 0.02f;
            float marginX = inset / AtlasColumns;
            float marginY = inset / AtlasRows;

            for (int face = 0; face < FaceCount; face++)
            {
                float column = face % AtlasColumns;
                float row = face / AtlasColumns;

                for (int i = 0; i <= 20; i++)
                {
                    for (int j = 0; j <= 20; j++)
                    {
                        Vector2 atlas = canvas.AtlasUv(face, new Vector2(i / 20f, j / 20f), inset);
                        float lx = atlas.x - column / AtlasColumns;
                        float ly = atlas.y - row / AtlasRows;
                        float width = 1f / AtlasColumns;
                        float height = 1f / AtlasRows;

                        Require(lx >= marginX - Tolerance && lx <= width - marginX + Tolerance,
                            $"face {face}: u {atlas.x} is inside the inset");
                        Require(ly >= marginY - Tolerance && ly <= height - marginY + Tolerance,
                            $"face {face}: v {atlas.y} is inside the inset");
                    }
                }
            }
        }

        /// <summary>
        /// 面内の軸は X 面が (z, y)、Y 面が (x, z)、Z 面が (x, y)。
        /// 往復の検査は C# 内で閉じているため、軸を入れ替えても通ってしまいます。
        /// シェーダと同じ対応であることは、ここで対応そのものを固定して確かめます。
        /// </summary>
        private static void TileAxesFollowTheMapping()
        {
            var canvas = new LiquidBodyCanvas();
            Vector3 q = new Vector3(0.2f, -0.4f, 0.6f);
            Vector2 x = canvas.TileUv(q, 0);
            Vector2 y = canvas.TileUv(q, 1);
            Vector2 z = canvas.TileUv(q, 2);

            Require(Mathf.Abs(x.x - 0.8f) < Tolerance && Mathf.Abs(x.y - 0.3f) < Tolerance, $"X face maps to {x}, expected (z, y)");
            Require(Mathf.Abs(y.x - 0.6f) < Tolerance && Mathf.Abs(y.y - 0.8f) < Tolerance, $"Y face maps to {y}, expected (x, z)");
            Require(Mathf.Abs(z.x - 0.6f) < Tolerance && Mathf.Abs(z.y - 0.3f) < Tolerance, $"Z face maps to {z}, expected (x, y)");
        }

        private static void FrontAndBackNeverShareATile()
        {
            var canvas = new LiquidBodyCanvas();
            var random = new System.Random(13);

            for (int i = 0; i < 1000; i++)
            {
                Vector3 n = RandomVector(random).normalized;
                if (n.sqrMagnitude < 0.5f)
                {
                    continue;
                }

                for (int axis = 0; axis < 3; axis++)
                {
                    float c = canvas.AxisComponent(n, axis);
                    if (Mathf.Abs(c) < 1e-6f)
                    {
                        continue;
                    }

                    int behind = canvas.FaceOf(axis, -c);
                    Require(canvas.FaceWeight(n, behind) == 0f,
                        $"normal {n} writes to face {behind}, which faces away from it");
                }
            }
        }

        private static void FaceWeightsSumToOne()
        {
            var canvas = new LiquidBodyCanvas();
            var random = new System.Random(17);

            for (int i = 0; i < 1000; i++)
            {
                Vector3 n = RandomVector(random).normalized;
                if (n.sqrMagnitude < 0.5f)
                {
                    continue;
                }

                float sum = 0f;
                for (int face = 0; face < FaceCount; face++)
                {
                    float w = canvas.FaceWeight(n, face);
                    Require(w >= 0f && w <= 1f, $"weight {w} out of range");
                    sum += w;
                }

                Require(Mathf.Abs(sum - 1f) < Tolerance, $"weights for {n} sum to {sum}");
            }

            Require(Mathf.Abs(canvas.FaceWeight(Vector3.forward, 4) - 1f) < Tolerance, "a +Z normal does not fully select +Z");
        }

        // ------------------------------------------------------------------
        // 付着と浸漬
        // ------------------------------------------------------------------

        private static void StampFalloffShape()
        {
            var canvas = new LiquidBodyCanvas();
            const float radius = 0.08f;
            Require(Mathf.Abs(canvas.StampFalloff(0f, radius) - 1f) < Tolerance, "not 1 at the centre");
            Require(canvas.StampFalloff(radius, radius) == 0f, "not 0 at the radius");
            Require(canvas.StampFalloff(radius * 3f, radius) == 0f, "not 0 beyond the radius");

            float previous = 2f;
            for (int i = 0; i <= 50; i++)
            {
                float value = canvas.StampFalloff(radius * i / 50f, radius);
                Require(value <= previous, "falloff increases with distance");
                previous = value;
            }

            Require(IsFinite(canvas.StampFalloff(0f, 0f)), "a zero radius is not finite");
        }

        /// <summary>
        /// 胴の側面（x = 0.275）と腕の外側（x = 0.49）が同じ +X タイルを共有するとき、
        /// 腕に付けた付着が胴に描かれないこと。腕の丸み（0.07 m 程度）の中では描かれること。
        /// </summary>
        private static void DepthMaskSeparatesSurfaces()
        {
            var canvas = new LiquidBodyCanvas();
            const float tolerance = 0.08f;
            const float arm = 0.49f;

            Require(canvas.DepthMask(arm, arm, 1f, tolerance) == 1f, "the surface the stamp landed on is masked");
            Require(canvas.DepthMask(arm - 0.07f, arm, 1f, tolerance) == 1f, "the curve of the arm is masked");
            Require(canvas.DepthMask(0.275f, arm, 1f, tolerance) == 0f, "the torso side shows paint put on the arm");
            Require(canvas.DepthMask(-0.35f, arm, 1f, tolerance) == 0f, "the other arm shows paint put on this arm");
            Require(canvas.DepthMask(0.275f, arm, 0f, tolerance) == 1f, "a texel with no recorded depth is masked");

            float previous = 2f;
            for (int i = 0; i <= 40; i++)
            {
                float value = canvas.DepthMask(arm - i * 0.005f, arm, 1f, tolerance);
                Require(value >= 0f && value <= 1f, $"mask {value} out of range");
                Require(value <= previous + 1e-6f, "the mask grows with distance");
                previous = value;
            }
        }

        /// <summary>
        /// 頭頂と後頭部は髪、顔の正面は肌、手は肌、胴は衣服。重みは非負で和が 1。
        /// 頭と手を無効（半径 0）にすると全身が衣服になること。
        /// </summary>
        private static void RegionsFollowTheBody()
        {
            var canvas = new LiquidBodyCanvas();
            Vector4 head = new Vector4(0f, 1.66f, 0f, 0.13f);
            Vector3 face = Vector3.forward;
            Vector3 up = Vector3.up;
            Vector4 left = new Vector4(-0.34f, 0.78f, 0f, 0.06f);
            Vector4 right = new Vector4(0.34f, 0.78f, 0f, 0.06f);

            Vector3 crown = canvas.RegionWeights(new Vector3(0f, 1.78f, 0f), head, face, up, left, right);
            Vector3 back = canvas.RegionWeights(new Vector3(0f, 1.66f, -0.11f), head, face, up, left, right);
            Vector3 front = canvas.RegionWeights(new Vector3(0f, 1.64f, 0.11f), head, face, up, left, right);
            Vector3 hand = canvas.RegionWeights(new Vector3(0.34f, 0.8f, 0.03f), head, face, up, left, right);
            Vector3 chest = canvas.RegionWeights(new Vector3(0f, 1.3f, 0.11f), head, face, up, left, right);

            Require(crown.y > 0.9f, $"the crown is not hair: {crown}");
            Require(back.y > 0.9f, $"the back of the head is not hair: {back}");
            Require(front.z > 0.9f, $"the face is not skin: {front}");
            Require(hand.z > 0.9f, $"the hand is not skin: {hand}");
            Require(chest.x > 0.99f, $"the chest is not clothing: {chest}");

            var random = new System.Random(29);
            for (int i = 0; i < 1000; i++)
            {
                Vector3 p = new Vector3(
                    (float)(random.NextDouble() - 0.5), (float)random.NextDouble() * 2f, (float)(random.NextDouble() - 0.5));
                Vector3 w = canvas.RegionWeights(p, head, face, up, left, right);
                Require(w.x >= -1e-5f && w.y >= -1e-5f && w.z >= -1e-5f, $"negative weight {w}");
                Require(Mathf.Abs(w.x + w.y + w.z - 1f) < Tolerance, $"weights {w} do not sum to one");
            }

            Vector4 none = new Vector4(0f, 0f, 0f, 0f);
            Vector3 off = canvas.RegionWeights(new Vector3(0f, 1.78f, 0f), none, face, up, none, none);
            Require(off.x == 1f, $"with regions off the crown is not clothing: {off}");
        }

        private static void EvaporationEncodingMatchesDryingTime()
        {
            var canvas = new LiquidBodyCanvas();
            const float maxRate = 0.2f;
            const float dt = 0.1f;

            foreach (float seconds in new[] { 5f, 30f, 90f, 600f })
            {
                float encoded = canvas.EncodeEvaporation(seconds, maxRate);
                Require(encoded > 0f && encoded <= 1f, $"{seconds}s encodes to {encoded}");

                // Canvas Update シェーダは amount - dt * encoded * maxRate で減らします。
                float shader = Mathf.Max(0f, 1f - dt * encoded * maxRate);
                float solver = canvas.EvaporateAmount(1f, seconds, dt);
                Require(Mathf.Abs(shader - solver) < Tolerance, $"{seconds}s: shader {shader} != solver {solver}");
            }

            Require(canvas.EncodeEvaporation(0f, maxRate) == 0f, "never drying does not encode to 0");
            Require(canvas.EncodeEvaporation(-1f, maxRate) == 0f, "a negative time does not encode to 0");
            Require(canvas.EncodeEvaporation(0.5f, maxRate) == 1f, "a drying time faster than the cap does not clamp");
            Require(canvas.EvaporateAmount(0.4f, 0f, 10f) == 0.4f, "never drying still evaporated");
            Require(canvas.EvaporateAmount(0.01f, 1f, 10f) == 0f, "evaporation went below zero");
        }

        private static void ImmersionLevelFollowsTheBodyAxis()
        {
            var canvas = new LiquidBodyCanvas();
            Vector3 origin = new Vector3(0f, 1f, 0f);
            const float half = 1.1f;

            Require(Mathf.Abs(canvas.ImmersionLevel(1f, origin, Vector3.up, half)) < Tolerance, "surface at the origin is not 0");
            Require(Mathf.Abs(canvas.ImmersionLevel(1f + half, origin, Vector3.up, half) - 1f) < Tolerance, "surface at the top is not 1");
            Require(Mathf.Abs(canvas.ImmersionLevel(1f - half, origin, Vector3.up, half) + 1f) < Tolerance, "surface at the bottom is not -1");

            // 体が傾くと、同じ液面でも体軸に沿った交点は遠くなります。
            Vector3 leaning = new Vector3(0f, 1f, 1f).normalized;
            float upright = canvas.ImmersionLevel(1.5f, origin, Vector3.up, half);
            float leant = canvas.ImmersionLevel(1.5f, origin, leaning, half);
            Require(leant > upright, "leaning does not raise the level along the body");

            Require(canvas.ImmersionLevel(2f, origin, Vector3.forward, half) == 2f, "lying below the surface is not fully immersed");
            Require(canvas.ImmersionLevel(0f, origin, Vector3.forward, half) == -2f, "lying above the surface is not dry");
        }

        private static void FilmLineDrains()
        {
            var canvas = new LiquidBodyCanvas();
            float level = 0.8f;
            for (int i = 0; i < 100; i++)
            {
                float next = canvas.DrainLevel(level, 0.2f, 0.05f, 0.1f);
                Require(next <= level, "the line rose while draining");
                Require(next >= -1f, "the line went below the bottom");
                level = next;
            }

            Require(canvas.DrainLevel(0.3f, 1f, 0.05f, 10f) == 0.3f, "a fully viscous film drained");
            Require(canvas.DrainLevel(-0.99f, 0f, 1f, 10f) == -1f, "the line did not stop at the bottom");
        }

        // ------------------------------------------------------------------
        // シェーダとの一致
        // ------------------------------------------------------------------

        private static void ShaderConstantsAgree()
        {
            string shaders = Path.Combine(_packageDirectory, "Runtime", "Shaders");
            string canvasInclude = File.ReadAllText(Path.Combine(shaders, "SabaLiquidCanvas.cginc"));
            string update = File.ReadAllText(Path.Combine(shaders, "SabaLiquidCanvasUpdate.shader"));

            Require(DefineValue(canvasInclude, "SABA_LIQUID_ATLAS_COLUMNS") == AtlasColumns, "atlas columns differ");
            Require(DefineValue(canvasInclude, "SABA_LIQUID_ATLAS_ROWS") == AtlasRows, "atlas rows differ");
            Require(DefineValue(update, "SABA_LIQUID_MAX_STAMPS") == MaxStampsPerUpdate, "stamp array length differs");

            // 面内の軸の対応。TileAxesFollowTheMapping が C# 側を固定し、ここで HLSL 側を固定します。
            string mapping = "axis == 0 ? q.zy : (axis == 1 ? q.xz : q.xy)";
            Require(canvasInclude.Contains(mapping), "SabaLiquidTileUv no longer uses the (z, y) / (x, z) / (x, y) mapping");

            // 奥行きの重み。DepthMaskSeparatesSurfaces が C# 側を固定し、ここで HLSL 側を固定します。
            string depthMask = "1.0 - smoothstep(tolerance, tolerance * 2.0, abs(surfaceDepth - storedDepth))";
            Require(canvasInclude.Contains(depthMask), "SabaLiquidDepthMask no longer fades between one and two tolerances");

            // 部位の推定。RegionsFollowTheBody が C# 側を固定し、ここで HLSL 側の閾値を固定します。
            foreach (string rule in new[]
            {
                "1.0 - smoothstep(head.w * 0.95, head.w * 1.25, distance)",
                "smoothstep(0.1, 0.5, dot(direction, face))",
                "smoothstep(0.35, 0.7, dot(direction, up))",
                "smoothstep(leftHand.w * 0.9, leftHand.w * 1.4,",
            })
            {
                Require(canvasInclude.Contains(rule), $"SabaLiquidRegionWeights no longer contains '{rule}'");
            }
        }

        private static float DefineValue(string source, string name)
        {
            Match match = Regex.Match(source, @"#define\s+" + name + @"\s+([0-9.]+)");
            Require(match.Success, $"#define {name} not found");
            return float.Parse(match.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
        }

        private static void DegenerateInputStaysFinite()
        {
            var canvas = new LiquidBodyCanvas();
            Vector3 up = canvas.SolveFrameUp(Vector3.zero, Vector3.zero, Vector3.zero);
            Vector3 right = canvas.SolveFrameRight(up, Vector3.zero, Vector3.zero, Vector3.zero);
            Vector3 forward = canvas.SolveFrameForward(right, up);
            Require(IsFinite(up) && IsFinite(right) && IsFinite(forward), "the frame is not finite");
            Require(Mathf.Abs(forward.magnitude - 1f) < Tolerance, "the frame collapsed");

            Vector4 row = canvas.CanvasRow(Vector3.right, Vector3.zero, 0f);
            Require(IsFinite(row.x) && IsFinite(row.w), "a zero half extent is not finite");
            Require(IsFinite(canvas.ImmersionLevel(1f, Vector3.zero, Vector3.up, 0f)), "a zero height is not finite");
            Require(IsFinite(canvas.FaceWeight(Vector3.zero, 0)), "a zero normal is not finite");
        }

        // ------------------------------------------------------------------
        // 補助
        // ------------------------------------------------------------------

        private static float Apply(Vector4 row, Vector3 p)
        {
            return row.x * p.x + row.y * p.y + row.z * p.z + row.w;
        }

        private static Vector3 SetAxis(Vector3 v, int axis, float value)
        {
            if (axis == 0) v.x = value;
            else if (axis == 1) v.y = value;
            else v.z = value;
            return v;
        }

        private static Vector3 RandomVector(System.Random random)
        {
            return new Vector3(
                (float)(random.NextDouble() * 2.0 - 1.0),
                (float)(random.NextDouble() * 2.0 - 1.0),
                (float)(random.NextDouble() * 2.0 - 1.0));
        }

        private static bool Near(Vector3 a, Vector3 b)
        {
            return (a - b).magnitude < Tolerance;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private static bool IsFinite(Vector3 value)
        {
            return IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
        }

        private static void Run(string name, Action check)
        {
            try
            {
                check();
                Console.WriteLine($"  ok   {name}");
            }
            catch (CheckFailed failure)
            {
                _failures++;
                Console.WriteLine($"  FAIL {name}");
                Console.WriteLine($"       {failure.Message}");
            }
            catch (Exception error)
            {
                _failures++;
                Console.WriteLine($"  FAIL {name}");
                Console.WriteLine($"       threw {error.GetType().Name}: {error.Message}");
            }
        }

        private static void Require(bool condition, string message)
        {
            if (!condition)
            {
                throw new CheckFailed(message);
            }
        }

        private sealed class CheckFailed : Exception
        {
            public CheckFailed(string message) : base(message) { }
        }

        internal static void Check(bool condition, string message)
        {
            Require(condition, message);
        }
    }

    /// <summary>
    /// Checks on the Source geometry in LiquidCanvasPoolSolver.cs. A partial of
    /// LiquidCanvasPool for the same reason the canvas checks are a partial of
    /// LiquidBodyCanvas; the entry point above runs them.
    /// </summary>
    public partial class LiquidCanvasPool
    {
        private const float Tolerance = 1e-4f;

        internal static void RayCapsuleCases()
        {
            var pool = new LiquidCanvasPool();
            Vector3 a = new Vector3(0f, 0.2f, 0f);
            Vector3 b = new Vector3(0f, 1.6f, 0f);
            const float r = 0.2f;

            // 横から胴に当たる。表面は x = -0.2 なので距離は 1.8。
            float side = pool.RayCapsule(new Vector3(-2f, 1f, 0f), Vector3.right, a, b, r);
            LiquidBodyCanvas.Check(Mathf.Abs(side - 1.8f) < Tolerance, $"side hit at {side}");

            // 真上から頭頂の半球に当たる。頭頂は y = 1.8。
            float top = pool.RayCapsule(new Vector3(0f, 3f, 0f), Vector3.down, a, b, r);
            LiquidBodyCanvas.Check(Mathf.Abs(top - 1.2f) < Tolerance, $"top hit at {top}");

            // 真下から足元の半球に当たる。底は y = 0。
            float bottom = pool.RayCapsule(new Vector3(0f, -1f, 0f), Vector3.up, a, b, r);
            LiquidBodyCanvas.Check(Mathf.Abs(bottom - 1f) < Tolerance, $"bottom hit at {bottom}");

            LiquidBodyCanvas.Check(pool.RayCapsule(new Vector3(-2f, 1f, 0.3f), Vector3.right, a, b, r) < 0f, "a ray passing beside the body hit it");
            LiquidBodyCanvas.Check(pool.RayCapsule(new Vector3(-2f, 1f, 0f), -Vector3.right, a, b, r) < 0f, "a ray pointing away hit the body");
            LiquidBodyCanvas.Check(pool.RayCapsule(new Vector3(0f, 1f, 0f), Vector3.right, a, b, r) < 0f, "a ray from inside the body hit it");
            LiquidBodyCanvas.Check(pool.RayCapsule(new Vector3(-2f, 2.5f, 0f), Vector3.right, a, b, r) < 0f, "a ray above the head hit it");

            // 軸と平行な光線（円柱の式が退化する場合）。
            float parallel = pool.RayCapsule(new Vector3(0.1f, 5f, 0f), Vector3.down, a, b, r);
            LiquidBodyCanvas.Check(parallel > 0f && IsFinite(parallel), $"a ray parallel to the axis gave {parallel}");
        }

        internal static void CapsuleHitsLieOnTheSurface()
        {
            var pool = new LiquidCanvasPool();
            var random = new System.Random(23);
            Vector3 a = new Vector3(1f, 0.25f, -2f);
            Vector3 b = new Vector3(1.2f, 1.5f, -2.1f);
            const float r = 0.25f;
            int hits = 0;

            for (int i = 0; i < 2000; i++)
            {
                Vector3 origin = a + RandomVector(random) * 4f;
                Vector3 target = Vector3.Lerp(a, b, (float)random.NextDouble()) + RandomVector(random) * 0.3f;
                Vector3 direction = (target - origin).normalized;
                float t = pool.RayCapsule(origin, direction, a, b, r);
                if (t < 0f)
                {
                    continue;
                }

                hits++;
                Vector3 point = origin + direction * t;
                float distance = DistanceToSegment(point, a, b);
                LiquidBodyCanvas.Check(Mathf.Abs(distance - r) < 1e-3f, $"hit point is {distance} from the axis, not {r}");

                Vector3 normal = pool.CapsuleNormal(point, a, b);
                LiquidBodyCanvas.Check(Mathf.Abs(normal.magnitude - 1f) < Tolerance, "normal is not unit length");
                LiquidBodyCanvas.Check(Vector3.Dot(normal, direction) <= 1e-3f, "the ray hit the far side of the body");
            }

            LiquidBodyCanvas.Check(hits > 500, $"only {hits} of 2000 aimed rays hit");
        }

        internal static void ConeSamplesStayInsideTheCone()
        {
            var pool = new LiquidCanvasPool();
            Vector3 axis = new Vector3(0.3f, -1f, 0.2f).normalized;
            const float angle = 12f;
            float cosLimit = Mathf.Cos((angle + 0.01f) * Mathf.Deg2Rad);
            float widest = 1f;

            for (int i = 0; i < 1000; i++)
            {
                Vector3 d = pool.ConeDirection(axis, angle, pool.Hash01(i * 2), pool.Hash01(i * 2 + 1));
                LiquidBodyCanvas.Check(Mathf.Abs(d.magnitude - 1f) < Tolerance, "sample is not unit length");
                float c = Vector3.Dot(d, axis);
                LiquidBodyCanvas.Check(c >= cosLimit, $"sample is {Mathf.Acos(c) * Mathf.Rad2Deg} degrees off axis");
                widest = Mathf.Min(widest, c);
            }

            LiquidBodyCanvas.Check(widest < Mathf.Cos(angle * 0.7f * Mathf.Deg2Rad), "samples never reach the edge of the cone");

            Vector3 straight = pool.ConeDirection(Vector3.up, 0f, 0.5f, 0.5f);
            LiquidBodyCanvas.Check((straight - Vector3.up).magnitude < Tolerance, "a zero-angle cone is not the axis");
        }

        internal static void HashIsBoundedAndRepeatable()
        {
            var pool = new LiquidCanvasPool();
            float sum = 0f;
            for (int i = -5000; i < 5000; i++)
            {
                float h = pool.Hash01(i);
                LiquidBodyCanvas.Check(h >= 0f && h < 1f, $"hash({i}) = {h}");
                LiquidBodyCanvas.Check(h == pool.Hash01(i), $"hash({i}) is not repeatable");
                sum += h;
            }

            float mean = sum / 10000f;
            LiquidBodyCanvas.Check(Mathf.Abs(mean - 0.5f) < 0.05f, $"hash mean is {mean}");
        }

        internal static void PlayerLocalRoundTrips()
        {
            var pool = new LiquidCanvasPool();
            Vector3 position = new Vector3(3f, 0.5f, -7f);
            Vector3 facing = new Vector3(1f, 0.4f, 1f);
            Vector3 world = new Vector3(3.2f, 1.7f, -6.6f);

            Vector3 local = pool.ToPlayerLocal(world, position, facing);
            Vector3 back = pool.FromPlayerLocal(local, position, facing);
            LiquidBodyCanvas.Check((back - world).magnitude < Tolerance, $"round trip gave {back}");

            // 同じプレイヤー基準の座標は、プレイヤーが動いて向きを変えても体の同じ所を指します。
            Vector3 moved = new Vector3(-1f, 0.5f, 2f);
            Vector3 turned = new Vector3(-1f, 0f, 0f);
            Vector3 there = pool.FromPlayerLocal(local, moved, turned);
            LiquidBodyCanvas.Check(Mathf.Abs((there - moved).magnitude - (world - position).magnitude) < Tolerance,
                "the distance from the player changed");
            LiquidBodyCanvas.Check(Mathf.Abs((there.y - moved.y) - (world.y - position.y)) < Tolerance,
                "the height above the feet changed");

            // 右手側の点は、向きを変えても右手側に残ります。
            Vector3 rightSide = pool.FromPlayerLocal(new Vector3(0.5f, 1f, 0f), Vector3.zero, Vector3.forward);
            LiquidBodyCanvas.Check(rightSide.x > 0.49f, $"+x local is not to the right of a player facing +z: {rightSide}");

            Vector3 degenerate = pool.ToPlayerLocal(world, position, Vector3.up);
            LiquidBodyCanvas.Check(IsFinite(degenerate.x) && IsFinite(degenerate.z), "facing straight up is not finite");
        }

        /// <summary>
        /// ターゲット番号は、0 以上がプレイヤー ID、-1 が無し、-2 以下がマネキンです。
        /// 命中は番号のまま同期されるため、重なると別の相手に付着が付きます。
        /// </summary>
        internal static void MannequinTargetsAreDistinct()
        {
            var pool = new LiquidCanvasPool();
            for (int index = 0; index < 1000; index++)
            {
                int target = pool.MannequinTarget(index);
                LiquidBodyCanvas.Check(target <= -2, $"mannequin {index} maps to {target}, which is a player or none");
                LiquidBodyCanvas.Check(pool.MannequinIndex(target) == index, $"mannequin {index} does not round trip");
            }

            LiquidBodyCanvas.Check(pool.MannequinIndex(-1) < 0, "none reads as a mannequin");
            for (int player = 0; player < 1000; player++)
            {
                LiquidBodyCanvas.Check(pool.MannequinIndex(player) < 0, $"player {player} reads as a mannequin");
            }
        }

        private static float DistanceToSegment(Vector3 p, Vector3 a, Vector3 b)
        {
            Vector3 ba = b - a;
            float h = Mathf.Clamp01(Vector3.Dot(p - a, ba) / Vector3.Dot(ba, ba));
            return (p - (a + ba * h)).magnitude;
        }

        private static Vector3 RandomVector(System.Random random)
        {
            return new Vector3(
                (float)(random.NextDouble() * 2.0 - 1.0),
                (float)(random.NextDouble() * 2.0 - 1.0),
                (float)(random.NextDouble() * 2.0 - 1.0));
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
