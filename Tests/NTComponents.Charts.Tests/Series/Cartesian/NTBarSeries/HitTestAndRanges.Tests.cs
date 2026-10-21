using NTComponents.Charts.Core;
using SkiaSharp;

namespace NTComponents.Charts.Tests.Series.Cartesian;

public class NTBarSeriesHitTestAndRanges_Tests : BarTestBase {
    private static Pt[] Categories(params decimal[] values) => values.Select((v, i) => new Pt(((char)('A' + i)).ToString(), v)).ToArray();

    [Fact]
    public void HitTest_BeforeAnyRender_ReturnsNull() {
        var cut = RenderChart(XAxisKind.Default, SeriesFragment<NTBarSeries<Pt>>(Categories(10m, 20m), Primary));

        SeriesOf<NTBarSeries<Pt>>(cut).HitTest(new SKPoint(75, 150), Area).Should().BeNull();
    }

    [Fact]
    public void HitTest_InsideEachBar_ReturnsItsIndexAndData() {
        var data = Categories(10m, 20m, 30m);
        var cut = RenderChart(XAxisKind.Default, SeriesFragment<NTBarSeries<Pt>>(data, Primary));
        var series = SeriesOf<NTBarSeries<Pt>>(cut);
        using var surface = Draw(cut, series);

        for (var i = 0; i < data.Length; i++) {
            var hit = series.HitTest(Center(VerticalBar(cut.Instance, i, data.Length, data[i].Y)), Area);

            hit!.Value.Index.Should().Be(i);
            hit.Value.Data.Should().BeSameAs(data[i]);
        }
    }

    [Fact]
    public void HitTest_AboveTheBarTopAndBetweenBars_ReturnsNull() {
        var data = Categories(10m, 20m, 30m);
        var cut = RenderChart(XAxisKind.Default, SeriesFragment<NTBarSeries<Pt>>(data, Primary));
        var series = SeriesOf<NTBarSeries<Pt>>(cut);
        using var surface = Draw(cut, series);
        var shortest = VerticalBar(cut.Instance, 0, 3, 10m);
        var second = VerticalBar(cut.Instance, 1, 3, 20m);

        series.HitTest(new SKPoint(shortest.MidX, shortest.Top - 2f), Area).Should().BeNull();
        series.HitTest(new SKPoint((shortest.Right + second.Left) / 2f, shortest.Bottom - 5f), Area).Should().BeNull();
    }

    [Fact]
    public void HitTest_AtTheEdgesOfABar_IsInclusiveOfLeftAndTopExclusiveOfRightAndBottom() {
        var data = Categories(10m, 20m);
        var cut = RenderChart(XAxisKind.Default, SeriesFragment<NTBarSeries<Pt>>(data, Primary));
        var series = SeriesOf<NTBarSeries<Pt>>(cut);
        using var surface = Draw(cut, series);
        var rect = VerticalBar(cut.Instance, 0, 2, 10m);

        series.HitTest(new SKPoint(rect.Left + 0.2f, rect.MidY), Area).Should().NotBeNull();
        series.HitTest(new SKPoint(rect.Left - 0.2f, rect.MidY), Area).Should().BeNull();
        series.HitTest(new SKPoint(rect.Right - 0.2f, rect.MidY), Area).Should().NotBeNull();
        series.HitTest(new SKPoint(rect.Right + 0.2f, rect.MidY), Area).Should().BeNull();
    }

    [Fact]
    public void HitTest_NegativeBar_HitsBelowTheBaseline() {
        var data = Categories(30m, -20m);
        var cut = RenderChart(XAxisKind.Default, SeriesFragment<NTBarSeries<Pt>>(data, Primary));
        var series = SeriesOf<NTBarSeries<Pt>>(cut);
        using var surface = Draw(cut, series);

        series.HitTest(Center(VerticalBar(cut.Instance, 1, 2, -20m)), Area)!.Value.Index.Should().Be(1);
    }

    [Fact]
    public void HitTest_AfterReplacingData_UsesTheNewGeometry() {
        var cut = RenderChart(XAxisKind.Default, SeriesFragment<NTBarSeries<Pt>>(Categories(10m, 20m), Primary));
        var series = SeriesOf<NTBarSeries<Pt>>(cut);
        using (Draw(cut, series)) {
        }
        var oldTop = VerticalBar(cut.Instance, 1, 2, 20m);
        series.HitTest(new SKPoint(oldTop.MidX, oldTop.Top + 3f), Area).Should().NotBeNull();

        ReRender(cut, new ChartSetup { XAxis = XAxisKind.Default }, SeriesFragment<NTBarSeries<Pt>>(Categories(10m, 5m), Primary));
        using (Draw(cut, series)) {
        }

        series.HitTest(new SKPoint(oldTop.MidX, oldTop.Top + 3f), Area).Should().BeNull();
    }

    [Fact]
    public void HitTest_AfterInPlaceMutationAndInvalidate_UsesTheNewGeometry() {
        var data = Categories(10m, 20m).ToList();
        var cut = RenderChart(XAxisKind.Default, SeriesFragment<NTBarSeries<Pt>>(data, Primary));
        var series = SeriesOf<NTBarSeries<Pt>>(cut);
        using (Draw(cut, series)) {
        }
        var probeBefore = VerticalBar(cut.Instance, 0, 2, 10m);
        probeBefore = new SKRect(probeBefore.Left, probeBefore.Top - 20f, probeBefore.Right, probeBefore.Top - 10f);
        series.HitTest(Center(probeBefore), Area).Should().BeNull();

        data[0] = data[0] with { Y = 20m };
        series.Invalidate();
        using (Draw(cut, series)) {
        }

        series.HitTest(Center(probeBefore), Area).Should().NotBeNull();
    }

    [Fact]
    public void Ranges_VerticalBarsUseCategoryIndexForXAndTotalsForY() {
        var cut = RenderChart(XAxisKind.Default, SeriesFragment<NTBarSeries<Pt>>(Categories(10m, -5m, 30m), Primary));
        var series = SeriesOf<NTBarSeries<Pt>>(cut);

        series.GetXRange().Should().Be((0d, 2d));
        series.GetYRange().Should().Be((-5m, 30m));
    }

    [Fact]
    public void Ranges_WithEmptyData_AreNull() {
        var cut = RenderChart(XAxisKind.Default, SeriesFragment<NTBarSeries<Pt>>([], Primary));
        var series = SeriesOf<NTBarSeries<Pt>>(cut);

        series.GetXRange().Should().BeNull();
        series.GetYRange().Should().BeNull();
    }

    [Fact]
    public void GetYRange_WithWindow_OnlyConsidersBarsInsideItWithoutOverscan() {
        var data = Enumerable.Range(1, 6).Select(i => new Pt(i, i * 10m)).ToArray();
        var cut = RenderChart(XAxisKind.Int, SeriesFragment<NTBarSeries<Pt>>(data, Primary));

        SeriesOf<NTBarSeries<Pt>>(cut).GetYRange(3, 4).Should().Be((30m, 40m));
    }

    [Fact]
    public void GetYRange_WithWindowOutsideTheData_IsNull() {
        var data = Enumerable.Range(1, 6).Select(i => new Pt(i, i * 10m)).ToArray();
        var cut = RenderChart(XAxisKind.Int, SeriesFragment<NTBarSeries<Pt>>(data, Primary));

        SeriesOf<NTBarSeries<Pt>>(cut).GetYRange(100, 200).Should().BeNull();
    }

    [Fact]
    public void ChartYRange_AnchorsPositiveBarsAtZeroAndPadsOnlyTheTop() {
        var cut = RenderChart(XAxisKind.Default, SeriesFragment<NTBarSeries<Pt>>(Categories(100m, 200m), Primary));

        ChartYRange(cut.Instance, padded: true).Should().Be((0m, 210m));
        ChartYRange(cut.Instance).Should().Be((0m, 200m));
    }

    [Fact]
    public void ChartYRange_WithNegativeBars_KeepsTheDataMinimum() {
        var cut = RenderChart(XAxisKind.Default, SeriesFragment<NTBarSeries<Pt>>(Categories(100m, -50m), Primary));

        ChartYRange(cut.Instance).Should().Be((-50m, 100m));
    }

    [Fact]
    public void ChartYRange_ValueAxisMinimumOverridesTheZeroFloor() {
        var cut = RenderChart(XAxisKind.Default, SeriesFragment<NTBarSeries<Pt>>(Categories(100m, 200m), Primary, ("ValueAxisMinimum", 50m)));

        ChartYRange(cut.Instance).Min.Should().Be(50m);
    }

    [Fact]
    public void ChartXRange_ForCategoriesPadsHalfACategoryEachSideSoEdgeBarsAreNotClipped() {
        var cut = RenderChart(XAxisKind.Default, SeriesFragment<NTBarSeries<Pt>>(Categories(1m, 2m, 3m, 4m), Primary));

        ChartXRange(cut.Instance, padded: true).Should().Be((-0.5d, 3.5d));
    }

    [Fact]
    public void YSeriesOnSecondaryAxis_BarsScaleAgainstTheSecondaryRange() {
        var cut = RenderChart(
            new ChartSetup { XAxis = XAxisKind.Default, SecondaryYAxis = true },
            SeriesFragment<NTLineSeries<Pt>>(Categories(1m, 2m)),
            SeriesFragment<NTBarSeries<Pt>>(Categories(1000m, 2000m), Primary, ("UseSecondaryYAxis", true)));
        var bars = SeriesOf<NTBarSeries<Pt>>(cut);

        using var surface = Draw(cut, bars);

        // The tallest bar nearly reaches the top of the canvas (range padded by 5%), which only happens with the secondary scale.
        ColumnCount(surface, (int)cut.Instance.ScaleX(1, Area), Red).Should().BeGreaterThan(170);
    }
}
