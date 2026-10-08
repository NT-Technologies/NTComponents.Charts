using System.Reflection;
using Bunit.JSInterop;
using Microsoft.AspNetCore.Components;
using NTComponents.Charts.Core;
using NTComponents.Charts.Core.Series;
using SkiaSharp;

namespace NTComponents.Charts.Tests.Charts;

public class RobotoDrawing_Tests : BunitContext {
    private static readonly Point[] _points = [new("Alpha", 10m), new("Beta", 20m), new("Gamma", 15m)];

    public RobotoDrawing_Tests() {
        JSInterop.Mode = JSRuntimeMode.Loose;
        var module = JSInterop.SetupModule("./_content/NTComponents.Charts/ntcomponents-charts.js");
        module.Setup<float>("getDevicePixelRatio").SetResult(1f);
        module.Setup<Dictionary<string, string?>>("getThemeColors", _ => true).SetResult(new() { [nameof(NTColor.OnSurface)] = "#000000" });
        ComponentFactories.AddStub(type => type.FullName is "SkiaSharp.Views.Blazor.SKGLView" or "SkiaSharp.Views.Blazor.SKCanvasView");
    }

    [Theory]
    [InlineData("line")]
    [InlineData("bar")]
    [InlineData("pie")]
    [InlineData("radar")]
    [InlineData("treemap")]
    [InlineData("bubble")]
    public async Task Canvas_drawings_use_embedded_Roboto_weights_without_synthetic_bold(string kind) {
        var cut = RenderChart(kind);
        var info = new SKImageInfo(640, 480);
        using var surface = SKSurface.Create(info)!;

        await cut.InvokeAsync(() => typeof(NTChart<Point>)
            .GetMethod("OnPaintSurface", BindingFlags.Instance | BindingFlags.NonPublic, [typeof(SKCanvas), typeof(SKImageInfo)])!
            .Invoke(cut.Instance, [surface.Canvas, info]));

        AssertFont(Font(Field(cut.Instance, "_title"), "_titleFont"), 700);
        AssertFont(Font(cut.Instance.Legend!, "_font"), 700);
        AssertFont(Font(cut.Instance.Series.Single(), "_labelFont"), 700);
        AssertFont(Font(cut.Instance, "_debugFont"), 400);
        if (kind is "line" or "bar") {
            AssertFont(Font(cut.Instance.XAxis, "_textFont"), 700);
            AssertFont(Font(cut.Instance.YAxis, "_textFont"), 700);
        }
        if (kind is "treemap" or "bubble") {
            AssertFont(Font(cut.Instance.Series.Single(), "_navFont"), 500);
            AssertFont(Font(cut.Instance.Series.Single(), "_drillIndicatorFont"), 500);
        }
    }

    [Fact]
    public void Title_draws_the_exact_embedded_Roboto_Bold_glyphs() {
        var cut = Render<NTChart<Point>>(parameters => parameters
            .Add(p => p.TitleOptions, new NTTitleOptions("Roboto drawing 123") { TextColor = NTColor.OnSurface }));
        var info = new SKImageInfo(320, 80);
        using var actual = SKSurface.Create(info)!;
        using var expected = SKSurface.Create(info)!;
        actual.Canvas.Clear(SKColors.Transparent);
        expected.Canvas.Clear(SKColors.Transparent);
        var context = Context(cut.Instance, actual, info);

        ((IRenderable)Field(cut.Instance, "_title")).Render(context, context.TotalArea);
        using var stream = typeof(NTChart<>).Assembly.GetManifestResourceStream("NTComponents.Charts.Fonts.Roboto-Bold.ttf")!;
        using var fontData = SKData.Create(stream);
        using var typeface = SKTypeface.FromData(fontData)!;
        using var font = new SKFont(typeface, 20);
        using var paint = new SKPaint { Color = SKColors.Black, IsAntialias = true, Style = SKPaintStyle.Fill };
        expected.Canvas.DrawText("Roboto drawing 123", 160, 15, SKTextAlign.Center, font, paint);
        using var actualImage = actual.Snapshot();
        using var expectedImage = expected.Snapshot();
        using var actualBitmap = SKBitmap.FromImage(actualImage);
        using var expectedBitmap = SKBitmap.FromImage(expectedImage);

        actualBitmap.Bytes.Should().Equal(expectedBitmap.Bytes);
    }

    [Fact]
    public void Tooltip_drawings_use_Roboto_Medium_labels_and_Bold_values() {
        var cut = RenderChart("line");
        var tooltip = cut.FindComponent<NTTooltip<Point>>().Instance;
        typeof(NTChart<Point>).GetProperty(nameof(NTChart<Point>.HoveredDataPoint))!.SetValue(cut.Instance, _points[0]);
        typeof(NTChart<Point>).GetProperty(nameof(NTChart<Point>.HoveredSeries))!.SetValue(cut.Instance, cut.Instance.Series.Single());
        typeof(NTChart<Point>).GetProperty(nameof(NTChart<Point>.LastMousePosition))!.SetValue(cut.Instance, new SKPoint(200, 200));
        var info = new SKImageInfo(640, 480);
        using var surface = SKSurface.Create(info)!;
        var context = Context(cut.Instance, surface, info);

        tooltip.Render(context, context.TotalArea);

        AssertFont(Font(tooltip, "_headerFont"), 500);
        AssertFont(Font(tooltip, "_labelFont"), 500);
        AssertFont(Font(tooltip, "_valueFont"), 700);
    }

    private IRenderedComponent<NTChart<Point>> RenderChart(string kind) {
        var seriesType = kind switch {
            "line" => typeof(NTLineSeries<Point>),
            "bar" => typeof(NTBarSeries<Point>),
            "pie" => typeof(NTPieSeries<Point>),
            "radar" => typeof(NTRadarSeries<Point>),
            "treemap" => typeof(NTTreeMapSeries<Point>),
            "bubble" => typeof(NTBubblePackSeries<Point>),
            _ => throw new ArgumentOutOfRangeException(nameof(kind))
        };
        return Render<NTChart<Point>>(parameters => parameters
            .Add(p => p.TitleOptions, new NTTitleOptions("Roboto drawings"))
            .Add(p => p.DebugView, true)
            .Add(p => p.ChildContent, (RenderFragment)(builder => {
                builder.OpenComponent(0, seriesType);
                builder.AddAttribute(1, "Data", _points);
                builder.AddAttribute(2, "XValue", (Func<Point, object>)(point => point.Name));
                builder.AddAttribute(3, kind is "line" or "bar" ? "YValueSelector" : "ValueSelector", (Func<Point, decimal>)(point => point.Value));
                builder.AddAttribute(4, "AnimationEnabled", false);
                builder.AddAttribute(5, "Title", "Sales");
                if (kind is "line" or "bar") {
                    builder.AddAttribute(6, "ShowDataLabels", true);
                }
                builder.CloseComponent();
                builder.OpenComponent<NTLegend<Point>>(7);
                builder.CloseComponent();
                builder.OpenComponent<NTTooltip<Point>>(8);
                builder.CloseComponent();
            })));
    }

    private static NTRenderContext Context(NTChart<Point> chart, SKSurface surface, SKImageInfo info) => new() {
        Canvas = surface.Canvas,
        DefaultFont = chart.DefaultFont,
        RegularFont = chart.RegularFont,
        Density = 1f,
        Info = info,
        PlotArea = new SKRect(0, 0, info.Width, info.Height),
        TextColor = SKColors.Black,
        TotalArea = new SKRect(0, 0, info.Width, info.Height)
    };

    private static SKFont Font(object target, string name) => (SKFont)Field(target, name);

    private static object Field(object target, string name) {
        for (var type = target.GetType(); type is not null; type = type.BaseType) {
            var field = type.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
            if (field is not null) {
                var value = field.GetValue(target);
                value.Should().NotBeNull($"{target.GetType().Name}.{name} must be initialized by drawing");
                return value!;
            }
        }
        throw new InvalidOperationException($"Font field {name} was not found on {target.GetType().Name}.");
    }

    private static void AssertFont(SKFont font, int weight) {
        font.Typeface.FamilyName.Should().Be("Roboto");
        font.Typeface.FontWeight.Should().Be(weight);
        font.Typeface.FontSlant.Should().Be(SKFontStyleSlant.Upright);
        font.Embolden.Should().BeFalse("the embedded font already has the correct weight");
    }

    private sealed record Point(string Name, decimal Value);
}
