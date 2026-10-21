using BenchmarkDotNet.Attributes;
using NTComponents.Charts.Core.Axes;

namespace NTComponents.Charts.Benchmarks;

/// <summary>
///     The <see cref="LineBenchmarks"/> chart with a typed numeric X axis, so X is a continuous value axis instead of the
///     categorical axis the default <c>object</c> X selector produces.
/// </summary>
public class NumericXLineBenchmarks {
    private ChartHarness _chart = null!;

    [Params(100, 10_000)]
    public int Points { get; set; }

    [GlobalSetup]
    public void Setup() {
        var data = BenchData.Create(Points);
        var x = (Func<BenchDatum, object>)(d => d.X);
        _chart = new ChartHarness(ChartHarness.Combine(
            builder => {
                builder.OpenComponent<NTXAxisOptions<BenchDatum, double>>(0);
                builder.AddAttribute(1, "ValueSelector", (Func<BenchDatum, double>)(d => d.X));
                builder.CloseComponent();
            },
            ChartHarness.Series(typeof(NTLineSeries<BenchDatum>), data, ("XValue", x), ("YValueSelector", (Func<BenchDatum, decimal>)(d => d.Y)), ("Title", "A")),
            ChartHarness.Series(typeof(NTLineSeries<BenchDatum>), data, ("XValue", x), ("YValueSelector", (Func<BenchDatum, decimal>)(d => d.Y2)), ("Title", "B")),
            ChartHarness.Series(typeof(NTLineSeries<BenchDatum>), data, ("XValue", x), ("YValueSelector", (Func<BenchDatum, decimal>)(d => d.Y3)), ("Title", "C"))));
        _chart.Paint();
        if (_chart.Chart.XAxis.IsCategorical) {
            throw new InvalidOperationException("The numeric X axis did not register.");
        }
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
