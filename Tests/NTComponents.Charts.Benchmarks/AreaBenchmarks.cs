using BenchmarkDotNet.Attributes;

namespace NTComponents.Charts.Benchmarks;

/// <summary>Two-series area chart, steady-state repaint.</summary>
public class AreaBenchmarks {
    private ChartHarness _chart = null!;

    [Params(100, 5_000)]
    public int Points { get; set; }

    [GlobalSetup]
    public void Setup() {
        var data = BenchData.Create(Points);
        var x = (Func<BenchDatum, object>)(d => d.X);
        _chart = new ChartHarness(ChartHarness.Combine(
            ChartHarness.Series(typeof(NTAreaSeries<BenchDatum>), data, ("XValue", x), ("YValueSelector", (Func<BenchDatum, decimal>)(d => d.Y))),
            ChartHarness.Series(typeof(NTAreaSeries<BenchDatum>), data, ("XValue", x), ("YValueSelector", (Func<BenchDatum, decimal>)(d => d.Y2)))));
        _chart.Paint();
    }

    [GlobalCleanup]
    public void Cleanup() => _chart.Dispose();

    [Benchmark]
    public void SteadyRepaint() => _chart.Paint();
}
