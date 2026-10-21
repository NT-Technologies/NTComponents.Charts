using NTComponents.Charts.Core;
using NTComponents.Charts.Core.Series;
using SkiaSharp;

namespace NTComponents.Charts.Tests.Series.Cartesian;

public class NTLineSeriesRender_Tests : CartesianChartTestBase {
    private static readonly (string, object) Thick = ("StrokeWidth", 6f);
    private static readonly (string, object) NoMarkers = ("PointStyle", PointStyle.None);

    private static SKPoint Mid(SKPoint a, SKPoint b) => new((a.X + b.X) / 2, (a.Y + b.Y) / 2);

    [Fact]
    public void WithEmptyData_DrawsNothingAndReturnsTheRenderArea() {
        var cut = RenderChart(XAxisKind.Int, SeriesFragment<NTLineSeries<Pt>>([], ("Color", NTColor.Primary)));
        var series = SeriesOf<NTLineSeries<Pt>>(cut);
        using var surface = NewSurface();

        var result = series.Render(CreateContext(cut.Instance, surface), Area);

        result.Should().Be(Area);
        CountDrawnPixels(surface).Should().Be(0);
    }

    [Fact]
    public void WithData_ReturnsTheRenderAreaUnchanged() {
        var cut = RenderChart(XAxisKind.Int, SeriesFragment<NTLineSeries<Pt>>(Linear(3), ("Color", NTColor.Primary)));
        using var surface = NewSurface();

        var result = SeriesOf<NTLineSeries<Pt>>(cut).Render(CreateContext(cut.Instance, surface), Area);

        result.Should().Be(Area);
    }

    [Fact]
    public void DrawsTheLineBetweenConsecutivePointsInTheSeriesColor() {
        var cut = RenderChart(XAxisKind.Int, SeriesFragment<NTLineSeries<Pt>>(Linear(3), ("Color", NTColor.Primary), Thick, NoMarkers));
        var p0 = ExpectedPoint(cut.Instance, 1, 10m);
        var p1 = ExpectedPoint(cut.Instance, 2, 20m);
        var p2 = ExpectedPoint(cut.Instance, 3, 30m);

        using var surface = Draw(cut, SeriesOf<NTLineSeries<Pt>>(cut));

        IsClose(PixelAt(surface, Mid(p0, p1)), Red).Should().BeTrue();
        IsClose(PixelAt(surface, Mid(p1, p2)), Red).Should().BeTrue();
        IsWhite(PixelAt(surface, new SKPoint(Mid(p0, p1).X, 5))).Should().BeTrue("far above the line nothing is drawn");
    }

    [Fact]
    public void UsesTheColorOfTheSeriesAssignedToTheSecondaryPaletteSlot() {
        var cut = RenderChart(XAxisKind.Int, SeriesFragment<NTLineSeries<Pt>>(Linear(3), ("Color", NTColor.Secondary), Thick, NoMarkers));
        var p0 = ExpectedPoint(cut.Instance, 1, 10m);
        var p1 = ExpectedPoint(cut.Instance, 2, 20m);

        using var surface = Draw(cut, SeriesOf<NTLineSeries<Pt>>(cut));

        IsClose(PixelAt(surface, Mid(p0, p1)), Blue).Should().BeTrue();
    }

    [Fact]
    public void WithStrokeWidth_ThicknessOfTheLineFollowsTheParameter() {
        var thin = RenderChart(XAxisKind.Int, SeriesFragment<NTLineSeries<Pt>>(Linear(2, _ => 50m), ("Color", NTColor.Primary), ("StrokeWidth", 2f), NoMarkers));
        var thick = RenderChart(XAxisKind.Int, SeriesFragment<NTLineSeries<Pt>>(Linear(2, _ => 50m), ("Color", NTColor.Primary), ("StrokeWidth", 10f), NoMarkers));
        var midX = (int)ExpectedPoint(thin.Instance, 1.5, 50m).X;

        using var thinSurface = Draw(thin, SeriesOf<NTLineSeries<Pt>>(thin));
        using var thickSurface = Draw(thick, SeriesOf<NTLineSeries<Pt>>(thick));

        ColumnCount(thickSurface, midX, Red).Should().BeGreaterThan(ColumnCount(thinSurface, midX, Red) + 5);
    }

    [Fact]
    public void WithLineStyleNone_DrawsMarkersButNoLine() {
        var cut = RenderChart(XAxisKind.Int, SeriesFragment<NTLineSeries<Pt>>(Linear(3), ("Color", NTColor.Primary), ("LineStyle", LineStyle.None), Thick));
        var p0 = ExpectedPoint(cut.Instance, 1, 10m);
        var p1 = ExpectedPoint(cut.Instance, 2, 20m);

        using var surface = Draw(cut, SeriesOf<NTLineSeries<Pt>>(cut));

        IsClose(PixelAt(surface, p0), Red).Should().BeTrue("the data point marker is still drawn");
        IsWhite(PixelAt(surface, Mid(p0, p1))).Should().BeTrue("no line segment is drawn");
    }

    [Fact]
    public void WithPointStyleNone_DrawsNoMarkers() {
        var cut = RenderChart(XAxisKind.Int, SeriesFragment<NTLineSeries<Pt>>(Linear(3), ("Color", NTColor.Primary), ("LineStyle", LineStyle.None), NoMarkers));

        using var surface = Draw(cut, SeriesOf<NTLineSeries<Pt>>(cut));

        CountDrawnPixels(surface).Should().Be(0);
    }

    [Fact]
    public void WithDashedStyle_LeavesGapsAlongTheLineThatSolidDoesNotHave() {
        var data = Linear(2, _ => 50m);
        var solid = RenderChart(XAxisKind.Int, SeriesFragment<NTLineSeries<Pt>>(data, ("Color", NTColor.Primary), ("StrokeWidth", 2f), NoMarkers));
        var dashed = RenderChart(XAxisKind.Int, SeriesFragment<NTLineSeries<Pt>>(data, ("Color", NTColor.Primary), ("StrokeWidth", 2f), NoMarkers, ("LineStyle", LineStyle.Dashed)));
        var start = ExpectedPoint(solid.Instance, 1, 50m);
        var end = ExpectedPoint(solid.Instance, 2, 50m);

        using var solidSurface = Draw(solid, SeriesOf<NTLineSeries<Pt>>(solid));
        using var dashedSurface = Draw(dashed, SeriesOf<NTLineSeries<Pt>>(dashed));

        int Covered(SKSurface surface) {
            var row = (int)start.Y;
            var count = 0;
            for (var x = (int)start.X + 2; x < (int)end.X - 2; x++) {
                if (!IsWhite(PixelAt(surface, x, row - 1)) || !IsWhite(PixelAt(surface, x, row)) || !IsWhite(PixelAt(surface, x, row + 1))) {
                    count++;
                }
            }

            return count;
        }

        var span = (int)end.X - (int)start.X - 4;
        Covered(solidSurface).Should().Be(span, "a solid line covers every column");
        Covered(dashedSurface).Should().BeInRange((int)(span * 0.5), (int)(span * 0.95), "a dashed line alternates ink and gaps");
    }

    [Fact]
    public void WithStepInterpolation_RisesVerticallyAtThePreviousXBeforeRunningHorizontally() {
        var data = new[] { new Pt(1, 10m), new Pt(2, 30m) };
        var step = RenderChart(XAxisKind.Int, SeriesFragment<NTLineSeries<Pt>>(data, ("Color", NTColor.Primary), Thick, NoMarkers, ("Interpolation", LineInterpolation.Step)));
        var straight = RenderChart(XAxisKind.Int, SeriesFragment<NTLineSeries<Pt>>(data, ("Color", NTColor.Primary), Thick, NoMarkers));
        var p0 = ExpectedPoint(step.Instance, 1, 10m);
        var p1 = ExpectedPoint(step.Instance, 2, 30m);
        var onVerticalRiser = new SKPoint(p0.X, Mid(p0, p1).Y);
        var onHorizontalRun = new SKPoint(Mid(p0, p1).X, p1.Y);

        using var stepSurface = Draw(step, SeriesOf<NTLineSeries<Pt>>(step));
        using var straightSurface = Draw(straight, SeriesOf<NTLineSeries<Pt>>(straight));

        IsClose(PixelAt(stepSurface, onVerticalRiser), Red).Should().BeTrue();
        IsClose(PixelAt(stepSurface, onHorizontalRun), Red).Should().BeTrue();
        IsWhite(PixelAt(straightSurface, onVerticalRiser)).Should().BeTrue();
        IsWhite(PixelAt(straightSurface, onHorizontalRun)).Should().BeTrue();
    }

    [Theory]
    [InlineData(LineInterpolation.Straight)]
    [InlineData(LineInterpolation.Smoothed)]
    [InlineData(LineInterpolation.Curved)]
    [InlineData(LineInterpolation.Step)]
    public void WithAnyInterpolation_LinePassesThroughEveryDataPoint(LineInterpolation interpolation) {
        var data = new[] { new Pt(1, 10m), new Pt(2, 40m), new Pt(3, 15m), new Pt(4, 35m) };
        var cut = RenderChart(XAxisKind.Int, SeriesFragment<NTLineSeries<Pt>>(data, ("Color", NTColor.Primary), Thick, NoMarkers, ("Interpolation", interpolation)));

        using var surface = Draw(cut, SeriesOf<NTLineSeries<Pt>>(cut));

        foreach (var point in data) {
            IsClose(PixelAt(surface, ExpectedPoint(cut.Instance, Convert.ToDouble(point.X), point.Y)), Red).Should().BeTrue($"x={point.X} lies on the {interpolation} line");
        }
    }

    [Fact]
    public void CurvedAndSmoothedInterpolation_DrawDifferentlyFromStraightLines() {
        var data = new[] { new Pt(1, 10m), new Pt(2, 40m), new Pt(3, 15m), new Pt(4, 35m) };
        byte[] Render(LineInterpolation interpolation) {
            var cut = RenderChart(XAxisKind.Int, SeriesFragment<NTLineSeries<Pt>>(data, ("Color", NTColor.Primary), Thick, NoMarkers, ("Interpolation", interpolation)));
            using var surface = Draw(cut, SeriesOf<NTLineSeries<Pt>>(cut));
            return Snapshot(surface);
        }

        var straight = Render(LineInterpolation.Straight);

        Render(LineInterpolation.Curved).Should().NotEqual(straight);
        Render(LineInterpolation.Smoothed).Should().NotEqual(straight);
        Render(LineInterpolation.Step).Should().NotEqual(straight);
    }

    [Fact]
    public void ChangingInterpolationAfterFirstRender_RedrawsThePath() {
        var data = new[] { new Pt(1, 10m), new Pt(2, 30m) };
        var cut = RenderChart(XAxisKind.Int, SeriesFragment<NTLineSeries<Pt>>(data, ("Color", NTColor.Primary), Thick, NoMarkers));
        var series = SeriesOf<NTLineSeries<Pt>>(cut);
        var p0 = ExpectedPoint(cut.Instance, 1, 10m);
        var p1 = ExpectedPoint(cut.Instance, 2, 30m);
        var onVerticalRiser = new SKPoint(p0.X, Mid(p0, p1).Y);
        using (var before = Draw(cut, series)) {
            IsWhite(PixelAt(before, onVerticalRiser)).Should().BeTrue();
        }

        series.Interpolation = LineInterpolation.Step;

        using var after = Draw(cut, series);
        IsClose(PixelAt(after, onVerticalRiser), Red).Should().BeTrue();
    }

    [Fact]
    public void Rendering_TwiceWithoutChanges_ProducesIdenticalImages() {
        var cut = RenderChart(XAxisKind.Int, SeriesFragment<NTLineSeries<Pt>>(Linear(6), ("Color", NTColor.Primary)));
        var series = SeriesOf<NTLineSeries<Pt>>(cut);

        using var first = Draw(cut, series);
        using var second = Draw(cut, series);

        Snapshot(second).Should().Equal(Snapshot(first));
    }

    [Fact]
    public void ReplacingData_RedrawsTheNewLineAndErasesTheOldOne() {
        var cut = RenderChart(XAxisKind.Int, SeriesFragment<NTLineSeries<Pt>>(Linear(3), ("Color", NTColor.Primary), Thick, NoMarkers));
        var series = SeriesOf<NTLineSeries<Pt>>(cut);
        var oldMid = Mid(ExpectedPoint(cut.Instance, 1, 10m), ExpectedPoint(cut.Instance, 2, 20m));
        using (var before = Draw(cut, series)) {
            IsClose(PixelAt(before, oldMid), Red).Should().BeTrue();
        }

        ReRender(cut, new ChartSetup(), SeriesFragment<NTLineSeries<Pt>>([new Pt(1, 30m), new Pt(2, 30m), new Pt(3, 10m)], ("Color", NTColor.Primary), Thick, NoMarkers));

        using var after = Draw(cut, series);
        var newMid = Mid(ExpectedPoint(cut.Instance, 1, 30m), ExpectedPoint(cut.Instance, 2, 30m));
        IsClose(PixelAt(after, newMid), Red).Should().BeTrue();
        IsWhite(PixelAt(after, oldMid)).Should().BeTrue();
    }

    [Fact]
    public void MutatingDataInPlaceThenInvalidating_RedrawsTheLine() {
        var data = Linear(3).ToList();
        var cut = RenderChart(XAxisKind.Int, SeriesFragment<NTLineSeries<Pt>>(data, ("Color", NTColor.Primary), Thick, NoMarkers));
        var series = SeriesOf<NTLineSeries<Pt>>(cut);
        using (Draw(cut, series)) {
        }

        data[2] = data[2] with { Y = 200m };
        series.Invalidate();

        using var after = Draw(cut, series);
        IsClose(PixelAt(after, ExpectedPoint(cut.Instance, 3, 200m)), Red).Should().BeTrue();
    }

    [Fact]
    public void ReplacingTheYSelector_RedrawsWithTheNewValues() {
        var data = new[] { new Pt(1, 10m, 100m), new Pt(2, 20m, 400m), new Pt(3, 30m, 200m) };
        var cut = RenderChart(XAxisKind.Int, SeriesFragment<NTLineSeries<Pt>>(data, ("Color", NTColor.Primary), Thick, NoMarkers));
        var series = SeriesOf<NTLineSeries<Pt>>(cut);
        using (Draw(cut, series)) {
        }

        ReRender(cut, new ChartSetup(), SeriesFragment<NTLineSeries<Pt>>(data, ("Color", NTColor.Primary), Thick, NoMarkers, ("YValueSelector", (Func<Pt, decimal>)(p => p.Z))));

        using var after = Draw(cut, series);
        IsClose(PixelAt(after, ExpectedPoint(cut.Instance, 2, 400m)), Red).Should().BeTrue();
    }

    [Fact]
    public void WithUnsortedData_DrawsTheSameImageAsSortedData() {
        var sorted = new[] { new Pt(1, 10m), new Pt(2, 40m), new Pt(3, 15m), new Pt(4, 35m) };
        var shuffled = new[] { sorted[2], sorted[0], sorted[3], sorted[1] };
        var sortedChart = RenderChart(XAxisKind.Int, SeriesFragment<NTLineSeries<Pt>>(sorted, ("Color", NTColor.Primary)));
        var shuffledChart = RenderChart(XAxisKind.Int, SeriesFragment<NTLineSeries<Pt>>(shuffled, ("Color", NTColor.Primary)));

        using var expected = Draw(sortedChart, SeriesOf<NTLineSeries<Pt>>(sortedChart));
        using var actual = Draw(shuffledChart, SeriesOf<NTLineSeries<Pt>>(shuffledChart));

        Snapshot(actual).Should().Equal(Snapshot(expected));
    }

    [Fact]
    public void WithNegativeValues_DrawsBelowTheZeroPositionAtTheMatchingHeight() {
        var data = new[] { new Pt(1, -30m), new Pt(2, 30m), new Pt(3, -10m) };
        var cut = RenderChart(XAxisKind.Int, SeriesFragment<NTLineSeries<Pt>>(data, ("Color", NTColor.Primary), Thick, NoMarkers));
        var low = ExpectedPoint(cut.Instance, 1, -30m);
        var high = ExpectedPoint(cut.Instance, 2, 30m);

        using var surface = Draw(cut, SeriesOf<NTLineSeries<Pt>>(cut));

        low.Y.Should().BeGreaterThan(high.Y);
        IsClose(PixelAt(surface, low), Red).Should().BeTrue();
        IsClose(PixelAt(surface, high), Red).Should().BeTrue();
    }

    [Fact]
    public void WithSinglePoint_DrawsOnlyAMarkerAtTheCenter() {
        var cut = RenderChart(XAxisKind.Int, SeriesFragment<NTLineSeries<Pt>>([new Pt(5, 50m)], ("Color", NTColor.Primary)));

        using var surface = Draw(cut, SeriesOf<NTLineSeries<Pt>>(cut));

        var center = ExpectedPoint(cut.Instance, 5, 50m);
        IsClose(PixelAt(surface, center), Red).Should().BeTrue();
        CountDrawnPixels(surface).Should().BeInRange(30, 80, "an 8px circle covers about 50 pixels");
    }

    [Fact]
    public void WithConstantValues_DrawsAHorizontalLine() {
        var cut = RenderChart(XAxisKind.Int, SeriesFragment<NTLineSeries<Pt>>(Linear(4, _ => 7m), ("Color", NTColor.Primary), Thick, NoMarkers));
        var y = ExpectedPoint(cut.Instance, 1, 7m).Y;

        using var surface = Draw(cut, SeriesOf<NTLineSeries<Pt>>(cut));

        RowCount(surface, (int)y, Red).Should().BeGreaterThan(250);
    }

    [Fact]
    public void WithSecondaryYAxis_ScalesAgainstTheSecondaryRange() {
        var cut = RenderChart(
            new ChartSetup { SecondaryYAxis = true },
            SeriesFragment<NTLineSeries<Pt>>(Linear(3), ("Color", NTColor.Primary), Thick, NoMarkers),
            SeriesFragment<NTLineSeries<Pt>>(Linear(3, i => 1000m * (i + 1)), ("Color", NTColor.Secondary), Thick, NoMarkers, ("UseSecondaryYAxis", true)));
        var primary = SeriesOf<NTLineSeries<Pt>>(cut, 0);
        var secondary = SeriesOf<NTLineSeries<Pt>>(cut, 1);

        using var surface = Draw(cut, primary, secondary);

        // Both series span the full height of their own axis, so both end at the same pixel row.
        var primaryTop = ExpectedPoint(cut.Instance, 3, 30m);
        var secondaryTop = ExpectedPoint(cut.Instance, 3, 3000m, secondary: true);
        primaryTop.Y.Should().BeApproximately(secondaryTop.Y, 0.01f);
        IsClose(PixelAt(surface, secondaryTop), Blue).Should().BeTrue();
        IsClose(PixelAt(surface, ExpectedPoint(cut.Instance, 1, 1000m, secondary: true)), Blue).Should().BeTrue();
    }

    [Fact]
    public void WithSecondaryAxis_RangesAreIndependentOfThePrimaryAxis() {
        var cut = RenderChart(
            new ChartSetup { SecondaryYAxis = true },
            SeriesFragment<NTLineSeries<Pt>>(Linear(3)),
            SeriesFragment<NTLineSeries<Pt>>(Linear(3, i => 1000m * (i + 1)), ("UseSecondaryYAxis", true)));

        var (primaryMin, primaryMax) = ChartYRange(cut.Instance, padded: true);
        var (secondaryMin, secondaryMax) = ChartYRange(cut.Instance, padded: true, secondary: true);

        primaryMax.Should().BeLessThan(secondaryMin, "the axes are independent, a shared scale would push the secondary series off-canvas");
        secondaryMax.Should().BeGreaterThan(primaryMax);
        primaryMin.Should().BeLessThan(primaryMax);
    }

    [Fact]
    public void TwoSeries_DrawIndependentColoredLines() {
        var cut = RenderChart(
            XAxisKind.Int,
            SeriesFragment<NTLineSeries<Pt>>(Linear(3, i => 10m + (i * 5)), ("Color", NTColor.Primary), Thick, NoMarkers),
            SeriesFragment<NTLineSeries<Pt>>(Linear(3, i => 100m - (i * 5)), ("Color", NTColor.Secondary), Thick, NoMarkers));

        using var surface = Draw(cut, SeriesOf<NTLineSeries<Pt>>(cut, 0), SeriesOf<NTLineSeries<Pt>>(cut, 1));

        IsClose(PixelAt(surface, ExpectedPoint(cut.Instance, 1, 10m)), Red).Should().BeTrue();
        IsClose(PixelAt(surface, ExpectedPoint(cut.Instance, 1, 100m)), Blue).Should().BeTrue();
    }

    [Fact]
    public void WithDataLabels_DrawsAdditionalContentComparedToWithout() {
        var data = Linear(4);
        var plain = RenderChart(XAxisKind.Int, SeriesFragment<NTLineSeries<Pt>>(data, ("Color", NTColor.Primary)));
        var labelled = RenderChart(XAxisKind.Int, SeriesFragment<NTLineSeries<Pt>>(data, ("Color", NTColor.Primary), ("ShowDataLabels", true)));

        using var plainSurface = Draw(plain, SeriesOf<NTLineSeries<Pt>>(plain));
        using var labelledSurface = Draw(labelled, SeriesOf<NTLineSeries<Pt>>(labelled));

        CountDrawnPixels(labelledSurface).Should().BeGreaterThan(CountDrawnPixels(plainSurface) + 100);
    }

    [Fact]
    public void ManyPoints_SuppressMarkersBeyondSixHundredButNotAtSixHundred() {
        byte[] Render(int count) {
            var cut = RenderChart(XAxisKind.Int, SeriesFragment<NTLineSeries<Pt>>(Linear(count, i => i % 50), ("Color", NTColor.Primary), ("LineStyle", LineStyle.None)));
            using var surface = Draw(cut, SeriesOf<NTLineSeries<Pt>>(cut));
            return Snapshot(surface);
        }

        Render(600).Any(b => b != 255).Should().BeTrue("markers are drawn for up to 600 points");
        Render(601).All(b => b == 255).Should().BeTrue("beyond 600 points markers are skipped for performance");
    }

    [Fact]
    public void DenseDataWithAggregationEnabled_ReducesPointsBelowTheMarkerLimit() {
        var data = Linear(2000, i => i % 100);
        var plain = RenderChart(XAxisKind.Int, SeriesFragment<NTLineSeries<Pt>>(data, ("Color", NTColor.Primary), ("LineStyle", LineStyle.None)));
        var aggregated = RenderChart(XAxisKind.Int, SeriesFragment<NTLineSeries<Pt>>(data, ("Color", NTColor.Primary), ("LineStyle", LineStyle.None), ("EnableAggregation", true)));

        using var plainSurface = Draw(plain, SeriesOf<NTLineSeries<Pt>>(plain));
        using var aggregatedSurface = Draw(aggregated, SeriesOf<NTLineSeries<Pt>>(aggregated));

        CountDrawnPixels(plainSurface).Should().Be(0, "2000 points exceed the marker limit");
        CountDrawnPixels(aggregatedSurface).Should().BeGreaterThan(0, "aggregation brings the point count below the marker limit");
    }

    [Fact]
    public void AggregationBelowThreshold_LeavesTheSeriesUntouched() {
        var data = Linear(30);
        var plain = RenderChart(XAxisKind.Int, SeriesFragment<NTLineSeries<Pt>>(data, ("Color", NTColor.Primary)));
        var aggregated = RenderChart(XAxisKind.Int, SeriesFragment<NTLineSeries<Pt>>(data, ("Color", NTColor.Primary), ("EnableAggregation", true)));

        using var plainSurface = Draw(plain, SeriesOf<NTLineSeries<Pt>>(plain));
        using var aggregatedSurface = Draw(aggregated, SeriesOf<NTLineSeries<Pt>>(aggregated));

        Snapshot(aggregatedSurface).Should().Equal(Snapshot(plainSurface));
    }
}
