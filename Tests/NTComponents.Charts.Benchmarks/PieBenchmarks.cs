using BenchmarkDotNet.Attributes;

namespace NTComponents.Charts.Benchmarks;

/// <summary>Pie chart: steady repaint and hover.</summary>
public class PieBenchmarks {
    private ChartHarness _chart = null!;

    [Params(8, 200)]
    public int Slices { get; set; }

    [GlobalSetup]
    public void Setup() {
        _chart = new ChartHarness(ChartHarness.Series(typeof(NTPieSeries<BenchDatum>), BenchData.Create(Slices), ("XValue", (Func<BenchDatum, object>)(d => d.Name)), ("ValueSelector", (Func<BenchDatum, decimal>)(d => d.Y)), ("ExplosionOnEntry", false)));
        _chart.SettleAnimations();
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
