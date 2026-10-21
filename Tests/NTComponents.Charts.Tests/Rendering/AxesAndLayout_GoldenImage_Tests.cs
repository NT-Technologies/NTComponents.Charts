using NTComponents.Charts.Core;
using NTComponents.Charts.Core.Axes;
using NTComponents.Charts.Core.Series;

namespace NTComponents.Charts.Tests.Rendering;

public class AxesAndLayout_GoldenImage_Tests : GoldenChartTestBase {
    private static readonly GoldenRow[] _monthly = GoldenData.Monthly();

    [Fact]
    public async Task SecondaryYAxis_BarOnPrimaryLineOnSecondary_MatchesGolden() {
        var data = GoldenData.WithSecondary();
        var chart = Chart(c => c
            .Add<NTXAxisOptions<GoldenRow, string>>(("ValueSelector", (Func<GoldenRow, string>)(r => r.Category)), ("Title", "Month"))
            .Add<NTYAxisOptions<GoldenRow, decimal>>(("ValueSelector", GoldenSelectors.A), ("Title", "Units"))
            .Add<NTYAxisOptions<GoldenRow, decimal>>(("ValueSelector", GoldenSelectors.B), ("Title", "Revenue"), ("ShowGridLines", false))
            .Cartesian<NTBarSeries<GoldenRow>>(data, GoldenSelectors.Category, GoldenSelectors.A)
            .Cartesian<NTLineSeries<GoldenRow>>(data, GoldenSelectors.Category, GoldenSelectors.B, ("UseSecondaryYAxis", true), ("PointStyle", PointStyle.Filled)));

        await VerifyAsync("axis-secondary-y", chart);
    }

    [Fact]
    public async Task LogarithmicYAxis_SpanningFiveDecades_MatchesGolden() {
        var chart = Chart(c => c
            .Add<NTYAxisOptions<GoldenRow, decimal>>(("ValueSelector", GoldenSelectors.A), ("Scale", NTAxisScale.Logarithmic), ("Title", "Log scale"))
            .Cartesian<NTLineSeries<GoldenRow>>(GoldenData.Logarithmic(), GoldenSelectors.Category, GoldenSelectors.A, ("PointStyle", PointStyle.Filled)));

        await VerifyAsync("axis-logarithmic-y", chart);
    }

    [Fact]
    public async Task DateTimeXAxis_MonthlyDates_MatchesGolden() {
        var chart = Chart(c => c
            .Add<NTXAxisOptions<GoldenRow, DateTime>>(("ValueSelector", (Func<GoldenRow, DateTime>)(r => r.Date)), ("Title", "2024"))
            .Cartesian<NTLineSeries<GoldenRow>>(GoldenData.Dated(), GoldenSelectors.Date, GoldenSelectors.A, ("PointStyle", PointStyle.Filled))
            .Cartesian<NTLineSeries<GoldenRow>>(GoldenData.Dated(), GoldenSelectors.Date, GoldenSelectors.B));

        await VerifyAsync("axis-datetime-x", chart);
    }

    [Fact]
    public async Task CategoricalXAxis_WithTitles_MatchesGolden() {
        var chart = Chart(c => c
            .Add<NTXAxisOptions<GoldenRow, string>>(("ValueSelector", (Func<GoldenRow, string>)(r => r.Category)), ("Title", "Month"))
            .Add<NTYAxisOptions<GoldenRow, decimal>>(("ValueSelector", GoldenSelectors.A), ("Title", "Units"), ("LabelFormat", "{0:0}u"))
            .Cartesian<NTBarSeries<GoldenRow>>(_monthly, GoldenSelectors.Category, GoldenSelectors.A));

        await VerifyAsync("axis-categorical-x-titles", chart);
    }

    [Theory]
    [InlineData(LegendPosition.Top)]
    [InlineData(LegendPosition.Bottom)]
    [InlineData(LegendPosition.Left)]
    [InlineData(LegendPosition.Right)]
    [InlineData(LegendPosition.Floating)]
    public async Task Legend_AtPosition_MatchesGolden(LegendPosition position) {
        var chart = Chart(c => c
            .Cartesian<NTLineSeries<GoldenRow>>(_monthly, GoldenSelectors.Category, GoldenSelectors.A, ("Title", "Alpha"))
            .Cartesian<NTLineSeries<GoldenRow>>(_monthly, GoldenSelectors.Category, GoldenSelectors.B, ("Title", "Beta"))
            .Cartesian<NTBarSeries<GoldenRow>>(_monthly, GoldenSelectors.Category, GoldenSelectors.C, ("Title", "Gamma"))
            .Add<NTLegend<GoldenRow>>(("Position", position)));

        await VerifyAsync($"legend-{position.ToString().ToLowerInvariant()}", chart);
    }

    [Fact]
    public async Task Legend_OnPieChart_ListsEverySlice() {
        var chart = Chart(c => c
            .Valued<NTPieSeries<GoldenRow>>(GoldenData.Slices(), GoldenSelectors.Category, GoldenSelectors.A, ("ExplosionOnEntry", false))
            .Add<NTLegend<GoldenRow>>(("Position", LegendPosition.Right)));

        await VerifyAsync("legend-pie-right", chart);
    }

    [Fact]
    public async Task Title_WithColorOverride_MatchesGolden() {
        var chart = Chart(
            c => c.Cartesian<NTLineSeries<GoldenRow>>(_monthly, GoldenSelectors.Category, GoldenSelectors.A),
            p => p.Add(x => x.TitleOptions, new NTTitleOptions("Monthly Sales") { FontSize = 24f, TextColor = NTColor.Primary }));

        await VerifyAsync("title", chart);
    }

    [Fact]
    public async Task Annotations_AllBuiltInTypes_MatchesGolden() {
        var annotations = new List<NTChartAnnotation> {
            new() { Type = NTChartAnnotationType.YLine, Y = 15m, Label = "Target", StrokeColor = NTColor.Error, DashLength = 6f },
            new() { Type = NTChartAnnotationType.XLine, X = "Mar", Label = "Launch", StrokeColor = NTColor.Tertiary },
            new() { Type = NTChartAnnotationType.YRange, Y = 5m, Y2 = 9m, StrokeColor = NTColor.Secondary, Label = "Floor" },
            new() { Type = NTChartAnnotationType.XRange, X = "May", X2 = "Jun", StrokeColor = NTColor.Primary, Label = "Promo" },
            new() { Type = NTChartAnnotationType.Point, X = "Jul", Y = 21m, StrokeColor = NTColor.Error, MarkerSize = 7f },
            new() { Type = NTChartAnnotationType.Text, X = "Feb", Y = 20m, Label = "Note", FontSize = 14f }
        };
        var chart = Chart(
            c => c.Cartesian<NTLineSeries<GoldenRow>>(_monthly, GoldenSelectors.Category, GoldenSelectors.A, ("PointStyle", PointStyle.Filled)),
            p => p.Add(x => x.Annotations, annotations));

        await VerifyAsync("annotations", chart);
    }

    [Fact]
    public async Task Density2_LineAndBarWithLegendAndTitle_MatchesGolden() {
        var chart = Chart(
            c => c
                .Cartesian<NTBarSeries<GoldenRow>>(_monthly, GoldenSelectors.Category, GoldenSelectors.B, ("Title", "Beta"))
                .Cartesian<NTLineSeries<GoldenRow>>(_monthly, GoldenSelectors.Category, GoldenSelectors.A, ("Title", "Alpha"), ("PointStyle", PointStyle.Filled))
                .Add<NTLegend<GoldenRow>>(("Position", LegendPosition.Bottom)),
            p => p.Add(x => x.TitleOptions, new NTTitleOptions("Density two")));

        await VerifyAsync("density-2", chart, density: 2f);
    }

    [Fact]
    public async Task ZoomedView_XRangeNarrowed_MatchesGolden() {
        var chart = Chart(c => c.Cartesian<NTLineSeries<GoldenRow>>(_monthly, GoldenSelectors.Category, GoldenSelectors.A, ("PointStyle", PointStyle.Filled)));

        await VerifyAsync("view-zoomed", chart, prepare: c => c.InvokeAsync(() => c.Instance.SetViewXRange(2, 5)));
    }

    [Fact]
    public async Task PannedView_XRangeShiftedToTail_MatchesGolden() {
        var chart = Chart(c => c.Cartesian<NTBarSeries<GoldenRow>>(_monthly, GoldenSelectors.Category, GoldenSelectors.A));

        await VerifyAsync("view-panned", chart, prepare: c => c.InvokeAsync(() => c.Instance.SetViewXRange(4, 7)));
    }
}
