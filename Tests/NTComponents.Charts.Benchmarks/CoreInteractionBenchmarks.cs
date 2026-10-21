using BenchmarkDotNet.Attributes;

namespace NTComponents.Charts.Benchmarks;

/// <summary>
///     Isolates the chart-core mouse path (no paint) on the twelve-series legend chart: the per-move work done by
///     <c>NTChart.OnMouseMove</c> before the next frame is drawn.
/// </summary>
public class CoreInteractionBenchmarks {
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
    public void MouseMoveOnly() => _chart.MoveMouse();
}
