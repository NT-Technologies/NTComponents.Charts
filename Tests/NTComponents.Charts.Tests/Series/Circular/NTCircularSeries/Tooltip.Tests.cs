using NTComponents.Charts.Core;

namespace NTComponents.Charts.Tests.Series.Circular;

public class Tooltip_Tests : CircularChartTestBase {
    [Fact]
    public void Pie_ReportsXValueHeaderFormattedValueTitleAndSeriesColor() {
        var item = new Item("Alpha", 12.34m);
        var cut = RenderChart<NTPieSeries<Item>>([item, new Item("Beta", 1)], new() { ["Title"] = "Revenue" });
        var series = cut.FindComponent<NTPieSeries<Item>>().Instance;

        var tooltip = Tooltip(series, item);

        tooltip.Header.Should().Be("Alpha");
        var line = tooltip.Lines.Should().ContainSingle().Subject;
        line.Label.Should().Be("Revenue");
        line.Value.Should().Be("12.3");
        line.Color.Should().Be(PaletteColors[0]);
    }

    [Fact]
    public void WithoutTitle_LabelFallsBackToSeries() {
        var item = new Item("Alpha", 5);
        var cut = RenderChart<NTPieSeries<Item>>([item]);
        var series = cut.FindComponent<NTPieSeries<Item>>().Instance;

        Tooltip(series, item).Lines.Single().Label.Should().Be("Series");
    }

    [Fact]
    public void CustomDataLabelFormat_IsAppliedToTheTooltipValue() {
        var item = new Item("Alpha", 5);
        var cut = RenderChart<NTPieSeries<Item>>([item], new() { ["DataLabelFormat"] = "${0:0.00}!" });
        var series = cut.FindComponent<NTPieSeries<Item>>().Instance;

        Tooltip(series, item).Lines.Single().Value.Should().Be("$5.00!");
    }

    [Fact]
    public void Radar_UsesTheSameCircularTooltipContract() {
        var item = new Item("Speed", 7.25m);
        var cut = RenderChart<NTRadarSeries<Item>>([item, new Item("B", 1), new Item("C", 2)], new() { ["Title"] = "Stats" });
        var series = cut.FindComponent<NTRadarSeries<Item>>().Instance;

        var tooltip = Tooltip(series, item);

        tooltip.Header.Should().Be("Speed");
        tooltip.Lines.Single().Label.Should().Be("Stats");
        tooltip.Lines.Single().Value.Should().Be("7.3");
    }

    [Fact]
    public void CustomTooltipSelector_ReplacesTheDefaultPayload() {
        var item = new Item("Alpha", 5);
        var cut = RenderChart<NTPieSeries<Item>>([item], new() {
            ["TooltipInfoSelector"] = (Func<NTComponents.Charts.Core.NTSeriesTooltipContext<Item>, NTComponents.Charts.Core.TooltipInfo>)(context => new NTComponents.Charts.Core.TooltipInfo { Header = "custom" })
        });
        var series = cut.FindComponent<NTPieSeries<Item>>().Instance;

        Tooltip(series, item).Header.Should().Be("custom");
    }
}
