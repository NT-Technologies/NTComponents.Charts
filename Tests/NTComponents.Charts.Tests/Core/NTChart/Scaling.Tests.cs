using Microsoft.AspNetCore.Components;
using NTComponents.Charts.Core;
using NTComponents.Charts.Core.Axes;
using SkiaSharp;

namespace NTComponents.Charts.Tests.ChartCore;

public class NTChart_Scaling_Tests : ChartTestBase {
    private static readonly SKRect Area = new(100, 20, 500, 220);
    private const float PixelTolerance = 0.01f;

    private IRenderedComponent<NTChart<Pt>> Linear(RenderFragment? extra = null) => RenderChart(Combine(XAxis<double>(p => p.X), extra ?? Line()));

    private static Pt[] Decades(decimal baseY = 1m) => [new("a", 1, baseY), new("b", 10, baseY * 10), new("c", 100, baseY * 100), new("d", 1000, baseY * 1000)];

    [Fact]
    public void ScaleX_MapsPaddedRangeEdgesToInsetPlotEdges() {
        var chart = Linear();

        // The padded range is -0.15..3.15 and the scale insets 3px on each side.
        chart.Instance.ScaleX(-0.15, Area).Should().BeApproximately(103f, PixelTolerance);
        chart.Instance.ScaleX(3.15, Area).Should().BeApproximately(497f, PixelTolerance);
        chart.Instance.ScaleX(1.5, Area).Should().BeApproximately(300f, PixelTolerance);
    }

    [Fact]
    public void ScaleX_IsLinearBetweenPoints() {
        var chart = Linear();

        var step1 = chart.Instance.ScaleX(1, Area) - chart.Instance.ScaleX(0, Area);
        var step2 = chart.Instance.ScaleX(2, Area) - chart.Instance.ScaleX(1, Area);

        step1.Should().BeApproximately(step2, PixelTolerance);
        step1.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task ScaleX_FollowsViewRangeAfterZoom() {
        var chart = Linear();
        var before = chart.Instance.ScaleX(1, Area);

        await chart.InvokeAsync(() => chart.Instance.SetViewXRange(1, 2));

        chart.Instance.ScaleX(1, Area).Should().BeApproximately(103f, PixelTolerance);
        chart.Instance.ScaleX(2, Area).Should().BeApproximately(497f, PixelTolerance);
        before.Should().NotBeApproximately(103f, PixelTolerance);
    }

    [Theory]
    [InlineData(-0.15)]
    [InlineData(0)]
    [InlineData(1.25)]
    [InlineData(3)]
    public void ScaleXInverse_RoundTripsLinearValues(double value) {
        var chart = Linear();

        var pixel = chart.Instance.ScaleX(value, Area);

        chart.Instance.ScaleXInverse(pixel, Area).Should().BeApproximately(value, 1e-3);
    }

    [Fact]
    public void ScaleXInverse_WithPlotAreaNarrowerThanInset_ReturnsRangeMinimum() {
        var chart = Linear();

        chart.Instance.ScaleXInverse(50f, new SKRect(100, 0, 106, 100)).Should().BeApproximately(-0.15, 1e-9);
    }

    [Fact]
    public void ScaleX_LogarithmicScaleSpacesDecadesEvenly() {
        var chart = RenderChart(Combine(XAxis<double>(p => p.X, Args(("Scale", NTAxisScale.Logarithmic))), Line(Decades())));

        var firstDecade = chart.Instance.ScaleX(10, Area) - chart.Instance.ScaleX(1, Area);
        var secondDecade = chart.Instance.ScaleX(100, Area) - chart.Instance.ScaleX(10, Area);
        var thirdDecade = chart.Instance.ScaleX(1000, Area) - chart.Instance.ScaleX(100, Area);

        firstDecade.Should().BeGreaterThan(0);
        secondDecade.Should().BeApproximately(firstDecade, 0.05f);
        thirdDecade.Should().BeApproximately(firstDecade, 0.05f);
    }

    [Fact]
    public void ScaleX_LogarithmicScaleClampsNonPositiveValuesToTheMinimum() {
        var chart = RenderChart(Combine(XAxis<double>(p => p.X, Args(("Scale", NTAxisScale.Logarithmic))), Line(Decades())));

        var atZero = chart.Instance.ScaleX(0, Area);
        var negative = chart.Instance.ScaleX(-50, Area);

        atZero.Should().BeApproximately(103f, PixelTolerance);
        negative.Should().BeApproximately(atZero, PixelTolerance);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(10)]
    [InlineData(250)]
    [InlineData(1000)]
    public void ScaleXInverse_RoundTripsLogarithmicValues(double value) {
        var chart = RenderChart(Combine(XAxis<double>(p => p.X, Args(("Scale", NTAxisScale.Logarithmic))), Line(Decades())));

        var pixel = chart.Instance.ScaleX(value, Area);

        chart.Instance.ScaleXInverse(pixel, Area).Should().BeApproximately(value, value * 1e-3);
    }

    [Fact]
    public void ScaleY_MapsPaddedRangeEdgesToInsetPlotEdgesWithBottomAtLargerPixel() {
        var chart = Linear();

        // Padded Y range is 8.5..41.5; the vertical inset is 3px top and bottom, and Y grows upward.
        chart.Instance.ScaleY(8.5m, Area).Should().BeApproximately(217f, PixelTolerance);
        chart.Instance.ScaleY(41.5m, Area).Should().BeApproximately(23f, PixelTolerance);
        chart.Instance.ScaleY(25m, Area).Should().BeApproximately(120f, PixelTolerance);
    }

    [Fact]
    public void ScaleY_ZeroFloorUsesLargerBottomPaddingSoZeroBarsDoNotTouchTheAxis() {
        var chart = RenderChart(Bar());

        // Bars anchor the floor at 0, which reserves 9px at the bottom instead of 3px.
        chart.Instance.ScaleY(0m, Area).Should().BeApproximately(211f, PixelTolerance);
        chart.Instance.ScaleY(42m, Area).Should().BeApproximately(23f, PixelTolerance);
    }

    [Fact]
    public void ScaleY_AbsoluteMinimumClampsValuesBelowTheMinimum() {
        var data = new Pt[] { new("A", 0, 5m), new("B", 1, 10m) };
        var chart = RenderChart(Combine(YAxis(Args(("AbsoluteMinimum", (decimal?)0m))), Line(data)));

        var below = chart.Instance.ScaleY(-100m, Area);
        var atMinimum = chart.Instance.ScaleY(0m, Area);

        below.Should().BeApproximately(atMinimum, PixelTolerance);
    }

    [Fact]
    public void ScaleY_LogarithmicScaleSpacesDecadesEvenly() {
        var chart = RenderChart(Combine(YAxis(Args(("Scale", NTAxisScale.Logarithmic))), Line(Decades())));

        var first = chart.Instance.ScaleY(1m, Area) - chart.Instance.ScaleY(10m, Area);
        var second = chart.Instance.ScaleY(10m, Area) - chart.Instance.ScaleY(100m, Area);
        var third = chart.Instance.ScaleY(100m, Area) - chart.Instance.ScaleY(1000m, Area);

        first.Should().BeGreaterThan(0);
        second.Should().BeApproximately(first, 0.05f);
        third.Should().BeApproximately(first, 0.05f);
    }

    [Fact]
    public void ScaleY_LogarithmicScaleClampsNonPositiveValuesToTheMinimum() {
        var chart = RenderChart(Combine(YAxis(Args(("Scale", NTAxisScale.Logarithmic))), Line(Decades())));

        chart.Instance.ScaleY(0m, Area).Should().BeApproximately(chart.Instance.ScaleY(-10m, Area), PixelTolerance);
        chart.Instance.ScaleY(0m, Area).Should().BeGreaterThan(chart.Instance.ScaleY(1m, Area));
    }

    [Theory]
    [InlineData(8.5)]
    [InlineData(10)]
    [InlineData(27.25)]
    [InlineData(41.5)]
    public void ScaleYInverse_RoundTripsLinearValues(double value) {
        var chart = Linear();
        var y = (decimal)value;

        var pixel = chart.Instance.ScaleY(y, Area);

        chart.Instance.ScaleYInverse(pixel, Area).Should().BeApproximately(y, 0.001m);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(10)]
    [InlineData(300)]
    [InlineData(1000)]
    public void ScaleYInverse_RoundTripsLogarithmicValues(double value) {
        var chart = RenderChart(Combine(YAxis(Args(("Scale", NTAxisScale.Logarithmic))), Line(Decades())));
        var y = (decimal)value;

        var pixel = chart.Instance.ScaleY(y, Area);

        chart.Instance.ScaleYInverse(pixel, Area).Should().BeApproximately(y, y * 0.001m);
    }

    [Fact]
    public void ScaleYInverse_WithSecondaryAxis_UsesTheSecondaryRange() {
        var secondaryData = new Pt[] { new("A", 0, 1000m), new("B", 1, 2000m) };
        var chart = RenderChart(Combine(YAxis(), YAxis(), Line(), Line(secondaryData, extra: Args(("UseSecondaryYAxis", true)))));
        var secondary = (NTAxisOptions<Pt>)chart.Instance.SecondaryYAxis!;

        // Secondary padded range is 950..2050 so the bottom inset maps to 950.
        chart.Instance.ScaleYInverse(217f, secondary, Area).Should().BeApproximately(950m, 0.01m);
        chart.Instance.ScaleYInverse(23f, secondary, Area).Should().BeApproximately(2050m, 0.01m);
        chart.Instance.ScaleYInverse(217f, Area).Should().BeApproximately(8.5m, 0.01m);
    }

    [Fact]
    public async Task ScaleAfterPaint_MatchesScaleBeforePaintForTheSameRange() {
        var chart = Linear();
        var before = (chart.Instance.ScaleX(1, Area), chart.Instance.ScaleY(20m, Area));

        using var surface = await Paint(chart);

        (chart.Instance.ScaleX(1, Area), chart.Instance.ScaleY(20m, Area)).Should().Be(before);
    }

    [Fact]
    public async Task ScaleAcrossFrames_ReflectsViewRangeChangedBetweenFrames() {
        var chart = Linear();
        using var surface = await Paint(chart);
        var first = chart.Instance.ScaleX(1, PlotArea(chart));

        await chart.InvokeAsync(() => chart.Instance.SetViewXRange(0, 1));
        await Paint(chart, surface: surface);

        var second = chart.Instance.ScaleX(1, PlotArea(chart));
        second.Should().BeGreaterThan(first);
        second.Should().BeApproximately(PlotArea(chart).Right - 3f, PixelTolerance);
    }
}
