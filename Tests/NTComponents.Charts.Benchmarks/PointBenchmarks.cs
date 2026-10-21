using BenchmarkDotNet.Attributes;

namespace NTComponents.Charts.Benchmarks;

/// <summary>Single-series scatter chart, steady-state repaint and hover.</summary>
public class PointBenchmarks {
    private ChartHarness _chart = null!;

    [Params(100, 5_000)]
    public int Points { get; set; }

    [GlobalSetup]
    public void Setup() {
        var data = BenchData.Create(Points);
        _chart = new ChartHarness(ChartHarness.Series(typeof(NTPointSeries<BenchDatum>), data, ("XValue", (Func<BenchDatum, object>)(d => d.X)), ("YValueSelector", (Func<BenchDatum, decimal>)(d => d.Y))));
        _chart.Paint();
    }

    [GlobalCleanup]
    public void Cleanup() => _chart.Dispose();

    [Benchmark]
    public void SteadyRepaint() => _chart.Paint();

    [Benchmark]
    public void HoverMoveAndRepaint() {
        _chart.MoveMouse();
        _chart.Paint();
    }
}
