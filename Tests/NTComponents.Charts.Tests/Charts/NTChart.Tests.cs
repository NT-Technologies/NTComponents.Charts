using System.Reflection;
using Bunit.JSInterop;
using Microsoft.AspNetCore.Components;
using NTComponents.Charts;
using NTComponents.Charts.Core;
using SkiaSharp;

namespace NTComponents.Charts.Tests.Charts;

public class NTChart_Tests : BunitContext {
    private static readonly IReadOnlyList<BubblePoint> _bubbleData =
    [
        new("A", 12m),
        new("B", 28m),
        new("C", 7m)
    ];

    private static readonly IReadOnlyList<LinePoint> _lineData =
    [
        new(0d, 10m),
        new(1d, 15m),
        new(2d, 8m)
    ];

    public NTChart_Tests() {
        JSInterop.Mode = JSRuntimeMode.Loose;
        JSInterop.Setup<float>("eval", "window.devicePixelRatio || 1").SetResult(1f);
        ComponentFactories.AddStub(type => type.FullName is "SkiaSharp.Views.Blazor.SKGLView" or "SkiaSharp.Views.Blazor.SKCanvasView");
    }

    [Fact]
    public void RenderOrdered_places_annotation_between_series_and_tooltip() {
        // Assert
        ((int)RenderOrdered.Annotation).Should().Be((int)RenderOrdered.Series + 1);
        ((int)RenderOrdered.Tooltip).Should().Be((int)RenderOrdered.Annotation + 1);
    }

    [Fact]
    public void Chart_defaults_annotations_to_empty_collection() {
        // Arrange
        using var chart = new NTChart<LinePoint>();

        // Assert
        chart.Annotations.Should().NotBeNull().And.BeEmpty();
    }

    [Fact]
    public void Chart_uses_embedded_roboto_typefaces() {
        // Arrange
        var resources = typeof(NTChart<>).Assembly.GetManifestResourceNames();
        using var chart = new NTChart<LinePoint>();

        // Act
        var defaultTypeface = chart.DefaultFont.Typeface;
        var regularTypeface = chart.RegularFont.Typeface;

        // Assert
        resources.Should().Contain([
            "NTComponents.Charts.Fonts.OFL.txt",
            "NTComponents.Charts.Fonts.Roboto-Bold.ttf",
            "NTComponents.Charts.Fonts.Roboto-Medium.ttf",
            "NTComponents.Charts.Fonts.Roboto-Regular.ttf"
        ]);
        defaultTypeface.FamilyName.Should().Be("Roboto");
        defaultTypeface.FontWeight.Should().Be((int)SKFontStyleWeight.Bold);
        regularTypeface.FamilyName.Should().Be("Roboto");
        regularTypeface.FontWeight.Should().Be((int)SKFontStyleWeight.Medium);
    }

    [Fact]
    public void Debug_view_uses_embedded_roboto_regular_typeface() {
        // Arrange
        using var chart = new NTChart<LinePoint>();
        var info = new SKImageInfo(320, 240);
        using var surface = SKSurface.Create(info)!;
        var renderContext = new NTRenderContext {
            Canvas = surface.Canvas,
            DefaultFont = chart.DefaultFont,
            RegularFont = chart.RegularFont,
            Density = 1f,
            Info = info,
            PlotArea = new SKRect(10, 10, 310, 230),
            TextColor = SKColors.Black,
            TotalArea = new SKRect(0, 0, info.Width, info.Height)
        };

        // Act
        InvokeNonPublic(chart, "RenderDebugInfo", [renderContext]);
        var debugFont = (SKFont)GetPrivateField(chart, "_debugFont")!;

        // Assert
        debugFont.Typeface.FamilyName.Should().Be("Roboto");
        debugFont.Typeface.FontWeight.Should().Be((int)SKFontStyleWeight.Normal);
    }

    [Fact]
    public void ResolveAnnotationColor_clamps_opacity_bounds() {
        // Arrange
        var chart = new NTChart<LinePoint>();

        // Act
        var zeroOpacity = (SKColor)InvokeNonPublic(
            chart,
            "ResolveAnnotationColor",
            [TnTColor.Primary, -1f])!;
        var fullOpacity = (SKColor)InvokeNonPublic(
            chart,
            "ResolveAnnotationColor",
            [TnTColor.Primary, 2f])!;

        // Assert
        zeroOpacity.Alpha.Should().Be((byte)0);
        fullOpacity.Alpha.Should().Be((byte)255);
    }

    [Fact]
    public void ScaleAnnotationX_returns_null_when_value_is_null() {
        // Arrange
        var chart = new NTChart<LinePoint>();
        var plotArea = new SKRect(0, 0, 300, 200);

        // Act
        var result = (float?)InvokeNonPublic(chart, "ScaleAnnotationX", [null, plotArea]);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void RenderAnnotations_invokes_custom_renderer_for_custom_and_decorated_annotations() {
        // Arrange
        using var chart = new NTChart<LinePoint>();
        SetPrivateField(chart, "_chartCoordSystem", ChartCoordinateSystem.Cartesian);

        var callbackCount = 0;
        var scaleDelegatesWorked = false;
        chart.Annotations =
        [
            new NTChartAnnotation {
                Type = NTChartAnnotationType.YLine,
                Y = 0.4m,
                Label = "Threshold",
                CustomRenderer = _ => callbackCount++
            },
            new NTChartAnnotation {
                Type = NTChartAnnotationType.Custom,
                X = 0.5d,
                Y = 0.5m,
                CustomRenderer = context => {
                    callbackCount++;
                    var scaledX = context.ScaleX(context.Annotation.X);
                    var scaledY = context.ScaleY(context.Annotation.Y, context.Annotation.UseSecondaryYAxis);
                    scaleDelegatesWorked = scaledX.HasValue && scaledY.HasValue;
                }
            }
        ];

        var info = new SKImageInfo(320, 240);
        using var surface = SKSurface.Create(info)!;
        var plotArea = new SKRect(10, 10, 310, 230);
        var renderContext = new NTRenderContext {
            Canvas = surface.Canvas,
            DefaultFont = chart.DefaultFont,
            RegularFont = chart.RegularFont,
            Density = 1f,
            Info = info,
            PlotArea = plotArea,
            TextColor = SKColors.Black,
            TotalArea = new SKRect(0, 0, info.Width, info.Height)
        };

        // Act
        Action render = () => InvokeNonPublic(chart, "RenderAnnotations", [renderContext, plotArea]);

        // Assert
        render.Should().NotThrow();
        callbackCount.Should().Be(2);
        scaleDelegatesWorked.Should().BeTrue();
    }

    [Fact]
    public void BubbleChart_cursor_defaults_when_pan_flags_are_supplied() {
        // Arrange
        var cut = RenderBubbleChart(ChartInteractions.All);

        // Assert
        cut.WaitForAssertion(() => {
            var style = GetInteractiveContainerStyle(cut);
            style.Should().Contain("cursor: default");
            style.Should().NotContain("cursor: grab");
        });
    }

    [Fact]
    public void BubbleChart_with_no_interactions_shows_zoom_reset_button() {
        // Arrange
        var cut = RenderBubbleChart(ChartInteractions.None);

        // Assert
        cut.WaitForAssertion(() => {
            cut.Find(".nt-chart-buttons");
            cut.FindAll(".nt-chart-buttons button").Should().HaveCount(1);
        });
    }

    [Fact]
    public void LineChart_with_pan_interaction_shows_grab_cursor() {
        // Arrange
        var cut = RenderLineChart(ChartInteractions.XPan);

        // Assert
        cut.WaitForAssertion(() => GetInteractiveContainerStyle(cut).Should().Contain("cursor: grab"));
    }

    private IRenderedComponent<NTChart<BubblePoint>> RenderBubbleChart(ChartInteractions interactions) {
        return Render<NTChart<BubblePoint>>(parameters => parameters
            .Add(p => p.AllowExport, false)
            .Add(p => p.ChildContent, (RenderFragment)(builder => {
                builder.OpenComponent<NTBubblePackSeries<BubblePoint>>(0);
                builder.AddAttribute(1, "Data", _bubbleData);
                builder.AddAttribute(2, "XValue", (Func<BubblePoint, object>)(point => point.Name));
                builder.AddAttribute(3, "ValueSelector", (Func<BubblePoint, decimal>)(point => point.Value));
                builder.AddAttribute(4, "Interactions", interactions);
                builder.CloseComponent();
            })));
    }

    private IRenderedComponent<NTChart<LinePoint>> RenderLineChart(ChartInteractions interactions) {
        return Render<NTChart<LinePoint>>(parameters => parameters
            .Add(p => p.AllowExport, false)
            .Add(p => p.ChildContent, (RenderFragment)(builder => {
                builder.OpenComponent<NTLineSeries<LinePoint>>(0);
                builder.AddAttribute(1, "Data", _lineData);
                builder.AddAttribute(2, "XValue", (Func<LinePoint, object>)(point => point.X));
                builder.AddAttribute(3, "YValueSelector", (Func<LinePoint, decimal>)(point => point.Y));
                builder.AddAttribute(4, "Interactions", interactions);
                builder.CloseComponent();
            })));
    }

    private static string GetInteractiveContainerStyle<TData>(IRenderedComponent<NTChart<TData>> cut) where TData : class {
        var style = cut.FindAll("div")
            .Select(div => div.GetAttribute("style"))
            .FirstOrDefault(value => value?.Contains("cursor:") == true);

        style.Should().NotBeNull();
        return style!;
    }

    private static object? InvokeNonPublic(object target, string methodName, object?[] args) {
        var method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
        method.Should().NotBeNull($"private method '{methodName}' should exist on '{target.GetType().Name}'");
        return method!.Invoke(target, args);
    }

    private static object? GetPrivateField(object target, string fieldName) {
        var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        field.Should().NotBeNull($"private field '{fieldName}' should exist on '{target.GetType().Name}'");
        return field!.GetValue(target);
    }

    private static void SetPrivateField(object target, string fieldName, object value) {
        var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        field.Should().NotBeNull($"private field '{fieldName}' should exist on '{target.GetType().Name}'");
        field!.SetValue(target, value);
    }

    private sealed record BubblePoint(string Name, decimal Value);
    private sealed record LinePoint(double X, decimal Y);
}
