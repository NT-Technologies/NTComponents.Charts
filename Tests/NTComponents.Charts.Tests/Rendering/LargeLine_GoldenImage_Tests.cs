using NTComponents.Charts.Core.Series;

namespace NTComponents.Charts.Tests.Rendering;

/// <summary>Guards the large-data line path (far more points than plot pixels) against visual drift.</summary>
public class LargeLine_GoldenImage_Tests : GoldenChartTestBase {
    // Deterministic 10k-point signal: a slow sine wave plus a fixed high-frequency sawtooth so every pixel column holds a real min/max spread.
    private static readonly GoldenRow[] _large = [.. Enumerable.Range(0, 10_000).Select(i => new GoldenRow(
        $"P{i}", new DateTime(2024, 1, 1), i, 50m + (decimal)Math.Round(30 * Math.Sin(i / 700d), 3) + ((i * 7919) % 23) - 11, 0, 0, "", "", 0, new BoxPlotValues(0, 0, 0, 0, 0)))];

    [Fact]
    public async Task Line_TenThousandPoints_MatchesGolden() {
        var chart = Chart(c => c.Cartesian<NTLineSeries<GoldenRow>>(_large, GoldenSelectors.X, GoldenSelectors.A, ("Title", "Large")));

        await VerifyAsync("line-large-10k", chart);
    }
}
