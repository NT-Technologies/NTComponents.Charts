using System.Reflection;
using Bunit.JSInterop;
using Microsoft.AspNetCore.Components;
using NTComponents.Charts.Core;
using NTComponents.Charts.Core.Series;
using SkiaSharp;

namespace NTComponents.Charts.Tests.Charts;

public class SeriesCaching_Tests : BunitContext {
    public SeriesCaching_Tests() {
        JSInterop.Mode = JSRuntimeMode.Loose;
        var module = JSInterop.SetupModule("./_content/NTComponents.Charts/ntcomponents-charts.js");
        module.Setup<float>("getDevicePixelRatio").SetResult(1f);
        module.Setup<Dictionary<string, string?>>("getThemeColors", _ => true).SetResult([]);
        ComponentFactories.AddStub(type => type.FullName is "SkiaSharp.Views.Blazor.SKGLView" or "SkiaSharp.Views.Blazor.SKCanvasView");
    }

    [Fact]
    public void Line_paints_reuse_dash_and_hit_test_paths_until_geometry_changes() {
        var cut = RenderChart(typeof(NTLineSeries<Datum>), [new("A", 10m), new("B", 30m), new("C", 15m)], "YValueSelector", item => item.Value, new Dictionary<string, object> { ["LineStyle"] = LineStyle.Dashed });
        var series = cut.FindComponent<NTLineSeries<Datum>>().Instance;
        using var surface = SKSurface.Create(new SKImageInfo(300, 200))!;
        var context = CreateContext(cut.Instance, surface);
        var area = new SKRect(0, 0, 300, 180);

        series.Render(context, area);
        var dashEffect = Field(series, "_dashEffect");
        var linePath = Field(series, "_linePath");
        series.HitTest(new SKPoint(150, 90), area);
        var hitTestPath = Field(series, "_hitTestStrokePath");
        dashEffect.Should().NotBeNull();
        linePath.Should().NotBeNull();
        hitTestPath.Should().NotBeNull();

        series.Render(context, area);
        series.HitTest(new SKPoint(150, 90), area);

        Field(series, "_dashEffect").Should().BeSameAs(dashEffect);
        Field(series, "_linePath").Should().BeSameAs(linePath);
        Field(series, "_hitTestStrokePath").Should().BeSameAs(hitTestPath);

        series.StrokeWidth = 8f;
        series.HitTest(new SKPoint(150, 90), area);
        Field(series, "_hitTestStrokePath").Should().NotBeSameAs(hitTestPath);
    }

    [Fact]
    public void Pie_slice_values_are_cached_but_point_callbacks_run_on_each_paint() {
        var data = new List<Datum> { new("A", 10m), new("B", 20m) };
        var selectorCalls = 0;
        var renderCalls = 0;
        var cut = RenderChart(typeof(NTPieSeries<Datum>), data, "ValueSelector", item => { selectorCalls++; return item.Value; }, new Dictionary<string, object> { ["OnDataPointRender"] = (Action<NTDataPointRenderArgs<Datum>>)(_ => renderCalls++) });
        var series = cut.FindComponent<NTPieSeries<Datum>>().Instance;
        using var surface = SKSurface.Create(new SKImageInfo(300, 200))!;
        var context = CreateContext(cut.Instance, surface);
        var area = new SKRect(0, 0, 240, 180);

        series.Render(context, area);
        var firstSelectorCalls = selectorCalls;
        series.Render(context, area);

        selectorCalls.Should().Be(firstSelectorCalls);
        renderCalls.Should().Be(4);

        data[0] = data[0] with { Value = 30m };
        series.Invalidate();
        series.Render(context, area);

        selectorCalls.Should().BeGreaterThan(firstSelectorCalls);
        renderCalls.Should().Be(6);
    }

    [Fact]
    public void Radar_geometry_is_reused_and_rebuilt_after_invalidation() {
        var data = new List<Datum> { new("A", 10m), new("B", 20m), new("C", 15m) };
        var selectorCalls = 0;
        var renderCalls = 0;
        var cut = RenderChart(typeof(NTRadarSeries<Datum>), data, "ValueSelector", item => { selectorCalls++; return item.Value; }, new Dictionary<string, object> { ["OnDataPointRender"] = (Action<NTDataPointRenderArgs<Datum>>)(_ => renderCalls++) });
        var series = cut.FindComponent<NTRadarSeries<Datum>>().Instance;
        using var surface = SKSurface.Create(new SKImageInfo(300, 200))!;
        var context = CreateContext(cut.Instance, surface);
        var area = new SKRect(0, 0, 240, 180);

        series.Render(context, area);
        var points = Field(series, "_cachedRadarPoints");
        var firstSelectorCalls = selectorCalls;
        series.Render(context, area);

        Field(series, "_cachedRadarPoints").Should().BeSameAs(points);
        selectorCalls.Should().Be(firstSelectorCalls);
        renderCalls.Should().Be(6);

        data[0] = data[0] with { Value = 30m };
        series.Invalidate();
        series.Render(context, area);

        Field(series, "_cachedRadarPoints").Should().NotBeSameAs(points);
        selectorCalls.Should().BeGreaterThan(firstSelectorCalls);
        renderCalls.Should().Be(9);
    }

    [Fact]
    public void Bar_geometry_is_reused_until_explicit_invalidation() {
        var data = new List<Datum> { new("A", 10m), new("B", 20m) };
        var selectorCalls = 0;
        var renderCalls = 0;
        var cut = RenderChart(typeof(NTBarSeries<Datum>), data, "YValueSelector", item => { selectorCalls++; return item.Value; }, new Dictionary<string, object> { ["OnDataPointRender"] = (Action<NTDataPointRenderArgs<Datum>>)(_ => renderCalls++) });
        var series = cut.FindComponent<NTBarSeries<Datum>>().Instance;
        using var surface = SKSurface.Create(new SKImageInfo(300, 200))!;
        var context = CreateContext(cut.Instance, surface);
        var area = new SKRect(0, 0, 240, 180);

        series.Render(context, area);
        var rectangles = Field(series, "_cachedBarRects");
        var firstSelectorCalls = selectorCalls;
        series.Render(context, area);

        Field(series, "_cachedBarRects").Should().BeSameAs(rectangles);
        selectorCalls.Should().Be(firstSelectorCalls);
        renderCalls.Should().Be(4);

        data[0] = data[0] with { Value = 30m };
        series.Invalidate();
        series.Render(context, area);

        Field(series, "_cachedBarRects").Should().NotBeSameAs(rectangles);
        selectorCalls.Should().BeGreaterThan(firstSelectorCalls);
        renderCalls.Should().Be(6);
    }

    private IRenderedComponent<NTChart<Datum>> RenderChart(Type seriesType, IEnumerable<Datum> data, string selectorName, Func<Datum, decimal> selector, IReadOnlyDictionary<string, object>? extraAttributes = null) => Render<NTChart<Datum>>(parameters => parameters
        .Add(p => p.ChildContent, (RenderFragment)(builder => {
            builder.OpenComponent(0, seriesType);
            builder.AddAttribute(1, "Data", data);
            builder.AddAttribute(2, "XValue", (Func<Datum, object>)(item => item.Name));
            builder.AddAttribute(3, selectorName, selector);
            builder.AddAttribute(4, "AnimationEnabled", false);
            builder.AddAttribute(5, "ShowDataLabels", false);
            if (extraAttributes is not null) {
                foreach (var attribute in extraAttributes) {
                    builder.AddAttribute(6, attribute.Key, attribute.Value);
                }
            }
            builder.CloseComponent();
        })));

    private static NTRenderContext CreateContext(NTChart<Datum> chart, SKSurface surface) {
        var info = new SKImageInfo(300, 200);
        return new NTRenderContext {
            Canvas = surface.Canvas,
            DefaultFont = chart.DefaultFont,
            RegularFont = chart.RegularFont,
            Density = 1f,
            Info = info,
            PlotArea = new SKRect(0, 0, info.Width, info.Height),
            TextColor = SKColors.Black,
            TotalArea = new SKRect(0, 0, info.Width, info.Height)
        };
    }

    private static object Field(object instance, string name) => instance.GetType()
        .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(instance)!;

    private sealed record Datum(string Name, decimal Value);
}
