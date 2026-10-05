using System.Diagnostics;

namespace NTComponents.Charts.Core;

internal static class NTChartMotion {
    internal static long Timestamp => Stopwatch.GetTimestamp();

    internal static TimeSpan ElapsedSince(long timestamp) => Stopwatch.GetElapsedTime(timestamp);

    internal static TimeSpan ElapsedBetween(long startTimestamp, long endTimestamp) => Stopwatch.GetElapsedTime(startTimestamp, endTimestamp);

    internal static long TimestampFor(DateTime startTime) {
        var elapsed = DateTime.Now - startTime;
        return Stopwatch.GetTimestamp() - (long)(elapsed.TotalSeconds * Stopwatch.Frequency);
    }

    internal static float Progress(TimeSpan elapsed, NTMotionDuration duration) => (float)Math.Clamp(elapsed.TotalMilliseconds / duration.ToMilliseconds(), 0, 1);

    internal static float Ease(float progress, NTMotionEasing easing) {
        // Match the NTComponents web curves; Skia drawings cannot use CSS easing directly.
        var (x1, y1, x2, y2) = easing switch {
            NTMotionEasing.Emphasized or NTMotionEasing.Standard => (0.2f, 0f, 0f, 1f),
            NTMotionEasing.EmphasizedDecelerate => (0.05f, 0.7f, 0.1f, 1f),
            NTMotionEasing.EmphasizedAccelerate => (0.3f, 0f, 0.8f, 0.15f),
            NTMotionEasing.StandardDecelerate => (0f, 0f, 0f, 1f),
            NTMotionEasing.StandardAccelerate => (0.3f, 0f, 1f, 1f),
            _ => throw new ArgumentOutOfRangeException(nameof(easing), easing, null)
        };
        progress = Math.Clamp(progress, 0f, 1f);
        if (progress is 0f or 1f) {
            return progress;
        }

        // Time is the curve's x coordinate, not its Bezier parameter.
        var lower = 0f;
        var upper = 1f;
        for (var i = 0; i < 20; i++) {
            var t = (lower + upper) / 2f;
            if (Coordinate(t, x1, x2) < progress) {
                lower = t;
            }
            else {
                upper = t;
            }
        }
        return Coordinate((lower + upper) / 2f, y1, y2);
    }

    private static float Coordinate(float t, float first, float second) => (3f * (1f - t) * (1f - t) * t * first) + (3f * (1f - t) * t * t * second) + (t * t * t);
}
