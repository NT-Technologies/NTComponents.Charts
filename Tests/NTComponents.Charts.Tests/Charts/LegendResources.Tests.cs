using Microsoft.AspNetCore.Components;
using Bunit.JSInterop;
using NTComponents.Charts.Core;
using NTComponents.Charts;
using SkiaSharp;

namespace NTComponents.Charts.Tests.Charts;

public class LegendResources_Tests : BunitContext {
    public LegendResources_Tests() {
        JSInterop.Mode = JSRuntimeMode.Loose;
        var module = JSInterop.SetupModule("./_content/NTComponents.Charts/ntcomponents-charts.js");
        module.Setup<float>("getDevicePixelRatio").SetResult(1f);
        module.Setup<Dictionary<string, string?>>("getThemeColors", _ => true).SetResult([]);
        ComponentFactories.AddStub(type => type.FullName is "SkiaSharp.Views.Blazor.SKGLView" or "SkiaSharp.Views.Blazor.SKCanvasView");
    }

    [Fact]
    public void Stable_frames_reuse_legend_font_and_paints() {
        var chart = RenderChart();
        var legend = chart.FindComponent<NTLegend<Point>>().Instance;
        using var surface = SKSurface.Create(new SKImageInfo(640, 480))!;
        var context = CreateContext(chart.Instance, surface);

        legend.Render(context, context.TotalArea);
        var font = GetField<SKFont>(legend, "_font");
        var iconPaint = GetField<SKPaint>(legend, "_iconPaint");
        var textPaint = GetField<SKPaint>(legend, "_textPaint");

        legend.Render(context, context.TotalArea);

        GetField<SKFont>(legend, "_font").Should().BeSameAs(font);
        GetField<SKPaint>(legend, "_iconPaint").Should().BeSameAs(iconPaint);
        GetField<SKPaint>(legend, "_textPaint").Should().BeSameAs(textPaint);
    }

    [Fact]
    public void Changed_font_size_updates_reused_font_and_legend_layout() {
        var chart = RenderChart();
        var legend = chart.FindComponent<NTLegend<Point>>();
        using var surface = SKSurface.Create(new SKImageInfo(640, 480))!;
        var context = CreateContext(chart.Instance, surface);

        legend.Instance.Render(context, context.TotalArea);
        var font = GetField<SKFont>(legend.Instance, "_font");
        var originalHeight = GetProperty<SKRect>(legend.Instance, "LastDrawArea").Height;

        legend.Render(parameters => parameters.Add(p => p.FontSize, 20f));
        legend.Instance.Render(context, context.TotalArea);

        GetField<SKFont>(legend.Instance, "_font").Should().BeSameAs(font);
        font.Size.Should().Be(20f);
        GetProperty<SKRect>(legend.Instance, "LastDrawArea").Height.Should().BeGreaterThan(originalHeight);
    }

    [Fact]
    public void Title_invalidation_reuses_owned_font_and_paint() {
        var chart = RenderChart();
        var title = (IRenderable)GetField<object>(chart.Instance, "_title");
        using var surface = SKSurface.Create(new SKImageInfo(640, 480))!;
        var context = CreateContext(chart.Instance, surface);

        title.Render(context, context.TotalArea);
        var titleFont = GetField<SKFont>(title, "_titleFont");
        var titlePaint = GetField<SKPaint>(title, "_titlePaint");

        title.Invalidate();
        title.Render(context, context.TotalArea);

        GetField<SKFont>(title, "_titleFont").Should().BeSameAs(titleFont);
        GetField<SKPaint>(title, "_titlePaint").Should().BeSameAs(titlePaint);
    }

    private IRenderedComponent<NTChart<Point>> RenderChart() => Render<NTChart<Point>>(parameters => parameters
        .Add(p => p.TitleOptions, new NTTitleOptions("Resources"))
        .Add(p => p.ChildContent, (RenderFragment)(builder => {
            builder.OpenComponent<NTLineSeries<Point>>(0);
            builder.AddAttribute(1, "Data", new[] { new Point("First", 10m), new Point("Second", 20m) });
            builder.AddAttribute(2, "XValue", (Func<Point, object>)(point => point.Name));
            builder.AddAttribute(3, "YValueSelector", (Func<Point, decimal>)(point => point.Value));
            builder.AddAttribute(4, "AnimationEnabled", false);
            builder.CloseComponent();
            builder.OpenComponent<NTLegend<Point>>(5);
            builder.CloseComponent();
        })));

    private static NTRenderContext CreateContext(NTChart<Point> chart, SKSurface surface) => new() {
        Canvas = surface.Canvas,
        DefaultFont = chart.DefaultFont,
        RegularFont = chart.RegularFont,
        Density = 1f,
        Info = new SKImageInfo(640, 480),
        PlotArea = new SKRect(0, 0, 640, 480),
        TextColor = SKColors.Black,
        TotalArea = new SKRect(0, 0, 640, 480)
    };

    private static TField GetField<TField>(object target, string name) where TField : class =>
        (TField)target.GetType().GetField(name, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(target)!;

    private static TProperty GetProperty<TProperty>(object target, string name) =>
        (TProperty)target.GetType().GetProperty(name, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(target)!;

    private sealed record Point(string Name, decimal Value);
}
