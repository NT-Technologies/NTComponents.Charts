using BenchmarkDotNet.Attributes;

namespace NTComponents.Charts.Benchmarks;

/// <summary>Radar chart steady-state repaint.</summary>
public class RadarBenchmarks {
    private ChartHarness _chart = null!;

    [Params(8, 64)]
    public int Axes { get; set; }

    [GlobalSetup]
    public void Setup() {
        _chart = new ChartHarness(ChartHarness.Series(typeof(NTRadarSeries<BenchDatum>), BenchData.Create(Axes), ("XValue", (Func<BenchDatum, object>)(d => d.Name)), ("ValueSelector", (Func<BenchDatum, decimal>)(d => d.Y))));
        _chart.SettleAnimations();
    }

    [GlobalCleanup]
    public void Cleanup() => _chart.Dispose();

    [Benchmark]
    public void SteadyRepaint() => _chart.Paint();
}
