using Microsoft.AspNetCore.Components;
using NTComponents.Charts.Core;
using SkiaSharp;

namespace NTComponents.Charts.Tests.ChartCore;

public class NTChart_ReducedMotion_Tests : ChartTestBase {
    private static RenderFragment NoAxes() => Combine(XAxis<double>(p => p.X, Args(("Visible", false))), YAxis(Args(("Visible", false))));

    private static RenderFragment AnimatedBar() => Bar(extra: Args(("AnimationEnabled", true), ("AnimationDuration", NTMotionDuration.Ms1000)));

    [Fact]
    public async Task ReducedMotionPreference_DisablesSeriesAnimationWithoutChangingTheParameter() {
        var chart = RenderChart(Combine(NoAxes(), AnimatedBar()));
        var series = chart.Instance.Series.Single();
        series.AnimationActive.Should().BeTrue();

        await chart.InvokeAsync(() => chart.Instance.OnReducedMotionChanged(true));

        series.AnimationEnabled.Should().BeTrue("the consumer parameter is untouched");
        series.AnimationActive.Should().BeFalse();

        await chart.InvokeAsync(() => chart.Instance.OnReducedMotionChanged(false));
        series.AnimationActive.Should().BeTrue();
    }

    [Fact]
    public async Task ReducedMotion_FirstFrameIsAlreadyTheSettledFrame() {
        var settled = RenderChart(Combine(NoAxes(), Bar()));
        using var settledSurface = await Paint(settled);
        using var settledBitmap = Bitmap(settledSurface);

        var animated = RenderChart(Combine(NoAxes(), AnimatedBar()));
        await animated.InvokeAsync(() => animated.Instance.OnReducedMotionChanged(true));
        using var snappedSurface = await Paint(animated);
        using var snappedBitmap = Bitmap(snappedSurface);

        CountColor(snappedBitmap, SKColors.Red, 10).Should().BeGreaterThan(100).And.Be(CountColor(settledBitmap, SKColors.Red, 10), "bars must not grow from zero");
    }

    [Fact]
    public async Task WithoutThePreference_TheEntryAnimationStartsFromAnEmptyChart() {
        var animated = RenderChart(Combine(NoAxes(), AnimatedBar()));
        using var surface = await Paint(animated);
        using var bitmap = Bitmap(surface);

        var settled = RenderChart(Combine(NoAxes(), Bar()));
        using var settledSurface = await Paint(settled);
        using var settledBitmap = Bitmap(settledSurface);

        CountColor(bitmap, SKColors.Red, 10).Should().BeLessThan(CountColor(settledBitmap, SKColors.Red, 10));
    }
}
