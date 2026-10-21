using System.Reflection;
using Bunit.JSInterop;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using NTComponents.Charts.Core;
using NTComponents.Charts.Core.Series;
using SkiaSharp;

namespace NTComponents.Charts.Tests.Rendering;

/// <summary>
///     Renders a complete chart frame headlessly through <c>NTChart.OnPaintSurface</c> and compares it to a golden PNG.
///     Determinism: fixed literal data, fixed surface size, series animation disabled, explicit theme palette, and no
///     reliance on wall-clock time (bubble-pack physics is stepped with a fixed synthetic frame time until it sleeps).
/// </summary>
public abstract class GoldenChartTestBase : BunitContext {
    /// <summary>Logical chart width in CSS pixels.</summary>
    protected const int Width = 640;

    /// <summary>Logical chart height in CSS pixels.</summary>
    protected const int Height = 400;

    private static readonly Action<NTChart<GoldenRow>, SKCanvas, SKImageInfo> _paintFrame = typeof(NTChart<GoldenRow>)
        .GetMethod("OnPaintSurface", BindingFlags.Instance | BindingFlags.NonPublic, [typeof(SKCanvas), typeof(SKImageInfo)])!
        .CreateDelegate<Action<NTChart<GoldenRow>, SKCanvas, SKImageInfo>>();

    private static readonly PropertyInfo _lastPlotArea = typeof(NTChart<GoldenRow>).GetProperty("LastPlotArea", BindingFlags.Instance | BindingFlags.NonPublic)!;

    /// <summary>Sets up JS interop stubs with a stable theme palette and stubs the Skia views.</summary>
    protected GoldenChartTestBase() {
        JSInterop.Mode = JSRuntimeMode.Loose;
        var module = JSInterop.SetupModule("./_content/NTComponents.Charts/ntcomponents-charts.js");
        module.Setup<float>("getDevicePixelRatio").SetResult(1f);
        module.Setup<Dictionary<string, string?>>("getThemeColors", _ => true).SetResult(GoldenThemeColors.Create());
        ComponentFactories.AddStub(type => type.FullName is "SkiaSharp.Views.Blazor.SKGLView" or "SkiaSharp.Views.Blazor.SKCanvasView");
    }

    /// <summary>Renders a chart with the given children and waits until the theme palette is resolved.</summary>
    protected IRenderedComponent<NTChart<GoldenRow>> Chart(Action<GoldenChartBuilder> children, Action<ComponentParameterCollectionBuilder<NTChart<GoldenRow>>>? configure = null) {
        var chart = Render<NTChart<GoldenRow>>(parameters => {
            parameters.Add(p => p.AllowExport, false);
            configure?.Invoke(parameters);
            parameters.Add(p => p.ChildContent, (RenderFragment)(builder => children(new GoldenChartBuilder(builder))));
        });
        chart.WaitForAssertion(() => chart.Instance.GetThemeColor(NTColor.Primary).Should().Be(SKColor.Parse("#545A92"), "the stub palette must be resolved before painting"));
        return chart;
    }

    /// <summary>Paints a warm-up frame, then two settled frames; the settled frames must be pixel-identical and the first is compared to the golden.</summary>
    protected async Task VerifyAsync(string goldenName, IRenderedComponent<NTChart<GoldenRow>> chart, float density = 1f, Func<NTChart<GoldenRow>, SKRect, SKPoint>? hoverAt = null, Func<IRenderedComponent<NTChart<GoldenRow>>, Task>? prepare = null, bool exactRepaint = true) {
        var width = (int)(Width * density);
        var height = (int)(Height * density);
        if (density != 1f) {
            await chart.InvokeAsync(() => chart.Instance.OnNativeViewportChanged(true, density));
        }
        if (prepare is not null) {
            await prepare(chart);
        }
        if (hoverAt is not null) {
            using var warmUp = await PaintAsync(chart, width, height);
            var point = hoverAt(chart.Instance, (SKRect)_lastPlotArea.GetValue(chart.Instance)!);
            await chart.InvokeAsync(() => chart.Instance.OnNativeMouseMove(new MouseEventArgs { OffsetX = point.X / density, OffsetY = point.Y / density }));
        }

        // The first paint of a chart measures legend/title/axis chrome; the golden is the settled frame a user sees.
        (await PaintAsync(chart, width, height)).Dispose();
        using var first = await PaintAsync(chart, width, height);
        using var second = await PaintAsync(chart, width, height);

        if (hoverAt is not null) {
            chart.Instance.HoveredSeries.Should().NotBeNull("the scenario pointer must land on a data point so the hover highlight and tooltip render");
        }
        if (exactRepaint) {
            CountExactDifferences(first, second).Should().Be(0, "repainting an unchanged chart must reproduce the identical frame (caches must not alter output)");
        }
        else {
            GoldenImageComparer.Compare(first, second).Passes.Should().BeTrue("repainting an unchanged chart must stay within tolerance");
        }
        GoldenImageComparer.AssertMatchesGolden(goldenName, first);
    }

    /// <summary>Returns the pixel position of a category/value on the primary axes, for hover scenarios.</summary>
    protected static SKPoint PixelOf(NTChart<GoldenRow> chart, SKRect plotArea, object x, decimal y) => new(chart.ScaleX(chart.GetScaledXValue(x), plotArea), chart.ScaleY(y, plotArea));

    /// <summary>
    ///     Pins bubble positions to a fixed row and puts physics to sleep. Initial bubble positions come from
    ///     <c>HashCode.Combine</c>, which is randomized per process, so the library's own layout differs on every test run.
    /// </summary>
    protected async Task FreezeBubblePhysicsAsync(IRenderedComponent<NTChart<GoldenRow>> chart) {
        var type = typeof(NTBubblePackSeries<GoldenRow>);
        var flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var series = chart.Instance.Series.OfType<NTBubblePackSeries<GoldenRow>>().Single();
        var visible = (System.Collections.IList)type.GetField("_visibleBubbles", flags)!.GetValue(series)!;
        for (var frame = 0; frame < 10 && visible.Count == 0; frame++) {
            using var painted = await PaintAsync(chart, Width, Height);
        }
        visible.Count.Should().BeGreaterThan(0, "bubbles appear after the series has painted a few frames");
        var area = (SKRect)type.GetField("_lastContentArea", flags)!.GetValue(series)!;
        var states = visible.Cast<object>().Select(b => b.GetType().GetProperty("State")!.GetValue(b)!).ToArray();
        for (var i = 0; i < states.Length; i++) {
            var stateType = states[i].GetType();
            stateType.GetProperty("Position")!.SetValue(states[i], new SKPoint(area.MidX + ((i - ((states.Length - 1) / 2f)) * 200f), area.MidY));
            stateType.GetProperty("Velocity")!.SetValue(states[i], SKPoint.Empty);
        }
        type.GetField("_physicsAwake", flags)!.SetValue(series, false);
    }

    private static async Task<SKBitmap> PaintAsync(IRenderedComponent<NTChart<GoldenRow>> chart, int width, int height) {
        var info = new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Premul);
        using var surface = SKSurface.Create(info)!;
        await chart.InvokeAsync(() => _paintFrame(chart.Instance, surface.Canvas, info));
        using var image = surface.Snapshot();
        return SKBitmap.FromImage(image);
    }

    private static int CountExactDifferences(SKBitmap first, SKBitmap second) {
        var a = first.Pixels;
        var b = second.Pixels;
        var count = 0;
        for (var i = 0; i < a.Length; i++) {
            if (a[i] != b[i]) {
                count++;
            }
        }
        return count;
    }
}
