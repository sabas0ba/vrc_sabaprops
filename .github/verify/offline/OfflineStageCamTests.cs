// Behavioural checks on the stage camera solver, executed without Unity.
//
// StageCamSolver.cs is a partial of StageCamRig that carries no base type and
// no VRChat references, so it compiles on its own against the shim in
// UnityEngineShim.cs. This file is another partial of the same class, which is
// how it reaches the solver's private members without widening their
// visibility in the shipped package. It is never part of the package.
//
// What this can assert: the tracking geometry itself. Distance invariance,
// roll-free aiming, the round trip that makes pickup adjustment work, frame
// rate independence of the smoothing, the deadzone contract, bone fallback,
// framing, and the continuity of the automatic camera move.
//
// What it cannot: that UdonSharp will compile the behaviour, that VRChat
// returns what the collection layer expects, or that anything is wired up in a
// scene. Those stay with the VRChat world tests, which need a real editor.
using System;
using UnityEngine;

namespace SabaProps.StageCam
{
    public partial class StageCamRig
    {
        private static int _failures;

        private static int Main()
        {
            Run("smoothing does not depend on the frame rate", SmoothingIsFrameRateIndependent);
            Run("smoothing degenerates safely", SmoothingDegeneratesSafely);
            Run("angle smoothing takes the short way around", AngleSmoothingTakesTheShortWay);

            Run("the deadzone holds still and then does not jump", DeadzoneHoldsStillAndDoesNotJump);
            Run("a zero deadzone follows exactly", ZeroDeadzoneFollowsExactly);

            Run("missing bones fall through to the next candidate", MissingBonesFallThrough);

            Run("the orbit holds its distance", OrbitHoldsItsDistance);
            Run("the camera aims at the subject without roll", AimPointsAtTheSubjectWithoutRoll);
            Run("horizontal yaw ignores pitch and roll", HorizontalYawIgnoresPitchAndRoll);

            Run("baking a grabbed pose reproduces it", BakeThenSolveReturnsTheSamePose);
            Run("baking degenerate poses keeps the previous value", BakingDegeneratePosesKeepsThePreviousValue);

            Run("framing distance scales with the subject", FramingDistanceScalesWithTheSubject);
            Run("framing round trips through the fraction", FramingRoundTripsThroughTheFraction);

            Run("the camera move is bounded, symmetric and periodic", CameraWorkIsBoundedAndPeriodic);
            Run("the camera move does not snap at its ends", CameraWorkDoesNotSnapAtItsEnds);
            Run("the phase stays in range", PhaseStaysInRange);

            Run("degenerate geometry stays finite", DegenerateGeometryStaysFinite);

            if (_failures > 0)
            {
                Console.Error.WriteLine($"\n{_failures} stage camera check(s) failed");
                return 1;
            }

            Console.WriteLine("\nall stage camera checks passed");
            return 0;
        }

        // ------------------------------------------------------------------
        // 平滑化
        // ------------------------------------------------------------------

        private static void SmoothingIsFrameRateIndependent()
        {
            var rig = new StageCamRig();
            const float tau = 0.25f;
            const float span = 0.3f;

            // 残差は exp(-dt/tau) なので、分割しても積は変わらないはず。
            float coarse = 1f - rig.SmoothingFactor(tau, span);

            float fine = 1f;
            for (int i = 0; i < 30; i++)
            {
                fine *= 1f - rig.SmoothingFactor(tau, span / 30f);
            }

            Require(Math.Abs(coarse - fine) < 1e-5f,
                $"residual after one step {coarse} != after 30 steps {fine}");

            // ベクトルの追従にも同じ性質が出ること。
            var target = new Vector3(3f, 1f, -2f);
            Vector3 one = rig.SmoothTowards(Vector3.zero, target, rig.SmoothingFactor(tau, span));

            Vector3 many = Vector3.zero;
            for (int i = 0; i < 30; i++)
            {
                many = rig.SmoothTowards(many, target, rig.SmoothingFactor(tau, span / 30f));
            }

            Require((one - many).magnitude < 1e-4f,
                $"one step reached {one.x},{one.y},{one.z} but 30 reached {many.x},{many.y},{many.z}");
        }

        private static void SmoothingDegeneratesSafely()
        {
            var rig = new StageCamRig();

            Require(rig.SmoothingFactor(0f, 0.016f) == 1f, "a zero time constant must follow instantly");
            Require(rig.SmoothingFactor(-1f, 0.016f) == 1f, "a negative time constant must follow instantly");
            Require(rig.SmoothingFactor(0.25f, 0f) == 0f, "a zero delta time must not move");
            Require(rig.SmoothingFactor(0.25f, -1f) == 0f, "a negative delta time must not move");

            float huge = rig.SmoothingFactor(0.25f, 1000f);
            Require(huge > 0.999f && huge <= 1f, $"a long frame must not overshoot, got {huge}");
        }

        private static void AngleSmoothingTakesTheShortWay()
        {
            var rig = new StageCamRig();

            // 350 度から 10 度へは 20 度進むのが最短で、340 度戻るのではない。
            float result = rig.SmoothAngleTowards(350f, 10f, 1f);
            Require(Math.Abs(Mathf.DeltaAngle(result, 10f)) < 1e-3f,
                $"expected to arrive at 10 degrees, got {result}");
            Require(result > 340f, $"expected to move forwards through 360, got {result}");

            float half = rig.SmoothAngleTowards(350f, 10f, 0.5f);
            Require(Math.Abs(half - 360f) < 1e-3f, $"expected the midpoint at 360, got {half}");
        }

        // ------------------------------------------------------------------
        // デッドゾーン
        // ------------------------------------------------------------------

        private static void DeadzoneHoldsStillAndDoesNotJump()
        {
            var rig = new StageCamRig();
            var current = new Vector3(1f, 2f, 3f);
            const float radius = 0.05f;

            Vector3 inside = current + new Vector3(0.03f, 0f, 0f);
            Require(Same(rig.ApplyDeadzone(current, inside, radius), current),
                "a target inside the deadzone must not move the output");

            // 閾値を跨いだ瞬間に飛ばないこと。超過分だけが残る。
            foreach (float offset in new[] { 0.0501f, 0.1f, 1f, 25f })
            {
                Vector3 outside = current + new Vector3(offset, 0f, 0f);
                Vector3 result = rig.ApplyDeadzone(current, outside, radius);
                float moved = (result - current).magnitude;
                Require(Math.Abs(moved - (offset - radius)) < 1e-4f,
                    $"at offset {offset} the output moved {moved}, expected {offset - radius}");
            }

            Vector3 justOutside = rig.ApplyDeadzone(current, current + new Vector3(radius + 1e-5f, 0f, 0f), radius);
            Require((justOutside - current).magnitude < 1e-4f,
                "leaving the deadzone must be continuous, not a step");
        }

        private static void ZeroDeadzoneFollowsExactly()
        {
            var rig = new StageCamRig();
            var current = new Vector3(1f, 2f, 3f);
            var target = new Vector3(1.001f, 2f, 3f);

            Require(Same(rig.ApplyDeadzone(current, target, 0f), target), "a zero radius must follow exactly");
            Require(Same(rig.ApplyDeadzone(current, target, -1f), target), "a negative radius must follow exactly");
        }

        // ------------------------------------------------------------------
        // ボーンのフォールバック
        // ------------------------------------------------------------------

        private static void MissingBonesFallThrough()
        {
            var rig = new StageCamRig();
            var primary = new Vector3(0f, 1.6f, 0f);
            var secondary = new Vector3(0f, 1.4f, 0f);
            var tertiary = new Vector3(0f, 1.2f, 0f);
            var fallback = new Vector3(0f, 0.1f, 0f);

            Require(Same(rig.FirstSampledPoint(primary, secondary, tertiary, fallback), primary),
                "a present bone must win");
            Require(Same(rig.FirstSampledPoint(Vector3.zero, secondary, tertiary, fallback), secondary),
                "a missing primary must fall to the secondary");
            Require(Same(rig.FirstSampledPoint(Vector3.zero, Vector3.zero, tertiary, fallback), tertiary),
                "two missing bones must fall to the tertiary");
            Require(Same(rig.FirstSampledPoint(Vector3.zero, Vector3.zero, Vector3.zero, fallback), fallback),
                "a non-humanoid avatar must reach the fallback");
        }

        // ------------------------------------------------------------------
        // 軌道と注視
        // ------------------------------------------------------------------

        private static void OrbitHoldsItsDistance()
        {
            var rig = new StageCamRig();
            var subject = new Vector3(4f, 1.5f, -7f);

            foreach (float yaw in new[] { -400f, -90f, 0f, 37f, 180f, 359f, 720f })
            {
                foreach (float pitch in new[] { -89f, -30f, 0f, 12f, 60f })
                {
                    foreach (float distance in new[] { 0.4f, 2.5f, 40f })
                    {
                        Vector3 camera = rig.OrbitPosition(subject, yaw, pitch, distance);
                        float measured = (camera - subject).magnitude;
                        Require(Math.Abs(measured - distance) < 1e-3f,
                            $"yaw {yaw} pitch {pitch}: expected {distance} m, measured {measured} m");
                    }
                }
            }
        }

        private static void AimPointsAtTheSubjectWithoutRoll()
        {
            var rig = new StageCamRig();
            var subject = new Vector3(-2f, 1.2f, 5f);

            foreach (float yaw in new[] { -270f, -45f, 0f, 37f, 150f, 300f })
            {
                foreach (float pitch in new[] { -75f, -20f, 0f, 25f, 75f })
                {
                    Vector3 camera = rig.OrbitPosition(subject, yaw, pitch, 3f);
                    Quaternion rotation = rig.AimRotation(yaw, pitch, 0f, 0f);

                    Vector3 forward = rotation * Vector3.forward;
                    Vector3 toSubject = (subject - camera).normalized;
                    Require((forward - toSubject).magnitude < 1e-3f,
                        $"yaw {yaw} pitch {pitch}: the camera looks away from the subject");

                    // roll がゼロなら、カメラの right はワールドの水平面に載る。
                    Vector3 right = rotation * Vector3.right;
                    Require(Math.Abs(right.y) < 1e-4f,
                        $"yaw {yaw} pitch {pitch}: the horizon is tilted by {right.y}");
                }
            }
        }

        private static void HorizontalYawIgnoresPitchAndRoll()
        {
            var rig = new StageCamRig();

            foreach (float yaw in new[] { -170f, 0f, 37f, 179f })
            {
                Quaternion facing = Quaternion.AngleAxis(yaw, Vector3.up);
                Require(Math.Abs(Mathf.DeltaAngle(rig.HorizontalYaw(facing), yaw)) < 1e-3f,
                    $"a plain yaw of {yaw} was not recovered");

                Quaternion pitched = facing * Quaternion.AngleAxis(50f, Vector3.right);
                Require(Math.Abs(Mathf.DeltaAngle(rig.HorizontalYaw(pitched), yaw)) < 1e-3f,
                    $"a pitch of 50 degrees disturbed the yaw of {yaw}");

                Quaternion rolled = facing * Quaternion.AngleAxis(35f, Vector3.forward);
                Require(Math.Abs(Mathf.DeltaAngle(rig.HorizontalYaw(rolled), yaw)) < 1e-3f,
                    $"a roll of 35 degrees disturbed the yaw of {yaw}");
            }

            // 真上を向いた頭。方位は forward からは決まらないが、破綻してはいけない。
            Quaternion lookingUp = Quaternion.AngleAxis(-90f, Vector3.right);
            Require(IsFinite(rig.HorizontalYaw(lookingUp)), "a vertical facing produced a non-finite yaw");
        }

        // ------------------------------------------------------------------
        // Pickup 補正の往復
        // ------------------------------------------------------------------

        private static void BakeThenSolveReturnsTheSamePose()
        {
            var rig = new StageCamRig();
            var subject = new Vector3(1f, 1.5f, 2f);

            // 掴んで置いた、軌道上にない任意の姿勢。roll も入れておく。
            var grabbed = new Vector3(3.2f, 2.9f, -1.4f);
            Quaternion grabbedRotation =
                Quaternion.AngleAxis(-118f, Vector3.up)
                * Quaternion.AngleAxis(21f, Vector3.right)
                * Quaternion.AngleAxis(14f, Vector3.forward);

            float yaw = rig.BakeYaw(subject, grabbed, 0f);
            float pitch = rig.BakePitch(subject, grabbed, 0f);
            float distance = rig.BakeDistance(subject, grabbed, 1f);
            float yawTrim = rig.BakeYawTrim(grabbedRotation, yaw);
            float pitchTrim = rig.BakePitchTrim(grabbedRotation, pitch);

            Vector3 restored = rig.OrbitPosition(subject, yaw, pitch, distance);
            Require((restored - grabbed).magnitude < 1e-3f,
                $"the position was not restored: {restored.x},{restored.y},{restored.z} vs {grabbed.x},{grabbed.y},{grabbed.z}");

            // roll は意図的に捨てるので、向きは forward で比べる。
            Quaternion restoredRotation = rig.AimRotation(yaw, pitch, yawTrim, pitchTrim);
            Vector3 wanted = grabbedRotation * Vector3.forward;
            Vector3 got = restoredRotation * Vector3.forward;
            Require((wanted - got).magnitude < 1e-3f,
                $"the aim was not restored: {got.x},{got.y},{got.z} vs {wanted.x},{wanted.y},{wanted.z}");

            Vector3 right = restoredRotation * Vector3.right;
            Require(Math.Abs(right.y) < 1e-4f, "the rebaked pose must come back level");
        }

        private static void BakingDegeneratePosesKeepsThePreviousValue()
        {
            var rig = new StageCamRig();
            var subject = new Vector3(1f, 1.5f, 2f);

            // カメラが被写体と同じ点にある。方位も仰角も距離も決まらない。
            Require(rig.BakeYaw(subject, subject, 42f) == 42f, "a coincident pose must keep the previous yaw");
            Require(rig.BakePitch(subject, subject, 17f) == 17f, "a coincident pose must keep the previous pitch");
            Require(rig.BakeDistance(subject, subject, 2.5f) == 2.5f, "a coincident pose must keep the previous distance");

            // カメラが被写体の真上にある。方位だけが決まらない。
            Vector3 above = subject + new Vector3(0f, 3f, 0f);
            Require(rig.BakeYaw(subject, above, 42f) == 42f, "a pose directly above must keep the previous yaw");
            Require(Math.Abs(rig.BakePitch(subject, above, 0f) - 90f) < 1e-3f,
                "a pose directly above must bake to a pitch of 90 degrees");
            Require(Math.Abs(rig.BakeDistance(subject, above, 0f) - 3f) < 1e-4f,
                "a pose directly above must still bake its distance");
        }

        // ------------------------------------------------------------------
        // 自動フレーミング
        // ------------------------------------------------------------------

        private static void FramingDistanceScalesWithTheSubject()
        {
            var rig = new StageCamRig();

            float small = rig.FramingDistance(1f, 0.6f, 60f);
            float large = rig.FramingDistance(2f, 0.6f, 60f);
            Require(small > 0f, "a valid subject must produce a distance");
            Require(Math.Abs(large - small * 2f) < 1e-3f,
                $"twice the subject must need twice the distance, got {small} and {large}");

            // 画面を大きく占めるほど寄る。
            Require(rig.FramingDistance(1.6f, 0.8f, 60f) < rig.FramingDistance(1.6f, 0.4f, 60f),
                "a larger screen fraction must come closer");

            // 解が決まらない入力は 0 を返し、呼び出し側が現在値を保つ。
            Require(rig.FramingDistance(0f, 0.6f, 60f) == 0f, "a subject with no height has no solution");
            Require(rig.FramingDistance(-1f, 0.6f, 60f) == 0f, "a negative height has no solution");
            Require(rig.FramingDistance(1.6f, 0.6f, 0f) == 0f, "a zero field of view has no solution");
        }

        private static void FramingRoundTripsThroughTheFraction()
        {
            var rig = new StageCamRig();

            foreach (float height in new[] { 0.25f, 1.6f, 3f })
            {
                foreach (float fraction in new[] { 0.2f, 0.6f, 0.9f })
                {
                    float distance = rig.FramingDistance(height, fraction, 60f);
                    float recovered = rig.FramingFraction(height, distance, 60f);
                    Require(Math.Abs(recovered - fraction) < 1e-3f,
                        $"height {height} fraction {fraction} came back as {recovered}");
                }
            }

            Require(rig.FramingFraction(1.6f, 0f, 60f) == 0f, "a zero distance has no solution");
            Require(rig.FramingFraction(0f, 3f, 60f) == 0f, "a subject with no height has no solution");
        }

        // ------------------------------------------------------------------
        // 自動カメラワーク
        // ------------------------------------------------------------------

        private static void CameraWorkIsBoundedAndPeriodic()
        {
            var rig = new StageCamRig();

            Require(Math.Abs(rig.PingPongEase(0f)) < 1e-6f, "the move must start at its base angle");
            Require(Math.Abs(rig.PingPongEase(0.5f) - 1f) < 1e-6f, "the move must reach its full sweep at half phase");
            Require(Math.Abs(rig.PingPongEase(1f)) < 1e-6f, "the move must return to its base angle");

            for (int i = 0; i <= 400; i++)
            {
                float phase = i / 100f - 2f;
                float ease = rig.PingPongEase(phase);
                Require(ease >= -1e-6f && ease <= 1f + 1e-6f, $"phase {phase} left the range with {ease}");

                // 周期性。位相をずらしても同じ値になる。
                Require(Math.Abs(ease - rig.PingPongEase(phase + 3f)) < 1e-5f,
                    $"phase {phase} is not periodic");
            }

            // 往路と復路が対称。
            Require(Math.Abs(rig.PingPongEase(0.25f) - rig.PingPongEase(0.75f)) < 1e-6f,
                "the move must be symmetric about its far end");

            // 3 要素が同じ位相を共有し、まとめて振り込む。
            const float phaseAtEnd = 0.5f;
            Require(Math.Abs(rig.CameraWorkYaw(phaseAtEnd, 10f, 90f) - 100f) < 1e-4f, "the yaw sweep is wrong");
            Require(Math.Abs(rig.CameraWorkPitch(phaseAtEnd, 8f, 6f) - 14f) < 1e-4f, "the pitch rise is wrong");
            Require(Math.Abs(rig.CameraWorkDistance(phaseAtEnd, 2.5f, -0.4f) - 2.1f) < 1e-4f, "the dolly is wrong");
        }

        private static void CameraWorkDoesNotSnapAtItsEnds()
        {
            var rig = new StageCamRig();
            const float h = 1e-3f;

            // 折り返しと周期の境目で速度がゼロに落ちること。落ちないとカメラが端で
            // 急に切り返し、映像として見ていられなくなる。
            float atStart = (rig.PingPongEase(h) - rig.PingPongEase(0f)) / h;
            float atTurn = (rig.PingPongEase(0.5f + h) - rig.PingPongEase(0.5f - h)) / (2f * h);
            float acrossWrap = (rig.PingPongEase(1f + h) - rig.PingPongEase(1f - h)) / (2f * h);

            Require(Math.Abs(atStart) < 0.02f, $"the move starts with a speed of {atStart}");
            Require(Math.Abs(atTurn) < 0.02f, $"the move turns with a speed of {atTurn}");
            Require(Math.Abs(acrossWrap) < 0.02f, $"the move crosses the wrap with a speed of {acrossWrap}");

            // 境目で値そのものが飛ばないこと。
            Require(Math.Abs(rig.PingPongEase(1f - h) - rig.PingPongEase(h)) < 1e-4f,
                "the move is discontinuous across the wrap");
        }

        private static void PhaseStaysInRange()
        {
            var rig = new StageCamRig();

            float phase = 0f;
            for (int i = 0; i < 10000; i++)
            {
                phase = rig.AdvancePhase(phase, 4f, 1f / 90f);
                Require(phase >= 0f && phase < 1f, $"the phase left [0, 1) with {phase}");
            }

            Require(rig.AdvancePhase(0.3f, 0f, 0.016f) == 0.3f, "a zero period must hold the phase");
            Require(rig.AdvancePhase(0.3f, -1f, 0.016f) == 0.3f, "a negative period must hold the phase");
        }

        // ------------------------------------------------------------------
        // 退化入力
        // ------------------------------------------------------------------

        private static void DegenerateGeometryStaysFinite()
        {
            var rig = new StageCamRig();
            var subject = new Vector3(1f, 1.5f, 2f);

            foreach (float pitch in new[] { -90f, 90f, -180f, 180f, 0f })
            {
                foreach (float distance in new[] { 0f, 1e-6f, 1e6f })
                {
                    Vector3 position = rig.OrbitPosition(subject, 0f, pitch, distance);
                    Require(IsFinite(position), $"pitch {pitch} distance {distance} produced {position.x},{position.y},{position.z}");

                    Quaternion rotation = rig.AimRotation(0f, pitch, 0f, 0f);
                    Require(IsFinite(rotation), $"pitch {pitch} produced a non-finite rotation");
                }
            }

            // 距離ゼロは被写体そのものの位置。視線が上方向と平行になるが破綻しない。
            Require((rig.OrbitPosition(subject, 45f, 30f, 0f) - subject).magnitude < 1e-6f,
                "a zero distance must place the camera on the subject");

            Vector3 deadzoned = rig.ApplyDeadzone(subject, subject, 0.05f);
            Require(IsFinite(deadzoned), "a zero-length deadzone delta produced a non-finite result");
        }

        // ------------------------------------------------------------------
        // ハーネス
        // ------------------------------------------------------------------

        /// <summary>Exact component equality. The shim deliberately has no tolerant operator.</summary>
        private static bool Same(Vector3 a, Vector3 b)
        {
            return a.x == b.x && a.y == b.y && a.z == b.z;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private static bool IsFinite(Vector3 value)
        {
            return IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
        }

        private static bool IsFinite(Quaternion value)
        {
            return IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z) && IsFinite(value.w);
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
    }
}
