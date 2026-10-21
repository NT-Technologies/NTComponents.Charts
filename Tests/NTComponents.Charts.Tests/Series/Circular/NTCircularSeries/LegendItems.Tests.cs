using NTComponents.Charts.Core;

namespace NTComponents.Charts.Tests.Series.Circular;

public class LegendItems_Tests : CircularChartTestBase {
    [Fact]
    public void EachDataItem_GetsOneEntryWithXValueLabelAndPaletteColor() {
        var cut = RenderChart<NTPieSeries<Item>>([new("Alpha", 1), new("Beta", 2), new("Gamma", 3)]);
        var series = cut.FindComponent<NTPieSeries<Item>>().Instance;

        var entries = LegendItems(series);

        entries.Select(e => e.Label).Should().Equal("Alpha", "Beta", "Gamma");
        entries.Select(e => e.Key).Should().Equal("Alpha", "Beta", "Gamma");
        entries.Select(e => e.Index).Should().Equal(0, 1, 2);
        entries.Select(e => e.Color).Should().Equal(PaletteColors[0], PaletteColors[1], PaletteColors[2]);
        entries.Should().OnlyContain(e => e.IsVisible);
    }

    [Fact]
    public void MoreItemsThanPaletteEntries_WrapAroundPalette() {
        var cut = RenderChart<NTPieSeries<Item>>(Items(1, 1, 1, 1, 1, 1, 1, 1));
        var series = cut.FindComponent<NTPieSeries<Item>>().Instance;

        var entries = LegendItems(series);

        entries[6].Color.Should().Be(PaletteColors[0]);
        entries[7].Color.Should().Be(PaletteColors[1]);
    }

    [Fact]
    public void NullXValue_FallsBackToOneBasedItemLabel() {
        var cut = RenderChart(SeriesFragment<NTPieSeries<Item>>(Items(1, 2), new() {
            ["XValue"] = (Func<Item, object>)(_ => null!)
        }));
        var series = cut.FindComponent<NTPieSeries<Item>>().Instance;

        LegendItems(series).Select(e => e.Label).Should().Equal("Item 1", "Item 2");
    }

    [Fact]
    public void TogglingSlice_MarksOnlyThatEntryHiddenAndTogglingAgainRestoresIt() {
        var cut = RenderChart<NTPieSeries<Item>>(Items(1, 2, 3));
        var series = cut.FindComponent<NTPieSeries<Item>>().Instance;

        ToggleLegend(series, 1);
        LegendItems(series).Select(e => e.IsVisible).Should().Equal(true, false, true);

        ToggleLegend(series, 1);
        LegendItems(series).Select(e => e.IsVisible).Should().Equal(true, true, true);
    }

    [Fact]
    public void HiddenSeries_MarksEveryEntryHidden() {
        var cut = RenderChart<NTPieSeries<Item>>(Items(1, 2), new() { ["Visible"] = false });
        var series = cut.FindComponent<NTPieSeries<Item>>().Instance;

        LegendItems(series).Should().OnlyContain(e => !e.IsVisible);
    }

    [Fact]
    public void ToggleWithoutIndex_FlipsSeriesVisibilityInsteadOfASlice() {
        var cut = RenderChart<NTPieSeries<Item>>(Items(1, 2));
        var series = cut.FindComponent<NTPieSeries<Item>>().Instance;

        ToggleLegend(series, null);

        series.Visible.Should().BeFalse();
        LegendItems(series).Should().OnlyContain(e => !e.IsVisible);
    }

    [Fact]
    public void ReplacingData_ProducesEntriesForTheNewData() {
        var cut = RenderChart<NTPieSeries<Item>>(Items(1, 2, 3));
        var series = cut.FindComponent<NTPieSeries<Item>>().Instance;
        _ = LegendItems(series);

        cut.Render(p => p.Add(c => c.ChildContent, SeriesFragment<NTPieSeries<Item>>([new Item("Only", 5)])));

        LegendItems(series).Select(e => e.Label).Should().Equal("Only");
    }
}
