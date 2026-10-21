using NTComponents.Charts.Core;
using NTComponents.Charts.Core.Series;
using SkiaSharp;

namespace NTComponents.Charts.Tests.Series.Cartesian;

/// <summary>Bars round only the corners at their value end, and the hovered bar gets the Material 3 state layer.</summary>
public class NTBarSeriesValueEndCorners_Tests : BarTestBase {
    private static Pt[] Categories(params decimal[] values) => values.Select((v, i) => new Pt(((char)('A' + i)).ToString(), v)).ToArray();

    private static bool IsFilled(SKSurface surface, float x, float y) => IsClose(PixelAt(surface, new SKPoint(x, y)), Red);

    [Fact]
    public void NewInstance_DefaultsToTheExtraSmallRadiusAndDeEmphasisOpacity() {
        var series = new NTBarSeries<Pt>();

        series.CornerRadius.Should().Be(4f);
        series.NonHoveredBarOpacity.Should().Be(NTChartTokens.DeEmphasisOpacity);
    }

    [Theory]
    [InlineData(0, true)]
    [InlineData(1, false)]
    public void Render_VerticalBars_RoundOnlyTheValueEnd(int index, bool positive) {
        var cut = RenderChart(XAxisKind.Default, SeriesFragment<NTBarSeries<Pt>>(Categories(30m, -20m), Primary, ("CornerRadius", 12f)));
        var rect = VerticalBar(cut.Instance, index, 2, positive ? 30m : -20m);

        using var surface = Draw(cut, SeriesOf<NTBarSeries<Pt>>(cut));

        var (valueY, baselineY) = positive ? (rect.Top + 1.5f, rect.Bottom - 1.5f) : (rect.Bottom - 1.5f, rect.Top + 1.5f);
        IsFilled(surface, rect.Left + 1.5f, valueY).Should().BeFalse("the value-end corner is rounded");
        IsFilled(surface, rect.Right - 1.5f, valueY).Should().BeFalse("the value-end corner is rounded");
        IsFilled(surface, rect.Left + 1.5f, baselineY).Should().BeTrue("the baseline end stays square");
        IsFilled(surface, rect.Right - 1.5f, baselineY).Should().BeTrue("the baseline end stays square");
    }

    [Theory]
    [InlineData(0, true)]
    [InlineData(1, false)]
    public void Render_HorizontalBars_RoundOnlyTheValueEnd(int index, bool positive) {
        var cut = RenderChart(XAxisKind.Default, SeriesFragment<NTBarSeries<Pt>>(Categories(30m, -20m), Primary, Horizontal, ("CornerRadius", 12f)));
        var rect = HorizontalBar(cut.Instance, index, 2, positive ? 30m : -20m);

        using var surface = Draw(cut, SeriesOf<NTBarSeries<Pt>>(cut));

        var (valueX, baselineX) = positive ? (rect.Right - 1.5f, rect.Left + 1.5f) : (rect.Left + 1.5f, rect.Right - 1.5f);
        IsFilled(surface, valueX, rect.Top + 1.5f).Should().BeFalse("the value-end corner is rounded");
        IsFilled(surface, valueX, rect.Bottom - 1.5f).Should().BeFalse("the value-end corner is rounded");
        IsFilled(surface, baselineX, rect.Top + 1.5f).Should().BeTrue("the baseline end stays square");
        IsFilled(surface, baselineX, rect.Bottom - 1.5f).Should().BeTrue("the baseline end stays square");
    }

    [Fact]
    public void Render_ZeroRadius_DrawsSquareBars() {
        var cut = RenderChart(XAxisKind.Default, SeriesFragment<NTBarSeries<Pt>>(Categories(30m), Primary, ("CornerRadius", 0f)));
        var rect = VerticalBar(cut.Instance, 0, 1, 30m);

        using var surface = Draw(cut, SeriesOf<NTBarSeries<Pt>>(cut));

        IsFilled(surface, rect.Left + 1.5f, rect.Top + 1.5f).Should().BeTrue();
    }

    [Fact]
    public void Render_HoveredBar_GetsAnOnColorStateLayerAtHoverThenPressedOpacity() {
        var cut = RenderChart(XAxisKind.Default, SeriesFragment<NTBarSeries<Pt>>(Categories(30m, 30m), Primary));
        var series = SeriesOf<NTBarSeries<Pt>>(cut);
        var center = Center(VerticalBar(cut.Instance, 0, 2, 30m));
        using var resting = Draw(cut, series);
        var normal = PixelAt(resting, center);
        SetHover(cut.Instance, series, 0);

        using var hovered = Draw(cut, series);
        series.IsPressed = true;
        using var pressed = Draw(cut, series);

        MaxDelta(normal, PixelAt(hovered, center)).Should().BeInRange((int)(255 * NTChartTokens.HoverOpacity) - 2, (int)(255 * NTChartTokens.HoverOpacity) + 2);
        MaxDelta(normal, PixelAt(pressed, center)).Should().BeInRange((int)(255 * NTChartTokens.PressedOpacity) - 2, (int)(255 * NTChartTokens.PressedOpacity) + 3);
    }

    private static int MaxDelta(SKColor a, SKColor b) => Math.Max(Math.Abs(a.Red - b.Red), Math.Max(Math.Abs(a.Green - b.Green), Math.Abs(a.Blue - b.Blue)));
}
