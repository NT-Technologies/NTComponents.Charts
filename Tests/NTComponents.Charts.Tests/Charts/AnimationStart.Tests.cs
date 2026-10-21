using System.Diagnostics;
using System.Reflection;
using Bunit.JSInterop;
using Microsoft.AspNetCore.Components;
using NTComponents.Charts.Core;
using NTComponents.Charts.Core.Series;
using SkiaSharp;

namespace NTComponents.Charts.Tests.Charts;

public class AnimationStart_Tests : BunitContext {
    private static readonly long TicksPerMs = Stopwatch.Frequency / 1000;

    public AnimationStart_Tests() {
        JSInterop.Mode = JSRuntimeMode.Loose;
        var module = JSInterop.SetupModule("./_content/NTComponents.Charts/ntcomponents-charts.js");
        module.Setup<float>("getDevicePixelRatio").SetResult(1f);
        module.Setup<Dictionary<string, string?>>("getThemeColors", _ => true).SetResult(new() { ["Surface"] = "#ffffff", ["PrimaryFixed"] = "#000000" });
        ComponentFactories.AddStub(type => type.FullName is "SkiaSharp.Views.Blazor.SKGLView" or "SkiaSharp.Views.Blazor.SKCanvasView");
    }

    [Fact]
    public async Task AfterDataArrives_ProgressStaysZeroUntilTheFirstRenderThenRunsForItsDuration() {
        var chart = CreateChart(animation: true);
        var series = chart.Instance.Series.Cast<ProbeSeries>().Single();
        series.Running.Should().BeTrue("a pending animation keeps the render loop requesting frames");
        series.RequiresAnimationFrame.Should().BeTrue();
        series.Running.Should().BeTrue("asking whether it runs must not start the clock");

        await Paint(chart);
        series.Progress.Should().BeLessThan(0.05f, "the clock starts at the first render");
        series.RequiresAnimationFrame.Should().BeTrue();

        series.AgeClock(TimeSpan.FromMilliseconds(500));
        series.Progress.Should().BeApproximately(0.5f, 0.05f);
        series.AgeClock(TimeSpan.FromMilliseconds(1100));
        series.Progress.Should().Be(1f);
        series.RequiresAnimationFrame.Should().BeFalse();
    }

    [Fact]
    public async Task FirstRenderLongAfterDataWasSet_StillAnimates() {
        var chart = CreateChart(animation: true);
        var series = chart.Instance.Series.Cast<ProbeSeries>().Single();
        series.AgeClock(TimeSpan.FromSeconds(30), keepPending: true);

        await Paint(chart);

        series.Progress.Should().BeLessThan(0.05f);
        series.RequiresAnimationFrame.Should().BeTrue();
    }

    [Fact]
    public void ExplicitlyPinnedStart_ClearsThePendingFlag() {
        var chart = CreateChart(animation: true);
        var series = chart.Instance.Series.Cast<ProbeSeries>().Single();

        series.AgeClock(TimeSpan.FromSeconds(2));

        series.Progress.Should().Be(1f);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, true)]
    public async Task WhenAnimationIsNotActive_ProgressSnapsToOneImmediately(bool animationEnabled, bool reducedMotion) {
        var chart = CreateChart(animationEnabled);
        await chart.InvokeAsync(() => chart.Instance.OnReducedMotionChanged(reducedMotion));
        var series = chart.Instance.Series.Cast<ProbeSeries>().Single();

        series.Progress.Should().Be(1f);
        series.Running.Should().BeFalse();
        series.RequiresAnimationFrame.Should().BeFalse();
    }

    private IRenderedComponent<NTChart<Point>> CreateChart(bool animation) {
        Point[] data = [new(0, 1), new(1, 4), new(2, 2)];
        return Render<NTChart<Point>>(parameters => parameters.Add(p => p.ChildContent, (RenderFragment)(builder => {
                builder.OpenComponent<ProbeSeries>(0);
                builder.AddAttribute(1, "Data", data);
                builder.AddAttribute(2, "XValue", (Func<Point, object>)(p => p.X));
                builder.AddAttribute(3, "YValueSelector", (Func<Point, decimal>)(p => p.Y));
                builder.AddAttribute(4, "AnimationEnabled", animation);
                builder.AddAttribute(5, "AnimationDuration", NTMotionDuration.Ms1000);
                builder.CloseComponent();
            })));
    }

    private static async Task Paint(IRenderedComponent<NTChart<Point>> chart) {
        using var surface = SKSurface.Create(new SKImageInfo(640, 320));
        await chart.InvokeAsync(() => typeof(NTChart<Point>).GetMethod("OnPaintSurface", BindingFlags.Instance | BindingFlags.NonPublic, [typeof(SKCanvas), typeof(SKImageInfo)])!.Invoke(chart.Instance, [surface.Canvas, new SKImageInfo(640, 320)]));
    }

    /// <summary>Line series exposing the animation clock without wall-clock waits.</summary>
    private sealed class ProbeSeries : NTLineSeries<Point> {
        public float Progress => GetAnimationProgress();
        public bool Running => IsAnimationRunning;

        /// <summary>Moves the clock start back by <paramref name="age" />; pins the start unless <paramref name="keepPending" />.</summary>
        public void AgeClock(TimeSpan age, bool keepPending = false) {
            if (keepPending) {
                typeof(NTBaseSeries<Point>).GetField("_animationStartTimestamp", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(this, Stopwatch.GetTimestamp() - (long)age.TotalMilliseconds * TicksPerMs);
                return;
            }

            AnimationStartTime = DateTime.Now - age;
        }
    }

    private sealed class Point(double x, decimal y) {
        public double X { get; } = x;
        public decimal Y { get; } = y;
    }
}
