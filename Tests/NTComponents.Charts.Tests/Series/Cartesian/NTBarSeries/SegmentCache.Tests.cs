using SkiaSharp;

namespace NTComponents.Charts.Tests.Series.Cartesian;

/// <summary>Segments are evaluated once per bar and cached until the data, a selector or the segment visibility changes.</summary>
public class NTBarSeriesSegmentCache_Tests : BarTestBase {
    private static readonly ChartSetup _setup = new() { XAxis = XAxisKind.Default };
    private static readonly Pt[] _rows = [new("A", 0m), new("B", 0m)];
    private static readonly Func<Pt, IEnumerable<NTBarSegment>> _short = _ => [new NTBarSegment { Value = 10m, Label = "Low", CustomColor = SKColors.Red }];
    private static readonly Func<Pt, IEnumerable<NTBarSegment>> _tall = row => [new NTBarSegment { Value = row.X.ToString() == "A" ? 40m : 20m, Label = "High", CustomColor = SKColors.Blue }];

    [Fact]
    public void ReplacingTheSegmentSelector_DrawsTheSameImageAsAFreshChart() {
        var replaced = RenderChart(_setup, SeriesFragment<NTBarSeries<Pt>>(_rows, ("SegmentSelector", _short)));
        using (Draw(replaced, SeriesOf<NTBarSeries<Pt>>(replaced))) { }
        ReRender(replaced, _setup, SeriesFragment<NTBarSeries<Pt>>(_rows, ("SegmentSelector", _tall)));
        var fresh = RenderChart(_setup, SeriesFragment<NTBarSeries<Pt>>(_rows, ("SegmentSelector", _tall)));

        using var replacedImage = Draw(replaced, SeriesOf<NTBarSeries<Pt>>(replaced));
        using var freshImage = Draw(fresh, SeriesOf<NTBarSeries<Pt>>(fresh));

        SeriesOf<NTBarSeries<Pt>>(replaced).GetYRange().Should().Be((20m, 40m));
        Snapshot(replacedImage).Should().Equal(Snapshot(freshImage));
    }

    [Fact]
    public void SteadyFrames_InvokeTheSegmentSelectorOncePerBar() {
        var calls = 0;
        Func<Pt, IEnumerable<NTBarSegment>> counting = row => {
            calls++;
            return _tall(row);
        };
        var cut = RenderChart(_setup, SeriesFragment<NTBarSeries<Pt>>(_rows, ("SegmentSelector", counting)));
        var series = SeriesOf<NTBarSeries<Pt>>(cut);
        using (Draw(cut, series)) { }
        var afterFirstFrame = calls;

        using (Draw(cut, series)) { }
        using (Draw(cut, series)) { }

        afterFirstFrame.Should().BeLessThanOrEqualTo(_rows.Length);
        calls.Should().Be(afterFirstFrame);
    }
}
