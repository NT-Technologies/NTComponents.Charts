using Microsoft.AspNetCore.Components;
using NTComponents.Charts.Core;
using SkiaSharp;


namespace NTComponents.Charts.Tests.ChartCore;

public class NTChart_Lifecycle_Tests : ChartTestBase {
    private static RenderFragment NoAxes() => Combine(XAxis<double>(p => p.X, Args(("Visible", false))), YAxis(Args(("Visible", false))));

    // ---------- frame determinism and cache invalidation ----------

    [Fact]
    public async Task RepeatedFramesWithoutChanges_ProduceIdenticalPixels() {
        var chart = RenderChart(Combine(XAxis<double>(p => p.X), YAxis(), Legend(), Line()), p => p.Add(c => c.TitleOptions, new NTTitleOptions("Stable")));
        // The bottom legend centers itself under the plot area of the previous frame, so the very first frame is a warm-up.
        using var first = await Paint(chart);
        using var warm = await Paint(chart);
        using var firstBitmap = Bitmap(warm);

        using var second = await Paint(chart);
        using var third = await Paint(chart);

        Bitmap(second).Bytes.Should().Equal(firstBitmap.Bytes);
        Bitmap(third).Bytes.Should().Equal(firstBitmap.Bytes);
    }

    [Fact]
    public async Task PaintingOntoTheSameSurfaceTwice_DoesNotAccumulateOverdraw() {
        var chart = RenderChart(Combine(XAxis<double>(p => p.X), YAxis(), Line()));
        using var surface = await Paint(chart);
        var once = Bitmap(surface).Bytes;

        await Paint(chart, surface: surface);

        Bitmap(surface).Bytes.Should().Equal(once, "every frame clears the canvas first");
    }

    [Fact]
    public async Task DensityChange_RescalesMarginsOnTheNextFrame() {
        var chart = RenderChart(Combine(NoAxes(), Line()), p => p.Add(c => c.Margin, ChartMargin.All(10)));
        using var surface = await Paint(chart);
        PlotArea(chart).Left.Should().BeApproximately(10, 0.01f);

        await chart.InvokeAsync(() => chart.Instance.OnNativeViewportChanged(true, 2f));
        await Paint(chart, surface: surface);

        chart.Instance.Density.Should().Be(2f);
        PlotArea(chart).Left.Should().BeApproximately(20, 0.01f);
    }

    [Theory]
    [InlineData(float.NaN)]
    [InlineData(0f)]
    [InlineData(-1f)]
    public async Task InvalidDensityFromTheViewport_IsIgnored(float density) {
        var chart = RenderChart(Combine(NoAxes(), Line()));

        await chart.InvokeAsync(() => chart.Instance.OnNativeViewportChanged(true, density));

        chart.Instance.Density.Should().Be(1f);
    }

    [Fact]
    public async Task HidingASeries_RebuildsAxesForTheRemainingData() {
        var big = new Pt[] { new("a", 0, 1_000_000m), new("b", 1, 9_000_000m) };
        Pt[] small = [new("a", 0, 1m), new("b", 1, 9m)];
        RenderFragment Content(bool bigVisible) => Combine(YAxis(), Line(small), Line(big, extra: Args(("Visible", bigVisible))));
        var chart = RenderChart(Content(true));
        using var surface = await Paint(chart);
        var withBig = PlotArea(chart);

        chart.Render(p => p.Add(c => c.ChildContent, Content(false)));
        await Paint(chart, surface: surface);
        var withoutBig = PlotArea(chart);

        withoutBig.Left.Should().BeLessThan(withBig.Left, "the Y gutter shrinks once large labels disappear");

        chart.Render(p => p.Add(c => c.ChildContent, Content(true)));
        await Paint(chart, surface: surface);
        PlotArea(chart).Left.Should().BeApproximately(withBig.Left, 0.01f);
    }

    [Fact]
    public async Task RangePaddingChange_RescalesPointsOnTheNextFrame() {
        var chart = RenderChart(Combine(NoAxes(), Line()));
        using var surface = await Paint(chart);
        var before = chart.Instance.ScaleX(0, PlotArea(chart));

        chart.Render(p => p.Add(c => c.RangePadding, 0.5));
        await Paint(chart, surface: surface);

        chart.Instance.ScaleX(0, PlotArea(chart)).Should().BeGreaterThan(before + 20);
    }

    [Fact]
    public async Task ParameterRerender_RedrawsWithTheNewBackground() {
        var chart = RenderChart(Combine(NoAxes(), Line()));
        using var surface = await Paint(chart);

        chart.Render(p => p.Add(c => c.BackgroundColor, NTColor.Secondary));
        await Paint(chart, surface: surface);
        using var bitmap = Bitmap(surface);

        bitmap.GetPixel(2, 2).Should().Be(new SKColor(0, 0, 255));
    }

    [Fact]
    public async Task ThemeChange_RecolorsSeriesPaletteOnTheNextFrame() {
        var chart = RenderChart(Combine(NoAxes(), Bar()));
        using var surface = await Paint(chart);
        using var before = Bitmap(surface);
        CountColor(before, SKColors.Red, 10).Should().BeGreaterThan(100);

        ThemeColors["Primary"] = "#0000ff";
        await chart.InvokeAsync(chart.Instance.OnThemeChanged);
        await Paint(chart, surface: surface);
        using var after = Bitmap(surface);

        CountColor(after, SKColors.Red, 10).Should().Be(0);
        CountColor(after, new SKColor(0, 0, 255), 10).Should().BeGreaterThan(100);
    }

    [Fact]
    public async Task ThemeColors_AreResolvedFromCssAndFallBackWhenMissing() {
        ThemeColors.Remove("Secondary");
        ThemeColors["Primary"] = "not-a-color";
        var chart = RenderChart(Combine(NoAxes(), Line()));
        await chart.InvokeAsync(chart.Instance.OnThemeChanged);

        chart.Instance.GetThemeColor(NTColor.Surface).Should().Be(SKColors.White);
        chart.Instance.GetThemeColor(NTColor.Primary).Should().Be(SKColors.RoyalBlue, "an unparsable value falls back to the built-in primary");
        chart.Instance.GetThemeColor(NTColor.Secondary).Should().Be(SKColors.Gray);
        chart.Instance.GetThemeColor(NTColor.Transparent).Should().Be(SKColors.Transparent);
    }

    // ---------- rendering surface ----------

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void HardwareAccelerationParameter_SelectsTheSkiaViewType(bool hardware) {
        var chart = RenderChart(Combine(NoAxes(), Line()), p => p.Add(c => c.EnableHardwareAcceleration, hardware));

        CreatedViews.Should().Contain(hardware ? "SKGLView" : "SKCanvasView");
        CreatedViews.Should().NotContain(hardware ? "SKCanvasView" : "SKGLView");
    }

    [Fact]
    public void ChartRoot_ForwardsAdditionalAttributesAndKeepsTheCanvasHost() {
        var chart = Render<NTChart<Pt>>(p => p.AddUnmatched("data-chart", "revenue").AddUnmatched("class", "extra"));

        chart.Find(".nt-chart").GetAttribute("data-chart").Should().Be("revenue");
        chart.Find(".nt-chart").ClassList.Should().Contain(["nt-chart", "extra"]);
        chart.FindAll(".nt-chart-canvas-host").Should().ContainSingle();
    }

    // ---------- disposal ----------

    [Fact]
    public async Task DisposedChart_StopsPaintingAndIgnoresLateEvents() {
        var log = new List<string>();
        var chart = RenderChart(Combine(NoAxes(), Recording("a", log)));
        using var surface = await Paint(chart);
        log.Clear();

        await chart.InvokeAsync(() => chart.Instance.DisposeAsync().AsTask());
        await Paint(chart, surface: surface);
        await Move(chart, 300, 150);
        await chart.InvokeAsync(chart.Instance.OnThemeChanged);
        var viewport = () => chart.InvokeAsync(() => chart.Instance.OnNativeViewportChanged(true, 3f));

        log.Should().BeEmpty("a disposed chart no longer renders its series");
        await viewport.Should().NotThrowAsync();
        chart.Instance.Density.Should().Be(1f);
    }

    [Fact]
    public async Task DisposingTheChart_ReleasesItsFontsAndDisposesChildrenOnce() {
        var chart = RenderChart(Combine(NoAxes(), Legend(), Tooltip(), Line()), p => p.Add(c => c.TitleOptions, new NTTitleOptions("Bye")));
        using var surface = await Paint(chart);
        var fonts = new[] { chart.Instance.DefaultFont, chart.Instance.RegularFont };
        fonts.Should().OnlyContain(f => f.Handle != IntPtr.Zero);

        await chart.InvokeAsync(() => chart.Instance.DisposeAsync().AsTask());
        var second = () => chart.InvokeAsync(() => chart.Instance.DisposeAsync().AsTask());

        fonts.Should().OnlyContain(f => f.Handle == IntPtr.Zero);
        await second.Should().NotThrowAsync("disposal must be idempotent");
    }

    [Fact]
    public async Task UnmountingTheChart_DisposesSeriesAndLegendWithoutErrors() {
        var chart = RenderChart(Combine(NoAxes(), Legend(), Tooltip(), Line()));
        var instance = chart.Instance;

        var act = () => DisposeComponentsAsync();

        await act.Should().NotThrowAsync();
        instance.Series.Should().BeEmpty();
        instance.Legend.Should().BeNull();
    }
}
