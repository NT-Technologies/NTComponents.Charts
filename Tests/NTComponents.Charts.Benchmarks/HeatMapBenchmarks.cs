using BenchmarkDotNet.Attributes;

namespace NTComponents.Charts.Benchmarks;

/// <summary>A heat map with a categorical X axis and ten rows.</summary>
public class HeatMapBenchmarks {
    private ChartHarness _chart = null!;

    [Params(20, 200)]
    public int Columns { get; set; }

    [GlobalSetup]
    public void Setup() {
        var random = new Random(42);
        var data = new List<BenchDatum>(Columns * 10);
        for (var c = 0; c < Columns; c++) {
            for (var r = 0; r < 10; r++) {
                data.Add(new BenchDatum($"Col {c}", c, r, (decimal)random.NextDouble() * 100, 0, "G"));
            }
        }

        _chart = new ChartHarness(ChartHarness.Series(typeof(NTHeatMapSeries<BenchDatum>), data, ("XValue", (Func<BenchDatum, object>)(d => d.Name)), ("YValueSelector", (Func<BenchDatum, decimal>)(d => d.Y)), ("WeightSelector", (Func<BenchDatum, decimal>)(d => d.Y2))));
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
}
