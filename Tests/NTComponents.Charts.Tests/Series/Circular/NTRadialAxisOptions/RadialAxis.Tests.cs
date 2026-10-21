using Microsoft.AspNetCore.Components;
using NTComponents.Charts.Core;
using NTComponents.Charts.Core.Axes;
using SkiaSharp;

namespace NTComponents.Charts.Tests.Series.Circular;

public class RadialAxis_Tests : CircularChartTestBase {
    // Bare chart plot area (8,8)-(392,292); axis insets 60 => (68,68)-(332,232): center (200,150), radius 82.
    private static readonly SKPoint Center = new(200, 150);
    private const float AxisRadius = 82f;

    private IRenderedComponent<NTChart<Item>> RenderRadar(Item[] data, bool seriesVisible = false) => RenderChart(builder => {
        SeriesFragment<NTRadarSeries<Item>>(data, new() { ["ShowDataLabels"] = false, ["Visible"] = seriesVisible, ["MaxValue"] = (decimal?)5m })(builder);
        builder.OpenComponent<NTRadialAxisOptions<Item>>(100);
        builder.CloseComponent();
    });

    private static void UseLabels(IRenderedComponent<NTChart<Item>> cut) {
        var axis = cut.FindComponent<NTRadialAxisOptions<Item>>().Instance;
        axis.Labels = Enumerable.Range(0, 20).Select(i => $"L{i}").ToList();
    }

    private static bool IsGray(SKColor c) => Math.Abs(c.Red - c.Green) < 12 && Math.Abs(c.Green - c.Blue) < 12 && c.Red is > 90 and < 235;

    /// <summary>Counts gray runs along a ray; each ring/edge crossing is one run.</summary>
    private static int GrayRuns(SKSurface surface, float degrees, float fromRadius, float toRadius) {
        var runs = 0;
        var inRun = false;
        for (var r = fromRadius; r <= toRadius; r += 0.5f) {
            var p = Polar(Center, r, degrees);
            var gray = IsGray(PixelAt(surface, p.X, p.Y));
            if (gray && !inRun) {
                runs++;
            }
            inRun = gray;
        }

        return runs;
    }

    [Theory]
    [InlineData(1f, 60f)]
    [InlineData(2f, 120f)]
    public void Measure_InsetsTheAreaOnEverySideByScaledPadding(float density, float inset) {
        var cut = RenderRadar(Items(1, 2, 3));
        using var surface = NewSurface(800, 600);
        var axis = cut.FindComponent<NTRadialAxisOptions<Item>>().Instance;
        var area = new SKRect(10, 20, 700, 500);

        var measured = axis.Measure(CreateContext(cut.Instance, surface, density), area);

        measured.Should().Be(new SKRect(10 + inset, 20 + inset, 700 - inset, 500 - inset));
    }

    [Fact]
    public void InvisibleAxis_LeavesTheAreaUntouchedAndDrawsNothing() {
        var cut = RenderRadar(Items(1, 2, 3));
        UseLabels(cut);
        var axis = cut.FindComponent<NTRadialAxisOptions<Item>>().Instance;
        axis.Visible = false;
        using var surface = NewSurface();
        var context = CreateContext(cut.Instance, surface);
        var area = new SKRect(0, 0, 400, 300);

        axis.Measure(context, area).Should().Be(area);
        axis.Render(context, area).Should().Be(area);
        CountNonBackground(surface).Should().Be(0);
    }

    [Fact]
    public async Task CircleShape_DrawsOneConcentricRingPerLevel() {
        var cut = RenderRadar(Items(5, 5, 5));
        UseLabels(cut);
        var axis = cut.FindComponent<NTRadialAxisOptions<Item>>().Instance;
        axis.Shape = RadialAxisShape.Circle;
        axis.Levels = 4;
        using var surface = NewSurface();

        await PaintAsync(cut, surface);

        // Angle 0 is not one of the three spokes (-90, 30, 150) so only rings cross the ray.
        GrayRuns(surface, 0, 5, AxisRadius + 3).Should().Be(4);
        var ring3 = Polar(Center, (AxisRadius * 3 / 4) - 0.5f, 0); // ring 3 sits at 61.5px, between two pixel centers
        IsGray(PixelAt(surface, ring3.X, ring3.Y)).Should().BeTrue();
    }

    [Fact]
    public async Task PolygonShape_DrawsRingsAsPolygonsThroughTheSpokeEnds() {
        var cut = RenderRadar(Items(5, 5, 5));
        UseLabels(cut);
        var axis = cut.FindComponent<NTRadialAxisOptions<Item>>().Instance;
        axis.Levels = 3;
        using var surface = NewSurface();

        await PaintAsync(cut, surface);

        var outerEdgeMidpoint = new SKPoint((Polar(Center, AxisRadius, -90).X + Polar(Center, AxisRadius, 30).X) / 2f, (Polar(Center, AxisRadius, -90).Y + Polar(Center, AxisRadius, 30).Y) / 2f);
        IsGray(PixelAt(surface, outerEdgeMidpoint.X, outerEdgeMidpoint.Y)).Should().BeTrue("the outer polygon edge passes between spoke ends");
        var circleSpot = Polar(Center, AxisRadius, 0);
        IsBackground(PixelAt(surface, circleSpot.X, circleSpot.Y)).Should().BeTrue("a polygon ring is inside the circle away from vertices");
    }

    [Fact]
    public async Task EachDataItem_GetsASpokeFromTheCenter() {
        var cut = RenderRadar(Items(5, 5, 5, 5, 5));
        UseLabels(cut);
        using var surface = NewSurface();

        await PaintAsync(cut, surface);

        for (var i = 0; i < 5; i++) {
            var p = Polar(Center, AxisRadius * 0.9f, -90 + (i * 72f));
            AnyPixel(surface, new SKRect(p.X - 1.5f, p.Y - 1.5f, p.X + 1.5f, p.Y + 1.5f), c => !IsBackground(c)).Should().BeTrue($"spoke {i}");
        }
        var between = Polar(Center, AxisRadius * 0.15f, -90 + 36f);
        IsBackground(PixelAt(surface, between.X, between.Y)).Should().BeTrue();
    }

    [Fact]
    public async Task AxisShrinksTheRadarPlotAreaByTheLabelPadding() {
        var cut = RenderChart(builder => {
            SeriesFragment<NTRadarSeries<Item>>(Items(8, 8, 8), new() { ["ShowDataLabels"] = false, ["MaxValue"] = (decimal?)8m })(builder);
            builder.OpenComponent<NTRadialAxisOptions<Item>>(100);
            builder.CloseComponent();
        });
        UseLabels(cut);
        using var surface = NewSurface();

        await PaintAsync(cut, surface);

        var vertex = Polar(Center, AxisRadius, -90);
        PixelAt(surface, vertex.X, vertex.Y).Should().Be(PaletteColors[0], "radar vertices follow the inset radius of 82");
        var withoutAxisVertex = Polar(Center, 145, -90);
        IsBackground(PixelAt(surface, withoutAxisVertex.X, withoutAxisVertex.Y)).Should().BeTrue();
    }

    [Fact]
    public async Task EmptyData_DrawsNoRingsButStillReservesTheLabelPadding() {
        var cut = RenderRadar([]);
        UseLabels(cut);
        using var surface = NewSurface();

        await PaintAsync(cut, surface);

        CountNonBackground(surface).Should().Be(0);
    }

    [Fact]
    public async Task NonRadarSeries_GetsInsetAreaWithoutRings() {
        var cut = RenderChart(builder => {
            SeriesFragment<NTPieSeries<Item>>(Items(1, 1), new() { ["ShowDataLabels"] = false })(builder);
            builder.OpenComponent<NTRadialAxisOptions<Item>>(100);
            builder.CloseComponent();
        });
        using var surface = NewSurface();

        await PaintAsync(cut, surface);

        IsGray(PixelAt(surface, 250, 150)).Should().BeFalse();
        var extent = Stats(surface, PaletteColors[0]).Bounds;
        extent.Height.Should().BeApproximately(164f, 2f, "pie radius is half the inset height (164/2 = 82 per side)");
    }

    [Fact]
    public void Levels_CanBeSetFromMarkup() {
        var act = () => RenderChart(builder => {
            SeriesFragment<NTRadarSeries<Item>>(Items(1, 2, 3))(builder);
            builder.OpenComponent<NTRadialAxisOptions<Item>>(100);
            builder.AddAttribute(101, "Levels", 3);
            builder.CloseComponent();
        });

        act.Should().NotThrow();
    }

    [Fact]
    public async Task DefaultConfiguration_RendersWithoutThrowing() {
        var cut = RenderRadar(Items(5, 5, 5));
        cut.FindComponent<NTRadarSeries<Item>>().Instance.MaxValue = null;
        using var surface = NewSurface();

        var act = () => PaintAsync(cut, surface);

        await act.Should().NotThrowAsync();
    }
}
