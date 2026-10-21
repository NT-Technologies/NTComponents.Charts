using BenchmarkDotNet.Attributes;

namespace NTComponents.Charts.Benchmarks;

/// <summary>Three-series line chart with legend and title.</summary>
public class LineBenchmarks {
    private ChartHarness _chart = null!;

    [Params(100, 10_000)]
    public int Points { get; set; }

    [GlobalSetup]
    public void Setup() {
        var data = BenchData.Create(Points);
        var x = (Func<BenchDatum, object>)(d => d.X);
        _chart = new ChartHarness(ChartHarness.Combine(
            ChartHarness.Series(typeof(NTLineSeries<BenchDatum>), data, ("XValue", x), ("YValueSelector", (Func<BenchDatum, decimal>)(d => d.Y)), ("Title", "A")),
            ChartHarness.Series(typeof(NTLineSeries<BenchDatum>), data, ("XValue", x), ("YValueSelector", (Func<BenchDatum, decimal>)(d => d.Y2)), ("Title", "B")),
            ChartHarness.Series(typeof(NTLineSeries<BenchDatum>), data, ("XValue", x), ("YValueSelector", (Func<BenchDatum, decimal>)(d => d.Y3)), ("Title", "C"))));
        _chart.Paint();
    }

    [GlobalCleanup]
    public void Cleanup() => _chart.Dispose();

    [Benchmark]
    public void SteadyRepaint() => _chart.Paint();

    [Benchmark]
    public void InvalidateAndRepaint() {
        _chart.InvalidateSeries();
        _chart.Paint();
    }

    [Benchmark]
    public void HoverMoveAndRepaint() {
        _chart.MoveMouse();
        _chart.Paint();
    }

    [Benchmark]
    public void ResizeRepaint() => _chart.PaintAlternatingSize();
}
