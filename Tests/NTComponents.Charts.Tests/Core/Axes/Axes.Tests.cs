using Microsoft.AspNetCore.Components;
using NTComponents.Charts.Core;
using NTComponents.Charts.Core.Axes;
using NTComponents.Charts.Core.Series;
using SkiaSharp;

namespace NTComponents.Charts.Tests.ChartCore;

public class Axes_Tests : ChartTestBase {
    private static readonly DateTime Jan = new(2024, 1, 15);
    private static readonly DateTime Feb = new(2024, 2, 20);
    private static readonly DateTime Mar = new(2024, 3, 25);

    private static Pt[] Dated() => [new("a", 0, 5m, Feb), new("b", 1, 7m, Jan), new("c", 2, 3m, Mar)];

    private static Pt[] Span(int count, double step) => Enumerable.Range(0, count).Select(i => new Pt($"P{i}", i * step, i * 10m)).ToArray();


    private IRenderedComponent<NTChart<Pt>> DateChart(Pt[]? data = null, IDictionary<string, object>? axis = null) =>
        RenderChart(Combine(XAxis<DateTime>(p => p.Date, axis), Line(data ?? Dated(), x: p => p.Date)));

    // ---------- tick scanning helpers ----------

    private static List<double> Centroids(IEnumerable<float> coverage) {
        var result = new List<double>();
        double weighted = 0, total = 0;
        var index = 0;
        foreach (var value in coverage) {
            if (value > 0.2f) {
                weighted += (index + 0.5) * value;
                total += value;
            }
            else if (total > 0) {
                result.Add(weighted / total);
                weighted = total = 0;
            }
            index++;
        }
        if (total > 0) {
            result.Add(weighted / total);
        }
        return result;
    }

    private static float Dark(SKBitmap bitmap, int x, int y) => InkAt(bitmap, x, y, SKColors.Black, 0);

    private static List<double> XTickPositions(SKBitmap bitmap, SKRect plot) =>
        Centroids(Enumerable.Range(0, bitmap.Width).Select(x => Dark(bitmap, x, (int)plot.Bottom + 3)));

    private static List<double> YTickPositions(SKBitmap bitmap, SKRect plot) =>
        Centroids(Enumerable.Range(0, bitmap.Height).Select(y => Dark(bitmap, (int)plot.Left - 3, y)));

    // ---------- categorical vs continuous ----------

    [Theory]
    [InlineData(typeof(string), true)]
    [InlineData(typeof(object), true)]
    [InlineData(typeof(double), false)]
    [InlineData(typeof(int), false)]
    [InlineData(typeof(decimal), false)]
    [InlineData(typeof(DateTime), false)]
    [InlineData(typeof(DateTimeOffset), false)]
    [InlineData(typeof(DateOnly), false)]
    [InlineData(typeof(TimeOnly), false)]
    [InlineData(typeof(double?), false)]
    public void IsCategorical_DependsOnTheAxisValueType(Type valueType, bool expected) {
        var axis = (INTXAxis<Pt>)Activator.CreateInstance(typeof(NTXAxisOptions<,>).MakeGenericType(typeof(Pt), valueType))!;

        axis.IsCategorical.Should().Be(expected);
    }

    [Fact]
    public void IsCategorical_HorizontalBarsMakeTheXAxisAValueAxis() {
        var chart = RenderChart(Combine(XAxis<string>(p => p.Name), Bar(x: p => p.Name, extra: Args(("Orientation", NTChartOrientation.Horizontal)))));

        chart.Instance.XAxis.IsCategorical.Should().BeFalse();
    }

    [Fact]
    public void AxisOptions_Defaults() {
        var x = new NTXAxisOptions<Pt, double>();
        var y = new NTYAxisOptions<Pt, decimal>();

        x.Visible.Should().BeTrue();
        x.Scale.Should().Be(NTAxisScale.Linear);
        x.AxisFontSize.Should().Be(12);
        x.TitleFontSize.Should().Be(14);
        x.EnableAutoDateGrouping.Should().BeTrue();
        x.DateGroupingThreshold.Should().Be(0);
        x.RenderOrder.Should().Be(RenderOrdered.Axis);
        y.ShowGridLines.Should().BeTrue();
        y.GridLineColor.Should().Be(NTColor.OutlineVariant);
        y.AbsoluteMinimum.Should().BeNull();
    }

    [Fact]
    public void AxisOutsideAChart_ThrowsOnInitialization() {
        using var ctx = new BunitContext();

        var act = () => ctx.Render<NTXAxisOptions<Pt, double>>();

        act.Should().Throw<InvalidOperationException>().WithMessage("*must be used within*");
    }

    // ---------- label formatting ----------

    [Fact]
    public void FormatValue_NumbersUseDefaultToStringAndNullIsEmpty() {
        var chart = RenderChart(Combine(XAxis<double>(p => p.X), Line()));

        chart.Instance.XAxis.FormatValue(2.5).Should().Be(2.5.ToString());
        chart.Instance.XAxis.FormatValue("Label").Should().Be("Label");
        chart.Instance.XAxis.FormatValue(null).Should().BeEmpty();
    }

    [Fact]
    public void FormatValue_LabelFormatIsAppliedAndInvalidFormatsFallBackToToString() {
        var formatted = RenderChart(Combine(XAxis<double>(p => p.X, Args(("LabelFormat", "{0:0.0} units"))), Line()));
        var invalid = RenderChart(Combine(XAxis<double>(p => p.X, Args(("LabelFormat", "{1}"))), Line()));

        formatted.Instance.XAxis.FormatValue(2.345).Should().Be(string.Format("{0:0.0} units", 2.345));
        invalid.Instance.XAxis.FormatValue(2.5).Should().Be(2.5.ToString());
    }

    [Fact]
    public async Task FormatValue_DateLabelsFollowTheDateGroupingLevelOfTheCurrentView() {
        var chart = DateChart();

        // Before layout the plot width is unknown, so a 66 day span is too dense for daily labels.
        chart.Instance.XAxis.FormatValue(Feb).Should().Be(Feb.ToString("MMM yyyy"));

        using var surface = await Paint(chart);

        chart.Instance.XAxis.FormatValue(Feb).Should().Be(Feb.ToString("MM/dd/yyyy"));
    }

    [Fact]
    public void FormatValue_DateLabelWithExplicitFormatUsesTheFormat() {
        var chart = DateChart(axis: Args(("LabelFormat", "{0:yyyy-MM-dd}")));

        chart.Instance.XAxis.FormatValue(Feb).Should().Be("2024-02-20");
    }

    // ---------- date grouping ----------

    [Fact]
    public void ResolveDateGroupingLevel_ChoosesDayMonthOrYearByVisibleSpan() {
        var chart = DateChart();
        var axis = chart.Instance.XAxis;

        axis.ResolveDateGroupingLevel(Jan.Ticks, Jan.AddDays(10).Ticks, 600, 1).Should().Be(NTDateGroupingLevel.Day);
        axis.ResolveDateGroupingLevel(Jan.Ticks, Jan.AddYears(3).Ticks, 100, 1).Should().Be(NTDateGroupingLevel.Month);
        axis.ResolveDateGroupingLevel(Jan.Ticks, Jan.AddYears(10).Ticks, 100, 1).Should().Be(NTDateGroupingLevel.Year);
    }

    [Fact]
    public void ResolveDateGroupingLevel_IsIndependentOfArgumentOrderAndScalesWithDensity() {
        var chart = DateChart();
        var axis = chart.Instance.XAxis;

        axis.ResolveDateGroupingLevel(Jan.AddDays(10).Ticks, Jan.Ticks, 600, 1).Should().Be(NTDateGroupingLevel.Day);
        // Doubling the density halves the number of points that fit, turning a 100 day span into months.
        axis.ResolveDateGroupingLevel(Jan.Ticks, Jan.AddDays(100).Ticks, 600, 1).Should().Be(NTDateGroupingLevel.Day);
        axis.ResolveDateGroupingLevel(Jan.Ticks, Jan.AddDays(100).Ticks, 600, 4).Should().Be(NTDateGroupingLevel.Month);
    }

    [Fact]
    public void ResolveDateGroupingLevel_ThresholdRaisesTheDayCutoff() {
        var chart = DateChart(axis: Args(("DateGroupingThreshold", 2000)));

        chart.Instance.XAxis.ResolveDateGroupingLevel(Jan.Ticks, Jan.AddYears(3).Ticks, 100, 1).Should().Be(NTDateGroupingLevel.Day);
    }

    [Fact]
    public void ResolveDateGroupingLevel_DisabledOrNonDateAxis_IsNone() {
        var disabled = DateChart(axis: Args(("EnableAutoDateGrouping", false)));
        var numeric = RenderChart(Combine(XAxis<double>(p => p.X), Line()));

        disabled.Instance.XAxis.ResolveDateGroupingLevel(Jan.Ticks, Jan.AddYears(10).Ticks, 100, 1).Should().Be(NTDateGroupingLevel.None);
        numeric.Instance.XAxis.ResolveDateGroupingLevel(0, 3, 600, 1).Should().Be(NTDateGroupingLevel.None);
        numeric.Instance.GetActiveDateGroupingLevel().Should().Be(NTDateGroupingLevel.None);
    }

    [Fact]
    public async Task GetActiveDateGroupingLevel_UsesTheCurrentViewAndPlotWidth() {
        var chart = DateChart();
        using var surface = await Paint(chart);
        chart.Instance.GetActiveDateGroupingLevel().Should().Be(NTDateGroupingLevel.Day);

        await chart.InvokeAsync(() => chart.Instance.SetViewXRange(Jan.AddYears(-20).Ticks, Jan.AddYears(20).Ticks));

        chart.Instance.GetActiveDateGroupingLevel().Should().Be(NTDateGroupingLevel.Year);
    }

    // ---------- tick generation (observed through the drawn tick marks) ----------

    [Fact]
    public async Task XAxis_ContinuousTicks_AreEvenlySpacedNiceNumbersInsideTheRange() {
        var data = Span(11, 10); // x: 0..100
        var chart = RenderChart(Combine(XAxis<double>(p => p.X), Line(data)));
        using var surface = await Paint(chart);
        using var bitmap = Bitmap(surface);
        var plot = PlotArea(chart);

        var positions = XTickPositions(bitmap, plot);
        var values = positions.Select(p => chart.Instance.ScaleXInverse((float)p, plot)).ToList();

        positions.Count.Should().BeInRange(4, 60);
        var spacing = values[1] - values[0];
        values.Zip(values.Skip(1), (a, b) => b - a).Should().OnlyContain(d => Math.Abs(d - spacing) < spacing * 0.02);
        var mantissa = spacing / Math.Pow(10, Math.Floor(Math.Log10(spacing)));
        new[] { 1d, 2d, 5d, 10d }.Should().Contain(m => Math.Abs(m - mantissa) < 0.02);
        values.Should().OnlyContain(v => Math.Abs(v / spacing - Math.Round(v / spacing)) < 0.02, "ticks sit on multiples of the spacing");
        values.Min().Should().BeGreaterThanOrEqualTo(-5.01);
        values.Max().Should().BeLessThanOrEqualTo(105.01);
    }

    [Fact]
    public async Task XAxis_CategoricalTicks_AreDrawnAtEachCategoryPosition() {
        var chart = RenderChart(Line(x: p => p.Name));
        using var surface = await Paint(chart);
        using var bitmap = Bitmap(surface);
        var plot = PlotArea(chart);

        var positions = XTickPositions(bitmap, plot);

        positions.Should().HaveCount(4);
        for (var i = 0; i < 4; i++) {
            positions[i].Should().BeApproximately(chart.Instance.ScaleX(i, plot), 0.7);
        }
    }

    [Fact]
    public async Task XAxis_IntegerValueType_OnlyGetsWholeNumberTicks() {
        var chart = RenderChart(Combine(XAxis<int>(p => (int)p.X), Line(x: p => (int)p.X)));
        using var surface = await Paint(chart);
        using var bitmap = Bitmap(surface);
        var plot = PlotArea(chart);

        var values = XTickPositions(bitmap, plot).Select(p => chart.Instance.ScaleXInverse((float)p, plot)).ToList();

        values.Should().HaveCount(4);
        values.Should().OnlyContain(v => Math.Abs(v - Math.Round(v)) < 0.01);
        values.Select(v => Math.Round(v)).Should().Equal(0d, 1d, 2d, 3d);
    }

    [Fact]
    public async Task XAxis_ZoomingIntoAViewRegeneratesTicksForTheNewRange() {
        var chart = RenderChart(Combine(XAxis<double>(p => p.X), Line(Span(11, 10))));
        using var surface = await Paint(chart);
        using var before = Bitmap(surface);
        var wide = XTickPositions(before, PlotArea(chart)).Select(p => chart.Instance.ScaleXInverse((float)p, PlotArea(chart))).ToList();
        var wideSpacing = wide[1] - wide[0];

        await chart.InvokeAsync(() => chart.Instance.SetViewXRange(40, 50));
        await Paint(chart, surface: surface);
        using var after = Bitmap(surface);
        var plot = PlotArea(chart);
        var zoomed = XTickPositions(after, plot).Select(p => chart.Instance.ScaleXInverse((float)p, plot)).ToList();

        (zoomed[1] - zoomed[0]).Should().BeLessThan(wideSpacing);
        zoomed.Min().Should().BeGreaterThanOrEqualTo(39.99);
        zoomed.Max().Should().BeLessThanOrEqualTo(50.01);
    }

    [Fact]
    public async Task XAxis_DateTicks_AreDrawnWithinTheVisibleRange() {
        var chart = DateChart();
        using var surface = await Paint(chart);
        using var bitmap = Bitmap(surface);
        var plot = PlotArea(chart);

        var values = XTickPositions(bitmap, plot).Select(p => chart.Instance.ScaleXInverse((float)p, plot)).ToList();

        values.Should().HaveCountGreaterThan(2);
        values.Select(v => new DateTime((long)v)).Should().OnlyContain(d => d >= Jan.AddDays(-5) && d <= Mar.AddDays(5));
    }

    [Fact]
    public async Task YAxis_Ticks_SitOnNiceValuesAtTheirScaledPositions() {
        var data = Span(11, 1).Select((p, i) => p with { Y = i * 10m }).ToArray(); // y: 0..100
        var chart = RenderChart(Combine(YAxis(), Line(data)));
        using var surface = await Paint(chart);
        using var bitmap = Bitmap(surface);
        var plot = PlotArea(chart);

        var positions = YTickPositions(bitmap, plot);

        positions.Count.Should().BeGreaterThan(4);
        var values = positions.Select(p => (double)chart.Instance.ScaleYInverse((float)p, plot)).ToList();
        values.Should().OnlyContain(v => Math.Abs(v / 10 - Math.Round(v / 10)) < 0.01, "a 0..100 range with ~12 target ticks uses a spacing of 10");
        values.Should().Contain(v => Math.Abs(v) < 0.1);
        values.Max().Should().BeApproximately(100, 0.1);
    }

    [Fact]
    public async Task YAxis_LogarithmicScale_PlacesTicksOnPowersOfTen() {
        var data = new Pt[] { new("a", 0, 1m), new("b", 1, 10m), new("c", 2, 100m), new("d", 3, 1000m) };
        var chart = RenderChart(Combine(YAxis(Args(("Scale", NTAxisScale.Logarithmic))), Line(data)));
        using var surface = await Paint(chart);
        using var bitmap = Bitmap(surface);
        var plot = PlotArea(chart);

        foreach (var power in new decimal[] { 1, 10, 100, 1000 }) {
            InkAt(bitmap, (int)plot.Left - 3, (int)chart.Instance.ScaleY(power, plot), SKColors.Black, 1).Should().BeGreaterThan(0.3f, $"a tick is expected at {power}");
        }
        var between = (int)chart.Instance.ScaleY(30m, plot);
        InkAt(bitmap, (int)plot.Left - 3, between, SKColors.Black, 1).Should().BeLessThan(0.2f, "no tick is expected between powers of ten");
    }

    [Fact]
    public async Task YAxis_GridLines_FollowTheTicksAndCanBeDisabledOrRecolored() {
        async Task<(SKBitmap Bitmap, SKRect Plot, IRenderedComponent<NTChart<Pt>> Chart)> Frame(IDictionary<string, object>? axis) {
            var chart = RenderChart(Combine(XAxis<double>(p => p.X), YAxis(axis), Recording("a")));
            var surface = await Paint(chart);
            return (Bitmap(surface), PlotArea(chart), chart);
        }

        var (gridded, plot, chart) = await Frame(null);
        var (none, _, _) = await Frame(Args(("ShowGridLines", false)));
        var (green, _, _) = await Frame(Args(("GridLineColor", NTColor.Tertiary)));
        var column = (int)plot.MidX;
        int Rows(SKBitmap b, SKColor c) => Centroids(Enumerable.Range((int)plot.Top + 2, (int)plot.Height - 6).Select(y => InkAt(b, column, y, c, 0))).Count;
        var gridRows = Rows(gridded, new SKColor(0xdd, 0xdd, 0xdd));

        gridRows.Should().BeGreaterThan(3);
        Rows(none, new SKColor(0xdd, 0xdd, 0xdd)).Should().Be(0);
        Rows(green, new SKColor(0, 0xaa, 0)).Should().Be(gridRows);
        chart.Should().NotBeNull();
    }

    [Fact]
    public async Task XAxis_Title_IsDrawnBelowTheTickLabelsAndCentered() {
        var chart = RenderChart(Combine(XAxis<double>(p => p.X, Args(("Title", "TIME"))), YAxis(), Recording("a")));
        using var surface = await Paint(chart);
        using var bitmap = Bitmap(surface);
        var plot = PlotArea(chart);

        var ink = BoundsOf(bitmap, SKColors.Black, 90, new SKRectI(0, (int)plot.Bottom + 25, Width, Height));

        ink.Should().NotBeNull();
        ((ink!.Value.Left + ink.Value.Right) / 2f).Should().BeApproximately(plot.MidX, 4f);
        ink.Value.Bottom.Should().BeGreaterThan(Height - 20);
    }

    [Fact]
    public async Task HiddenXAxis_DrawsNoTicksOrLine() {
        var chart = RenderChart(Combine(XAxis<double>(p => p.X, Args(("Visible", false))), Recording("a")));
        using var surface = await Paint(chart);
        using var bitmap = Bitmap(surface);
        var plot = PlotArea(chart);

        XTickPositions(bitmap, plot).Should().BeEmpty();
        InkAt(bitmap, (int)plot.MidX, (int)plot.Bottom, SKColors.Black).Should().Be(0f);
    }

    [Fact]
    public async Task RemovingAnAxisComponent_StopsDrawingItsLineOnTheNextFrame() {
        var chart = RenderChart(Combine(XAxis<double>(p => p.X), YAxis(), Recording("a")));
        using var surface = await Paint(chart);

        chart.Render(p => p.Add(c => c.ChildContent, Combine(YAxis(), Recording("a"))));
        await Paint(chart, surface: surface);
        using var bitmap = Bitmap(surface);
        var plot = PlotArea(chart);

        // The default X axis is categorical and, with this data, still draws ticks; the numeric axis' continuous ticks are gone.
        chart.Instance.XAxis.IsCategorical.Should().BeTrue();
        XTickPositions(bitmap, plot).Should().HaveCount(4);
    }

    // ---------- Material 3 axis colors ----------

    private async Task<SKBitmap> LabelBand(IRenderedComponent<NTChart<Pt>> chart) {
        using var surface = await Paint(chart);
        var bitmap = Bitmap(surface);
        return bitmap;
    }

    [Fact]
    public async Task TickLabels_UseOnSurfaceVariantWhileTextColorIsTheDefault() {
        ThemeColors["OnSurfaceVariant"] = "#ff00ff";
        var chart = RenderChart(Combine(XAxis<double>(p => p.X), YAxis(), Line()));

        using var bitmap = await LabelBand(chart);
        var plot = PlotArea(chart);

        BoundsOf(bitmap, new SKColor(255, 0, 255), 40, new SKRectI(0, (int)plot.Bottom + 6, Width, Height)).Should().NotBeNull("tick labels are secondary text");
    }

    [Fact]
    public async Task TickLabels_FollowAnExplicitChartTextColor() {
        ThemeColors["OnSurfaceVariant"] = "#ff00ff";
        var chart = RenderChart(Combine(XAxis<double>(p => p.X), YAxis(), Line()), p => p.Add(c => c.TextColor, NTColor.Tertiary));

        using var bitmap = await LabelBand(chart);
        var plot = PlotArea(chart);
        var band = new SKRectI(0, (int)plot.Bottom + 6, Width, Height);

        BoundsOf(bitmap, new SKColor(0, 170, 0), 40, band).Should().NotBeNull();
        BoundsOf(bitmap, new SKColor(255, 0, 255), 40, band).Should().BeNull();
    }

    [Fact]
    public async Task AxisTicks_AreFourPixelsLongInTheOutlineColor() {
        ThemeColors["Outline"] = "#ff0000";
        var chart = RenderChart(Combine(XAxis<double>(p => p.X), YAxis(), Recording("a")));

        using var bitmap = await LabelBand(chart);
        var plot = PlotArea(chart);
        var tickRun = Enumerable.Range((int)plot.Bottom + 1, 8).Count(y => RowHasOutlineInk(bitmap, y, plot));

        tickRun.Should().BeInRange(3, 4);
    }

    private static bool RowHasOutlineInk(SKBitmap bitmap, int y, SKRect plot) => Enumerable.Range((int)plot.Left, (int)plot.Width).Any(x => InkAt(bitmap, x, y, new SKColor(255, 0, 0), 0) > 0.4f);

    [Fact]
    public void HeatMapSeries_SuppressTheGridLinesOfTheirAxis() {
        bool Suppressed(RenderFragment series) {
            var chart = RenderChart(Combine(XAxis<double>(p => p.X), YAxis(), series));
            var method = typeof(NTComponents.Charts.Core.Axes.NTYAxisOptions<Pt, decimal>).GetMethod("HasGridlessSeriesForCurrentAxis", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
            return (bool)method.Invoke(chart.Instance.YAxis, null)!;
        }

        Suppressed(Series<NTHeatMapSeries<Pt>>(Sample, extra: Args(("WeightSelector", (Func<Pt, decimal>)(p => p.Y))))).Should().BeTrue();
        Suppressed(Line()).Should().BeFalse();
    }
}
