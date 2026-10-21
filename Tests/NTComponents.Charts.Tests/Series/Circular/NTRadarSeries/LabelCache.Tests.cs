using NTComponents.Charts.Core;
using SkiaSharp;

namespace NTComponents.Charts.Tests.Series.Circular;

/// <summary>Axis label text is reused across frames and must follow data changes.</summary>
public class RadarLabelCache_Tests : CircularChartTestBase {
    private static readonly SKRect _area = new(0, 0, CanvasWidth, CanvasHeight);
    private static readonly Dictionary<string, object> _labels = new() { ["ShowDataLabels"] = true };

    private static byte[] Paint(IRenderedComponent<NTChart<Item>> cut) {
        using var surface = NewSurface();
        cut.FindComponent<NTRadarSeries<Item>>().Instance.Render(CreateContext(cut.Instance, surface), _area);
        return Snapshot(surface);
    }

    // Doubling every value keeps the shape (points scale by the data maximum), so only invalidated label text can change the image.
    [Fact]
    public void InvalidatingAfterValuesChange_RedrawsTheLabels() {
        var data = new List<Item>(Items(10, 20, 15, 25));
        var changed = RenderChart<NTRadarSeries<Item>>(data, _labels);
        var before = Paint(changed);
        for (var i = 0; i < data.Count; i++) {
            data[i] = data[i] with { Value = data[i].Value * 2 };
        }
        changed.FindComponent<NTRadarSeries<Item>>().Instance.Invalidate();
        var fresh = RenderChart<NTRadarSeries<Item>>(Items(20, 40, 30, 50), _labels);

        var after = Paint(changed);

        after.Should().NotEqual(before);
        after.Should().Equal(Paint(fresh));
    }
}
