using SkiaSharp;

namespace NTComponents.Charts.Tests.Series.Circular;

/// <summary>Fallback group colors must not depend on the per-process randomized HashCode.</summary>
public class TreeMapGroupColor_Tests : TreeMapTestBase {
    private SKColor[] PaintGroupColors() {
        var cut = RenderChart<NTTreeMapSeries<Item>>(Grouped(("a", 4, "Alpha"), ("b", 3, "Beta"), ("c", 2, "Gamma")), new() {
            ["ShowLabels"] = false,
            ["ShowNavigation"] = false,
            ["ShowDrillIndicator"] = false,
            ["GroupSelector"] = (Func<Item, string?>)(item => item.Group)
        });
        using var surface = NewSurface();
        cut.FindComponent<NTTreeMapSeries<Item>>().Instance.Render(CreateContext(cut.Instance, surface), Area);
        return PaletteColors.Where(c => Stats(surface, c).Count > 0).ToArray();
    }

    [Fact]
    public void SameData_InTwoCharts_GivesTheSameGroupColors() {
        var first = PaintGroupColors();

        PaintGroupColors().Should().Equal(first);
        first.Should().NotBeEmpty();
    }
}
