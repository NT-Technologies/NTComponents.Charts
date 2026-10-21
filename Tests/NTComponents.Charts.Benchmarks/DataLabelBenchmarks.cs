using BenchmarkDotNet.Attributes;

namespace NTComponents.Charts.Benchmarks;

/// <summary>Steady frames of cartesian charts with data labels shown on every point or bar.</summary>
public class DataLabelBenchmarks {
    private ChartHarness _line = null!;
    private ChartHarness _bar = null!;

    [GlobalSetup]
    public void Setup() {
        var data = BenchData.Create(100);
        var x = (Func<BenchDatum, object>)(d => d.X);
        _line = new ChartHarness(ChartHarness.Combine(
            ChartHarness.Series(typeof(NTLineSeries<BenchDatum>), data, ("XValue", x), ("YValueSelector", (Func<BenchDatum, decimal>)(d => d.Y)), ("ShowDataLabels", true)),
            ChartHarness.Series(typeof(NTLineSeries<BenchDatum>), data, ("XValue", x), ("YValueSelector", (Func<BenchDatum, decimal>)(d => d.Y2)), ("ShowDataLabels", true))));
        var bars = BenchData.Create(20);
        _bar = new ChartHarness(ChartHarness.Combine(
            ChartHarness.Series(typeof(NTBarSeries<BenchDatum>), bars, ("XValue", (Func<BenchDatum, object>)(d => d.Name)), ("YValueSelector", (Func<BenchDatum, decimal>)(d => d.Y)), ("ShowDataLabels", true)),
            ChartHarness.Series(typeof(NTBarSeries<BenchDatum>), bars, ("XValue", (Func<BenchDatum, object>)(d => d.Name)), ("YValueSelector", (Func<BenchDatum, decimal>)(d => d.Y2)),
                ("SegmentSelector", (Func<BenchDatum, IEnumerable<NTBarSegment>>)(d => [new NTBarSegment { Value = d.Y2 - 50, Label = "A" }, new NTBarSegment { Value = 50, Label = "B" }])), ("ShowDataLabels", true))));
        _line.Paint();
        _bar.Paint();
    }

    [GlobalCleanup]
    public void Cleanup() {
        _line.Dispose();
        _bar.Dispose();
    }

    [Benchmark]
    public void LineLabelsSteadyRepaint() => _line.Paint();

    [Benchmark]
    public void BarLabelsSteadyRepaint() => _bar.Paint();
}
