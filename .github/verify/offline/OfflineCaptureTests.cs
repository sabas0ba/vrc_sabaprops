// Behavioural checks on the capture package's arithmetic, executed without Unity.
//
// CaptureRecorderSchedule.cs and CapturePlayerTimeline.cs are partials of the
// two behaviours that carry no base type and no VRChat references, so they
// compile on their own against the shim in UnityEngineShim.cs. This file adds
// further partials of the same classes, which is how the checks reach their
// private members without widening them in the shipped package. It is never
// part of the package.
//
// What this can assert: the capture schedule, the frame budget, the ring
// buffer indexing, the thinning permutation, time lookup, and the playback
// position arithmetic.
//
// What it cannot: that UdonSharp compiles the behaviours, that VRChat allows
// the RenderTexture and Camera calls at run time, or that the panel is wired.
// Those stay with the VRChat world tests and with a check in the client.
using System;
using System.Collections.Generic;
using UnityEngine;

namespace SabaProps.Capture
{
    public partial class CaptureRecorder
    {
        private static int _failures;

        private static int Main()
        {
            Run("capacity follows the frame limit and the budget", CapacityFollowsLimitAndBudget);
            Run("capacity never drops below one frame", CapacityNeverDropsBelowOne);
            Run("the schedule stays on its grid", ScheduleStaysOnItsGrid);
            Run("the schedule skips missed slots instead of bursting", ScheduleSkipsMissedSlots);
            Run("the schedule clamps a too short interval", ScheduleClampsShortInterval);
            Run("the schedule does not drift over hours", ScheduleDoesNotDriftOverHours);
            Run("thinning needs two frames and unknown policies thin", EffectivePolicyFallsBack);
            Run("physical indices wrap around the ring", PhysicalIndicesWrap);
            Run("thinning is a permutation that keeps even frames", ThinningIsAPermutation);
            Run("repeated thinning keeps uniform spacing", RepeatedThinningKeepsUniformSpacing);
            Run("nearest lookup finds the closest time", NearestLookupFindsClosestTime);
            Run("nearest lookup works across the ring seam", NearestLookupAcrossTheSeam);

            CapturePlayer.RunChecks();

            if (_failures > 0)
            {
                Console.Error.WriteLine($"\n{_failures} capture check(s) failed");
                return 1;
            }

            Console.WriteLine("\nall capture checks passed");
            return 0;
        }

        // ------------------------------------------------------------------
        // 保存枚数
        // ------------------------------------------------------------------

        private static void CapacityFollowsLimitAndBudget()
        {
            var recorder = new CaptureRecorder();

            Require(recorder.CapacityFor(384, 216, FormatARGB32, 360, 0f) == 360,
                "without a budget the frame limit must decide");

            // 384x216x4 = 331776 byte。64 MB には 202 枚入る。
            Require(recorder.CapacityFor(384, 216, FormatARGB32, 360, 64f) == 202,
                $"expected 202 frames in 64 MB, got {recorder.CapacityFor(384, 216, FormatARGB32, 360, 64f)}");

            // RGB565 は半分の VRAM なので倍入る。
            Require(recorder.CapacityFor(384, 216, FormatRGB565, 1000, 64f) == 404,
                "RGB565 must fit twice as many frames");

            // 予算が十分なら枚数の上限が勝つ。
            Require(recorder.CapacityFor(384, 216, FormatARGB32, 100, 1024f) == 100,
                "a generous budget must not raise the frame limit");

            Require(recorder.CapacityFor(64, 36, FormatARGB32, 100000, 0f) == HardFrameLimit,
                "the hard limit must cap the frame count");

            // 大きな画像でも int の桁あふれで負にならない。
            Require(recorder.CapacityFor(16384, 16384, FormatARGB32, 10, 100000f) == 10,
                "large frames must not overflow the budget arithmetic");
        }

        private static void CapacityNeverDropsBelowOne()
        {
            var recorder = new CaptureRecorder();

            Require(recorder.CapacityFor(1920, 1080, FormatARGB32, 360, 1f) == 1, "a tiny budget still keeps one frame");
            Require(recorder.CapacityFor(0, -5, FormatARGB32, 0, 0f) == 1, "degenerate settings still keep one frame");
            Require(recorder.CapacityFor(384, 216, 99, 10, 0f) == 10, "an unknown format must fall back safely");
        }

        // ------------------------------------------------------------------
        // 撮影周期
        // ------------------------------------------------------------------

        private static void ScheduleStaysOnItsGrid()
        {
            var recorder = new CaptureRecorder();

            // 遅れて撮っても次の予定は格子上に留まる。
            Require(Same(recorder.NextCaptureTime(0.0, 10.0, 0.0), 10.0), "the first step must be one interval");
            Require(Same(recorder.NextCaptureTime(10.0, 10.0, 10.37), 20.0), "a late frame must not shift the grid");
            Require(Same(recorder.NextCaptureTime(20.0, 10.0, 20.0), 30.0), "an exact frame must advance one step");
        }

        private static void ScheduleSkipsMissedSlots()
        {
            var recorder = new CaptureRecorder();

            // 35 秒止まっていた場合、3 回分をまとめて撮らず、次の格子へ戻る。
            double next = recorder.NextCaptureTime(10.0, 10.0, 45.0);
            Require(Same(next, 50.0), $"expected the next slot at 50, got {next}");

            // 格子ちょうどに now が来た場合は、その時刻を 2 回撮らない。
            next = recorder.NextCaptureTime(10.0, 10.0, 40.0);
            Require(Same(next, 50.0), $"expected 50 when now lands on the grid, got {next}");

            for (int i = 0; i < 200; i++)
            {
                double now = 10.0 + i * 0.73;
                double result = recorder.NextCaptureTime(10.0, 10.0, now);
                Require(result > now, $"now {now}: the next slot {result} is not in the future");
                Require(result - now <= 10.0 + 1e-9, $"now {now}: the next slot {result} is more than one interval away");
                double phase = (result - 10.0) / 10.0;
                Require(Math.Abs(phase - Math.Round(phase)) < 1e-9, $"now {now}: {result} left the grid");
            }
        }

        private static void ScheduleClampsShortInterval()
        {
            var recorder = new CaptureRecorder();

            Require(Same(recorder.NextCaptureTime(0.0, 0.0, 0.0), MinInterval), "a zero interval must use the minimum");
            Require(Same(recorder.NextCaptureTime(0.0, -3.0, 0.0), MinInterval), "a negative interval must use the minimum");
        }

        private static void ScheduleDoesNotDriftOverHours()
        {
            var recorder = new CaptureRecorder();

            // 90 fps で 3 時間、フレームごとに予定を確認する。撮影回数が格子の数と一致すること。
            const double frame = 1.0 / 90.0;
            const double interval = 7.5;
            double scheduled = 0.0;
            int shots = 0;

            for (long i = 0; i <= 90L * 3 * 3600; i++)
            {
                double now = i * frame;
                if (now >= scheduled)
                {
                    shots++;
                    scheduled = recorder.NextCaptureTime(scheduled, interval, now);
                }
            }

            int expected = (int)Math.Floor(3 * 3600 / interval) + 1;
            Require(shots == expected, $"expected {expected} captures in three hours, got {shots}");
            Require(Math.Abs(scheduled - expected * interval) < 1e-6,
                $"the schedule drifted to {scheduled}, expected {expected * interval}");
        }

        // ------------------------------------------------------------------
        // リングバッファと間引き
        // ------------------------------------------------------------------

        private static void EffectivePolicyFallsBack()
        {
            var recorder = new CaptureRecorder();

            Require(recorder.EffectivePolicy(PolicyThin, 2) == PolicyThin, "two frames can be thinned");
            Require(recorder.EffectivePolicy(PolicyThin, 1) == PolicyStop,
                "one frame cannot be thinned without losing the first frame, so it must stop");
            Require(recorder.EffectivePolicy(PolicyRing, 1) == PolicyRing, "the ring policy is kept as is");
            Require(recorder.EffectivePolicy(PolicyStop, 360) == PolicyStop, "the stop policy is kept as is");
            Require(recorder.EffectivePolicy(99, 360) == PolicyThin, "an unknown policy falls back to thinning");
            Require(recorder.EffectivePolicy(-1, 1) == PolicyStop, "an unknown policy on one frame falls back to stopping");
        }

        private static void PhysicalIndicesWrap()
        {
            var recorder = new CaptureRecorder();

            Require(recorder.PhysicalIndex(0, 3, 5) == 3, "no offset keeps the index");
            Require(recorder.PhysicalIndex(3, 3, 5) == 1, "the index must wrap");
            Require(recorder.PhysicalIndex(4, 0, 5) == 4, "logical zero is the head");
            Require(recorder.PhysicalIndex(2, -1, 5) == 1, "a negative logical index must stay in range");
            Require(recorder.PhysicalIndex(0, 7, 0) == 0, "a zero capacity must not divide by zero");
        }

        private static void ThinningIsAPermutation()
        {
            var recorder = new CaptureRecorder();

            foreach (int capacity in new[] { 1, 2, 3, 8, 9, 360 })
            {
                foreach (int head in new[] { 0, 1, capacity / 2, capacity - 1 })
                {
                    foreach (int count in new[] { 0, 1, capacity / 2, capacity })
                    {
                        var order = new int[capacity];
                        int kept = recorder.ThinOrder(order, head, count, capacity);

                        Require(kept == (count + 1) / 2, $"cap {capacity} count {count}: kept {kept}");

                        var seen = new HashSet<int>();
                        foreach (int index in order)
                        {
                            Require(index >= 0 && index < capacity, $"cap {capacity}: index {index} out of range");
                            seen.Add(index);
                        }

                        Require(seen.Count == capacity,
                            $"cap {capacity} head {head} count {count}: the order is not a permutation");

                        for (int k = 0; k < kept; k++)
                        {
                            Require(order[k] == recorder.PhysicalIndex(head, 2 * k, capacity),
                                $"cap {capacity} head {head}: kept slot {k} is not logical frame {2 * k}");
                        }
                    }
                }
            }
        }

        private static void RepeatedThinningKeepsUniformSpacing()
        {
            // 実際の Recorder と同じ手順で、時刻だけを追う。
            var recorder = new CaptureRecorder();
            const int capacity = 16;
            var times = new double[capacity];
            var scratch = new double[capacity];
            var order = new int[capacity];
            int head = 0;
            int count = 0;
            double interval = 1.0;
            double scheduled = 0.0;

            for (int step = 0; step < 400; step++)
            {
                double now = scheduled;
                if (count >= capacity)
                {
                    int kept = recorder.ThinOrder(order, head, count, capacity);
                    for (int i = 0; i < capacity; i++)
                    {
                        scratch[i] = times[order[i]];
                    }

                    Array.Copy(scratch, times, capacity);
                    head = 0;
                    count = kept;
                    interval *= 2.0;
                    scheduled = times[kept - 1] + interval;
                    continue;
                }

                times[recorder.PhysicalIndex(head, count, capacity)] = now;
                count++;
                scheduled = recorder.NextCaptureTime(scheduled, interval, now);
            }

            Require(times[0] == 0.0, "the first frame of the session must survive thinning");

            double spacing = times[1] - times[0];
            for (int i = 1; i < count; i++)
            {
                double gap = times[i] - times[i - 1];
                Require(Math.Abs(gap - spacing) < 1e-9, $"gap {i} is {gap}, expected {spacing}");
            }

            Require(Math.Abs(spacing - interval) < 1e-9,
                $"the spacing {spacing} does not match the current interval {interval}");
        }

        private static void NearestLookupFindsClosestTime()
        {
            var recorder = new CaptureRecorder();
            var times = new[] { 0.0, 10.0, 20.0, 30.0, 40.0 };

            Require(recorder.NearestLogical(times, 0, 5, 5, -5.0) == 0, "before the first frame must clamp to it");
            Require(recorder.NearestLogical(times, 0, 5, 5, 99.0) == 4, "after the last frame must clamp to it");
            Require(recorder.NearestLogical(times, 0, 5, 5, 14.0) == 1, "14 is closest to 10");
            Require(recorder.NearestLogical(times, 0, 5, 5, 16.0) == 2, "16 is closest to 20");
            Require(recorder.NearestLogical(times, 0, 5, 5, 15.0) == 1, "a tie goes to the earlier frame");
            Require(recorder.NearestLogical(times, 0, 5, 5, 30.0) == 3, "an exact time is found");
            Require(recorder.NearestLogical(times, 0, 0, 5, 30.0) == -1, "an empty recorder has no frame");
            Require(recorder.NearestLogical(times, 0, 3, 5, 99.0) == 2, "only the stored frames are searched");
        }

        private static void NearestLookupAcrossTheSeam()
        {
            var recorder = new CaptureRecorder();

            // リングバッファが 2 周目に入り、最古が添字 3 にある状態。
            var times = new[] { 50.0, 60.0, 70.0, 20.0, 30.0, 40.0 };
            const int head = 3;

            for (int logical = 0; logical < 6; logical++)
            {
                double t = 20.0 + logical * 10.0;
                Require(recorder.NearestLogical(times, head, 6, 6, t) == logical,
                    $"time {t} should be logical frame {logical}");
            }

            Require(recorder.NearestLogical(times, head, 6, 6, 44.0) == 2, "44 is closest to 40 at logical 2");
        }

        // ------------------------------------------------------------------

        internal static void Run(string name, Action check)
        {
            try
            {
                check();
                Console.WriteLine($"ok: {name}");
            }
            catch (Exception exception)
            {
                _failures++;
                Console.Error.WriteLine($"FAIL: {name}\n      {exception.Message}");
            }
        }

        internal static void Require(bool condition, string message)
        {
            if (!condition)
            {
                throw new Exception(message);
            }
        }

        private static bool Same(double a, double b)
        {
            return Math.Abs(a - b) < 1e-9;
        }
    }

    public partial class CapturePlayer
    {
        internal static void RunChecks()
        {
            CaptureRecorder.Run("the playhead advances with the rate", PlayheadAdvancesWithRate);
            CaptureRecorder.Run("the playhead loops or stops at the end", PlayheadLoopsOrStops);
            CaptureRecorder.Run("stepping wraps or clamps", SteppingWrapsOrClamps);
            CaptureRecorder.Run("the slider round trips through frames", SliderRoundTrips);
            CaptureRecorder.Run("thumbnails span the whole timeline", ThumbnailsSpanTheTimeline);
            CaptureRecorder.Run("the clock is formatted for people", ClockIsFormatted);
        }

        private static void PlayheadAdvancesWithRate()
        {
            var player = new CapturePlayer();

            float head = player.AdvancePlayhead(0f, 8f, 0.25f, 100, true);
            Require(Math.Abs(head - 2f) < 1e-5f, $"8 frames/s for 0.25 s should move 2 frames, got {head}");
            Require(player.PlayheadFrame(head, 100) == 2, "the shown frame must follow the playhead");

            // フレームレートに依存しない。
            float fine = 0f;
            for (int i = 0; i < 90; i++)
            {
                fine = player.AdvancePlayhead(fine, 8f, 1f / 90f, 100, true);
            }

            Require(Math.Abs(fine - 8f) < 1e-3f, $"one second at 90 fps should move 8 frames, got {fine}");

            Require(player.AdvancePlayhead(5f, 8f, -1f, 100, true) == 5f, "a negative delta must not move");
            Require(player.AdvancePlayhead(5f, 8f, 1f, 0, true) == 0f, "an empty timeline must reset");
            Require(player.PlayheadFrame(3f, 0) == -1, "an empty timeline shows nothing");
        }

        private static void PlayheadLoopsOrStops()
        {
            var player = new CapturePlayer();

            float looped = player.AdvancePlayhead(9.5f, 1f, 1f, 10, true);
            Require(looped >= 0f && looped < 10f && Math.Abs(looped - 0.5f) < 1e-5f,
                $"looping past the end should wrap to 0.5, got {looped}");

            float stopped = player.AdvancePlayhead(9.5f, 1f, 5f, 10, false);
            Require(player.PlayheadFrame(stopped, 10) == 9, $"a non-looping playhead must stop on the last frame, got {stopped}");
        }

        private static void SteppingWrapsOrClamps()
        {
            var player = new CapturePlayer();

            Require(player.StepFrame(9, 1, 10, true) == 0, "stepping past the end must wrap when looping");
            Require(player.StepFrame(0, -1, 10, true) == 9, "stepping before the start must wrap when looping");
            Require(player.StepFrame(9, 1, 10, false) == 9, "stepping past the end must clamp");
            Require(player.StepFrame(0, -1, 10, false) == 0, "stepping before the start must clamp");
            Require(player.StepFrame(0, 1, 0, true) == -1, "an empty timeline has no frame");
        }

        private static void SliderRoundTrips()
        {
            var player = new CapturePlayer();

            foreach (int count in new[] { 2, 3, 17, 360, 4096 })
            {
                for (int frame = 0; frame < count; frame += Math.Max(1, count / 50))
                {
                    float value = player.FrameToSlider(frame, count);
                    Require(value >= 0f && value <= 1f, $"count {count} frame {frame}: slider {value} out of range");
                    Require(player.SliderToFrame(value, count) == frame,
                        $"count {count}: frame {frame} came back as {player.SliderToFrame(value, count)}");
                }

                Require(player.SliderToFrame(1f, count) == count - 1, "the right end is the latest frame");
                Require(player.SliderToFrame(-2f, count) == 0, "values below zero clamp to the first frame");
            }

            Require(player.SliderToFrame(0.7f, 1) == 0, "a single frame is always shown");
            Require(player.SliderToFrame(0.7f, 0) == -1, "an empty timeline shows nothing");
            Require(player.FrameToSlider(0, 1) == 0f, "a single frame sits at zero");
        }

        private static void ThumbnailsSpanTheTimeline()
        {
            var player = new CapturePlayer();

            Require(player.ThumbnailFrame(0, 8, 100) == 0, "the first thumbnail is the oldest frame");
            Require(player.ThumbnailFrame(7, 8, 100) == 99, "the last thumbnail is the latest frame");

            int previous = -1;
            for (int i = 0; i < 8; i++)
            {
                int frame = player.ThumbnailFrame(i, 8, 100);
                Require(frame > previous, $"thumbnail {i} does not advance: {frame} after {previous}");
                previous = frame;
            }

            Require(player.ThumbnailFrame(2, 8, 3) == 2, "a short timeline lists every frame");
            Require(player.ThumbnailFrame(5, 8, 3) == -1, "unused thumbnails are empty");
            Require(player.ThumbnailFrame(0, 1, 50) == 49, "a single thumbnail shows the latest frame");
            Require(player.ThumbnailFrame(0, 8, 0) == -1, "an empty timeline has no thumbnails");
            Require(player.ThumbnailFrame(8, 8, 100) == -1, "an index past the strip is empty");
        }

        private static void ClockIsFormatted()
        {
            var player = new CapturePlayer();

            Require(player.FormatClock(0.0) == "0:00", $"got {player.FormatClock(0.0)}");
            Require(player.FormatClock(9.9) == "0:09", $"got {player.FormatClock(9.9)}");
            Require(player.FormatClock(75.0) == "1:15", $"got {player.FormatClock(75.0)}");
            Require(player.FormatClock(3600.0) == "1:00:00", $"got {player.FormatClock(3600.0)}");
            Require(player.FormatClock(3 * 3600 + 5 * 60 + 7) == "3:05:07", $"got {player.FormatClock(3 * 3600 + 5 * 60 + 7)}");
            Require(player.FormatClock(-4.0) == "0:00", "negative time must read as zero");
            Require(player.FormatClock(double.NaN) == "0:00", "NaN must read as zero");
            Require(player.FormatClock(1e12) != null, "huge values must not throw");
        }

        private static void Require(bool condition, string message)
        {
            CaptureRecorder.Require(condition, message);
        }
    }
}
