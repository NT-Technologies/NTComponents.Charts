using NTComponents.Charts.Core;
using NTComponents.Charts.Core.Series;
using SkiaSharp;

namespace NTComponents.Charts.Tests.Series.Cartesian;

public class NTLineSeriesDateGrouping_Tests : CartesianChartTestBase {
    // Ten years of monthly data; monthly values are the squares 1..144, repeated every year.
    private static Pt[] TenYearsMonthly() => Enumerable.Range(0, 120)
        .Select(i => new Pt(new DateTime(2010 + (i / 12), 1 + (i % 12), 15), (i % 12 + 1) * (i % 12 + 1)))
        .ToArray();

    [Theory]
    [InlineData(AggregationMode.Average, 650.0 / 12.0)]
    [InlineData(AggregationMode.Sum, 650.0)]
    [InlineData(AggregationMode.Min, 1.0)]
    [InlineData(AggregationMode.Max, 144.0)]
    [InlineData(AggregationMode.Median, 42.5)]
    public void YRange_WhenYearsAreGrouped_ReflectsTheAggregatedYearlyValue(AggregationMode mode, double expected) {
        var cut = RenderChart(XAxisKind.DateTime, SeriesFragment<NTLineSeries<Pt>>(TenYearsMonthly(), ("AggregationMode", mode)));

        var range = SeriesOf<NTLineSeries<Pt>>(cut).GetYRange();

        range.Should().NotBeNull();
        ((double)range!.Value.Min).Should().BeApproximately(expected, 1e-6);
        ((double)range.Value.Max).Should().BeApproximately(expected, 1e-6);
    }

    [Fact]
    public void YRange_WithAutoDateGroupingDisabled_UsesRawValues() {
        var setup = new ChartSetup { XAxis = XAxisKind.DateTime, XAxisAttributes = [("EnableAutoDateGrouping", false)] };
        var cut = RenderChart(setup, SeriesFragment<NTLineSeries<Pt>>(TenYearsMonthly(), ("AggregationMode", AggregationMode.Sum)));

        SeriesOf<NTLineSeries<Pt>>(cut).GetYRange().Should().Be((1m, 144m));
    }

    [Fact]
    public void YRange_ChangingAggregationMode_InvalidatesTheGroupedRange() {
        var data = TenYearsMonthly();
        var cut = RenderChart(XAxisKind.DateTime, SeriesFragment<NTLineSeries<Pt>>(data, ("AggregationMode", AggregationMode.Sum)));
        var series = SeriesOf<NTLineSeries<Pt>>(cut);
        series.GetYRange().Should().Be((650m, 650m));

        series.AggregationMode = AggregationMode.Max;

        series.GetYRange().Should().Be((144m, 144m));
    }

    [Fact]
    public void YRange_WhenDaysAreGroupedAndDaysAreMissing_IncludesZeroForTheGaps() {
        var data = new[] {
            new Pt(new DateTime(2024, 1, 1), 10m), new Pt(new DateTime(2024, 1, 2), 20m),
            new Pt(new DateTime(2024, 1, 5), 30m), new Pt(new DateTime(2024, 1, 6), 40m)
        };
        var cut = RenderChart(XAxisKind.DateTime, SeriesFragment<NTLineSeries<Pt>>(data));

        SeriesOf<NTLineSeries<Pt>>(cut).GetYRange().Should().Be((0m, 40m));
    }

    [Fact]
    public void YRange_WhenDaysAreGroupedWithoutGaps_ReflectsTheRawValues() {
        var data = Enumerable.Range(0, 5).Select(i => new Pt(new DateTime(2024, 1, 1 + i), 10m * (i + 2))).ToArray();
        var cut = RenderChart(XAxisKind.DateTime, SeriesFragment<NTLineSeries<Pt>>(data));

        SeriesOf<NTLineSeries<Pt>>(cut).GetYRange().Should().Be((20m, 60m));
    }

    [Fact]
    public void Render_MissingDayBetweenPoints_IsDrawnAsASyntheticZeroThatCannotBeHit() {
        var data = new[] { new Pt(new DateTime(2024, 1, 1), 50m), new Pt(new DateTime(2024, 1, 3), 50m) };
        var cut = RenderChart(XAxisKind.DateTime, SeriesFragment<NTLineSeries<Pt>>(data, ("Color", NTColor.Primary), ("StrokeWidth", 6f), ("PointStyle", PointStyle.None)));
        var series = SeriesOf<NTLineSeries<Pt>>(cut);
        var (_, yMax) = ChartYRange(cut.Instance, padded: true);
        var (xMin, xMax) = ChartXRange(cut.Instance, true);

        using var surface = Draw(cut, series);

        // The line dips to the zero baseline on the missing day instead of connecting the neighbours straight.
        var middleX = (float)(3 + (((new DateTime(2024, 1, 2).Ticks + (TimeSpan.TicksPerHour * 12d)) - xMin) / (xMax - xMin) * (SurfaceWidth - 6)));
        var zeroY = ExpectedPoint(cut.Instance, new DateTime(2024, 1, 2).Ticks + (TimeSpan.TicksPerHour * 12d), 0m).Y;
        var fiftyY = ExpectedPoint(cut.Instance, new DateTime(2024, 1, 2).Ticks, 50m).Y;
        IsClose(PixelAt(surface, new SKPoint(middleX, zeroY)), Red).Should().BeTrue("the line touches zero on the missing day");
        IsWhite(PixelAt(surface, new SKPoint(middleX, fiftyY))).Should().BeTrue("neighbours are not connected directly across the gap");
        yMax.Should().BeGreaterThan(50m);
        series.HitTest(new SKPoint(middleX, zeroY), Area).Should().BeNull("a synthetic gap bucket is not a data point");
    }
}
