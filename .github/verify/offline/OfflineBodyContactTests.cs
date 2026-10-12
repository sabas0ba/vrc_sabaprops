// Behavioural checks on the body contact solver, executed without Unity.
//
// BodyContactSolver.cs and BodyContactDebugViewSolver.cs are partials that carry
// no base type and no VRChat references, so they compile on their own against
// the shim in UnityEngineShim.cs. This file adds further partials of the same
// classes, which is how it reaches the solvers' private members without
// widening their visibility in the shipped package. It is never part of the
// package.
//
// What this can assert: the rules that decide who gives way, the penetration
// and separation geometry, the pass-through state machine, the response
// smoothing, and -- by stepping two simulated clients against each other with
// a network delay -- that the response settles instead of oscillating.
//
// What it cannot: that UdonSharp will compile the behaviour, that VRChat
// returns the bone positions the collection layer expects, how TeleportTo
// feels in a headset, or how real remote interpolation differs from the fixed
// delay modelled here. Those stay with the VRChat world tests and with the
// manual procedure in the package README.
using System;
using UnityEngine;

namespace SabaProps.BodyContact
{
    public partial class BodyContactSystem
    {
        private const float EyeHeight = 1.6f;
        private const float Tolerance = 0.03f;
        private const float FrameTime = 1f / 90f;
        private const float ResponseTime = 0.03f;

        private static int _failures;

        private static int Main()
        {
            Run("the standard body is finite, mirrored and fully populated", StandardBodyIsWellFormed);
            Run("missing joints remove only the parts and probes that need them", MissingJointsRemoveTheirParts);

            Run("a limb gives way to a core part and nothing else", YieldRulesMatchTheTable);
            Run("the core shares add up and the mover gives way", CoreSharesAddUpAndTheMoverYields);

            Run("the closest point is clamped to the segment", ClosestPointIsClamped);
            Run("shallow penetration is tolerated", ShallowPenetrationIsTolerated);
            Run("separation is horizontal and ignores vertical contact", SeparationIsHorizontal);
            Run("accumulating the same correction twice changes nothing", AccumulationDoesNotDoubleCount);

            Run("a hand pushed into a static torso backs its owner out", HandIntoStaticTorsoBacksOut);
            Run("desktop ignores the hands", DesktopIgnoresTheHands);
            Run("a remote limb in the local torso moves nobody", RemoteLimbDoesNotPush);

            Run("pass-through opens on overlap, on timeout, and closes on separation", PassThroughStateMachine);

            Run("the response does not depend on the frame rate", ResponseIsFrameRateIndependent);
            Run("a step is limited to the maximum speed", StepIsLimited);

            Run("two standing players settle without reversing, with latency", StandingPlayersSettleWithLatency);
            Run("a walker is held back and the standing player barely moves", WalkerYieldsToStandingPlayer);

            BodyContactDebugView.RunChecks();

            if (_failures > 0)
            {
                Console.Error.WriteLine($"\n{_failures} body contact check(s) failed");
                return 1;
            }

            Console.WriteLine("\nall body contact checks passed");
            return 0;
        }

        // ------------------------------------------------------------------
        // Body shape
        // ------------------------------------------------------------------

        private static void StandardBodyIsWellFormed()
        {
            var system = new BodyContactSystem();
            for (int joint = 0; joint < JointCount; joint++)
            {
                Vector3 position = system.StandardJoint(joint);
                Require(IsFinite(position), $"joint {joint} is not finite");
                Require(position.y > 0f && position.y <= 1f, $"joint {joint} is outside the unit eye height");
            }

            int[] left = { JointLeftShoulder, JointLeftElbow, JointLeftWrist, JointLeftHip, JointLeftKnee, JointLeftAnkle };
            int[] right = { JointRightShoulder, JointRightElbow, JointRightWrist, JointRightHip, JointRightKnee, JointRightAnkle };
            for (int i = 0; i < left.Length; i++)
            {
                Vector3 l = system.StandardJoint(left[i]);
                Vector3 r = system.StandardJoint(right[i]);
                Require(l.x < 0f && Near(l.x, -r.x) && Near(l.y, r.y) && Near(l.z, r.z), $"joints {left[i]} and {right[i]} are not mirrored");
            }

            Body body = Body.Standing(system, Vector3.zero);
            for (int part = 0; part < PartCount; part++)
            {
                Require(body.PartRadii[part] > 0f, $"part {part} has no radius");
                Require(IsFinite(body.PartA[part]) && IsFinite(body.PartB[part]), $"part {part} is not finite");
            }

            for (int probe = 0; probe < ProbeCount; probe++)
            {
                Require(body.ProbeRadii[probe] > 0f, $"probe {probe} has no radius");
            }

            // The chest probe has to sit inside the torso capsule of the same body,
            // or two players would be judged as apart while their chests overlap.
            Vector3 onAxis = system.ClosestPointOnSegment(body.PartA[PartTorso], body.PartB[PartTorso], body.ProbePositions[ProbeChest]);
            Require((onAxis - body.ProbePositions[ProbeChest]).magnitude < 1e-4f, "the chest probe is off the torso axis");
        }

        private static void MissingJointsRemoveTheirParts()
        {
            var system = new BodyContactSystem();
            Body body = Body.Standing(system, Vector3.zero);
            body.JointValid[JointLeftElbow] = false;
            body.JointValid[JointRightWrist] = false;
            body.Rebuild(system);

            Require(body.PartRadii[PartLeftUpperArm] == 0f, "the left upper arm survived a missing elbow");
            Require(body.PartRadii[PartLeftForearm] == 0f, "the left forearm survived a missing elbow");
            Require(body.PartRadii[PartRightForearm] == 0f, "the right forearm survived a missing wrist");
            Require(body.PartRadii[PartRightUpperArm] > 0f, "the right upper arm was removed");
            Require(body.PartRadii[PartTorso] > 0f, "the torso was removed");
            Require(body.ProbeRadii[ProbeRightHand] == 0f, "the right hand probe survived a missing wrist");
            Require(body.ProbeRadii[ProbeLeftHand] > 0f, "the left hand probe was removed");
        }

        // ------------------------------------------------------------------
        // Rules
        // ------------------------------------------------------------------

        private static void YieldRulesMatchTheTable()
        {
            var system = new BodyContactSystem();

            Require(system.YieldWeight(true, false, false, 0.2f) == 1f, "a limb in a core part must give way fully");
            Require(system.YieldWeight(true, true, false, 0.5f) == 0f, "limb against limb must not react");
            Require(system.YieldWeight(false, true, false, 0.5f) == 0f, "a core probe must not give way to a remote limb");
            Require(Near(system.YieldWeight(false, false, false, 0.3f), 0.3f), "core against core must use the share");
            Require(system.YieldWeight(false, false, true, 0.3f) == 1f, "a static target never moves, so the local player takes it all");
            Require(system.YieldWeight(false, true, true, 0.3f) == 0f, "a static limb is still a limb");

            for (int part = 0; part < PartCount; part++)
            {
                bool expected = part != PartHead && part != PartTorso;
                Require(system.PartIsLimb(part) == expected, $"part {part} is in the wrong class");
            }

            for (int probe = 0; probe < ProbeCount; probe++)
            {
                bool expected = probe >= CoreProbeCount;
                Require(system.ProbeIsLimb(probe) == expected, $"probe {probe} is in the wrong class");
            }
        }

        private static void CoreSharesAddUpAndTheMoverYields()
        {
            var system = new BodyContactSystem();
            float[] speeds = { 0f, 0.01f, 0.5f, 1.4f, 4f };
            foreach (float a in speeds)
            {
                foreach (float b in speeds)
                {
                    float mine = system.CoreYieldShare(a, b);
                    float theirs = system.CoreYieldShare(b, a);
                    Require(Near(mine + theirs, 1f), $"shares for {a} and {b} do not add up to 1");
                    Require(mine > 0f && mine < 1f, $"share for {a} against {b} is out of range");
                }
            }

            Require(Near(system.CoreYieldShare(0f, 0f), 0.5f), "two standing players must split evenly");
            Require(system.CoreYieldShare(1.4f, 0f) > 0.95f, "a walker must take nearly all of it");
            Require(system.CoreYieldShare(0f, 1.4f) < 0.05f, "a standing player must take nearly none of it");
        }

        // ------------------------------------------------------------------
        // Geometry
        // ------------------------------------------------------------------

        private static void ClosestPointIsClamped()
        {
            var system = new BodyContactSystem();
            Vector3 a = new Vector3(0f, 0f, 0f);
            Vector3 b = new Vector3(0f, 1f, 0f);

            Require(Near(system.ClosestPointOnSegment(a, b, new Vector3(1f, 0.25f, 0f)), new Vector3(0f, 0.25f, 0f)), "interior");
            Require(Near(system.ClosestPointOnSegment(a, b, new Vector3(1f, -3f, 0f)), a), "below the start");
            Require(Near(system.ClosestPointOnSegment(a, b, new Vector3(1f, 5f, 0f)), b), "above the end");
            Require(Near(system.ClosestPointOnSegment(a, a, new Vector3(1f, 5f, 0f)), a), "degenerate segment");
        }

        private static void ShallowPenetrationIsTolerated()
        {
            var system = new BodyContactSystem();
            Vector3 axisPoint = Vector3.zero;

            float apart = system.PenetrationDepth(new Vector3(0.5f, 0f, 0f), 0.1f, axisPoint, 0.1f);
            Require(apart < 0f, "separated shapes must report a negative depth");

            float depth = system.PenetrationDepth(new Vector3(0.15f, 0f, 0f), 0.1f, axisPoint, 0.1f);
            Require(Near(depth, 0.05f), $"expected 0.05 of depth, got {depth}");

            Require(system.EffectiveDepth(0.02f, 0.03f) == 0f, "depth inside the tolerance must be ignored");
            Require(Near(system.EffectiveDepth(0.05f, 0.03f), 0.02f), "only the excess must remain");
            Require(Near(system.EffectiveDepth(0.05f, -1f), 0.05f), "a negative tolerance must act as zero");
        }

        private static void SeparationIsHorizontal()
        {
            var system = new BodyContactSystem();
            Vector3 fallback = new Vector3(0f, 0f, -1f);

            Vector3 level = system.SeparationPerDepth(new Vector3(0.2f, 1f, 0f), new Vector3(0f, 1f, 0f), fallback);
            Require(Near(level, new Vector3(1f, 0f, 0f)), $"a level contact must give a unit horizontal direction, got {level}");

            Vector3 above = system.SeparationPerDepth(new Vector3(0.01f, 1.2f, 0f), new Vector3(0f, 1f, 0f), fallback);
            Require(above.sqrMagnitude == 0f, "contact from above cannot be resolved by moving sideways and must be ignored");

            // 45 degrees: the horizontal share is about 0.707, so the gain is its inverse.
            Vector3 diagonal = system.SeparationPerDepth(new Vector3(0.1f, 1.1f, 0f), new Vector3(0f, 1f, 0f), fallback);
            Require(diagonal.y == 0f, "the result must stay horizontal");
            Require(Near(diagonal.magnitude, 1.41421f, 1e-3f), $"expected a gain of sqrt(2), got {diagonal.magnitude}");

            // Just above the cut-off the gain is capped, so one frame can never move further than twice the depth.
            Vector3 steep = system.SeparationPerDepth(new Vector3(0.035f, 1.1f, 0f), new Vector3(0f, 1f, 0f), fallback);
            Require(steep.magnitude <= 2f + 1e-4f, $"the gain must be capped at 2, got {steep.magnitude}");

            Vector3 centred = system.SeparationPerDepth(new Vector3(0f, 1f, 0f), new Vector3(0f, 1f, 0f), fallback);
            Require(Near(centred, fallback), "a probe on the axis must use the fallback direction");

            Vector3 nowhere = system.SeparationPerDepth(Vector3.zero, Vector3.zero, Vector3.zero);
            Require(IsFinite(nowhere) && nowhere.sqrMagnitude == 0f, "no direction at all must give zero, not NaN");
        }

        private static void AccumulationDoesNotDoubleCount()
        {
            var system = new BodyContactSystem();
            Vector3 push = new Vector3(0.04f, 0f, 0f);

            Vector3 once = system.AccumulateSeparation(Vector3.zero, push);
            Vector3 twice = system.AccumulateSeparation(once, push);
            Require(Near(once, push) && Near(twice, push), "the same correction twice must equal it once");

            Vector3 smaller = system.AccumulateSeparation(once, push * 0.5f);
            Require(Near(smaller, push), "a smaller correction in the same direction is already covered");

            Vector3 larger = system.AccumulateSeparation(once, push * 2f);
            Require(Near(larger, push * 2f), "a larger correction in the same direction must extend to it");

            Vector3 sideways = system.AccumulateSeparation(once, new Vector3(0f, 0f, 0.03f));
            Require(Near(sideways, new Vector3(0.04f, 0f, 0.03f)), "an orthogonal correction must add in full");

            Vector3 nothing = system.AccumulateSeparation(once, Vector3.zero);
            Require(Near(nothing, once), "a zero correction must change nothing");
        }

        // ------------------------------------------------------------------
        // One body against another
        // ------------------------------------------------------------------

        private static void HandIntoStaticTorsoBacksOut()
        {
            var system = new BodyContactSystem();
            Body target = Body.Standing(system, Vector3.zero);

            // The player stands in front of the target and reaches into its torso.
            // The hand keeps its place relative to the player's body, as a real
            // hand does when TeleportTo moves the play space.
            Body player = Body.Standing(system, new Vector3(0f, 0f, -0.7f));
            player.Joints[JointRightWrist] = new Vector3(0f, 1.15f, -0.08f);
            player.Rebuild(system);

            float previousDepth = float.MaxValue;
            for (int frame = 0; frame < 180; frame++)
            {
                var depths = new float[ProbeCount];
                var contacts = new Vector3[ProbeCount];
                Vector3 separation = system.SolveBody(
                    player.ProbePositions, player.ProbeRadii, ProbeCount,
                    target.PartA, target.PartB, target.PartRadii, 0,
                    true, 1f, Tolerance, new Vector3(0f, 0f, -1f),
                    Vector3.zero, depths, contacts);

                float depth = depths[ProbeRightHand];
                Require(depth <= previousDepth + 1e-5f, $"frame {frame}: the hand went deeper ({previousDepth} to {depth})");
                Require(separation.z <= 1e-6f, $"frame {frame}: the player was pulled towards the target");
                Require(Mathf.Abs(separation.y) < 1e-6f, $"frame {frame}: the separation left the horizontal plane");
                previousDepth = depth;

                Vector3 step = system.StepTowards(separation, system.ResponseFactor(ResponseTime, FrameTime), 6f * FrameTime);
                player.Translate(system, step);
            }

            Require(previousDepth <= Tolerance + 1e-3f, $"the hand is still {previousDepth} deep");
            Require(previousDepth >= Tolerance - 1e-3f, $"the player was pushed past the tolerance, to {previousDepth}");
        }

        private static void DesktopIgnoresTheHands()
        {
            var system = new BodyContactSystem();
            Body target = Body.Standing(system, Vector3.zero);
            Body player = Body.Standing(system, new Vector3(0f, 0f, -0.7f));
            player.Joints[JointRightWrist] = new Vector3(0f, 1.15f, -0.08f);
            player.Rebuild(system);

            Vector3 separation = Solve(system, player, target, CoreProbeCount, false, 0.5f);
            Require(separation.sqrMagnitude == 0f, "with core probes only, a hand in a torso must not move the player");

            Vector3 withLimbs = Solve(system, player, target, ProbeCount, false, 0.5f);
            Require(withLimbs.sqrMagnitude > 0f, "with limb probes, the same hand must move the player");
        }

        private static void RemoteLimbDoesNotPush()
        {
            var system = new BodyContactSystem();
            Body player = Body.Standing(system, Vector3.zero);

            // The remote player reaches into the local torso. Seen from the local
            // client that is a remote forearm inside the local chest.
            Body remote = Body.Standing(system, new Vector3(0f, 0f, 0.7f));
            remote.Joints[JointRightElbow] = new Vector3(0f, 1.2f, 0.3f);
            remote.Joints[JointRightWrist] = new Vector3(0f, 1.2f, 0f);
            remote.Rebuild(system);

            Vector3 separation = Solve(system, player, remote, ProbeCount, false, 0.5f);
            Require(separation.sqrMagnitude == 0f, "being reached into must not move the player who is reached into");

            // And the other way round, on the remote player's own client, it does.
            Vector3 theirs = Solve(system, remote, player, ProbeCount, false, 0.5f);
            Require(theirs.z > 0f, "the player who reaches in must be the one who backs away");
        }

        // ------------------------------------------------------------------
        // Pass-through
        // ------------------------------------------------------------------

        private static void PassThroughStateMachine()
        {
            var system = new BodyContactSystem();

            Require(!system.NextArmed(false, true, 0f, 2.5f), "an overlap that was never armed must stay passable");
            Require(system.NextArmed(false, false, 0f, 2.5f), "separation must arm the contact");
            Require(system.NextArmed(true, true, 1f, 2.5f), "a short contact must stay armed");
            Require(!system.NextArmed(true, true, 2.5f, 2.5f), "a contact held for the full time must open");
            Require(system.NextArmed(true, false, 9f, 2.5f), "no contact must stay armed whatever the timer says");
            Require(system.NextArmed(true, true, 99f, 0f), "a zero timeout must disable the pass-through");

            Body a = Body.Standing(system, Vector3.zero);
            Body apart = Body.Standing(system, new Vector3(0f, 0f, 1f));
            Body overlapping = Body.Standing(system, new Vector3(0f, 0f, 0.1f));
            Require(
                system.CoreContactDepth(a.ProbePositions, a.ProbeRadii, apart.PartA, apart.PartB, apart.PartRadii, 0) <= 0f,
                "players a metre apart are not in core contact");
            Require(
                system.CoreContactDepth(a.ProbePositions, a.ProbeRadii, overlapping.PartA, overlapping.PartB, overlapping.PartRadii, 0) > 0f,
                "players 10 cm apart are in core contact");

            // A hand alone is not core contact, so reaching out does not start the timer.
            a.Joints[JointRightWrist] = apart.Joints[JointHips];
            a.Rebuild(system);
            Require(
                system.CoreContactDepth(a.ProbePositions, a.ProbeRadii, apart.PartA, apart.PartB, apart.PartRadii, 0) <= 0f,
                "a hand in a torso must not count as core contact");
        }

        // ------------------------------------------------------------------
        // Response
        // ------------------------------------------------------------------

        private static void ResponseIsFrameRateIndependent()
        {
            var system = new BodyContactSystem();
            const float tau = 0.05f;

            float whole = 1f - system.ResponseFactor(tau, 1f / 30f);
            float split = 1f;
            for (int i = 0; i < 4; i++)
            {
                split *= 1f - system.ResponseFactor(tau, 1f / 120f);
            }

            Require(Near(whole, split, 1e-5f), $"one 30 Hz frame left {whole}, four 120 Hz frames left {split}");
            Require(system.ResponseFactor(tau, 0f) == 0f, "no time must mean no response");
            Require(system.ResponseFactor(0f, 0.01f) == 1f, "a zero time constant must respond at once");
        }

        private static void StepIsLimited()
        {
            var system = new BodyContactSystem();
            Vector3 separation = new Vector3(0.3f, 0f, 0.4f);

            Vector3 free = system.StepTowards(separation, 0.5f, 1f);
            Require(Near(free, separation * 0.5f), "below the limit the step is the scaled separation");

            Vector3 limited = system.StepTowards(separation, 1f, 0.1f);
            Require(Near(limited.magnitude, 0.1f), $"the step must be clipped to 0.1, got {limited.magnitude}");
            Require(Near(limited.normalized, separation.normalized), "clipping must keep the direction");

            Require(system.StepTowards(Vector3.zero, 1f, 0.1f).sqrMagnitude == 0f, "nothing to do must stay nothing");
            Require(IsFinite(system.StepTowards(separation, 1f, 0f)), "a zero limit must not produce NaN");
        }

        // ------------------------------------------------------------------
        // Two clients
        // ------------------------------------------------------------------

        // Each client sees the other player where they were `delay` frames ago,
        // which is the part of VRChat's behaviour that could make two symmetric
        // corrections chase each other. The response has no attractive term: a
        // client only ever moves away from where it believes the other is. So
        // stale data can make both back off further than needed, but it cannot
        // pull them together again, and that is what "no reversal" asserts.
        private static void StandingPlayersSettleWithLatency()
        {
            foreach (int delay in new[] { 0, 5, 9, 18 })
            {
                var system = new BodyContactSystem();
                var simulation = new TwoClients(system, new Vector3(-0.06f, 0f, 0f), new Vector3(0.06f, 0f, 0f), delay);
                float initialExcess = simulation.CoreDepth() - Tolerance;

                for (int frame = 0; frame < 450; frame++)
                {
                    simulation.Step(Vector3.zero, Vector3.zero);
                }

                Require(simulation.ReversalsA == 0 && simulation.ReversalsB == 0,
                    $"delay {delay}: the correction reversed ({simulation.ReversalsA}, {simulation.ReversalsB} times)");
                Require(simulation.MovedLastFrame < 1e-5f, $"delay {delay}: the players are still moving after 5 s");

                float depth = simulation.CoreDepth();
                Require(depth <= Tolerance + 1e-3f, $"delay {delay}: the players are still {depth} deep");

                // With no delay the two halves meet exactly at the tolerance. With
                // delay, each client keeps correcting against a stale position, so
                // each can resolve up to the whole overlap on its own before it
                // sees that the other has moved too. The share only sets how fast a
                // client moves, not where it stops. The extra gap is therefore
                // bounded by the overlap they started with, and that is the bound
                // asserted here: it is a property of the method, not a tuning target.
                float gap = Tolerance - depth;
                Require(gap <= initialExcess + 1e-3f,
                    $"delay {delay}: the players overshot by {gap} m, more than the {initialExcess} m they started with");
                if (delay == 0)
                {
                    Require(gap < 5e-3f, $"with no delay the players must stop at the tolerance, not {gap} m beyond it");
                }

                Require(Mathf.Abs(simulation.RootA.x + simulation.RootB.x) < 1e-3f,
                    $"delay {delay}: two standing players did not split the correction evenly");
            }
        }

        private static void WalkerYieldsToStandingPlayer()
        {
            var system = new BodyContactSystem();
            var simulation = new TwoClients(system, new Vector3(-1.2f, 0f, 0f), Vector3.zero, 9);
            Vector3 walk = new Vector3(1.4f * FrameTime, 0f, 0f);

            // Two seconds, which is less than the default pass-through time.
            for (int frame = 0; frame < 180; frame++)
            {
                simulation.Step(walk, Vector3.zero);
            }

            Require(simulation.RootA.x < simulation.RootB.x, "the walker passed through the standing player");
            Require(simulation.RootB.x < 0.15f, $"the standing player was shoved {simulation.RootB.x} m");

            // The walker keeps pressing, so it sits at the depth where the
            // response cancels the walking speed: about speed * time constant.
            float depth = simulation.CoreDepth();
            Require(depth < Tolerance + 1.4f * ResponseTime * 1.5f, $"the walker sank {depth} m into the standing player");
        }

        private sealed class TwoClients
        {
            private readonly BodyContactSystem _system;
            private readonly int _delay;
            private readonly Vector3[] _historyA;
            private readonly Vector3[] _historyB;
            private int _frame;
            private Vector3 _lastStepA;
            private Vector3 _lastStepB;
            private float _speedA;
            private float _speedB;
            private float _delayedSpeedA;
            private float _delayedSpeedB;

            public Vector3 RootA;
            public Vector3 RootB;
            public int ReversalsA;
            public int ReversalsB;
            public float MovedLastFrame;

            public TwoClients(BodyContactSystem system, Vector3 rootA, Vector3 rootB, int delay)
            {
                _system = system;
                _delay = delay;
                RootA = rootA;
                RootB = rootB;
                _historyA = new Vector3[delay + 1];
                _historyB = new Vector3[delay + 1];
                for (int i = 0; i <= delay; i++)
                {
                    _historyA[i] = rootA;
                    _historyB[i] = rootB;
                }
            }

            public void Step(Vector3 inputA, Vector3 inputB)
            {
                int slot = _frame % (_delay + 1);
                Vector3 seenA = _historyA[slot];
                Vector3 seenB = _historyB[slot];

                RootA += inputA;
                RootB += inputB;

                Body a = Body.Standing(_system, RootA);
                Body b = Body.Standing(_system, RootB);
                Body staleA = Body.Standing(_system, seenA);
                Body staleB = Body.Standing(_system, seenB);

                float inputSpeedA = inputA.magnitude / FrameTime;
                float inputSpeedB = inputB.magnitude / FrameTime;
                float factor = _system.ResponseFactor(ResponseTime, FrameTime);

                Vector3 separationA = Solve(_system, a, staleB, CoreProbeCount, false, _system.CoreYieldShare(inputSpeedA, _delayedSpeedB));
                Vector3 separationB = Solve(_system, b, staleA, CoreProbeCount, false, _system.CoreYieldShare(inputSpeedB, _delayedSpeedA));
                Vector3 stepA = _system.StepTowards(separationA, factor, 6f * FrameTime);
                Vector3 stepB = _system.StepTowards(separationB, factor, 6f * FrameTime);

                if (stepA.sqrMagnitude > 0f)
                {
                    if (Vector3.Dot(stepA, _lastStepA) < 0f) ReversalsA++;
                    _lastStepA = stepA;
                }

                if (stepB.sqrMagnitude > 0f)
                {
                    if (Vector3.Dot(stepB, _lastStepB) < 0f) ReversalsB++;
                    _lastStepB = stepB;
                }

                RootA += stepA;
                RootB += stepB;
                MovedLastFrame = stepA.magnitude + stepB.magnitude;

                // The speed a client reports for the other player is as stale as their position.
                _delayedSpeedA = _speedA;
                _delayedSpeedB = _speedB;
                _speedA = inputSpeedA;
                _speedB = inputSpeedB;

                _historyA[slot] = RootA;
                _historyB[slot] = RootB;
                _frame++;
            }

            public float CoreDepth()
            {
                Body a = Body.Standing(_system, RootA);
                Body b = Body.Standing(_system, RootB);
                return _system.CoreContactDepth(a.ProbePositions, a.ProbeRadii, b.PartA, b.PartB, b.PartRadii, 0);
            }
        }

        // ------------------------------------------------------------------
        // Helpers
        // ------------------------------------------------------------------

        private static Vector3 Solve(
            BodyContactSystem system, Body player, Body target, int probeCount, bool targetIsStatic, float share)
        {
            Vector3 fallback = player.Joints[JointHips] - target.Joints[JointHips];
            return system.SolveBody(
                player.ProbePositions, player.ProbeRadii, probeCount,
                target.PartA, target.PartB, target.PartRadii, 0,
                targetIsStatic, share, Tolerance, fallback,
                Vector3.zero, new float[ProbeCount], new Vector3[ProbeCount]);
        }

        /// <summary>One body in both of its roles: the parts others collide with and the probes it collides with.</summary>
        private sealed class Body
        {
            public readonly Vector3[] Joints = new Vector3[JointCount];
            public readonly bool[] JointValid = new bool[JointCount];
            public readonly Vector3[] PartA = new Vector3[PartCount];
            public readonly Vector3[] PartB = new Vector3[PartCount];
            public readonly float[] PartRadii = new float[PartCount];
            public readonly Vector3[] ProbePositions = new Vector3[ProbeCount];
            public readonly float[] ProbeRadii = new float[ProbeCount];

            public static Body Standing(BodyContactSystem system, Vector3 root)
            {
                var body = new Body();
                system.FillStandardJoints(root, Quaternion.identity, EyeHeight, body.Joints, body.JointValid);
                body.Rebuild(system);
                return body;
            }

            public void Rebuild(BodyContactSystem system)
            {
                system.FillParts(Joints, JointValid, EyeHeight, 1f, PartA, PartB, PartRadii, 0);
                system.FillProbes(Joints, JointValid, EyeHeight, 1f, ProbePositions, ProbeRadii);
            }

            public void Translate(BodyContactSystem system, Vector3 offset)
            {
                for (int joint = 0; joint < JointCount; joint++)
                {
                    Joints[joint] += offset;
                }

                Rebuild(system);
            }
        }

        private static bool Near(float a, float b, float epsilon = 1e-4f)
        {
            return Math.Abs(a - b) <= epsilon;
        }

        private static bool Near(Vector3 a, Vector3 b, float epsilon = 1e-4f)
        {
            return (a - b).magnitude <= epsilon;
        }

        private static bool IsFinite(Vector3 value)
        {
            return !(float.IsNaN(value.x) || float.IsNaN(value.y) || float.IsNaN(value.z)
                || float.IsInfinity(value.x) || float.IsInfinity(value.y) || float.IsInfinity(value.z));
        }

        internal static void Run(string name, Action check)
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

        internal static void Require(bool condition, string message)
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
    }

    public partial class BodyContactDebugView
    {
        internal static void RunChecks()
        {
            BodyContactSystem.Run("the debug circle basis is orthonormal for any axis", PerpendicularIsOrthonormal);
            BodyContactSystem.Run("the debug circle closes and keeps its radius", CircleClosesAndKeepsItsRadius);
        }

        private static void PerpendicularIsOrthonormal()
        {
            var view = new BodyContactDebugView();
            Vector3[] axes =
            {
                new Vector3(0f, 1f, 0f),
                new Vector3(0f, -0.4f, 0f),
                new Vector3(1f, 0f, 0f),
                new Vector3(0.3f, 0.2f, -0.7f),
                new Vector3(0.001f, 2f, 0.001f),
            };

            foreach (Vector3 axis in axes)
            {
                Vector3 side = view.Perpendicular(axis);
                BodyContactSystem.Require(Math.Abs(side.magnitude - 1f) < 1e-4f, $"not unit length for {axis}");
                BodyContactSystem.Require(Math.Abs(Vector3.Dot(side, axis.normalized)) < 1e-4f, $"not perpendicular to {axis}");
            }

            Vector3 degenerate = view.Perpendicular(Vector3.zero);
            BodyContactSystem.Require(Math.Abs(degenerate.magnitude - 1f) < 1e-4f, "a zero axis must still give a unit vector");
        }

        private static void CircleClosesAndKeepsItsRadius()
        {
            var view = new BodyContactDebugView();
            Vector3 center = new Vector3(1f, 2f, 3f);
            Vector3 u = Vector3.right;
            Vector3 v = Vector3.forward;

            for (int i = 0; i <= CircleSegments; i++)
            {
                Vector3 point = view.CirclePoint(center, u, v, 0.25f, i);
                BodyContactSystem.Require(Math.Abs((point - center).magnitude - 0.25f) < 1e-4f, $"point {i} is off the circle");
                BodyContactSystem.Require(Math.Abs(point.y - center.y) < 1e-5f, $"point {i} left the plane");
            }

            Vector3 first = view.CirclePoint(center, u, v, 0.25f, 0);
            Vector3 last = view.CirclePoint(center, u, v, 0.25f, CircleSegments);
            BodyContactSystem.Require((first - last).magnitude < 1e-4f, "the circle does not close");
        }
    }
}
