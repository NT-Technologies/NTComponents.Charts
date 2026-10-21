using NTComponents.Charts.Core;

namespace NTComponents.Charts.Tests.Series.Circular;

/// <summary>Label fitting and value text are reused across frames and must follow format and data changes.</summary>
public class TreeMapLabelCache_Tests : TreeMapTestBase {
    private static Dictionary<string, object> Labelled(string format) => With(With(Plain("A", "B", "C"), "ShowLabels", true), "DataLabelFormat", format);

    private static byte[] Paint(IRenderedComponent<NTChart<Item>> cut) {
        using var surface = NewSurface();
        cut.FindComponent<NTTreeMapSeries<Item>>().Instance.Render(CreateContext(cut.Instance, surface), Area);
        return Snapshot(surface);
    }

    [Fact]
    public void ChangingTheLabelFormatAfterRendering_DrawsTheSameImageAsAFreshChart() {
        var data = Named(("A", 1200), ("B", 800), ("C", 400));
        var changed = RenderChart<NTTreeMapSeries<Item>>(data, Labelled("{0:N0}"));
        var before = Paint(changed);
        changed.Render(parameters => parameters.Add(p => p.ChildContent, SeriesFragment<NTTreeMapSeries<Item>>(data, Labelled("{0:N2}"))));
        var fresh = RenderChart<NTTreeMapSeries<Item>>(data, Labelled("{0:N2}"));

        var after = Paint(changed);

        after.Should().NotEqual(before);
        after.Should().Equal(Paint(fresh));
    }

    [Fact]
    public void InvalidatingAfterAValueChange_DrawsTheSameImageAsAFreshChart() {
        var data = new List<Item>(Named(("A", 1200), ("B", 800), ("C", 400)));
        var changed = RenderChart<NTTreeMapSeries<Item>>(data, Labelled("{0:N0}"));
        Paint(changed);
        data[1] = data[1] with { Value = 801 };
        changed.FindComponent<NTTreeMapSeries<Item>>().Instance.Invalidate();
        var fresh = RenderChart<NTTreeMapSeries<Item>>(Named(("A", 1200), ("B", 801), ("C", 400)), Labelled("{0:N0}"));

        Paint(changed).Should().Equal(Paint(fresh));
    }
}
