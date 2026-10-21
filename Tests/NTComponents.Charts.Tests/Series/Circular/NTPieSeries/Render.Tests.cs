using NTComponents.Charts.Core.Series;
using NTComponents.Charts.Core;
using SkiaSharp;

namespace NTComponents.Charts.Tests.Series.Circular;

public class Render_Tests : CircularChartTestBase {
    private static readonly SKRect Area = new(0, 0, 400, 300);
    private static readonly SKPoint Center = new(200, 150);
    private const float Radius = 150f;

    private (NTPieSeries<Item> Series, NTChart<Item> Chart, SKSurface Surface) Create(IEnumerable<Item> data, Dictionary<string, object>? attributes = null) {
        var merged = new Dictionary<string, object> { ["ShowDataLabels"] = false };
        foreach (var attribute in attributes ?? []) {
            merged[attribute.Key] = attribute.Value;
        }
        var cut = RenderChart<NTPieSeries<Item>>(data, merged);
        return (cut.FindComponent<NTPieSeries<Item>>().Instance, cut.Instance, NewSurface());
    }

    private static SKColor PixelAtPolar(SKSurface surface, float radius, float degrees) {
        var point = Polar(Center, radius, degrees);
        return PixelAt(surface, point.X, point.Y);
    }

    [Fact]
    public void Slices_AreColoredClockwiseFromTwelveOClockInPaletteOrder() {
        var (series, chart, surface) = Create(Items(1, 1, 2));
        using var _ = surface;

        series.Render(CreateContext(chart, surface), Area);

        PixelAtPolar(surface, 90, -45).Should().Be(PaletteColors[0]);
        PixelAtPolar(surface, 90, 45).Should().Be(PaletteColors[1]);
        PixelAtPolar(surface, 90, 180).Should().Be(PaletteColors[2]);
        PixelAtPolar(surface, 90, 265).Should().Be(PaletteColors[2], "the last slice ends exactly at twelve o'clock");
    }

    [Fact]
    public void Pie_CoversExactlyTheInscribedCircle() {
        var (series, chart, surface) = Create(Items(1, 1, 2));
        using var _ = surface;

        series.Render(CreateContext(chart, surface), Area);

        var expected = MathF.PI * Radius * Radius;
        CountNonBackground(surface).Should().BeInRange((long)(expected * 0.99), (long)(expected * 1.01));
        IsBackground(PixelAt(surface, Center.X + Radius + 6, Center.Y)).Should().BeTrue();
        IsBackground(PixelAt(surface, Center.X, Center.Y)).Should().BeFalse();
    }

    [Fact]
    public void SliceAreas_AreProportionalToValues() {
        var (series, chart, surface) = Create(Items(1, 2, 5));
        using var _ = surface;

        series.Render(CreateContext(chart, surface), Area);

        var counts = PaletteColors.Take(3).Select(c => (double)Stats(surface, c).Count).ToArray();
        var total = counts.Sum();
        (counts[0] / total).Should().BeApproximately(1 / 8d, 0.01);
        (counts[1] / total).Should().BeApproximately(2 / 8d, 0.01);
        (counts[2] / total).Should().BeApproximately(5 / 8d, 0.01);
    }

    [Fact]
    public void NonSquareArea_UsesTheShorterSideForTheRadiusAndCentersThePie() {
        var (series, chart, surface) = Create(Items(1, 1));
        using var _ = surface;

        series.Render(CreateContext(chart, surface), new SKRect(0, 0, 400, 100));

        var combined = SKRect.Union(Stats(surface, PaletteColors[0]).Bounds, Stats(surface, PaletteColors[1]).Bounds);
        combined.Height.Should().BeApproximately(100f, 2f);
        combined.Width.Should().BeApproximately(100f, 2f);
        combined.MidX.Should().BeApproximately(200f, 1.5f);
    }

    [Fact]
    public void ZeroValue_DrawsNothingForThatSliceAndOthersFillTheCircle() {
        var (series, chart, surface) = Create(Items(0, 5, 5));
        using var _ = surface;

        series.Render(CreateContext(chart, surface), Area);

        Stats(surface, PaletteColors[0]).Count.Should().Be(0);
        PixelAt(surface, Center.X + 40, Center.Y - 40).Should().Be(PaletteColors[1]);
        PixelAt(surface, Center.X - 40, Center.Y + 40).Should().Be(PaletteColors[2]);
    }

    [Fact]
    public void NegativeValue_IsIgnoredLikeZero() {
        var (series, chart, surface) = Create(Items(-3, 5, 5));
        using var _ = surface;

        series.Render(CreateContext(chart, surface), Area);

        Stats(surface, PaletteColors[0]).Count.Should().Be(0);
        Stats(surface, PaletteColors[1]).Count.Should().BeGreaterThan(30000);
    }

    [Theory]
    [InlineData(new double[0])]
    [InlineData(new[] { 0d, 0d })]
    [InlineData(new[] { -1d, -2d })]
    public void NoPositiveValues_DrawsNothing(double[] values) {
        var (series, chart, surface) = Create(values.Select((v, i) => new Item($"I{i}", (decimal)v)).ToArray());
        using var _ = surface;

        series.Render(CreateContext(chart, surface), Area);

        CountNonBackground(surface).Should().Be(0);
    }

    [Fact]
    public void SingleItem_PaintsAFullCircle() {
        var (series, chart, surface) = Create(Items(7));
        using var _ = surface;

        series.Render(CreateContext(chart, surface), Area);

        foreach (var degrees in new[] { 0f, 90f, 180f, 270f }) {
            PixelAtPolar(surface, 120, degrees).Should().Be(PaletteColors[0]);
        }
    }

    [Fact]
    public void HiddenSlice_IsRemovedAndRemainingSlicesRenormalize() {
        var (series, chart, surface) = Create(Items(1, 1, 2));
        using var _ = surface;
        ToggleLegend(series, 2);

        series.Render(CreateContext(chart, surface), Area);

        PixelAt(surface, Center.X + 60, Center.Y).Should().Be(PaletteColors[0], "slice 0 now spans the right half");
        PixelAt(surface, Center.X - 60, Center.Y).Should().Be(PaletteColors[1], "slice 1 now spans the left half");
        Stats(surface, PaletteColors[2]).Count.Should().Be(0);
    }

    [Fact]
    public void HiddenSeries_PaintsNothing() {
        var (series, chart, surface) = Create(Items(1, 2), new() { ["Visible"] = false });
        using var _ = surface;

        series.Render(CreateContext(chart, surface), Area);

        CountNonBackground(surface).Should().Be(0);
    }

    [Fact]
    public void FixedInnerRadius_LeavesAHoleOfThatRadius() {
        var (series, chart, surface) = Create(Items(1, 1), new() { ["InnerRadius"] = 40f });
        using var _ = surface;

        series.Render(CreateContext(chart, surface), Area);

        IsBackground(PixelAt(surface, Center.X, Center.Y)).Should().BeTrue();
        IsBackground(PixelAt(surface, Center.X - 30, Center.Y + 1)).Should().BeTrue();
        PixelAt(surface, Center.X - 50, Center.Y + 1).Should().Be(PaletteColors[1]);
    }

    [Fact]
    public void FixedInnerRadius_IsMultipliedByRenderDensity() {
        var (series, chart, surface) = Create(Items(1, 1), new() { ["InnerRadius"] = 40f });
        using var _ = surface;

        series.Render(CreateContext(chart, surface, 2f), Area);

        IsBackground(PixelAt(surface, Center.X - 70, Center.Y + 1)).Should().BeTrue("hole radius is 40 * density 2 = 80");
        PixelAt(surface, Center.X - 90, Center.Y + 1).Should().Be(PaletteColors[1]);
    }

    [Fact]
    public void InnerRadiusRatio_SetsHoleProportionalToOuterRadius() {
        var (series, chart, surface) = Create(Items(1, 1), new() { ["InnerRadiusRatio"] = 0.5f });
        using var _ = surface;

        series.Render(CreateContext(chart, surface), Area);

        IsBackground(PixelAt(surface, Center.X - 70, Center.Y + 1)).Should().BeTrue();
        PixelAt(surface, Center.X - 80, Center.Y + 1).Should().Be(PaletteColors[1]);
    }

    [Fact]
    public void FixedInnerRadius_TakesPrecedenceOverRatio() {
        var (series, chart, surface) = Create(Items(1, 1), new() { ["InnerRadius"] = 20f, ["InnerRadiusRatio"] = 0.9f });
        using var _ = surface;

        series.Render(CreateContext(chart, surface), Area);

        PixelAt(surface, Center.X - 40, Center.Y + 1).Should().Be(PaletteColors[1]);
    }

    [Fact]
    public void InnerRadiusRatioAboveOne_IsClampedSoARingStillShows() {
        var (series, chart, surface) = Create(Items(1, 1), new() { ["InnerRadiusRatio"] = 5f });
        using var _ = surface;

        series.Render(CreateContext(chart, surface), Area);

        IsBackground(PixelAt(surface, Center.X - 100, Center.Y + 1)).Should().BeTrue();
        PixelAt(surface, Center.X - 147, Center.Y + 1).Should().Be(PaletteColors[1], "ratio is clamped to 0.95 leaving a thin ring");
    }

    [Fact]
    public void NegativeInnerRadiusRatio_IsClampedToAFullPie() {
        var (series, chart, surface) = Create(Items(1, 1), new() { ["InnerRadiusRatio"] = -1f });
        using var _ = surface;

        series.Render(CreateContext(chart, surface), Area);

        PixelAt(surface, Center.X - 2, Center.Y + 1).Should().Be(PaletteColors[1]);
    }

    [Fact]
    public void OnDataPointRender_ReceivesDataAndIndexAndCanOverrideColor() {
        var seen = new List<(int Index, string Name)>();
        var (series, chart, surface) = Create(Items(1, 1), new() {
            ["OnDataPointRender"] = (Action<NTDataPointRenderArgs<Item>>)(args => {
                seen.Add((args.Index, args.Data.Name));
                if (args.Index == 1) {
                    args.Color = args.GetThemeColor(NTColor.Success);
                }
            })
        });
        using var _ = surface;

        series.Render(CreateContext(chart, surface), Area);

        seen.Should().Equal((0, "Item0"), (1, "Item1"));
        PixelAt(surface, Center.X - 60, Center.Y).Should().Be(PaletteColors[5]);
        PixelAt(surface, Center.X + 60, Center.Y).Should().Be(PaletteColors[0]);
    }

    [Fact]
    public void DataLabels_AreDrawnInsideLargeSlicesButNotTinyOnes() {
        var (series, chart, surface) = Create(Items(100, 1), new() { ["ShowDataLabels"] = true });
        using var _ = surface;
        var bigSweep = 360f * 100 / 101;
        var bigLabel = Polar(Center, Radius * 0.67f, -90 + (bigSweep / 2));
        var tinyLabel = Polar(Center, Radius * 0.67f, -90 + bigSweep + (360f / 101 / 2));

        series.Render(CreateContext(chart, surface), Area);

        AnyPixel(surface, new SKRect(bigLabel.X - 15, bigLabel.Y - 10, bigLabel.X + 15, bigLabel.Y + 10), c => !IsNear(c, PaletteColors[0])).Should().BeTrue("the big slice shows its value");
        AnyPixel(surface, new SKRect(tinyLabel.X - 1, tinyLabel.Y - 1, tinyLabel.X + 1, tinyLabel.Y + 1), c => !IsNear(c, PaletteColors[1])).Should().BeFalse("the 3.6 degree slice cannot fit a label");
    }

    [Fact]
    public void DataLabelColor_IsUsedForLabels() {
        var (series, chart, surface) = Create(Items(1, 1), new() { ["ShowDataLabels"] = true, ["DataLabelColor"] = NTColor.Success, ["DataLabelSize"] = 24f });
        using var _ = surface;

        series.Render(CreateContext(chart, surface), Area);

        Stats(surface, PaletteColors[5]).Count.Should().BeGreaterThan(20, "label glyph pixels use the configured label color");
    }

    [Fact]
    public void DisabledDataLabels_LeaveTheSliceUniform() {
        var (series, chart, surface) = Create(Items(1, 1), new() { ["ShowDataLabels"] = false });
        using var _ = surface;

        series.Render(CreateContext(chart, surface), Area);

        var expected = MathF.PI * Radius * Radius / 2;
        Stats(surface, PaletteColors[0]).Count.Should().BeInRange((long)(expected * 0.99), (long)(expected * 1.01));
    }

    [Fact]
    public void DegenerateArea_DrawsNothingAndDoesNotThrow() {
        var (series, chart, surface) = Create(Items(1, 2));
        using var _ = surface;
        var context = CreateContext(chart, surface);

        var act = () => series.Render(context, SKRect.Empty);

        act.Should().NotThrow();
        CountNonBackground(surface).Should().Be(0);
    }

    [Fact]
    public void Render_ReturnsTheAreaUnchanged() {
        var (series, chart, surface) = Create(Items(1, 2));
        using var _ = surface;

        series.Render(CreateContext(chart, surface), Area).Should().Be(Area);
    }

    [Fact]
    public void AllOtherSlicesHidden_RemainingSliceFillsTheCircle() {
        var (series, chart, surface) = Create(Items(1, 1));
        using var _ = surface;
        ToggleLegend(series, 1);

        series.Render(CreateContext(chart, surface), Area);

        PixelAtPolar(surface, 100, 180).Should().Be(PaletteColors[0]);
        PixelAtPolar(surface, 100, 0).Should().Be(PaletteColors[0]);
    }

    [Fact]
    public void ZeroOrNegativeSiblings_LeaveTheOnlyPositiveSliceFullCircle() {
        var (series, chart, surface) = Create(Items(0, 5, -2));
        using var _ = surface;

        series.Render(CreateContext(chart, surface), Area);

        PixelAtPolar(surface, 100, 180).Should().Be(PaletteColors[1]);
        PixelAtPolar(surface, 100, 0).Should().Be(PaletteColors[1]);
    }
}
