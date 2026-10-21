using BenchmarkDotNet.Attributes;
using Microsoft.AspNetCore.Components;
using NTComponents.Charts.Core.Series;

namespace NTComponents.Charts.Benchmarks;

/// <summary>
///     Attributes steady-state frame cost of the 3-series/100-point line chart by removing one feature at a time
///     (legend+title, point markers) to show where time goes.
/// </summary>
public class LineAblationBenchmarks {
    private ChartHarness _full = null!;
    private ChartHarness _noChrome = null!;
    private ChartHarness _noMarkers = null!;
    private ChartHarness _bare = null!;
    private ChartHarness _noStroke = null!;

    private static ChartHarness Create(bool chrome, bool markers, bool stroke = true) {
        var data = BenchData.Create(100);
        var x = (Func<BenchDatum, object>)(d => d.X);
        (string, object)[] Extra() => [.. markers ? Array.Empty<(string, object)>() : [("PointStyle", PointStyle.None)], .. stroke ? Array.Empty<(string, object)>() : [("LineStyle", LineStyle.None)]];
        RenderFragment Line(Func<BenchDatum, decimal> y) => ChartHarness.Series(typeof(NTLineSeries<BenchDatum>), data, [("XValue", x), ("YValueSelector", y), .. Extra()]);
        var harness = new ChartHarness(ChartHarness.Combine(Line(d => d.Y), Line(d => d.Y2), Line(d => d.Y3)), legend: chrome, title: chrome);
        harness.Paint();
        return harness;
    }

    [GlobalSetup]
    public void Setup() {
        _full = Create(true, true);
        _noChrome = Create(false, true);
        _noMarkers = Create(true, false);
        _bare = Create(false, false);
        _noStroke = Create(false, false, false);
    }

    [GlobalCleanup]
    public void Cleanup() {
        _full.Dispose();
        _noChrome.Dispose();
        _noMarkers.Dispose();
        _bare.Dispose();
        _noStroke.Dispose();
    }

    [Benchmark(Baseline = true)]
    public void Full() => _full.Paint();

    [Benchmark]
    public void NoLegendNoTitle() => _noChrome.Paint();

    [Benchmark]
    public void NoMarkers() => _noMarkers.Paint();

    [Benchmark]
    public void NoLegendNoTitleNoMarkers() => _bare.Paint();

    [Benchmark]
    public void NoLegendNoTitleNoMarkersNoStroke() => _noStroke.Paint();
}
