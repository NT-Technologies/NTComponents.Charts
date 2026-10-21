using Microsoft.AspNetCore.Components;
using NTComponents.Charts.Core;
using SkiaSharp;

namespace NTComponents.Charts.Tests.ChartCore;

public class NTTooltip_Tests : ChartTestBase {
    private static readonly SKColor Magenta = new(255, 0, 255);
    private static readonly SKColor Blue = new(0, 0, 255);
    private static readonly SKColor Green = new(0, 170, 0);

    private static RenderFragment NoAxes() => Combine(XAxis<double>(p => p.X, Args(("Visible", false))), YAxis(Args(("Visible", false))));

    private static (int Index, Pt? Data)? Hit(SKPoint _, SKRect __) => (1, Sample[1]);

    private IRenderedComponent<NTChart<Pt>> TooltipChart(IDictionary<string, object>? tooltip = null, IDictionary<string, object>? series = null, Action<ComponentParameterCollectionBuilder<NTChart<Pt>>>? configure = null) =>
        RenderChart(Combine(NoAxes(), Tooltip(tooltip), Recording("a", hit: Hit, extra: series ?? Args(("Title", "Revenue")))), configure);

    private async Task<SKBitmap> HoverAndPaint(IRenderedComponent<NTChart<Pt>> chart, double x, double y) {
        using var surface = await Paint(chart);
        await Move(chart, x, y);
        await Paint(chart, surface: surface);
        return Bitmap(surface);
    }

    // ---------- content ----------

    [Fact]
    public async Task Content_ComesFromTheHoveredSeriesWithHeaderValueAndSeriesColor() {
        TooltipInfo? seen = null;
        NTSeriesTooltipContext<Pt>? context = null;
        Func<NTSeriesTooltipContext<Pt>, TooltipInfo> selector = c => {
            context = c;
            seen = c.DefaultInfo;
            return c.DefaultInfo;
        };
        var chart = TooltipChart(series: Args(("Title", "Revenue"), ("TooltipInfoSelector", selector)));

        using var bitmap = await HoverAndPaint(chart, 300, 150);

        seen.Should().NotBeNull();
        seen!.Header.Should().Be("1");
        seen.Lines.Should().ContainSingle();
        seen.Lines[0].Label.Should().Be("Revenue");
        seen.Lines[0].Value.Should().Be("40");
        seen.Lines[0].Color.Should().Be(SKColors.Red);
        context!.Series.Should().BeSameAs(chart.Instance.Series.Single());
        context.Data.Should().Be(Sample[1]);
        context.PointIndex.Should().Be(1);
        context.SegmentLabel.Should().BeNull();
    }

    [Fact]
    public async Task Content_SeriesWithoutTitleUsesTheGenericLabel() {
        TooltipInfo? seen = null;
        Func<NTSeriesTooltipContext<Pt>, TooltipInfo> selector = c => seen = c.DefaultInfo;
        var chart = TooltipChart(series: Args(("TooltipInfoSelector", selector)));

        using var bitmap = await HoverAndPaint(chart, 300, 150);

        seen!.Lines[0].Label.Should().Be("Series");
    }

    [Fact]
    public async Task Content_DataLabelFormatShapesTheValue() {
        TooltipInfo? seen = null;
        Func<NTSeriesTooltipContext<Pt>, TooltipInfo> selector = c => seen = c.DefaultInfo;
        var chart = TooltipChart(series: Args(("DataLabelFormat", "{0:C0} USD"), ("TooltipInfoSelector", selector)));

        using var bitmap = await HoverAndPaint(chart, 300, 150);

        seen!.Lines[0].Value.Should().Be(string.Format("{0:C0} USD", 40m));
    }

    [Fact]
    public async Task Content_HeaderUsesTheAxisFormatForDateValues() {
        TooltipInfo? seen = null;
        Func<NTSeriesTooltipContext<Pt>, TooltipInfo> selector = c => seen = c.DefaultInfo;
        var feb = new DateTime(2024, 2, 20);
        var data = new Pt[] { new("a", 0, 5m, new DateTime(2024, 1, 15)), new("b", 1, 7m, feb), new("c", 2, 3m, new DateTime(2024, 3, 25)) };
        var chart = RenderChart(Combine(XAxis<DateTime>(p => p.Date, Args(("Visible", false))), YAxis(Args(("Visible", false))), Tooltip(), DateRecording(data, selector)));

        using var bitmap = await HoverAndPaint(chart, 300, 150);

        seen!.Header.Should().Be(feb.ToString("MM/dd/yyyy"));
    }

    private static RenderFragment DateRecording(Pt[] data, Func<NTSeriesTooltipContext<Pt>, TooltipInfo> selector) => builder => {
        builder.OpenComponent<RecordingSeries>(0);
        builder.AddAttribute(1, "Data", data);
        builder.AddAttribute(2, "XValue", (Func<Pt, object>)(p => p.Date));
        builder.AddAttribute(3, "YValueSelector", (Func<Pt, decimal>)(p => p.Y));
        builder.AddAttribute(4, "AnimationEnabled", false);
        builder.AddAttribute(5, "Hit", (Func<SKPoint, SKRect, (int Index, Pt? Data)?>)((_, _) => (1, data[1])));
        builder.AddAttribute(6, "TooltipInfoSelector", selector);
        builder.CloseComponent();
    };

    // ---------- visibility ----------

    [Fact]
    public async Task Tooltip_IsDrawnOnlyWhileAPointIsHovered() {
        var chart = TooltipChart();
        using var surface = await Paint(chart);
        using var idle = Bitmap(surface);
        BoundsOf(idle, Magenta).Should().BeNull();

        await Move(chart, 300, 150);
        await Paint(chart, surface: surface);
        using var hovered = Bitmap(surface);
        BoundsOf(hovered, Magenta).Should().NotBeNull();

        await Fire(chart, h => h.MouseLeave());
        await Paint(chart, surface: surface);
        using var left = Bitmap(surface);
        BoundsOf(left, Magenta).Should().BeNull();
    }

    [Fact]
    public async Task DisabledTooltip_DrawsNothing() {
        var chart = TooltipChart(Args(("Enabled", false)));

        using var bitmap = await HoverAndPaint(chart, 300, 150);

        BoundsOf(bitmap, Magenta).Should().BeNull();
        chart.Instance.HoveredSeries.Should().NotBeNull("hover itself is independent of the tooltip");
    }

    [Fact]
    public async Task SelectorReturningNoLines_SuppressesTheTooltip() {
        Func<NTSeriesTooltipContext<Pt>, TooltipInfo> selector = _ => new TooltipInfo { Header = "x", Lines = [] };
        var chart = TooltipChart(series: Args(("TooltipInfoSelector", selector)));

        using var bitmap = await HoverAndPaint(chart, 300, 150);

        BoundsOf(bitmap, Magenta).Should().BeNull();
    }

    [Fact]
    public async Task SelectorContent_ReplacesTheDefaultPayload() {
        var custom = new TooltipInfo { Header = null, Lines = [new TooltipLine { Label = "A", Value = "1", Color = SKColors.Lime }, new TooltipLine { Label = "B", Value = "2", Color = SKColors.Cyan }] };
        Func<NTSeriesTooltipContext<Pt>, TooltipInfo> selector = _ => custom;
        var chart = TooltipChart(series: Args(("TooltipInfoSelector", selector)));

        using var bitmap = await HoverAndPaint(chart, 300, 150);
        var bounds = BoundsOf(bitmap, Magenta)!.Value;

        // Two 20px lines, no header, 12px block padding on each side.
        (bounds.Bottom - bounds.Top).Should().BeCloseTo(64, 2);
        BoundsOf(bitmap, SKColors.Lime, 10, bounds).Should().NotBeNull("each line shows its color swatch");
        BoundsOf(bitmap, SKColors.Cyan, 10, bounds).Should().NotBeNull();
    }

    // ---------- positioning ----------

    [Fact]
    public async Task Position_SitsAboveAndRightOfThePointerBySixteenPixels() {
        var chart = TooltipChart();

        using var bitmap = await HoverAndPaint(chart, 200, 150);
        var bounds = BoundsOf(bitmap, Magenta)!.Value;

        bounds.Left.Should().BeCloseTo(216, 2);
        bounds.Bottom.Should().BeCloseTo(134, 2);
        // 12 padding + 20 header + 4 gap + 20 line + 12 padding.
        (bounds.Bottom - bounds.Top).Should().BeCloseTo(68, 2);
    }

    [Fact]
    public async Task Position_FlipsToTheLeftOfThePointerNearTheRightEdge() {
        var chart = TooltipChart();

        using var bitmap = await HoverAndPaint(chart, 600, 150);
        var bounds = BoundsOf(bitmap, Magenta)!.Value;

        bounds.Right.Should().BeCloseTo(584, 2);
        bounds.Right.Should().BeLessThan(Width - 8);
    }

    [Fact]
    public async Task Position_FlipsBelowThePointerNearTheTopEdge() {
        var chart = TooltipChart();

        using var bitmap = await HoverAndPaint(chart, 200, 40);
        var bounds = BoundsOf(bitmap, Magenta)!.Value;

        bounds.Top.Should().BeCloseTo(56, 2);
    }

    [Fact]
    public async Task Position_ScalesWithDensityAndPointerTracksTheMouse() {
        var chart = TooltipChart();
        await chart.InvokeAsync(() => chart.Instance.OnNativeViewportChanged(true, 2f));

        using var bitmap = await HoverAndPaint(chart, 100, 100);
        var bounds = BoundsOf(bitmap, Magenta)!.Value;

        // Pointer is (200,200) device pixels; offset is 16 * density.
        bounds.Left.Should().BeCloseTo(232, 3);
        bounds.Bottom.Should().BeCloseTo(168, 3);
        (bounds.Bottom - bounds.Top).Should().BeCloseTo(136, 4);
    }

    [Fact]
    public async Task Position_FollowsThePointerBetweenFrames() {
        var chart = TooltipChart();
        using var surface = await Paint(chart);
        await Move(chart, 200, 150);
        await Paint(chart, surface: surface);
        using var first = Bitmap(surface);

        await Move(chart, 260, 170);
        await Paint(chart, surface: surface);
        using var second = Bitmap(surface);

        var a = BoundsOf(first, Magenta)!.Value;
        var b = BoundsOf(second, Magenta)!.Value;
        (b.Left - a.Left).Should().BeCloseTo(60, 2);
        (b.Bottom - a.Bottom).Should().BeCloseTo(20, 2);
    }

    // ---------- colors ----------

    [Fact]
    public async Task Background_DefaultsToTheChartTooltipColor() {
        var chart = TooltipChart();

        using var bitmap = await HoverAndPaint(chart, 200, 150);

        BoundsOf(bitmap, Magenta).Should().NotBeNull();
    }

    [Fact]
    public async Task Background_ChartLevelParameterOverridesTheTheme() {
        var chart = TooltipChart(configure: p => p.Add(c => c.TooltipBackgroundColor, NTColor.Secondary));

        using var bitmap = await HoverAndPaint(chart, 200, 150);

        BoundsOf(bitmap, Blue).Should().NotBeNull();
        BoundsOf(bitmap, Magenta).Should().BeNull();
    }

    [Fact]
    public async Task Background_SeriesColorBeatsChartAndTooltipColorBeatsSeries() {
        var seriesLevel = TooltipChart(series: Args(("TooltipBackgroundColor", (NTColor?)NTColor.Secondary)));
        using var seriesBitmap = await HoverAndPaint(seriesLevel, 200, 150);
        BoundsOf(seriesBitmap, Blue).Should().NotBeNull();
        BoundsOf(seriesBitmap, Magenta).Should().BeNull();

        var tooltipLevel = TooltipChart(Args(("BackgroundColor", (NTColor?)NTColor.Tertiary)), Args(("TooltipBackgroundColor", (NTColor?)NTColor.Secondary)));
        using var tooltipBitmap = await HoverAndPaint(tooltipLevel, 200, 150);
        BoundsOf(tooltipBitmap, Green, 8, new SKRectI(150, 50, 500, 200)).Should().NotBeNull();
        BoundsOf(tooltipBitmap, Blue).Should().BeNull();
    }

    [Fact]
    public async Task ShowSeriesLabels_False_ShrinksTheTooltipWidth() {
        var withLabels = TooltipChart();
        var withoutLabels = TooltipChart(Args(("ShowSeriesLabels", false)));

        using var a = await HoverAndPaint(withLabels, 200, 150);
        using var b = await HoverAndPaint(withoutLabels, 200, 150);

        var wide = BoundsOf(a, Magenta)!.Value;
        var narrow = BoundsOf(b, Magenta)!.Value;
        (wide.Right - wide.Left).Should().BeGreaterThan((narrow.Right - narrow.Left));
    }

    // ---------- Material 3 rich tooltip ----------

    private static readonly SKColor LabelInk = new(0x11, 0x11, 0x11);

    [Fact]
    public async Task Text_IsOpaqueOnSurfaceVariantNotFadedToAlpha() {
        var chart = TooltipChart();

        using var bitmap = await HoverAndPaint(chart, 200, 150);

        CountColor(bitmap, LabelInk, 8).Should().BeGreaterThan(20, "header, label and value are drawn at full alpha in OnSurfaceVariant");
    }

    [Fact]
    public async Task Container_HasNoBorderOrHeaderDivider() {
        var chart = TooltipChart();

        using var bitmap = await HoverAndPaint(chart, 200, 150);
        var bounds = BoundsOf(bitmap, Magenta)!.Value;

        for (var y = bounds.Top + 8; y < bounds.Bottom - 8; y++) {
            IsColor(bitmap.GetPixel(bounds.Left + 4, y), Magenta).Should().BeTrue($"row {y} left of the text must be plain container color");
        }
        IsColor(bitmap.GetPixel(bounds.Left, (bounds.Top + bounds.Bottom) / 2), Magenta).Should().BeTrue("there is no outline stroke on the edge");
    }

    [Fact]
    public async Task Container_UsesTheMediumCornerRadius() {
        var chart = TooltipChart();

        using var bitmap = await HoverAndPaint(chart, 200, 150);
        var bounds = BoundsOf(bitmap, Magenta)!.Value;

        IsColor(bitmap.GetPixel(bounds.Left + 1, bounds.Top + 1), Magenta).Should().BeFalse("a 12px radius cuts the corner");
        IsColor(bitmap.GetPixel(bounds.Left + 6, bounds.Top + 6), Magenta).Should().BeTrue();
    }

    [Fact]
    public async Task Container_CastsALevelTwoShadowBelowItself() {
        var chart = TooltipChart();

        using var bitmap = await HoverAndPaint(chart, 200, 150);
        var bounds = BoundsOf(bitmap, Magenta)!.Value;
        var x = (bounds.Left + bounds.Right) / 2;

        bitmap.GetPixel(x, bounds.Bottom + 3).Red.Should().BeLessThan(250, "the ambient shadow darkens the surface under the tooltip");
        bitmap.GetPixel(x, bounds.Bottom + 40).Should().Be(SKColors.White, "the shadow is local to the tooltip");
    }

    [Fact]
    public async Task Width_IsCappedAtThreeHundredTwentyAndLongTextWraps() {
        var longText = string.Join(' ', Enumerable.Repeat("measurement", 14));
        var custom = new TooltipInfo { Header = longText, Lines = [new TooltipLine { Label = "Series", Value = longText, Color = SKColors.Lime }] };
        Func<NTSeriesTooltipContext<Pt>, TooltipInfo> selector = _ => custom;
        var chart = TooltipChart(series: Args(("TooltipInfoSelector", selector)));

        using var bitmap = await HoverAndPaint(chart, 400, 280);
        var bounds = BoundsOf(bitmap, Magenta)!.Value;

        (bounds.Right - bounds.Left).Should().BeInRange(250, 321);
        (bounds.Bottom - bounds.Top).Should().BeGreaterThan(68 + 40, "wrapped text adds rows");
    }

    [Fact]
    public async Task RepaintingTheSameHover_IsPixelIdentical() {
        var chart = TooltipChart();
        using var surface = await Paint(chart);
        await Move(chart, 200, 150);
        await Paint(chart, surface: surface);
        using var first = Bitmap(surface);
        await Paint(chart, surface: surface);
        using var second = Bitmap(surface);

        first.Bytes.Should().Equal(second.Bytes, "cached text blobs must draw exactly the same frame");
    }

    [Fact]
    public async Task Disposing_RemovesTheTooltipFromTheFrame() {
        var chart = TooltipChart();
        using var surface = await Paint(chart);
        await Move(chart, 200, 150);

        chart.Render(p => p.Add(c => c.ChildContent, Combine(NoAxes(), Recording("a", hit: Hit))));
        await Paint(chart, surface: surface);
        using var bitmap = Bitmap(surface);

        BoundsOf(bitmap, Magenta).Should().BeNull();
    }

    [Fact]
    public void TooltipInfo_DefaultsToNoHeaderAndNoLines() {
        var info = new TooltipInfo();

        info.Header.Should().BeNull();
        info.Lines.Should().NotBeNull().And.BeEmpty();
    }
}
