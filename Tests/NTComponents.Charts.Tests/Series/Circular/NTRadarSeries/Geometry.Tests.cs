using NTComponents.Charts.Core;
using NTComponents.Charts.Core.Series;
using SkiaSharp;

namespace NTComponents.Charts.Tests.Series.Circular;

/// <summary>Vertex placement, fill/stroke and hit-testing of the radar polygon.</summary>
public class Geometry_Tests : CircularChartTestBase {
    // 300x250 area -> radius 125 centered on (200, 150)
    private static readonly SKRect Area = new(50, 25, 350, 275);
    private static readonly SKPoint Center = new(200, 150);
    private const float Radius = 125f;
    private static readonly SKColor Series0 = PaletteColors[0];

    private (NTRadarSeries<Item> Series, NTChart<Item> Chart, SKSurface Surface) Create(Item[] data, Dictionary<string, object>? attributes = null) {
        var merged = new Dictionary<string, object> { ["ShowDataLabels"] = false };
        foreach (var attribute in attributes ?? []) {
            merged[attribute.Key] = attribute.Value;
        }
        var cut = RenderChart<NTRadarSeries<Item>>(data, merged);
        return (cut.FindComponent<NTRadarSeries<Item>>().Instance, cut.Instance, NewSurface());
    }

    private static SKPoint Vertex(int index, int count, float fraction) => Polar(Center, Radius * fraction, (index * 360f / count) - 90f);

    [Fact]
    public void Vertices_AreSpacedEvenlyClockwiseFromTwelveOClockAndScaledByValueOverMax() {
        var (series, chart, surface) = Create(Items(8, 4, 8, 2));
        using var _ = surface;

        series.Render(CreateContext(chart, surface), Area);

        var expected = new[] { Vertex(0, 4, 1f), Vertex(1, 4, 0.5f), Vertex(2, 4, 1f), Vertex(3, 4, 0.25f) };
        foreach (var vertex in expected) {
            PixelAt(surface, vertex.X, vertex.Y).Should().Be(Series0, $"vertex marker expected at {vertex}");
        }
    }

    [Fact]
    public void VertexMarkers_AreFourPixelDisksAroundEachVertex() {
        var (series, chart, surface) = Create(Items(8, 8, 8));
        using var _ = surface;

        series.Render(CreateContext(chart, surface), Area);

        var vertex = Vertex(1, 3, 1f);
        PixelAt(surface, vertex.X + 3, vertex.Y).Should().Be(Series0);
        PixelAt(surface, vertex.X - 3, vertex.Y).Should().Be(Series0);
        var outsideMarkerAwayFromPolygon = Polar(Center, Radius + 8, 30);
        IsBackground(PixelAt(surface, outsideMarkerAwayFromPolygon.X, outsideMarkerAwayFromPolygon.Y)).Should().BeTrue();
    }

    [Fact]
    public void ExplicitMaxValue_OverridesTheDataMaximum() {
        var (series, chart, surface) = Create(Items(5, 5, 5), new() { ["MaxValue"] = (decimal?)10m });
        using var _ = surface;

        series.Render(CreateContext(chart, surface), Area);

        foreach (var index in Enumerable.Range(0, 3)) {
            var half = Vertex(index, 3, 0.5f);
            PixelAt(surface, half.X, half.Y).Should().Be(Series0);
            var full = Vertex(index, 3, 1f);
            IsBackground(PixelAt(surface, full.X, full.Y)).Should().BeTrue($"vertex {index} must not be placed at the data max");
        }
    }

    [Fact]
    public void ChangingMaxValue_MovesTheVerticesWithoutAnExplicitInvalidate() {
        var (series, chart, surface) = Create(Items(5, 5, 5), new() { ["MaxValue"] = (decimal?)10m });
        using var _ = surface;
        series.Render(CreateContext(chart, surface), Area);

        series.MaxValue = 5m;
        surface.Canvas.Clear(SKColors.White);
        series.Render(CreateContext(chart, surface), Area);

        var full = Vertex(0, 3, 1f);
        PixelAt(surface, full.X, full.Y).Should().Be(Series0);
    }

    [Fact]
    public void AllZeroValues_CollapseEveryVertexOntoTheCenterWithoutThrowing() {
        var (series, chart, surface) = Create(Items(0, 0, 0));
        using var _ = surface;
        var context = CreateContext(chart, surface);

        var act = () => series.Render(context, Area);

        act.Should().NotThrow();
        PixelAt(surface, Center.X, Center.Y).Should().Be(Series0);
        Stats(surface, Series0).Bounds.Width.Should().BeLessThan(12f);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void FewerThanThreeItems_DrawNothing(int count) {
        var (series, chart, surface) = Create(Items(Enumerable.Repeat(5m, count).ToArray()));
        using var _ = surface;

        series.Render(CreateContext(chart, surface), Area);

        CountNonBackground(surface).Should().Be(0);
    }

    [Fact]
    public void FewerThanThreeItems_ReportNoHits() {
        var (series, chart, surface) = Create(Items(5, 5));
        using var _ = surface;
        series.Render(CreateContext(chart, surface), Area);

        series.HitTest(Vertex(0, 2, 1f), Area).Should().BeNull();
    }

    [Fact]
    public void Fill_UsesAreaOpacityAndStrokeUsesStrokeWidth() {
        var (opaque, chart, surface) = Create(Items(8, 8, 8), new() { ["AreaOpacity"] = 1f, ["StrokeWidth"] = 1f });
        using var _ = surface;
        opaque.Render(CreateContext(chart, surface), Area);
        PixelAt(surface, Center.X, Center.Y).Should().Be(Series0, "AreaOpacity 1 fills the polygon with the series color");

        var (hollow, hollowChart, hollowSurface) = Create(Items(8, 8, 8), new() { ["AreaOpacity"] = 0f });
        using var __ = hollowSurface;
        hollow.Render(CreateContext(hollowChart, hollowSurface), Area);
        IsBackground(PixelAt(hollowSurface, Center.X, Center.Y)).Should().BeTrue("AreaOpacity 0 leaves the interior empty");
        var edgeMidpoint = new SKPoint((Vertex(0, 3, 1f).X + Vertex(1, 3, 1f).X) / 2f, (Vertex(0, 3, 1f).Y + Vertex(1, 3, 1f).Y) / 2f);
        PixelAt(hollowSurface, edgeMidpoint.X, edgeMidpoint.Y).Should().Be(Series0, "the outline passes through the edge midpoint");
    }

    [Fact]
    public void StrokeWidth_ControlsOutlineThickness() {
        var (thin, thinChart, thinSurface) = Create(Items(8, 8, 8), new() { ["AreaOpacity"] = 0f, ["StrokeWidth"] = 1f });
        using var _ = thinSurface;
        var (thick, thickChart, thickSurface) = Create(Items(8, 8, 8), new() { ["AreaOpacity"] = 0f, ["StrokeWidth"] = 9f });
        using var __ = thickSurface;

        thin.Render(CreateContext(thinChart, thinSurface), Area);
        thick.Render(CreateContext(thickChart, thickSurface), Area);

        // Midpoint of the bottom edge (vertex 1 -> vertex 2) nudged 3px inside the polygon.
        var mid = new SKPoint((Vertex(1, 3, 1f).X + Vertex(2, 3, 1f).X) / 2f, (Vertex(1, 3, 1f).Y + Vertex(2, 3, 1f).Y) / 2f - 3f);
        IsBackground(PixelAt(thinSurface, mid.X, mid.Y)).Should().BeTrue();
        PixelAt(thickSurface, mid.X, mid.Y).Should().Be(Series0);
    }

    [Fact]
    public void HiddenSeries_PaintsNothing() {
        var (series, chart, surface) = Create(Items(8, 4, 8), new() { ["Visible"] = false });
        using var _ = surface;

        series.Render(CreateContext(chart, surface), Area);

        CountNonBackground(surface).Should().Be(0);
    }

    [Fact]
    public void OnDataPointRender_CanRecolorIndividualVertexMarkers() {
        var (series, chart, surface) = Create(Items(8, 8, 8), new() {
            ["OnDataPointRender"] = (Action<NTDataPointRenderArgs<Item>>)(args => {
                if (args.Index == 1) {
                    args.Color = args.GetThemeColor(NTColor.TertiaryFixed);
                }
            })
        });
        using var _ = surface;

        series.Render(CreateContext(chart, surface), Area);

        var recolored = Vertex(1, 3, 1f);
        var other = Vertex(2, 3, 1f);
        PixelAt(surface, recolored.X, recolored.Y).Should().Be(PaletteColors[2]);
        PixelAt(surface, other.X, other.Y).Should().Be(Series0);
    }

    [Fact]
    public void DataLabels_AreDrawnAboveVerticesOnlyWhenEnabled() {
        var (labeled, chart, surface) = Create(Items(8, 8, 8), new() { ["ShowDataLabels"] = true });
        using var _ = surface;
        var (plain, plainChart, plainSurface) = Create(Items(8, 8, 8));
        using var __ = plainSurface;
        var top = Vertex(0, 3, 1f);
        var labelRegion = new SKRect(top.X - 12, top.Y - 22, top.X + 12, top.Y - 8);

        labeled.Render(CreateContext(chart, surface), Area);
        plain.Render(CreateContext(plainChart, plainSurface), Area);

        AnyPixel(surface, labelRegion, c => !IsBackground(c)).Should().BeTrue();
        AnyPixel(plainSurface, labelRegion, c => !IsBackground(c)).Should().BeFalse();
    }

    [Fact]
    public void HitTest_ReturnsNearestVertexWithinTenPixelsAndTheBoundDataItem() {
        var data = Items(8, 4, 8, 2);
        var (series, chart, surface) = Create(data);
        using var _ = surface;
        series.Render(CreateContext(chart, surface), Area);
        var vertex = Vertex(1, 4, 0.5f);

        var near = series.HitTest(new SKPoint(vertex.X + 9f, vertex.Y), Area);
        var far = series.HitTest(new SKPoint(vertex.X + 10.5f, vertex.Y), Area);

        near!.Value.Index.Should().Be(1);
        near.Value.Data.Should().BeSameAs(data[1]);
        far.Should().BeNull();
    }

    [Fact]
    public void HitTest_ThresholdIsExclusiveAtTenPixels() {
        var (series, chart, surface) = Create(Items(8, 8, 8));
        using var _ = surface;
        series.Render(CreateContext(chart, surface), Area);
        var vertex = Vertex(0, 3, 1f);

        series.HitTest(new SKPoint(vertex.X, vertex.Y + 9.9f), Area).Should().NotBeNull();
        series.HitTest(new SKPoint(vertex.X, vertex.Y + 10.1f), Area).Should().BeNull();
    }

    [Fact]
    public void HitTest_WithCoincidentVertices_PrefersTheLowestIndex() {
        var (series, chart, surface) = Create(Items(0, 0, 0));
        using var _ = surface;
        series.Render(CreateContext(chart, surface), Area);

        series.HitTest(Center, Area)!.Value.Index.Should().Be(0);
    }

    [Fact]
    public void HitTest_UsesTheAreaItIsGivenNotTheLastRenderedOne() {
        var (series, chart, surface) = Create(Items(8, 8, 8));
        using var _ = surface;
        series.Render(CreateContext(chart, surface), Area);
        var smaller = new SKRect(100, 50, 300, 250);
        var topOfSmaller = Polar(new SKPoint(200, 150), 100, -90);

        series.HitTest(topOfSmaller, smaller)!.Value.Index.Should().Be(0);
        series.HitTest(topOfSmaller, Area).Should().BeNull();
    }

    [Fact]
    public void HitTest_OnEmptyData_ReturnsNull() {
        var (series, _, surface) = Create([]);
        using var _ = surface;

        series.HitTest(Center, Area).Should().BeNull();
    }
}
