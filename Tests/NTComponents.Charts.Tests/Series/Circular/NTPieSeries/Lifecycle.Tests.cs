using NTComponents.Charts.Core;
using SkiaSharp;

namespace NTComponents.Charts.Tests.Series.Circular;

/// <summary>Entry animation, cache invalidation and native resource disposal of the pie series.</summary>
public class Lifecycle_Tests : CircularChartTestBase {
    private static readonly SKRect Area = new(0, 0, 400, 300);

    private static long Painted(SKSurface surface) => CountNonBackground(surface);

    private (ClockedPie Series, NTChart<Item> Chart) CreateAnimated(Item[] data) {
        var cut = RenderChart<ClockedPie>(data, new() {
            ["AnimationEnabled"] = true,
            ["AnimationDuration"] = NTMotionDuration.Ms500,
            ["ShowDataLabels"] = false
        });
        return (cut.FindComponent<ClockedPie>().Instance, cut.Instance);
    }

    private static long RenderAtAge(ClockedPie series, NTChart<Item> chart, TimeSpan age) {
        using var surface = NewSurface();
        series.SetAnimationAge(age);
        series.Render(CreateContext(chart, surface), Area);
        return Painted(surface);
    }

    [Fact]
    public void EntryAnimation_RevealsMoreOfThePieAsTimeAdvancesAndEndsComplete() {
        var (series, chart) = CreateAnimated(Items(1, 1, 2));

        var atStart = RenderAtAge(series, chart, TimeSpan.Zero);
        var midway = RenderAtAge(series, chart, TimeSpan.FromMilliseconds(120));
        var complete = RenderAtAge(series, chart, TimeSpan.FromSeconds(10));

        var full = MathF.PI * 150 * 150;
        complete.Should().BeInRange((long)(full * 0.99), (long)(full * 1.01));
        atStart.Should().BeLessThan(complete / 20);
        midway.Should().BeGreaterThan(atStart);
        midway.Should().BeLessThan(complete);
    }

    [Fact]
    public void EntryAnimation_RequestsFramesUntilFinished() {
        var (series, chart) = CreateAnimated(Items(1, 1, 2));
        using var surface = NewSurface();
        series.SetAnimationAge(TimeSpan.FromMilliseconds(50));
        series.Render(CreateContext(chart, surface), Area);
        RequiresAnimationFrame(series).Should().BeTrue();

        series.SetAnimationAge(TimeSpan.FromSeconds(10));

        RequiresAnimationFrame(series).Should().BeFalse();
    }

    [Fact]
    public void EntryExplosion_PushesSlicesOutwardWhileAnimatingAndSettlesAtRest() {
        var (series, chart) = CreateAnimated(Items(1, 1, 2));
        using var animating = NewSurface();
        series.SetAnimationAge(TimeSpan.FromMilliseconds(100));
        series.Render(CreateContext(chart, animating), Area);
        var outside = Polar(new SKPoint(200, 150), 152, -45);

        using var settled = NewSurface();
        series.SetAnimationAge(TimeSpan.FromSeconds(10));
        series.Render(CreateContext(chart, settled), Area);

        IsBackground(PixelAt(settled, outside.X, outside.Y)).Should().BeTrue("at rest slices sit inside the 150px radius");
        Stats(animating, PaletteColors[0]).Bounds.Right.Should().BeGreaterThan(Stats(settled, PaletteColors[0]).Bounds.Right + 1f, "entering slices are displaced outward before settling");
    }

    [Fact]
    public void DisabledEntryExplosion_KeepsSlicesInPlaceWhileAnimating() {
        var cut = RenderChart<ClockedPie>(Items(1, 1, 2), new() {
            ["AnimationEnabled"] = true,
            ["ExplosionOnEntry"] = false,
            ["ShowDataLabels"] = false
        });
        var series = cut.FindComponent<ClockedPie>().Instance;
        using var surface = NewSurface();
        series.SetAnimationAge(TimeSpan.FromMilliseconds(100));

        series.Render(CreateContext(cut.Instance, surface), Area);

        Stats(surface, PaletteColors[0]).Count.Should().BeGreaterThan(0);
        Stats(surface, PaletteColors[0]).Bounds.Right.Should().BeLessThanOrEqualTo(350.5f);
    }

    [Fact]
    public void DisabledAnimation_PaintsTheFullPieImmediatelyAndNeverRequestsFrames() {
        var cut = RenderChart<NTPieSeries<Item>>(Items(1, 1, 2), new() { ["ShowDataLabels"] = false });
        var series = cut.FindComponent<NTPieSeries<Item>>().Instance;
        using var surface = NewSurface();

        series.Render(CreateContext(cut.Instance, surface), Area);

        var full = MathF.PI * 150 * 150;
        Painted(surface).Should().BeInRange((long)(full * 0.99), (long)(full * 1.01));
        RequiresAnimationFrame(series).Should().BeFalse();
    }

    [Fact]
    public void DataReplacement_RestartsTheEntryAnimation() {
        var (series, chart) = CreateAnimated(Items(1, 1, 2));
        RenderAtAge(series, chart, TimeSpan.FromSeconds(10)).Should().BeGreaterThan(60000);
        RequiresAnimationFrame(series).Should().BeFalse();

        series.Data = Items(2, 3);
        series.Invalidate();

        RequiresAnimationFrame(series).Should().BeTrue();
    }

    [Fact]
    public void Invalidate_PicksUpInPlaceDataMutationAndChangesPixels() {
        var data = new List<Item> { new("A", 1), new("B", 1), new("C", 2) };
        var cut = RenderChart<NTPieSeries<Item>>(data, new() { ["ShowDataLabels"] = false });
        var series = cut.FindComponent<NTPieSeries<Item>>().Instance;
        using var surface = NewSurface();
        series.Render(CreateContext(cut.Instance, surface), Area);
        var before = Snapshot(surface);

        data[0] = data[0] with { Value = 5 };
        surface.Canvas.Clear(SKColors.White);
        series.Render(CreateContext(cut.Instance, surface), Area);
        Snapshot(surface).Should().Equal(before, "geometry is cached until the series is invalidated");

        series.Invalidate();
        surface.Canvas.Clear(SKColors.White);
        series.Render(CreateContext(cut.Instance, surface), Area);

        Snapshot(surface).Should().NotEqual(before);
        Stats(surface, PaletteColors[0]).Count.Should().BeGreaterThan(Stats(surface, PaletteColors[2]).Count);
    }

    [Fact]
    public void Dispose_ReleasesEveryNativeResourceAndIsIdempotent() {
        var cut = RenderChart<NTPieSeries<Item>>(Items(1, 1), new() { ["ShowDataLabels"] = true });
        var series = cut.FindComponent<NTPieSeries<Item>>().Instance;
        using var surface = NewSurface();
        series.Render(CreateContext(cut.Instance, surface), Area);
        var resources = NativeResources(series);
        resources.Should().NotBeEmpty("rendering with labels allocates paints, fonts and a path builder");
        resources.Should().OnlyContain(r => r.Handle != IntPtr.Zero);

        series.Dispose();
        var secondDispose = () => series.Dispose();

        resources.Should().OnlyContain(r => r.Handle == IntPtr.Zero);
        secondDispose.Should().NotThrow();
        NativeResources(series).Should().BeEmpty();
    }

    [Fact]
    public void Dispose_UnregistersTheSeriesFromTheChart() {
        var cut = RenderChart<NTPieSeries<Item>>(Items(1, 1));
        var series = cut.FindComponent<NTPieSeries<Item>>().Instance;
        cut.Instance.Series.Should().ContainSingle();

        series.Dispose();

        cut.Instance.Series.Should().BeEmpty();
    }
}
