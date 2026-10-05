using System.Reflection;
using Bunit.JSInterop;
using Microsoft.AspNetCore.Components;
using NTComponents.Charts.Core;
using NTComponents.Charts.Core.Series;
using SkiaSharp;
using SkiaSharp.Views.Blazor;

namespace NTComponents.Charts.Tests.Charts;

public class ChartMotion_Tests : BunitContext {
    public ChartMotion_Tests() {
        JSInterop.Mode = JSRuntimeMode.Loose;
        var module = JSInterop.SetupModule("./_content/NTComponents.Charts/ntcomponents-charts.js");
        module.Setup<float>("getDevicePixelRatio").SetResult(1f);
        module.Setup<Dictionary<string, string?>>("getThemeColors", _ => true).SetResult([]);
#pragma warning disable CA1416
        ComponentFactories.Add<SKGLView, TestSkiaGlView>();
        ComponentFactories.Add<SKCanvasView, TestSkiaCanvasView>();
#pragma warning restore CA1416
    }

    [Theory]
    [InlineData(NTMotionDuration.Ms50, 50)]
    [InlineData(NTMotionDuration.Ms100, 100)]
    [InlineData(NTMotionDuration.Ms150, 150)]
    [InlineData(NTMotionDuration.Ms200, 200)]
    [InlineData(NTMotionDuration.Ms250, 250)]
    [InlineData(NTMotionDuration.Ms300, 300)]
    [InlineData(NTMotionDuration.Ms350, 350)]
    [InlineData(NTMotionDuration.Ms400, 400)]
    [InlineData(NTMotionDuration.Ms450, 450)]
    [InlineData(NTMotionDuration.Ms500, 500)]
    [InlineData(NTMotionDuration.Ms550, 550)]
    [InlineData(NTMotionDuration.Ms600, 600)]
    [InlineData(NTMotionDuration.Ms700, 700)]
    [InlineData(NTMotionDuration.Ms800, 800)]
    [InlineData(NTMotionDuration.Ms900, 900)]
    [InlineData(NTMotionDuration.Ms1000, 1000)]
    public void Duration_tokens_use_their_millisecond_timings_and_clamp_progress(NTMotionDuration duration, int milliseconds) {
        Motion("Progress", TimeSpan.FromMilliseconds(-1), duration).Should().Be(0f);
        Motion("Progress", TimeSpan.Zero, duration).Should().Be(0f);
        Motion("Progress", TimeSpan.FromMilliseconds(milliseconds / 2d), duration).Should().Be(0.5f);
        Motion("Progress", TimeSpan.FromMilliseconds(milliseconds), duration).Should().Be(1f);
        Motion("Progress", TimeSpan.FromMilliseconds(milliseconds + 1), duration).Should().Be(1f);
    }

    // Samples from NTComponents' NTShape runtime, independently evaluated in JavaScript.
    [Theory]
    [InlineData(NTMotionEasing.Emphasized, 0.607219f, 0.877834f, 0.975480f)]
    [InlineData(NTMotionEasing.EmphasizedDecelerate, 0.831530f, 0.950247f, 0.990511f)]
    [InlineData(NTMotionEasing.EmphasizedAccelerate, 0.035343f, 0.153998f, 0.405585f)]
    [InlineData(NTMotionEasing.Standard, 0.607219f, 0.877834f, 0.975480f)]
    [InlineData(NTMotionEasing.StandardDecelerate, 0.690551f, 0.889881f, 0.976445f)]
    [InlineData(NTMotionEasing.StandardAccelerate, 0.128502f, 0.372030f, 0.667569f)]
    public void Easing_matches_NTComponents_curves_without_overshoot(NTMotionEasing easing, float quarter, float half, float threeQuarters) {
        Motion("Ease", -0.1f, easing).Should().Be(0f);
        Motion("Ease", 0.25f, easing).Should().BeApproximately(quarter, 0.00001f);
        Motion("Ease", 0.5f, easing).Should().BeApproximately(half, 0.00001f);
        Motion("Ease", 0.75f, easing).Should().BeApproximately(threeQuarters, 0.00001f);
        Motion("Ease", 1.1f, easing).Should().Be(1f);
    }

    [Fact]
    public void Invalid_duration_tokens_fail_explicitly() {
        var act = () => Motion("Progress", TimeSpan.Zero, (NTMotionDuration)int.MaxValue);

        act.Should().Throw<TargetInvocationException>().WithInnerException<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Invalid_easing_tokens_fail_explicitly() {
        var act = () => Motion("Ease", 0.5f, (NTMotionEasing)int.MaxValue);

        act.Should().Throw<TargetInvocationException>().WithInnerException<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Default_motion_uses_NTComponents_tokens_for_series_and_hover() {
        var cut = RenderChart();
        var series = cut.FindComponent<NTLineSeries<Datum>>().Instance;

        series.AnimationDuration.Should().Be(NTMotionDuration.Ms500);
        series.AnimationEasing.Should().Be(NTMotionEasing.Emphasized);
        cut.Instance.HoverAnimationDuration.Should().Be(NTMotionDuration.Ms250);
        cut.Instance.HoverAnimationEasing.Should().Be(NTMotionEasing.Standard);
    }

    [Fact]
    public void Configured_easing_is_used_by_the_series() {
        var cut = Render<NTChart<Datum>>(parameters => parameters
            .Add(p => p.HoverAnimationDuration, NTMotionDuration.Ms150)
            .Add(p => p.HoverAnimationEasing, NTMotionEasing.StandardAccelerate)
            .Add(p => p.ChildContent, (RenderFragment)(builder => {
                builder.OpenComponent<NTLineSeries<Datum>>(0);
                builder.AddAttribute(1, "Data", new Datum[] { new("Sales", 10m) });
                builder.AddAttribute(2, "XValue", (Func<Datum, object>)(point => point.Name));
                builder.AddAttribute(3, "YValueSelector", (Func<Datum, decimal>)(point => point.Value));
                builder.AddAttribute(4, "AnimationDuration", NTMotionDuration.Ms400);
                builder.AddAttribute(5, "AnimationEasing", NTMotionEasing.EmphasizedAccelerate);
                builder.CloseComponent();
            })));
        var series = cut.FindComponent<NTLineSeries<Datum>>().Instance;
        var eased = (float)typeof(NTBaseSeries<Datum>)
            .GetMethod("EaseAnimation", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(series, [0.5f])!;

        series.AnimationDuration.Should().Be(NTMotionDuration.Ms400);
        eased.Should().BeApproximately(0.153998f, 0.00001f);
        cut.Instance.HoverAnimationDuration.Should().Be(NTMotionDuration.Ms150);
        cut.Instance.HoverAnimationEasing.Should().Be(NTMotionEasing.StandardAccelerate);
    }

    [Fact]
    public void Visibility_change_animates_from_the_previous_visible_state() {
        var cut = RenderChart();
        var series = cut.FindComponent<NTLineSeries<Datum>>().Instance;
        typeof(NTBaseSeries<Datum>).GetProperty(nameof(series.AnimationEnabled))!.SetValue(series, true);
        typeof(NTBaseSeries<Datum>).GetProperty(nameof(series.Visible))!.SetValue(series, false);
        typeof(NTBaseSeries<Datum>).GetMethod("HandleVisibilityChanged", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(series, null);
        var start = typeof(NTBaseSeries<Datum>).GetField("_visibilityAnimationStartTime", BindingFlags.Instance | BindingFlags.NonPublic)!;

        start.SetValue(series, TimestampFor(DateTime.Now.AddDays(1)));
        series.VisibilityFactor.Should().Be(1f, "hiding starts from the previously visible state");
        start.SetValue(series, TimestampFor(DateTime.Now.AddDays(-1)));
        series.VisibilityFactor.Should().Be(0f, "hiding completes after the selected duration");
    }

    [Fact]
    public void Disabled_pie_animation_snaps_hover_explosion_to_its_target() {
        var cut = RenderChart("pie");
        var series = cut.FindComponent<NTPieSeries<Datum>>().Instance;
        typeof(NTChart<Datum>).GetProperty(nameof(NTChart<Datum>.HoveredSeries))!.SetValue(cut.Instance, series);
        typeof(NTChart<Datum>).GetProperty(nameof(NTChart<Datum>.HoveredPointIndex))!.SetValue(cut.Instance, 0);
        var info = new SKImageInfo(320, 240);
        using var surface = SKSurface.Create(info)!;
        var context = Context(cut.Instance, surface, info);

        series.Render(context, context.TotalArea);

        var factors = (Dictionary<int, float>)typeof(NTPieSeries<Datum>)
            .GetField("_explosionFactors", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(series)!;
        factors[0].Should().Be(1f);
        typeof(NTChart<Datum>).GetProperty(nameof(NTChart<Datum>.HoveredPointIndex))!.SetValue(cut.Instance, null);
        series.Render(context, context.TotalArea);
        factors[0].Should().Be(0f);
    }

    [Fact]
    public void Disabled_bubble_animation_draws_the_full_target_radius() {
        var cut = RenderChart("bubble");
        var series = cut.FindComponent<NTBubblePackSeries<Datum>>().Instance;
        var info = new SKImageInfo(320, 240);
        using var surface = SKSurface.Create(info)!;
        var context = Context(cut.Instance, surface, info);

        series.Render(context, context.TotalArea);

        var bubbles = (System.Collections.IEnumerable)typeof(NTBubblePackSeries<Datum>)
            .GetField("_visibleBubbles", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(series)!;
        var bubble = bubbles.Cast<object>().Single();
        var state = bubble.GetType().GetProperty("State")!.GetValue(bubble)!;
        var radius = (float)state.GetType().GetProperty("Radius")!.GetValue(state)!;
        var targetRadius = (float)bubble.GetType().GetProperty("TargetRadius")!.GetValue(bubble)!;
        radius.Should().Be(targetRadius);
    }

    [Fact]
    public void Disabled_treemap_animation_completes_hover_immediately() {
        var cut = RenderChart("treemap");
        var series = cut.FindComponent<NTTreeMapSeries<Datum>>().Instance;
        var type = typeof(NTTreeMapSeries<Datum>);
        type.GetField("_hoverAnimFromIndex", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(series, 0);
        type.GetField("_hoverAnimToIndex", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(series, 1);
        type.GetField("_hoverAnimStartTimestamp", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(series, TimestampFor(DateTime.Now.AddDays(1)));

        var progress = (float)type.GetMethod("GetHoverAnimationProgress", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(series, null)!;

        progress.Should().Be(1f);
    }

    private IRenderedComponent<NTChart<Datum>> RenderChart(string kind = "line") => Render<NTChart<Datum>>(parameters => parameters
        .Add(p => p.ChildContent, (RenderFragment)(builder => {
            builder.OpenComponent(0, kind switch {
                "pie" => typeof(NTPieSeries<Datum>),
                "bubble" => typeof(NTBubblePackSeries<Datum>),
                "treemap" => typeof(NTTreeMapSeries<Datum>),
                _ => typeof(NTLineSeries<Datum>)
            });
            builder.AddAttribute(1, "Data", new Datum[] { new("Sales", 10m) });
            builder.AddAttribute(2, "XValue", (Func<Datum, object>)(point => point.Name));
            builder.AddAttribute(3, kind == "line" ? "YValueSelector" : "ValueSelector", (Func<Datum, decimal>)(point => point.Value));
            builder.AddAttribute(4, "AnimationEnabled", false);
            builder.CloseComponent();
        })));

    private static NTRenderContext Context(NTChart<Datum> chart, SKSurface surface, SKImageInfo info) => new() {
        Canvas = surface.Canvas,
        DefaultFont = chart.DefaultFont,
        RegularFont = chart.RegularFont,
        Density = 1f,
        Info = info,
        PlotArea = new SKRect(0, 0, info.Width, info.Height),
        TextColor = SKColors.Black,
        TotalArea = new SKRect(0, 0, info.Width, info.Height)
    };

    private static float Motion(string method, params object[] arguments) => (float)typeof(NTChart<>).Assembly
        .GetType("NTComponents.Charts.Core.NTChartMotion")!
        .GetMethod(method, BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, arguments)!;

    private static long TimestampFor(DateTime startTime) => (long)typeof(NTChart<>).Assembly
        .GetType("NTComponents.Charts.Core.NTChartMotion")!
        .GetMethod("TimestampFor", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, [startTime])!;

    private sealed record Datum(string Name, decimal Value);

    private sealed class TestSkiaGlView : SKGLView {
        protected override Task OnAfterRenderAsync(bool firstRender) => Task.CompletedTask;
    }

    private sealed class TestSkiaCanvasView : SKCanvasView {
        protected override Task OnAfterRenderAsync(bool firstRender) => Task.CompletedTask;
    }
}
