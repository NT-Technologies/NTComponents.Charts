using System.Reflection;
using Bunit.JSInterop;
using Microsoft.AspNetCore.Components;
using NTComponents.Charts;
using NTComponents.Charts.Core;
using NTComponents.Charts.Core.Axes;
using NTComponents.Charts.Core.Series;

namespace NTComponents.Charts.Tests.Charts;

public class SeriesInvalidation_Tests : BunitContext {
    public SeriesInvalidation_Tests() {
        JSInterop.Mode = JSRuntimeMode.Loose;
        var module = JSInterop.SetupModule("./_content/NTComponents.Charts/ntcomponents-charts.js");
        module.Setup<float>("getDevicePixelRatio").SetResult(1f);
        module.Setup<Dictionary<string, string?>>("getThemeColors", _ => true).SetResult([]);
        ComponentFactories.AddStub(type => type.FullName is "SkiaSharp.Views.Blazor.SKGLView" or "SkiaSharp.Views.Blazor.SKCanvasView");
    }

    [Fact]
    public void Invalidate_after_in_place_data_mutation_recalculates_cached_ranges() {
        var data = new[] { new Datum(1, 10m, 100m) };
        var cut = RenderChart(data);
        var series = cut.FindComponent<NTLineSeries<Datum>>().Instance;

        series.GetYRange().Should().Be((10m, 10m));
        data[0].Value = 35m;
        series.Invalidate();

        series.GetYRange().Should().Be((35m, 35m));
    }

    [Fact]
    public void Changing_value_selector_invalidates_cached_range() {
        var data = new[] { new Datum(1, 10m, 100m) };
        var cut = RenderChart(data);
        var series = cut.FindComponent<NTLineSeries<Datum>>().Instance;
        series.GetYRange().Should().Be((10m, 10m));

        cut.Render(parameters => parameters.Add(chart => chart.ChildContent, SeriesContent(data, point => point.X, point => point.Other)));

        series.GetYRange().Should().Be((100m, 100m));
    }

    [Fact]
    public void Changing_x_selector_invalidates_materialized_points_and_range() {
        var data = new[] { new Datum(1, 10m, 100m), new Datum(5, 20m, 200m) };
        var cut = RenderChart(data);
        cut.Instance.RegisterAxis(new NTXAxisOptions<Datum, int> { ValueSelector = point => point.X });
        var series = cut.FindComponent<NTLineSeries<Datum>>().Instance;
        series.GetXRange().Should().Be((1d, 5d));

        cut.Render(parameters => parameters.Add(chart => chart.ChildContent, SeriesContent(data, point => point.Other, point => point.Value)));

        series.GetXRange().Should().Be((100d, 200d));
    }

    [Fact]
    public void Disabling_animation_completes_pending_series_animation() {
        var cut = Render<NTChart<Datum>>(parameters => parameters
            .Add(chart => chart.ChildContent, (RenderFragment)(builder => {
                builder.OpenComponent<NTLineSeries<Datum>>(0);
                builder.AddAttribute(1, "Data", new[] { new Datum(1, 10m, 100m) });
                builder.AddAttribute(2, "XValue", (Func<Datum, object>)(point => point.X));
                builder.AddAttribute(3, "YValueSelector", (Func<Datum, decimal>)(point => point.Value));
                builder.AddAttribute(4, "AnimationEnabled", true);
                builder.AddAttribute(5, "AnimationDuration", NTMotionDuration.Ms1000);
                builder.CloseComponent();
            })));
        var series = cut.FindComponent<NTLineSeries<Datum>>().Instance;

        RequiresAnimationFrame(series).Should().BeTrue();
        series.AnimationEnabled = false;

        RequiresAnimationFrame(series).Should().BeFalse();
        ((float)typeof(NTBaseSeries<Datum>).GetMethod("GetAnimationProgress", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(series, null)!).Should().Be(1f);
    }

    private IRenderedComponent<NTChart<Datum>> RenderChart(Datum[] data) => Render<NTChart<Datum>>(parameters => parameters
        .Add(chart => chart.ChildContent, SeriesContent(data, point => point.X, point => point.Value)));

    private static RenderFragment SeriesContent(Datum[] data, Func<Datum, object> xSelector, Func<Datum, decimal> ySelector) => builder => {
            builder.OpenComponent<NTLineSeries<Datum>>(0);
            builder.AddAttribute(1, "Data", data);
            builder.AddAttribute(2, "XValue", xSelector);
            builder.AddAttribute(3, "YValueSelector", ySelector);
            builder.AddAttribute(4, "AnimationEnabled", false);
            builder.CloseComponent();
        };

    private static bool RequiresAnimationFrame(NTBaseSeries<Datum> series) => (bool)typeof(NTBaseSeries<Datum>)
        .GetProperty("RequiresAnimationFrame", BindingFlags.Instance | BindingFlags.NonPublic)!
        .GetValue(series)!;

    private sealed class Datum(int x, decimal value, decimal other) {
        public int X { get; } = x;
        public decimal Value { get; set; } = value;
        public decimal Other { get; } = other;
    }
}
