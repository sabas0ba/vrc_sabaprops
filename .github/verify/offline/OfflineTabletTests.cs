// Behavioural checks on the tablet, executed without Unity.
//
// The *Solver.cs files are partials of their behaviours that carry no base type
// and no VRChat references, so they compile on their own against the shim in
// UnityEngineShim.cs. The partials below reach their private members without
// widening them in the shipped package. The layout and the mesh builder are
// plain C# over UnityEngine value types and are called directly.
//
// What this can assert: the summon pose, the finger press state machine and
// its hysteresis, page wrapping, the reach anchor, player cycling, the button
// grid, and that the rounded meshes are closed, finite and wound the way Unity
// expects.
//
// What it cannot: that UdonSharp compiles the behaviours, that VRChat reports
// bones where the collection layer expects them, or that the builder wires a
// scene correctly. Those stay with the VRChat world tests.
using System;
using System.Collections.Generic;
using SabaProps.Tablet.Authoring;
using SabaProps.Tablet.Editors;
using UnityEngine;

namespace SabaProps.Tablet
{
    internal sealed class CheckFailed : Exception
    {
        public CheckFailed(string message) : base(message) { }
    }

    internal static class Checks
    {
        public static int Failures;

        public static void Run(string name, Action check)
        {
            try
            {
                check();
                Console.WriteLine("ok: " + name);
            }
            catch (Exception e)
            {
                Failures++;
                Console.Error.WriteLine("FAIL: " + name + "\n      " + e.Message);
            }
        }

        public static void Require(bool condition, string message)
        {
            if (!condition)
            {
                throw new CheckFailed(message);
            }
        }

        public static void Near(float expected, float actual, float tolerance, string what)
        {
            Require(Math.Abs(expected - actual) <= tolerance, $"{what}: expected {expected}, got {actual}");
        }

        public static void Near(Vector3 expected, Vector3 actual, float tolerance, string what)
        {
            Require((expected - actual).magnitude <= tolerance, $"{what}: expected {expected}, got {actual}");
        }
    }

    internal static class OfflineTabletTests
    {
        private static int Main()
        {
            TabletController.RunChecks();
            TabletTeleport.RunChecks();
            TabletReachTrigger.RunChecks();
            LayoutChecks.Run();
            MeshChecks.Run();

            if (Checks.Failures > 0)
            {
                Console.Error.WriteLine($"\n{Checks.Failures} tablet check(s) failed");
                return 1;
            }

            Console.WriteLine("\nall tablet checks passed");
            return 0;
        }
    }

    public partial class TabletController
    {
        internal static void RunChecks()
        {
            Checks.Run("the summon pose sits level in front of the head", SummonSitsInFront);
            Checks.Run("looking up or down does not move the summon point", SummonIgnoresHeadPitch);
            Checks.Run("the summoned face points back at the user and tilts up", SummonFacesTheUser);
            Checks.Run("the handle round trip restores the body pose", HandleRoundTrip);
            Checks.Run("pages wrap in both directions", PagesWrap);
            Checks.Run("a press needs an approach from the front", PressNeedsFrontApproach);
            Checks.Run("a press fires once until the finger retracts", PressFiresOnce);
            Checks.Run("leaving the button sideways cancels the press", LeavingSidewaysCancels);
            Checks.Run("the depth is measured from the front of the zone", DepthFromFront);
            Checks.Run("auto stow respects the limit and can be disabled", AutoStow);
            Checks.Run("the fingertip extends past the distal joint", FingertipExtends);
        }

        private static void FingertipExtends()
        {
            var c = new TabletController();
            var intermediate = new Vector3(0f, 1f, 0f);
            var distal = new Vector3(0f, 1f, 0.025f);
            Checks.Near(new Vector3(0f, 1f, 0.045f), c.FingertipFromBones(intermediate, distal, 0.8f), 1e-6f, "extended");
            Checks.Near(distal, c.FingertipFromBones(intermediate, distal, 0f), 0f, "no extension");
            // A missing intermediate bone comes back as the origin; do not extrapolate from it.
            Checks.Near(distal, c.FingertipFromBones(Vector3.zero, distal, 0.8f), 0f, "missing bone");
        }

        private static void SummonSitsInFront()
        {
            var c = new TabletController();
            var head = new Vector3(1f, 1.6f, -2f);
            Vector3 p = c.SummonPosition(head, 90f, 0.5f, -0.2f);
            Checks.Near(new Vector3(1.5f, 1.4f, -2f), p, 1e-4f, "yaw 90 puts the tablet on +x");
        }

        private static void SummonIgnoresHeadPitch()
        {
            var c = new TabletController();
            Vector3 level = Quaternion.AngleAxis(30f, Vector3.up) * Vector3.forward;
            Vector3 pitched = Quaternion.AngleAxis(30f, Vector3.up) * (Quaternion.AngleAxis(-70f, Vector3.right) * Vector3.forward);
            Checks.Near(c.HorizontalYaw(level, 0f), c.HorizontalYaw(pitched, 0f), 1e-3f, "yaw");
            Checks.Near(123f, c.HorizontalYaw(Vector3.up, 123f), 0f, "straight up falls back");
        }

        private static void SummonFacesTheUser()
        {
            var c = new TabletController();
            foreach (float yaw in new[] { 0f, 45f, -120f, 179f })
            {
                Quaternion q = c.SummonRotation(yaw, 30f);
                // The face is local -Z. It must point back towards the user (against the yaw forward)
                // and, with a positive tilt, upwards.
                Vector3 face = q * -Vector3.forward;
                Vector3 forward = c.YawForward(yaw);
                Checks.Require(Vector3.Dot(face, forward) < -0.8f, $"yaw {yaw}: face does not point at the user");
                Checks.Require(face.y > 0.4f, $"yaw {yaw}: face does not tilt up ({face.y})");
                // No roll: the local right axis stays horizontal.
                Vector3 right = q * Vector3.right;
                Checks.Near(0f, right.y, 1e-4f, $"yaw {yaw}: roll");
            }
        }

        private static void HandleRoundTrip()
        {
            var c = new TabletController();
            Quaternion rotation = c.SummonRotation(37f, 20f);
            var body = new Vector3(0.3f, 1.2f, -0.7f);
            var local = new Vector3(0f, 0.12f, 0f);
            Vector3 handle = body + rotation * local;
            Checks.Near(body, c.ParentPositionFromChild(handle, rotation, local), 1e-5f, "body");
        }

        private static void PagesWrap()
        {
            var c = new TabletController();
            Checks.Require(c.WrapIndex(2, 1, 3) == 0, "forward wrap");
            Checks.Require(c.WrapIndex(0, -1, 3) == 2, "backward wrap");
            Checks.Require(c.WrapIndex(1, -7, 3) == 0, "large negative step");
            Checks.Require(c.WrapIndex(5, 1, 0) == 0, "no pages");
        }

        private const float Press = 0.7f;
        private const float Release = 0.35f;

        private static void PressNeedsFrontApproach()
        {
            var c = new TabletController();
            // Arriving deep inside (from the side or from behind) does not arm.
            Checks.Require(c.NextPokeState(PokeIdle, true, 0.9f, Press, Release) == PokeIdle, "deep arrival armed");
            // In front of the zone does not arm either.
            Checks.Require(c.NextPokeState(PokeIdle, true, -0.1f, Press, Release) == PokeIdle, "in front armed");
            int s = c.NextPokeState(PokeIdle, true, 0.1f, Press, Release);
            Checks.Require(s == PokeArmed, "entering from the front arms");
            s = c.NextPokeState(s, true, 0.8f, Press, Release);
            Checks.Require(s == PokePressed, "pushing in presses");
        }

        private static void PressFiresOnce()
        {
            var c = new TabletController();
            int presses = 0;
            int state = PokeIdle;
            // Jitter around the press depth must not produce a second press.
            float[] path = { 0.1f, 0.4f, 0.72f, 0.68f, 0.71f, 0.5f, 0.69f, 0.9f, 0.4f, 0.2f, 0.5f, 0.75f };
            foreach (float depth in path)
            {
                int next = c.NextPokeState(state, true, depth, Press, Release);
                if (state == PokeArmed && next == PokePressed)
                {
                    presses++;
                }

                state = next;
            }

            Checks.Require(presses == 2, $"expected 2 presses (one after the retract to 0.2), got {presses}");
        }

        private static void LeavingSidewaysCancels()
        {
            var c = new TabletController();
            Checks.Require(c.NextPokeState(PokePressed, false, 0.9f, Press, Release) == PokeIdle, "pressed");
            Checks.Require(c.NextPokeState(PokeArmed, false, 0.3f, Press, Release) == PokeIdle, "armed");
            Checks.Require(c.NextPokeState(PokeArmed, true, -0.01f, Press, Release) == PokeIdle, "retracted in front");
        }

        private static void DepthFromFront()
        {
            var c = new TabletController();
            var center = new Vector3(0f, 0f, -0.0175f);
            var size = new Vector3(0.06f, 0.04f, 0.025f);
            Checks.Near(0f, c.PokeDepth(new Vector3(0f, 0f, -0.03f), center, size), 1e-6f, "front face");
            Checks.Near(0.025f, c.PokeDepth(new Vector3(0f, 0f, -0.005f), center, size), 1e-6f, "cap face");
            Checks.Require(c.PokeInsideRect(new Vector3(0.029f, -0.019f, 1f), center, size), "corner inside");
            Checks.Require(!c.PokeInsideRect(new Vector3(0.031f, 0f, 0f), center, size), "outside x");
        }

        private static void AutoStow()
        {
            var c = new TabletController();
            Checks.Require(!c.ShouldAutoStow(Vector3.zero, new Vector3(3f, 0f, 0f), 4f), "inside the limit");
            Checks.Require(c.ShouldAutoStow(Vector3.zero, new Vector3(3f, 0f, 3f), 4f), "outside the limit");
            Checks.Require(!c.ShouldAutoStow(Vector3.zero, new Vector3(300f, 0f, 0f), 0f), "disabled");
        }
    }

    public partial class TabletTeleport
    {
        internal static void RunChecks()
        {
            Checks.Run("player cycling is ordered by id and wraps", PlayerCycling);
            Checks.Run("player cycling skips excluded slots", PlayerCyclingSkips);
            Checks.Run("the arrival point faces the player", ArrivalFacesThePlayer);
        }

        private static void PlayerCycling()
        {
            var t = new TabletTeleport();
            int[] ids = { 7, 2, 11, 4 };
            Checks.Require(t.NextPlayerId(ids, 4, -1, 1) == 2, "first forward");
            Checks.Require(t.NextPlayerId(ids, 4, 4, 1) == 7, "forward");
            Checks.Require(t.NextPlayerId(ids, 4, 11, 1) == 2, "forward wrap");
            Checks.Require(t.NextPlayerId(ids, 4, 2, -1) == 11, "backward wrap");
            Checks.Require(t.NextPlayerId(ids, 4, 7, -1) == 4, "backward");
            Checks.Require(t.NextPlayerId(ids, 2, 7, 1) == 2, "count limits the array");
        }

        private static void PlayerCyclingSkips()
        {
            var t = new TabletTeleport();
            Checks.Require(t.NextPlayerId(new[] { -1, -1 }, 2, 3, 1) == -1, "nobody");
            Checks.Require(t.NextPlayerId(new[] { -1, 5 }, 2, -1, -1) == 5, "only one");
            Checks.Require(t.NextPlayerId(null, 0, 3, 1) == -1, "no array");
        }

        private static void ArrivalFacesThePlayer()
        {
            var t = new TabletTeleport();
            var target = new Vector3(2f, 0f, 1f);
            Vector3 forward = Quaternion.AngleAxis(60f, Vector3.up) * (Quaternion.AngleAxis(-40f, Vector3.right) * Vector3.forward);
            Vector3 arrival = t.AroundPlayer(target, forward, 1.2f, 0);
            Checks.Near(1.2f, (arrival - target).magnitude, 1e-4f, "distance ignores pitch");
            Checks.Near(0f, arrival.y - target.y, 1e-5f, "same height");
            Vector3 look = t.FacingRotation(arrival, target) * Vector3.forward;
            Checks.Require(Vector3.Dot(look, (target - arrival).normalized) > 0.9999f, "faces the player");
            Checks.Near(-Vector3.forward, t.AroundPlayer(target, Vector3.up, 1f, 0) - target, 1e-5f, "degenerate forward");
        }
    }

    public partial class TabletReachTrigger
    {
        internal static void RunChecks()
        {
            Checks.Run("the reach anchor follows yaw but not pitch", AnchorFollowsYawOnly);
            Checks.Run("dwell fires once per visit", DwellFiresOncePerVisit);
        }

        private static void AnchorFollowsYawOnly()
        {
            var r = new TabletReachTrigger();
            var head = new Vector3(0f, 1.6f, 0f);
            var offset = new Vector3(0f, 0.2f, -0.1f);
            Vector3 yawed = Quaternion.AngleAxis(90f, Vector3.up) * Vector3.forward;
            Vector3 pitched = Quaternion.AngleAxis(90f, Vector3.up) * (Quaternion.AngleAxis(60f, Vector3.right) * Vector3.forward);
            Vector3 a = r.AnchorPosition(head, yawed, offset);
            Checks.Near(new Vector3(-0.1f, 1.8f, 0f), a, 1e-4f, "behind the head after turning right");
            Checks.Near(a, r.AnchorPosition(head, pitched, offset), 1e-4f, "pitch ignored");
            Checks.Require(r.IsWithin(a + new Vector3(0.05f, 0f, 0f), a, 0.1f), "inside");
            Checks.Require(!r.IsWithin(a + new Vector3(0.15f, 0f, 0f), a, 0.1f), "outside");
        }

        private static void DwellFiresOncePerVisit()
        {
            var r = new TabletReachTrigger();
            const float threshold = 0.5f;
            float dwell = 0f;
            int fires = 0;
            bool[] inside = { true, true, true, true, true, true, true, true, false, true, true, true };
            foreach (bool now in inside)
            {
                dwell = r.NextDwell(dwell, now, 0.2f);
                if (dwell >= threshold)
                {
                    fires++;
                    dwell = -1f;
                }
            }

            Checks.Require(fires == 2, $"expected one fire per visit, got {fires}");
        }
    }

    internal static class LayoutChecks
    {
        public static void Run()
        {
            Checks.Run("pages split by the grid capacity", PageSplit);
            Checks.Run("cells tile the grid without overlap", CellsTile);
            Checks.Run("the grid sits below the header inside the screen", GridBelowHeader);
        }

        private static void PageSplit()
        {
            int cells = TabletLayout.CellsPerPage(4, 3);
            Checks.Require(cells == 12, "cells");
            Checks.Require(TabletLayout.PageCount(0, cells) == 1, "empty page");
            Checks.Require(TabletLayout.PageCount(12, cells) == 1, "full page");
            Checks.Require(TabletLayout.PageCount(13, cells) == 2, "overflow");
            Checks.Require(TabletLayout.CellsPerPage(0, -2) == 1, "degenerate grid");
        }

        private static void CellsTile()
        {
            var grid = new Vector2(0.27f, 0.15f);
            var center = new Vector2(0f, -0.015f);
            const float spacing = 0.006f;
            Vector2 cell = TabletLayout.CellSize(grid, 4, 3, spacing);
            Checks.Near(grid.x, cell.x * 4 + spacing * 3, 1e-6f, "width");
            Checks.Near(grid.y, cell.y * 3 + spacing * 2, 1e-6f, "height");

            Vector2 first = TabletLayout.CellCenter(0, grid, center, 4, 3, spacing);
            Vector2 last = TabletLayout.CellCenter(11, grid, center, 4, 3, spacing);
            Checks.Near(center.x - grid.x * 0.5f + cell.x * 0.5f, first.x, 1e-6f, "first x");
            Checks.Near(center.y + grid.y * 0.5f - cell.y * 0.5f, first.y, 1e-6f, "first y");
            Checks.Near(center.x + grid.x * 0.5f - cell.x * 0.5f, last.x, 1e-6f, "last x");
            Checks.Near(center.y - grid.y * 0.5f + cell.y * 0.5f, last.y, 1e-6f, "last y");

            Vector2 second = TabletLayout.CellCenter(1, grid, center, 4, 3, spacing);
            Checks.Near(cell.x + spacing, second.x - first.x, 1e-6f, "column pitch");
        }

        private static void GridBelowHeader()
        {
            var body = new Vector2(0.30f, 0.21f);
            Vector2 screen = TabletLayout.ScreenSize(body, 0.011f);
            const float header = 0.03f;
            const float spacing = 0.006f;
            Vector2 grid = TabletLayout.GridSize(screen, header, spacing);
            Vector2 gridCenter = TabletLayout.GridCenter(header);
            Vector2 headerCenter = TabletLayout.HeaderCenter(screen, header);

            float gridTop = gridCenter.y + grid.y * 0.5f;
            float headerBottom = headerCenter.y - header * 0.5f;
            Checks.Require(gridTop <= headerBottom + 1e-6f, "grid overlaps the header");
            Checks.Near(screen.y * 0.5f, headerCenter.y + header * 0.5f, 1e-6f, "header touches the top");
            Checks.Require(gridCenter.y - grid.y * 0.5f >= -screen.y * 0.5f - 1e-6f, "grid leaves the screen");
        }
    }

    internal static class MeshChecks
    {
        public static void Run()
        {
            Checks.Run("the outline is counter-clockwise and inside the rectangle", OutlineIsBounded);
            Checks.Run("the rounded box is closed", RoundedBoxIsClosed);
            Checks.Run("every triangle faces along its vertex normals", TrianglesFaceOutward);
            Checks.Run("the panel faces -Z", PanelFacesFront);
            Checks.Run("degenerate sizes stay finite", DegenerateSizesStayFinite);
            Checks.Run("the mesh conversion keeps every channel", MeshConversion);
        }

        private static void OutlineIsBounded()
        {
            List<Vector2> ring = TabletMeshBuilder.Outline(0.3f, 0.2f, 0.02f, 6);
            Checks.Require(ring.Count == 4 * 7, $"point count {ring.Count}");
            float area = 0f;
            for (int i = 0; i < ring.Count; i++)
            {
                Vector2 a = ring[i];
                Vector2 b = ring[(i + 1) % ring.Count];
                area += a.x * b.y - b.x * a.y;
                Checks.Require(Math.Abs(a.x) <= 0.15f + 1e-6f && Math.Abs(a.y) <= 0.1f + 1e-6f, "point outside");
            }

            Checks.Require(area > 0f, "not counter-clockwise");
            // The rounded corners cut a little off the full rectangle, never more than the corner squares.
            float full = 0.3f * 0.2f;
            float cut = (4f - Mathf.PI) * 0.02f * 0.02f;
            Checks.Near(full - cut, area * 0.5f, 2e-5f, "area");
        }

        private static void RoundedBoxIsClosed()
        {
            TabletMeshBuilder.MeshData data = TabletMeshBuilder.RoundedBox(0.3f, 0.2f, 0.012f, 0.016f, 6);
            // Each directed edge of a closed, consistently wound surface appears exactly once,
            // and its reverse appears exactly once. Vertices are split at hard edges, so compare positions.
            var edges = new Dictionary<string, int>();
            for (int t = 0; t < data.triangles.Count; t += 3)
            {
                for (int k = 0; k < 3; k++)
                {
                    string key = Key(data.vertices[data.triangles[t + k]]) + ">" + Key(data.vertices[data.triangles[t + (k + 1) % 3]]);
                    edges.TryGetValue(key, out int count);
                    edges[key] = count + 1;
                }
            }

            foreach (KeyValuePair<string, int> edge in edges)
            {
                Checks.Require(edge.Value == 1, "edge used twice in the same direction: " + edge.Key);
                string[] ends = edge.Key.Split('>');
                Checks.Require(edges.ContainsKey(ends[1] + ">" + ends[0]), "open edge: " + edge.Key);
            }
        }

        private static void TrianglesFaceOutward()
        {
            TabletMeshBuilder.MeshData data = TabletMeshBuilder.RoundedBox(0.06f, 0.04f, 0.005f, 0.006f, 6);
            for (int t = 0; t < data.triangles.Count; t += 3)
            {
                Vector3 a = data.vertices[data.triangles[t]];
                Vector3 b = data.vertices[data.triangles[t + 1]];
                Vector3 c = data.vertices[data.triangles[t + 2]];
                // Unity draws the side that sees the vertices clockwise; that side is cross(b - a, c - a).
                Vector3 face = Vector3.Cross(b - a, c - a);
                Vector3 normal = data.normals[data.triangles[t]] + data.normals[data.triangles[t + 1]] + data.normals[data.triangles[t + 2]];
                Checks.Require(Vector3.Dot(face, normal) > 0f, $"triangle {t / 3} is wound inwards");
            }
        }

        private static void PanelFacesFront()
        {
            TabletMeshBuilder.MeshData data = TabletMeshBuilder.RoundedPanel(0.278f, 0.188f, 0.005f, 6);
            foreach (Vector3 n in data.normals)
            {
                Checks.Near(-Vector3.forward, n, 1e-6f, "normal");
            }

            Checks.Require(data.triangles.Count == (data.vertices.Count - 1) * 3, "fan triangle count");
        }

        private static void DegenerateSizesStayFinite()
        {
            foreach (TabletMeshBuilder.MeshData data in new[]
            {
                TabletMeshBuilder.RoundedBox(0.1f, 0.1f, 0.01f, 5f, 6),
                TabletMeshBuilder.RoundedBox(0.1f, 0.05f, 0.01f, 0f, 6),
                TabletMeshBuilder.RoundedBox(0f, 0f, 0f, 0f, 0),
                TabletMeshBuilder.RoundedPanel(1f, 1f, 0f, 0),
            })
            {
                foreach (Vector3 v in data.vertices)
                {
                    Checks.Require(Finite(v), "vertex " + v);
                }

                foreach (Vector3 n in data.normals)
                {
                    Checks.Require(Finite(n), "normal " + n);
                }

                Checks.Require(data.triangles.Count % 3 == 0, "triangle list");
                foreach (int i in data.triangles)
                {
                    Checks.Require(i >= 0 && i < data.vertices.Count, "index out of range");
                }
            }
        }

        private static void MeshConversion()
        {
            TabletMeshBuilder.MeshData data = TabletMeshBuilder.RoundedBox(0.1f, 0.05f, 0.01f, 0.01f, 3);
            Mesh mesh = TabletMeshBuilder.ToMesh(data, "Cap");
            Checks.Require(mesh.name == "Cap", "name");
            Checks.Require(mesh.vertexCount == data.vertices.Count, "vertices");
            Checks.Require(mesh.normals.Length == data.vertices.Count, "normals");
            Checks.Require(mesh.uv.Length == data.vertices.Count, "uv");
            Checks.Require(mesh.triangles.Length == data.triangles.Count, "triangles");
        }

        private static string Key(Vector3 v)
        {
            return Math.Round(v.x, 5) + "," + Math.Round(v.y, 5) + "," + Math.Round(v.z, 5);
        }

        private static bool Finite(Vector3 v)
        {
            return !(float.IsNaN(v.x) || float.IsNaN(v.y) || float.IsNaN(v.z)
                || float.IsInfinity(v.x) || float.IsInfinity(v.y) || float.IsInfinity(v.z));
        }
    }
}
