using NTComponents.Charts.Core;
using NTComponents.Charts.Core.Series;
using SkiaSharp;

namespace NTComponents.Charts.Tests.Series.Cartesian;

public class NTBarSeriesVerticalBars_Tests : BarTestBase {
    private static Pt[] Categories(params decimal[] values) => values.Select((v, i) => new Pt(((char)('A' + i)).ToString(), v)).ToArray();

    [Fact]
    public void Render_WithEmptyData_DrawsNothingAndReturnsTheArea() {
        var cut = RenderChart(XAxisKind.Default, SeriesFragment<NTBarSeries<Pt>>([], Primary));
        using var surface = NewSurface();

        var result = SeriesOf<NTBarSeries<Pt>>(cut).Render(CreateContext(cut.Instance, surface), Area);

        result.Should().Be(Area);
        CountDrawnPixels(surface).Should().Be(0);
    }

    [Fact]
    public void Render_FillsEachBarRectangleWithTheSeriesColor() {
        var data = Categories(10m, 20m, 30m);
        var cut = RenderChart(XAxisKind.Default, SeriesFragment<NTBarSeries<Pt>>(data, Primary));

        using var surface = Draw(cut, SeriesOf<NTBarSeries<Pt>>(cut));

        for (var i = 0; i < data.Length; i++) {
            var rect = VerticalBar(cut.Instance, i, data.Length, data[i].Y);
            IsClose(PixelAt(surface, Center(rect)), Red).Should().BeTrue($"bar {i} is filled");
            IsClose(PixelAt(surface, new SKPoint(rect.MidX, rect.Top + 1.5f)), Red).Should().BeTrue($"bar {i} reaches its top");
            IsWhite(PixelAt(surface, new SKPoint(rect.MidX, rect.Top - 2.5f))).Should().BeTrue($"nothing is drawn above bar {i}");
            IsWhite(PixelAt(surface, new SKPoint(rect.Left - 2.5f, rect.MidY))).Should().BeTrue($"nothing is drawn left of bar {i}");
        }
    }

    [Fact]
    public void Render_BarHeightsAreProportionalToValues() {
        var cut = RenderChart(XAxisKind.Default, SeriesFragment<NTBarSeries<Pt>>(Categories(10m, 20m, 40m), Primary));

        using var surface = Draw(cut, SeriesOf<NTBarSeries<Pt>>(cut));

        var heights = Enumerable.Range(0, 3).Select(i => (double)ColumnCount(surface, (int)VerticalBar(cut.Instance, i, 3, 0m).MidX, Red)).ToArray();
        (heights[1] / heights[0]).Should().BeApproximately(2.0, 0.08);
        (heights[2] / heights[0]).Should().BeApproximately(4.0, 0.15);
    }

    [Fact]
    public void Render_BarsStartAtTheZeroBaselineWhenAllValuesArePositive() {
        var cut = RenderChart(XAxisKind.Default, SeriesFragment<NTBarSeries<Pt>>(Categories(100m, 200m), Primary));
        var rect = VerticalBar(cut.Instance, 0, 2, 100m);

        using var surface = Draw(cut, SeriesOf<NTBarSeries<Pt>>(cut));

        rect.Bottom.Should().BeApproximately(SurfaceHeight - 3f, 0.01f, "the value axis is anchored at zero, not at the smallest value");
        IsClose(PixelAt(surface, new SKPoint(rect.MidX, rect.Bottom - 1.5f)), Red).Should().BeTrue();
    }

    [Fact]
    public void Render_NegativeValuesHangBelowTheZeroBaseline() {
        var data = Categories(30m, -20m);
        var cut = RenderChart(XAxisKind.Default, SeriesFragment<NTBarSeries<Pt>>(data, Primary));
        var up = VerticalBar(cut.Instance, 0, 2, 30m);
        var down = VerticalBar(cut.Instance, 1, 2, -20m);

        using var surface = Draw(cut, SeriesOf<NTBarSeries<Pt>>(cut));

        down.Top.Should().BeApproximately(up.Bottom, 0.01f, "both bars share the zero baseline");
        IsClose(PixelAt(surface, Center(up)), Red).Should().BeTrue();
        IsClose(PixelAt(surface, Center(down)), Red).Should().BeTrue();
        IsWhite(PixelAt(surface, new SKPoint(down.MidX, down.Top - 3f))).Should().BeTrue("nothing above the zero line for the negative bar");
        IsWhite(PixelAt(surface, new SKPoint(up.MidX, up.Bottom + 3f))).Should().BeTrue("nothing below the zero line for the positive bar");
    }

    [Fact]
    public void Render_AllNegativeValues_BarsHangFromTheTopOfTheValueAxis() {
        var data = Categories(-10m, -30m);
        var cut = RenderChart(XAxisKind.Default, SeriesFragment<NTBarSeries<Pt>>(data, Primary));
        var shallow = VerticalBar(cut.Instance, 0, 2, -10m);
        var deep = VerticalBar(cut.Instance, 1, 2, -30m);

        using var surface = Draw(cut, SeriesOf<NTBarSeries<Pt>>(cut));

        shallow.Top.Should().BeApproximately(deep.Top, 0.01f);
        deep.Height.Should().BeGreaterThan(shallow.Height);
        IsClose(PixelAt(surface, Center(shallow)), Red).Should().BeTrue();
        IsClose(PixelAt(surface, Center(deep)), Red).Should().BeTrue();
    }

    [Fact]
    public void Render_ZeroValueBar_HasNoVisibleArea() {
        var cut = RenderChart(XAxisKind.Default, SeriesFragment<NTBarSeries<Pt>>(Categories(0m, 10m), Primary));

        using var surface = Draw(cut, SeriesOf<NTBarSeries<Pt>>(cut));

        ColumnCount(surface, (int)VerticalBar(cut.Instance, 0, 2, 0m).MidX, Red).Should().Be(0);
    }

    [Fact]
    public void Render_SingleBar_IsCenteredAndFillsMostOfTheWidth() {
        var cut = RenderChart(XAxisKind.Default, SeriesFragment<NTBarSeries<Pt>>(Categories(10m), Primary));
        var rect = VerticalBar(cut.Instance, 0, 1, 10m);

        using var surface = Draw(cut, SeriesOf<NTBarSeries<Pt>>(cut));

        rect.MidX.Should().BeApproximately(150f, 1f);
        rect.Width.Should().BeGreaterThan(250f);
        rect.Left.Should().BeGreaterThanOrEqualTo(0f, "the only bar must not be clipped by the canvas edge");
        IsClose(PixelAt(surface, Center(rect)), Red).Should().BeTrue();
    }

    [Fact]
    public void Render_NumericXAxis_PositionsBarsByXValue() {
        var data = new[] { new Pt(1, 10m), new Pt(5, 20m), new Pt(6, 30m) };
        var cut = RenderChart(XAxisKind.Int, SeriesFragment<NTBarSeries<Pt>>(data, Primary));
        var series = SeriesOf<NTBarSeries<Pt>>(cut);

        using var surface = Draw(cut, series);

        // Numeric X: bar centers follow the axis scale, so x=5 and x=6 are neighbours and x=1 is far left.
        var x1 = cut.Instance.ScaleX(1, Area);
        var x5 = cut.Instance.ScaleX(5, Area);
        var x6 = cut.Instance.ScaleX(6, Area);
        IsClose(PixelAt(surface, new SKPoint(x1, SurfaceHeight - 5)), Red).Should().BeTrue();
        IsClose(PixelAt(surface, new SKPoint(x5, SurfaceHeight - 5)), Red).Should().BeTrue();
        IsClose(PixelAt(surface, new SKPoint(x6, SurfaceHeight - 5)), Red).Should().BeTrue();
        IsWhite(PixelAt(surface, new SKPoint((x1 + x5) / 2, SurfaceHeight - 5))).Should().BeTrue("no bar between x=1 and x=5");
    }

    [Fact]
    public void Render_WithCornerRadius_RoundsTheCorners() {
        var data = Categories(30m, 10m);
        var square = RenderChart(XAxisKind.Default, SeriesFragment<NTBarSeries<Pt>>(data, Primary));
        var rounded = RenderChart(XAxisKind.Default, SeriesFragment<NTBarSeries<Pt>>(data, Primary, ("CornerRadius", 12f)));
        var rect = VerticalBar(square.Instance, 0, 2, 30m);
        var corner = new SKPoint(rect.Left + 2f, rect.Top + 2f);

        using var squareSurface = Draw(square, SeriesOf<NTBarSeries<Pt>>(square));
        using var roundedSurface = Draw(rounded, SeriesOf<NTBarSeries<Pt>>(rounded));

        IsClose(PixelAt(squareSurface, corner), Red).Should().BeTrue();
        IsWhite(PixelAt(roundedSurface, corner)).Should().BeTrue();
        IsClose(PixelAt(roundedSurface, Center(rect)), Red).Should().BeTrue();
    }

    [Fact]
    public void Render_WithValueAxisMinimum_BarsStartAtThatMinimum() {
        var data = Categories(10m, 20m);
        var cut = RenderChart(XAxisKind.Default, SeriesFragment<NTBarSeries<Pt>>(data, Primary, ("ValueAxisMinimum", 5m)));

        ChartYRange(cut.Instance).Min.Should().Be(5m);
        var rect = VerticalBar(cut.Instance, 0, 2, 10m);
        using var surface = Draw(cut, SeriesOf<NTBarSeries<Pt>>(cut));

        rect.Height.Should().BeGreaterThan(0f);
        rect.Bottom.Should().BeApproximately(SurfaceHeight - 3f, 0.01f);
        IsClose(PixelAt(surface, Center(rect)), Red).Should().BeTrue();
    }

    [Fact]
    public void Render_ReplacingData_RedrawsBarsAtTheNewHeights() {
        var cut = RenderChart(XAxisKind.Default, SeriesFragment<NTBarSeries<Pt>>(Categories(10m, 20m), Primary));
        var series = SeriesOf<NTBarSeries<Pt>>(cut);
        using (Draw(cut, series)) {
        }

        ReRender(cut, new ChartSetup { XAxis = XAxisKind.Default }, SeriesFragment<NTBarSeries<Pt>>(Categories(40m, 5m), Primary));

        using var surface = Draw(cut, series);
        var first = VerticalBar(cut.Instance, 0, 2, 40m);
        var second = VerticalBar(cut.Instance, 1, 2, 5m);
        IsClose(PixelAt(surface, new SKPoint(first.MidX, first.Top + 2f)), Red).Should().BeTrue();
        IsWhite(PixelAt(surface, new SKPoint(second.MidX, second.Top - 3f))).Should().BeTrue();
        ColumnCount(surface, (int)first.MidX, Red).Should().BeGreaterThan(ColumnCount(surface, (int)second.MidX, Red) * 5);
    }

    [Fact]
    public void Render_InPlaceMutationAfterInvalidate_RedrawsTheBar() {
        var data = Categories(10m, 20m).ToList();
        var cut = RenderChart(XAxisKind.Default, SeriesFragment<NTBarSeries<Pt>>(data, Primary));
        var series = SeriesOf<NTBarSeries<Pt>>(cut);
        var before = ColumnCount(Draw(cut, series), (int)VerticalBar(cut.Instance, 0, 2, 0m).MidX, Red);

        data[0] = data[0] with { Y = 80m };
        series.Invalidate();

        using var surface = Draw(cut, series);
        ColumnCount(surface, (int)VerticalBar(cut.Instance, 0, 2, 0m).MidX, Red).Should().BeGreaterThan((int)(before * 1.5));
    }

    [Fact]
    public void Render_Twice_ProducesIdenticalImages() {
        var cut = RenderChart(XAxisKind.Default, SeriesFragment<NTBarSeries<Pt>>(Categories(10m, 20m, 15m), Primary));
        var series = SeriesOf<NTBarSeries<Pt>>(cut);

        using var first = Draw(cut, series);
        using var second = Draw(cut, series);

        Snapshot(second).Should().Equal(Snapshot(first));
    }

    [Fact]
    public void Render_WithDataLabels_DrawsLabelsInAdditionToBars() {
        var plain = RenderChart(XAxisKind.Default, SeriesFragment<NTBarSeries<Pt>>(Categories(10m, 20m), Primary));
        var labelled = RenderChart(XAxisKind.Default, SeriesFragment<NTBarSeries<Pt>>(Categories(10m, 20m), Primary, ("ShowDataLabels", true)));

        using var plainSurface = Draw(plain, SeriesOf<NTBarSeries<Pt>>(plain));
        using var labelledSurface = Draw(labelled, SeriesOf<NTBarSeries<Pt>>(labelled));

        Snapshot(labelledSurface).Should().NotEqual(Snapshot(plainSurface));
    }

    [Fact]
    public void Render_FadeOtherBarsOnHover_KeepsTheHoveredBarOpaqueAndDimsTheRest() {
        var cut = RenderChart(XAxisKind.Default, SeriesFragment<NTBarSeries<Pt>>(Categories(30m, 30m), Primary, ("FadeOtherBarsOnHover", true)));
        var series = SeriesOf<NTBarSeries<Pt>>(cut);
        var first = VerticalBar(cut.Instance, 0, 2, 30m);
        var second = VerticalBar(cut.Instance, 1, 2, 30m);
        SetHover(cut.Instance, series, 0);

        using var surface = Draw(cut, series);

        IsClose(PixelAt(surface, Center(first)), Red, 24).Should().BeTrue("the hovered bar keeps full opacity under its state layer");
        var faded = PixelAt(surface, Center(second));
        faded.Green.Should().BeCloseTo((byte)(255 * (1f - NTChartTokens.DeEmphasisOpacity)), 4, "a 38% opaque red is a light pink");
        faded.Red.Should().Be(255);
    }

    [Fact]
    public void Render_NonHoveredBarOpacity_ControlsTheFade() {
        byte FadedGreen(float opacity) {
            var cut = RenderChart(XAxisKind.Default, SeriesFragment<NTBarSeries<Pt>>(Categories(30m, 30m), Primary, ("FadeOtherBarsOnHover", true), ("NonHoveredBarOpacity", opacity)));
            var series = SeriesOf<NTBarSeries<Pt>>(cut);
            SetHover(cut.Instance, series, 0);
            using var surface = Draw(cut, series);
            return PixelAt(surface, Center(VerticalBar(cut.Instance, 1, 2, 30m))).Green;
        }

        FadedGreen(0.8f).Should().BeLessThan(FadedGreen(0.2f));
    }

    [Fact]
    public void Render_WhenAnotherSeriesIsHovered_DimsTheWholeSeries() {
        var cut = RenderChart(XAxisKind.Default, SeriesFragment<NTBarSeries<Pt>>(Categories(30m), Primary), SeriesFragment<NTLineSeries<Pt>>([new Pt("A", 5m)], Secondary));
        var bars = SeriesOf<NTBarSeries<Pt>>(cut);
        var rect = VerticalBar(cut.Instance, 0, 1, 30m, seriesCount: 1);
        SetHover(cut.Instance, SeriesOf<NTLineSeries<Pt>>(cut), 0);

        using var surface = Draw(cut, bars);

        var pixel = PixelAt(surface, Center(rect));
        pixel.Green.Should().BeGreaterThan(180);
    }
}
