using System.Reflection;
using Bunit.JSInterop;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using NTComponents.Charts.Core;
using NTComponents.Charts.Core.Series;
using SkiaSharp;

namespace NTComponents.Charts.Tests.Charts;

public class RenderScheduling_Tests : BunitContext {
    private readonly Dictionary<string, string?> _colors = new() { ["Surface"] = "#ffffff", ["PrimaryFixed"] = "#000000" };

    public RenderScheduling_Tests() {
        JSInterop.Mode = JSRuntimeMode.Loose;
        var module = JSInterop.SetupModule("./_content/NTComponents.Charts/ntcomponents-charts.js");
        module.Setup<float>("getDevicePixelRatio").SetResult(1f);
        module.Setup<Dictionary<string, string?>>("getThemeColors", _ => true).SetResult(_colors);
        ComponentFactories.AddStub(type => type.FullName is "SkiaSharp.Views.Blazor.SKGLView" or "SkiaSharp.Views.Blazor.SKCanvasView");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Static_chart_disables_continuous_rendering(bool hardware) {
        var chart = CreateChart(animation: false, hardware: hardware);
        await Paint(chart);

        Field<bool>(chart.Instance, "_renderLoopEnabled").Should().BeFalse();
    }

    [Fact]
    public async Task Active_animation_starts_loop_and_final_frame_stops_it() {
        var chart = CreateChart(animation: true);
        await Paint(chart);
        Field<bool>(chart.Instance, "_renderLoopEnabled").Should().BeTrue();
        var series = chart.Instance.Series.Single();
        typeof(NTBaseSeries<Point>).GetProperty("AnimationStartTime", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(series, DateTime.Now.AddSeconds(-2));

        await Paint(chart);

        Field<bool>(chart.Instance, "_renderLoopEnabled").Should().BeFalse();
    }

    [Fact]
    public async Task Theme_redraw_preserves_animation_timestamp() {
        var chart = CreateChart(animation: true);
        var series = chart.Instance.Series.Single();
        var timestamp = Field<long>(series, "_animationStartTimestamp", typeof(NTBaseSeries<Point>));
        _colors["PrimaryFixed"] = "#ff0000";

        await chart.InvokeAsync(chart.Instance.OnThemeChanged);
        await Paint(chart);

        Field<long>(series, "_animationStartTimestamp", typeof(NTBaseSeries<Point>)).Should().Be(timestamp);
        chart.Instance.GetThemeColor(TnTColor.PrimaryFixed).Should().Be(SKColors.Red);
    }

    [Fact]
    public async Task Stationary_pointer_reuses_hit_test_and_static_axis_picture() {
        var chart = CreateChart(animation: false);
        await Paint(chart);
        await chart.InvokeAsync(() => chart.Instance.OnNativeMouseMove(new MouseEventArgs { OffsetX = 300, OffsetY = 150 }));
        await Paint(chart);
        var axisPicture = Field<SKPicture>(chart.Instance, "_axesPicture");
        var before = chart.RenderCount;

        await chart.InvokeAsync(() => chart.Instance.OnNativeMouseMove(new MouseEventArgs { OffsetX = 300, OffsetY = 150 }));
        await Paint(chart);

        chart.RenderCount.Should().Be(before);
        Field<bool>(chart.Instance, "_hitTestDirty").Should().BeFalse();
        Field<SKPicture>(chart.Instance, "_axesPicture").Should().BeSameAs(axisPicture);
    }

    [Fact]
    public async Task Hover_updates_cursor_once_and_reuses_it_for_unchanged_pointer() {
        var chart = CreateChart(animation: false);
        await Paint(chart);
        var area = Field<SKRect>(chart.Instance, "<LastPlotArea>k__BackingField");
        var pointer = new MouseEventArgs { OffsetX = chart.Instance.ScaleX(1, area), OffsetY = chart.Instance.ScaleY(4, area) };

        await chart.InvokeAsync(() => chart.Instance.OnNativeMouseMove(pointer));
        await Paint(chart);

        chart.Find(".nt-chart-canvas-host").GetAttribute("style").Should().Contain("cursor: pointer;");
        var renderCount = chart.RenderCount;
        await chart.InvokeAsync(() => chart.Instance.OnNativeMouseMove(pointer));
        await Paint(chart);
        chart.RenderCount.Should().Be(renderCount);

        await chart.InvokeAsync(() => chart.Instance.OnNativeMouseMove(new MouseEventArgs { OffsetX = 300, OffsetY = 250 }));
        await Paint(chart);
        await Paint(chart);
        chart.Find(".nt-chart-canvas-host").GetAttribute("style").Should().Contain("cursor: default;");
    }

    [Fact]
    public async Task Viewport_suspends_animation_and_density_change_invalidates_static_geometry() {
        var chart = CreateChart(animation: true);
        await Paint(chart);
        var picture = Field<SKPicture>(chart.Instance, "_axesPicture");

        await chart.InvokeAsync(() => chart.Instance.OnNativeViewportChanged(false, 1));
        Field<bool>(chart.Instance, "_renderLoopEnabled").Should().BeFalse();
        await chart.InvokeAsync(() => chart.Instance.OnNativeViewportChanged(true, 2));
        await Paint(chart);

        chart.Instance.Density.Should().Be(2);
        Field<SKPicture>(chart.Instance, "_axesPicture").Should().NotBeSameAs(picture);
        Field<bool>(chart.Instance, "_renderLoopEnabled").Should().BeTrue();
    }

    [Fact]
    public async Task Chart_invalidation_refreshes_pixels_after_mutating_existing_data() {
        var data = new[] { new Point(0, 1), new Point(1, 4), new Point(2, 2) };
        var chart = CreateChart(animation: false, data: data);
        using var surface = SKSurface.Create(new SKImageInfo(640, 320));
        await Paint(chart, surface);
        var before = Pixels(surface);
        data[1].Y = 2;

        await chart.InvokeAsync(chart.Instance.Invalidate);
        await Paint(chart, surface);

        Pixels(surface).Should().NotEqual(before);
    }

    private IRenderedComponent<NTChart<Point>> CreateChart(bool animation, bool hardware = false, Point[]? data = null) {
        data ??= [new(0, 1), new(1, 4), new(2, 2)];
        return Render<NTChart<Point>>(parameters => parameters
            .Add(p => p.EnableHardwareAcceleration, hardware)
            .Add(p => p.ChildContent, (RenderFragment)(builder => {
                builder.OpenComponent<NTLineSeries<Point>>(0);
                builder.AddAttribute(1, "Data", data);
                builder.AddAttribute(2, "XValue", (Func<Point, object>)(p => p.X));
                builder.AddAttribute(3, "YValueSelector", (Func<Point, decimal>)(p => p.Y));
                builder.AddAttribute(4, "AnimationEnabled", animation);
                builder.AddAttribute(5, "AnimationDuration", NTMotionDuration.Ms1000);
                builder.CloseComponent();
            })));
    }

    private static async Task Paint(IRenderedComponent<NTChart<Point>> chart, SKSurface? surface = null) {
        using var ownedSurface = surface is null ? SKSurface.Create(new SKImageInfo(640, 320)) : null;
        surface ??= ownedSurface!;
        await chart.InvokeAsync(() => typeof(NTChart<Point>).GetMethod("OnPaintSurface", BindingFlags.Instance | BindingFlags.NonPublic, [typeof(SKCanvas), typeof(SKImageInfo)])!.Invoke(chart.Instance, [surface.Canvas, new SKImageInfo(640, 320)]));
    }

    private static byte[] Pixels(SKSurface surface) {
        using var image = surface.Snapshot();
        using var bitmap = SKBitmap.FromImage(image);
        return bitmap.Bytes;
    }

    private static T Field<T>(object instance, string name, Type? declaringType = null) => (T)(declaringType ?? instance.GetType()).GetField(name, BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(instance)!;

    private sealed class Point(double x, decimal y) {
        public double X { get; } = x;
        public decimal Y { get; set; } = y;
    }
}
