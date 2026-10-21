using Microsoft.AspNetCore.Components;

namespace NTComponents.Charts.Tests.Series.Circular;

/// <summary>A radar draws each series as one polygon, so its legend lists series, not data points.</summary>
public class RadarLegend_Tests : CircularChartTestBase {
    private static RenderFragment TwoRadars(IEnumerable<Item> data) => builder => {
        builder.OpenRegion(0);
        SeriesFragment<NTRadarSeries<Item>>(data, new() { ["Title"] = "Team A" })(builder);
        builder.CloseRegion();
        builder.OpenRegion(1);
        SeriesFragment<NTRadarSeries<Item>>(data, new() { ["Title"] = "Team B" })(builder);
        builder.CloseRegion();
    };

    [Fact]
    public void TwoSeries_ListOneLegendItemPerSeriesInItsColor() {
        var cut = RenderChart(TwoRadars(Items(4, 6, 8)));
        var radars = cut.FindComponents<NTRadarSeries<Item>>().Select(component => component.Instance).ToArray();

        var items = radars.SelectMany(LegendItems).ToArray();

        items.Select(item => item.Label).Should().Equal("Team A", "Team B");
        items.Select(item => item.Index).Should().OnlyContain(index => index == null, "a series item hides the whole polygon, not one point");
        items.Select(item => item.Color).Should().Equal(cut.Instance.GetSeriesColor(radars[0]), cut.Instance.GetSeriesColor(radars[1]));
    }

    [Fact]
    public async Task TogglingASeriesItem_HidesThatRadarOnly() {
        var cut = RenderChart(TwoRadars(Items(4, 6, 8)));
        var radars = cut.FindComponents<NTRadarSeries<Item>>().Select(component => component.Instance).ToArray();

        await cut.InvokeAsync(() => ToggleLegend(radars[0], null));

        radars[0].Visible.Should().BeFalse();
        radars[1].Visible.Should().BeTrue();
        LegendItems(radars[0]).Single().IsVisible.Should().BeFalse();
    }
}
