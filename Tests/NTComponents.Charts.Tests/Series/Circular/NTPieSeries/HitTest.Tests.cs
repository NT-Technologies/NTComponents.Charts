using SkiaSharp;

namespace NTComponents.Charts.Tests.Series.Circular;

public class HitTest_Tests : CircularChartTestBase {
    private static readonly SKRect Area = new(0, 0, 400, 300);
    private static readonly SKPoint Center = new(200, 150);

    private (NTPieSeries<Item> Series, Item[] Data) RenderedPie(Item[] data, Dictionary<string, object>? attributes = null) {
        var merged = new Dictionary<string, object> { ["ShowDataLabels"] = false };
        foreach (var attribute in attributes ?? []) {
            merged[attribute.Key] = attribute.Value;
        }
        var cut = RenderChart<NTPieSeries<Item>>(data, merged);
        using var surface = NewSurface();
        var series = cut.FindComponent<NTPieSeries<Item>>().Instance;
        series.Render(CreateContext(cut.Instance, surface), Area);
        return (series, data);
    }

    private static int? IndexAt(NTPieSeries<Item> series, float radius, float degrees) {
        var point = Polar(Center, radius, degrees);
        return series.HitTest(point, Area)?.Index;
    }

    [Fact]
    public void PointInsideEachSlice_ReturnsThatSliceIndexAndData() {
        var (series, data) = RenderedPie(Items(1, 1, 2));

        var hit = series.HitTest(Polar(Center, 100, 45), Area);

        hit.Should().NotBeNull();
        hit!.Value.Index.Should().Be(1);
        hit.Value.Data.Should().BeSameAs(data[1]);
        IndexAt(series, 100, -45).Should().Be(0);
        IndexAt(series, 100, 180).Should().Be(2);
    }

    [Fact]
    public void RadiusBoundary_IsInclusiveAtTheOuterEdgeAndMissesJustBeyondIt() {
        var (series, _) = RenderedPie(Items(1, 1));

        IndexAt(series, 149.5f, 135).Should().Be(1);
        IndexAt(series, 151f, 135).Should().BeNull();
    }

    [Fact]
    public void PointAtTheCenter_HitsTheSliceContainingAngleZero() {
        var (series, _) = RenderedPie(Items(1, 1));

        series.HitTest(Center, Area)!.Value.Index.Should().Be(0, "atan2(0,0) is angle 0 which lies in the first half turn");
    }

    [Fact]
    public void SliceStartAngle_IsInclusiveAndEndAngleExclusive() {
        var (series, _) = RenderedPie(Items(1, 1, 1, 1));

        IndexAt(series, 100, 0).Should().Be(1, "exactly three o'clock starts slice 1");
        IndexAt(series, 100, 89.5f).Should().Be(1);
        IndexAt(series, 100, 90f).Should().Be(2);
        IndexAt(series, 100, 180f).Should().Be(3);
        IndexAt(series, 100, -0.5f).Should().Be(0);
    }

    [Fact]
    public void ExactlyTwelveOClock_BelongsToTheFirstSlice() {
        var (series, _) = RenderedPie(Items(1, 1, 1, 1));

        series.HitTest(new SKPoint(Center.X, Center.Y - 100), Area)!.Value.Index.Should().Be(0);
    }

    [Fact]
    public void JustCounterClockwiseOfTwelveOClock_BelongsToTheLastSlice() {
        var (series, _) = RenderedPie(Items(1, 1, 1, 1));

        series.HitTest(new SKPoint(Center.X - 1, Center.Y - 100), Area)!.Value.Index.Should().Be(3);
    }

    [Fact]
    public void Donut_MissesTheHoleAndHitsTheRing() {
        var (series, _) = RenderedPie(Items(1, 1), new() { ["InnerRadius"] = 60f });

        IndexAt(series, 30, 45).Should().BeNull();
        IndexAt(series, 59f, 45).Should().BeNull();
        IndexAt(series, 61f, 135).Should().Be(1);
        series.HitTest(Center, Area).Should().BeNull();
    }

    [Fact]
    public void HiddenSlice_CannotBeHitAndRemainingSlicesUseTheirRenormalizedAngles() {
        var cut = RenderChart<NTPieSeries<Item>>(Items(1, 1, 2), new() { ["ShowDataLabels"] = false });
        var series = cut.FindComponent<NTPieSeries<Item>>().Instance;
        using var surface = NewSurface();
        ToggleLegend(series, 2);
        series.Render(CreateContext(cut.Instance, surface), Area);

        IndexAt(series, 100, 45).Should().Be(0);
        IndexAt(series, 100, 135).Should().Be(1, "slice 1 now owns the former region of the hidden slice");
        IndexAt(series, 100, 200).Should().Be(1);
        for (var degrees = -85f; degrees < 270f; degrees += 10f) {
            IndexAt(series, 100, degrees).Should().NotBe(2, $"angle {degrees}");
        }
    }

    [Fact]
    public void ZeroValueSlice_IsNeverHit() {
        var (series, _) = RenderedPie(Items(0, 1, 1));

        for (var degrees = -85f; degrees < 270f; degrees += 15f) {
            IndexAt(series, 100, degrees).Should().NotBe(0, $"angle {degrees}");
        }
    }

    [Fact]
    public void BeforeFirstRender_NothingIsHit() {
        var cut = RenderChart<NTPieSeries<Item>>(Items(1, 1));
        var series = cut.FindComponent<NTPieSeries<Item>>().Instance;

        series.HitTest(Polar(Center, 50, 45), Area).Should().BeNull();
    }

    [Fact]
    public void AfterDataReplacement_StaleSlicesAreNotHitUntilTheNextRender() {
        var cut = RenderChart<NTPieSeries<Item>>(Items(1, 1), new() { ["ShowDataLabels"] = false });
        var series = cut.FindComponent<NTPieSeries<Item>>().Instance;
        using var surface = NewSurface();
        series.Render(CreateContext(cut.Instance, surface), Area);
        IndexAt(series, 100, 45).Should().Be(0);

        cut.Render(p => p.Add(c => c.ChildContent, SeriesFragment<NTPieSeries<Item>>([new Item("Only", 4)], new() { ["ShowDataLabels"] = false })));

        IndexAt(series, 100, 45).Should().BeNull();
        series.Render(CreateContext(cut.Instance, surface), Area);
        series.HitTest(Polar(Center, 100, 45), Area)!.Value.Data!.Name.Should().Be("Only");
    }
}
