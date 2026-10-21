using NTComponents.Charts.Core;
using SkiaSharp;

namespace NTComponents.Charts.Tests.Series.Circular;

/// <summary>Slice label text is reused across frames and must follow format and data changes.</summary>
public class PieLabelCache_Tests : CircularChartTestBase {
    private static readonly SKRect _area = new(0, 0, CanvasWidth, CanvasHeight);

    private static Dictionary<string, object> Labels(string format) => new() { ["ShowDataLabels"] = true, ["DataLabelFormat"] = format };

    private static byte[] Paint(IRenderedComponent<NTChart<Item>> cut) {
        using var surface = NewSurface();
        cut.FindComponent<NTPieSeries<Item>>().Instance.Render(CreateContext(cut.Instance, surface), _area);
        return Snapshot(surface);
    }

    [Fact]
    public void ChangingTheLabelFormatAfterRendering_DrawsTheSameImageAsAFreshChart() {
        var data = Items(40, 30, 30);
        var changed = RenderChart<NTPieSeries<Item>>(data, Labels("{0:0}"));
        var before = Paint(changed);
        changed.Render(parameters => parameters.Add(p => p.ChildContent, SeriesFragment<NTPieSeries<Item>>(data, Labels("{0:0.00}"))));
        var fresh = RenderChart<NTPieSeries<Item>>(data, Labels("{0:0.00}"));

        var after = Paint(changed);

        after.Should().NotEqual(before);
        after.Should().Equal(Paint(fresh));
    }

    // Doubling every value keeps the slice geometry, so only correctly invalidated label text can change the image.
    [Fact]
    public void InvalidatingAfterValuesChange_RedrawsTheLabels() {
        var data = new List<Item>(Items(40, 30, 30));
        var changed = RenderChart<NTPieSeries<Item>>(data, Labels("{0:0}"));
        var before = Paint(changed);
        for (var i = 0; i < data.Count; i++) {
            data[i] = data[i] with { Value = data[i].Value * 2 };
        }
        changed.FindComponent<NTPieSeries<Item>>().Instance.Invalidate();
        var fresh = RenderChart<NTPieSeries<Item>>(Items(80, 60, 60), Labels("{0:0}"));

        var after = Paint(changed);

        after.Should().NotEqual(before);
        after.Should().Equal(Paint(fresh));
    }
}
