using NTComponents.Charts.Core;
using NTComponents.Charts.Core.Axes;
using NTComponents.Charts.Core.Series;

namespace NTComponents.Charts.Tests.Rendering;

public class CartesianSeries_GoldenImage_Tests : GoldenChartTestBase {
    private static readonly GoldenRow[] _monthly = GoldenData.Monthly();

    [Fact]
    public async Task Line_MultiSeriesWithPointsAndDashed_MatchesGolden() {
        var chart = Chart(c => c
            .Cartesian<NTLineSeries<GoldenRow>>(_monthly, GoldenSelectors.Category, GoldenSelectors.A, ("Title", "Alpha"), ("PointStyle", PointStyle.Filled), ("PointShape", PointShape.Circle))
            .Cartesian<NTLineSeries<GoldenRow>>(_monthly, GoldenSelectors.Category, GoldenSelectors.B, ("Title", "Beta"), ("LineStyle", LineStyle.Dashed), ("PointStyle", PointStyle.Outlined), ("PointShape", PointShape.Diamond), ("StrokeWidth", 3f))
            .Cartesian<NTLineSeries<GoldenRow>>(_monthly, GoldenSelectors.Category, GoldenSelectors.C, ("Title", "Gamma"), ("PointStyle", PointStyle.None)));

        await VerifyAsync("line-multi-series-points-dashed", chart);
    }

    [Fact]
    public async Task Line_SmoothedStepAndCurved_MatchesGolden() {
        var chart = Chart(c => c
            .Cartesian<NTLineSeries<GoldenRow>>(_monthly, GoldenSelectors.Category, GoldenSelectors.A, ("Interpolation", LineInterpolation.Smoothed), ("PointStyle", PointStyle.Filled))
            .Cartesian<NTLineSeries<GoldenRow>>(_monthly, GoldenSelectors.Category, GoldenSelectors.B, ("Interpolation", LineInterpolation.Step), ("PointStyle", PointStyle.Filled))
            .Cartesian<NTLineSeries<GoldenRow>>(_monthly, GoldenSelectors.Category, GoldenSelectors.C, ("Interpolation", LineInterpolation.Curved), ("PointStyle", PointStyle.Filled)));

        await VerifyAsync("line-smoothed-step-curved", chart);
    }

    [Fact]
    public async Task Area_NonStackedOverlapping_MatchesGolden() {
        var chart = Chart(c => c
            .Cartesian<NTAreaSeries<GoldenRow>>(_monthly, GoldenSelectors.Category, GoldenSelectors.A, ("AreaOpacity", 0.4f))
            .Cartesian<NTAreaSeries<GoldenRow>>(_monthly, GoldenSelectors.Category, GoldenSelectors.B, ("AreaOpacity", 0.4f)));

        await VerifyAsync("area-non-stacked", chart);
    }

    // The library has no stacking mode for areas; stacking is expressed by feeding cumulative values, back series first.
    [Fact]
    public async Task Area_CumulativeStackedValues_MatchesGolden() {
        var chart = Chart(c => c
            .Cartesian<NTAreaSeries<GoldenRow>>(_monthly, GoldenSelectors.Category, r => r.A + r.B + r.C, ("AreaOpacity", 0.9f))
            .Cartesian<NTAreaSeries<GoldenRow>>(_monthly, GoldenSelectors.Category, r => r.A + r.B, ("AreaOpacity", 0.9f))
            .Cartesian<NTAreaSeries<GoldenRow>>(_monthly, GoldenSelectors.Category, GoldenSelectors.A, ("AreaOpacity", 0.9f)));

        await VerifyAsync("area-stacked-cumulative", chart);
    }

    [Fact]
    public async Task Bar_VerticalGrouped_MatchesGolden() {
        var chart = Chart(c => c
            .Cartesian<NTBarSeries<GoldenRow>>(_monthly, GoldenSelectors.Category, GoldenSelectors.A, ("Title", "Alpha"), ("CornerRadius", 3f))
            .Cartesian<NTBarSeries<GoldenRow>>(_monthly, GoldenSelectors.Category, GoldenSelectors.B, ("Title", "Beta"), ("CornerRadius", 3f)));

        await VerifyAsync("bar-vertical-grouped", chart);
    }

    [Fact]
    public async Task Bar_StackedSegments_MatchesGolden() {
        var segments = (Func<GoldenRow, IEnumerable<NTBarSegment>>)(r => [
            new NTBarSegment { Value = r.A, Label = "A", Color = NTColor.Primary },
            new NTBarSegment { Value = r.B, Label = "B", Color = NTColor.Tertiary },
            new NTBarSegment { Value = r.C, Label = "C", Color = NTColor.Secondary }
        ]);
        var chart = Chart(c => c.Cartesian<NTBarSeries<GoldenRow>>(_monthly, GoldenSelectors.Category, r => r.A + r.B + r.C, ("SegmentSelector", segments)));

        await VerifyAsync("bar-stacked-segments", chart);
    }

    [Fact]
    public async Task Bar_HorizontalWithNegatives_MatchesGolden() {
        var chart = Chart(c => c.Cartesian<NTBarSeries<GoldenRow>>(GoldenData.Negatives(), GoldenSelectors.Category, GoldenSelectors.A, ("Orientation", NTChartOrientation.Horizontal)));

        await VerifyAsync("bar-horizontal-negatives", chart);
    }

    [Fact]
    public async Task Bar_VerticalWithNegatives_MatchesGolden() {
        var chart = Chart(c => c.Cartesian<NTBarSeries<GoldenRow>>(GoldenData.Negatives(), GoldenSelectors.Category, GoldenSelectors.A, ("CornerRadius", 4f)));

        await VerifyAsync("bar-vertical-negatives", chart);
    }

    [Fact]
    public async Task Point_ScatterWithShapes_MatchesGolden() {
        var scatter = GoldenData.Scatter();
        var chart = Chart(c => c
            .Add<NTXAxisOptions<GoldenRow, double>>(("ValueSelector", (Func<GoldenRow, double>)(r => r.X)))
            .Cartesian<NTPointSeries<GoldenRow>>(scatter, GoldenSelectors.X, GoldenSelectors.A, ("PointSize", 10f), ("PointShape", PointShape.Circle))
            .Cartesian<NTPointSeries<GoldenRow>>(scatter, GoldenSelectors.X, GoldenSelectors.B, ("PointSize", 10f), ("PointShape", PointShape.Triangle), ("PointStyle", PointStyle.Outlined))
            .Cartesian<NTPointSeries<GoldenRow>>(scatter, GoldenSelectors.X, GoldenSelectors.C, ("PointSize", 10f), ("PointShape", PointShape.Square)));

        await VerifyAsync("point-scatter", chart);
    }

    [Fact]
    public async Task BoxPlot_Default_MatchesGolden() {
        var chart = Chart(c => c.Add<NTBoxPlotSeries<GoldenRow>>(("Data", GoldenData.BoxPlots()), ("XValue", GoldenSelectors.Category), ("YValueSelector", (Func<GoldenRow, decimal>)(r => r.Box.Median)), ("BoXValue", (Func<GoldenRow, BoxPlotValues>)(r => r.Box)), ("AnimationEnabled", false)));

        await VerifyAsync("boxplot", chart);
    }

    [Fact]
    public async Task HeatMap_Default_MatchesGolden() {
        var chart = Chart(c => c.Add<NTHeatMapSeries<GoldenRow>>(("Data", GoldenData.HeatCells()), ("XValue", GoldenSelectors.Category), ("YValueSelector", GoldenSelectors.A), ("WeightSelector", GoldenSelectors.Weight), ("AnimationEnabled", false)));

        await VerifyAsync("heatmap", chart);
    }
}
