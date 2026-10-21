using NTComponents.Charts.Core;
using SkiaSharp;

namespace NTComponents.Charts.Tests.Series.Circular;

/// <summary>Cache invalidation, entry animation and disposal of the radar series.</summary>
public class RadarLifecycle_Tests : CircularChartTestBase {
    private static readonly SKRect Area = new(50, 25, 350, 275);
    private static readonly SKPoint Center = new(200, 150);
    private const float Radius = 125f;

    private static SKPoint Vertex(int index, int count, float fraction) => Polar(Center, Radius * fraction, (index * 360f / count) - 90f);

    [Fact]
    public void InPlaceMutation_MovesVerticesOnlyAfterInvalidate() {
        var data = new List<Item> { new("A", 8), new("B", 8), new("C", 8) };
        var cut = RenderChart<NTRadarSeries<Item>>(data, new() { ["ShowDataLabels"] = false, ["MaxValue"] = (decimal?)8m });
        var series = cut.FindComponent<NTRadarSeries<Item>>().Instance;
        using var surface = NewSurface();
        series.Render(CreateContext(cut.Instance, surface), Area);
        var top = Vertex(0, 3, 1f);
        var halfTop = Vertex(0, 3, 0.5f);

        data[0] = data[0] with { Value = 4 };
        surface.Canvas.Clear(SKColors.White);
        series.Render(CreateContext(cut.Instance, surface), Area);
        PixelAt(surface, top.X, top.Y).Should().Be(PaletteColors[0], "cached vertices are reused");

        series.Invalidate();
        surface.Canvas.Clear(SKColors.White);
        series.Render(CreateContext(cut.Instance, surface), Area);

        PixelAt(surface, halfTop.X, halfTop.Y).Should().Be(PaletteColors[0]);
        IsBackground(PixelAt(surface, top.X, top.Y)).Should().BeTrue();
        series.HitTest(halfTop, Area)!.Value.Index.Should().Be(0);
    }

    [Fact]
    public void InPlaceMutation_ChangesDataMaxOnlyAfterInvalidate() {
        var data = new List<Item> { new("A", 4), new("B", 4), new("C", 4) };
        var cut = RenderChart<NTRadarSeries<Item>>(data, new() { ["ShowDataLabels"] = false });
        var series = cut.FindComponent<NTRadarSeries<Item>>().Instance;
        using var surface = NewSurface();
        series.Render(CreateContext(cut.Instance, surface), Area);

        data[1] = data[1] with { Value = 8 };
        series.Invalidate();
        surface.Canvas.Clear(SKColors.White);
        series.Render(CreateContext(cut.Instance, surface), Area);

        var a = Vertex(0, 3, 0.5f);
        var b = Vertex(1, 3, 1f);
        PixelAt(surface, a.X, a.Y).Should().Be(PaletteColors[0], "max grew to 8 so the 4s sit at half radius");
        PixelAt(surface, b.X, b.Y).Should().Be(PaletteColors[0]);
    }

    [Fact]
    public void ResizedArea_RecomputesVerticesWithoutInvalidate() {
        var cut = RenderChart<NTRadarSeries<Item>>(Items(8, 8, 8), new() { ["ShowDataLabels"] = false });
        var series = cut.FindComponent<NTRadarSeries<Item>>().Instance;
        using var surface = NewSurface();
        series.Render(CreateContext(cut.Instance, surface), Area);
        var smaller = new SKRect(100, 50, 300, 250);

        surface.Canvas.Clear(SKColors.White);
        series.Render(CreateContext(cut.Instance, surface), smaller);

        var top = Polar(new SKPoint(200, 150), 100, -90);
        PixelAt(surface, top.X, top.Y).Should().Be(PaletteColors[0]);
        var oldTop = Vertex(0, 3, 1f);
        IsBackground(PixelAt(surface, oldTop.X, oldTop.Y)).Should().BeTrue();
    }

    [Fact]
    public void ReplacedDataReference_RebuildsGeometry() {
        var cut = RenderChart<NTRadarSeries<Item>>(Items(8, 8, 8), new() { ["ShowDataLabels"] = false });
        var series = cut.FindComponent<NTRadarSeries<Item>>().Instance;
        using var surface = NewSurface();
        series.Render(CreateContext(cut.Instance, surface), Area);

        cut.Render(p => p.Add(c => c.ChildContent, SeriesFragment<NTRadarSeries<Item>>(Items(2, 4, 4, 4), new() { ["ShowDataLabels"] = false })));
        surface.Canvas.Clear(SKColors.White);
        series.Render(CreateContext(cut.Instance, surface), Area);

        var v0 = Vertex(0, 4, 0.5f);
        var v1 = Vertex(1, 4, 1f);
        PixelAt(surface, v0.X, v0.Y).Should().Be(PaletteColors[0]);
        PixelAt(surface, v1.X, v1.Y).Should().Be(PaletteColors[0]);
    }

    [Fact]
    public void EntryAnimation_GrowsThePolygonFromTheCenter() {
        var cut = RenderChart<ClockedRadar>(Items(8, 8, 8), new() { ["ShowDataLabels"] = false, ["AnimationEnabled"] = true });
        var series = cut.FindComponent<ClockedRadar>().Instance;

        long Painted(TimeSpan age) {
            using var surface = NewSurface();
            series.SetAnimationAge(age);
            series.Render(CreateContext(cut.Instance, surface), Area);
            return Stats(surface, PaletteColors[0]).Count;
        }

        var start = Painted(TimeSpan.Zero);
        var mid = Painted(TimeSpan.FromMilliseconds(120));
        var end = Painted(TimeSpan.FromSeconds(10));

        start.Should().BeLessThan(end / 4);
        mid.Should().BeGreaterThan(start).And.BeLessThan(end);
        using var final = NewSurface();
        series.Render(CreateContext(cut.Instance, final), Area);
        var top = Vertex(0, 3, 1f);
        PixelAt(final, top.X, top.Y).Should().Be(PaletteColors[0]);
    }

    [Fact]
    public void Dispose_ReleasesNativeResources() {
        var cut = RenderChart<NTRadarSeries<Item>>(Items(8, 8, 8), new() { ["ShowDataLabels"] = true });
        var series = cut.FindComponent<NTRadarSeries<Item>>().Instance;
        using var surface = NewSurface();
        series.Render(CreateContext(cut.Instance, surface), Area);
        var resources = NativeResources(series);
        resources.Should().NotBeEmpty();

        series.Dispose();

        resources.Should().OnlyContain(r => r.Handle == IntPtr.Zero);
    }

    [Fact]
    public async Task PointerOverVertex_HoversThatPointThroughTheChart() {
        var data = Items(8, 8, 8);
        var cut = RenderChart<NTRadarSeries<Item>>(data, new() { ["ShowDataLabels"] = false });
        using var surface = NewSurface();
        await PaintAsync(cut, surface);
        // Bare chart: plot area (5,5)-(395,295) => center (200,150), radius 145. Vertex 1 sits at -90+120 degrees.
        var vertex = Polar(new SKPoint(200, 150), 145, 30);

        await HoverAsync(cut, surface, vertex.X + 3, vertex.Y);

        cut.Instance.HoveredPointIndex.Should().Be(1);
        cut.Instance.HoveredDataPoint.Should().BeSameAs(data[1]);
    }
}
