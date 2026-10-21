using BenchmarkDotNet.Attributes;

namespace NTComponents.Charts.Benchmarks;

/// <summary>Twelve small line series so the legend and per-series loop dominate.</summary>
public class LegendBenchmarks {
    private ChartHarness _chart = null!;

    [GlobalSetup]
    public void Setup() {
        var data = BenchData.Create(50);
        var x = (Func<BenchDatum, object>)(d => d.X);
        var series = Enumerable.Range(0, 12).Select(i => ChartHarness.Series(typeof(NTLineSeries<BenchDatum>), data, ("XValue", x), ("YValueSelector", (Func<BenchDatum, decimal>)(d => d.Y + (i * 5))), ("Title", $"Series number {i}"))).ToArray();
        _chart = new ChartHarness(ChartHarness.Combine(series));
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
