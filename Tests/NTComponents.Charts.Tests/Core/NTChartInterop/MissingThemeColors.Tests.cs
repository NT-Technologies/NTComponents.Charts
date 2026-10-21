using Bunit.JSInterop;
using Microsoft.AspNetCore.Components;
using NTComponents.Charts.Core;
using SkiaSharp;

namespace NTComponents.Charts.Tests.ChartCore;

/// <summary>
///     A page without the theme runtime answers the theme-color request with null; the chart must keep rendering with its fallback colors.
/// </summary>
public class NTChartInterop_MissingThemeColors_Tests : BunitContext {
    private sealed record Point(string Month, decimal Value);

    [Fact]
    public void NullThemeColors_FallBackToDefaultColorsWithoutFailing() {
        JSInterop.Mode = JSRuntimeMode.Loose;
        var module = JSInterop.SetupModule("./_content/NTComponents.Charts/ntcomponents-charts.js");
        module.Setup<float>("getDevicePixelRatio").SetResult(1f);
        module.Setup<Dictionary<string, string?>?>("getThemeColors", _ => true).SetResult(null);
        ComponentFactories.AddStub(type => type.FullName is "SkiaSharp.Views.Blazor.SKGLView" or "SkiaSharp.Views.Blazor.SKCanvasView");
        Point[] data = [new("Jan", 10m), new("Feb", 20m)];

        var chart = Render<NTChart<Point>>(parameters => parameters.Add(p => p.ChildContent, (RenderFragment)(builder => {
            builder.OpenComponent<NTLineSeries<Point>>(0);
            builder.AddAttribute(1, "Data", data);
            builder.AddAttribute(2, "XValue", (Func<Point, object>)(p => p.Month));
            builder.AddAttribute(3, "YValueSelector", (Func<Point, decimal>)(p => p.Value));
            builder.CloseComponent();
        })));

        chart.WaitForAssertion(() => chart.Instance.GetThemeColor(NTColor.Primary).Should().Be(SKColors.RoyalBlue));
    }

    [Fact]
    public async Task ThemeRefreshWithEmptyVariables_KeepsTheColorsAlreadyApplied() {
        // A theme runtime raises its change event while swapping stylesheets, so the variables can read empty for a moment.
        JSInterop.Mode = JSRuntimeMode.Loose;
        var module = JSInterop.SetupModule("./_content/NTComponents.Charts/ntcomponents-charts.js");
        module.Setup<float>("getDevicePixelRatio").SetResult(1f);
        var theme = new Dictionary<string, string?> { ["Primary"] = "#904b3e", ["Surface"] = "#fff8f6" };
        module.Setup<Dictionary<string, string?>?>("getThemeColors", _ => true).SetResult(theme);
        ComponentFactories.AddStub(type => type.FullName is "SkiaSharp.Views.Blazor.SKGLView" or "SkiaSharp.Views.Blazor.SKCanvasView");
        var chart = Render<NTChart<Point>>();
        chart.WaitForAssertion(() => chart.Instance.GetThemeColor(NTColor.Primary).Should().Be(SKColor.Parse("#904b3e")));

        theme["Primary"] = null;
        theme["Surface"] = "";
        await chart.InvokeAsync(chart.Instance.OnThemeChanged);

        chart.Instance.GetThemeColor(NTColor.Primary).Should().Be(SKColor.Parse("#904b3e"));
        chart.Instance.GetThemeColor(NTColor.Surface).Should().Be(SKColor.Parse("#fff8f6"));
    }
}
