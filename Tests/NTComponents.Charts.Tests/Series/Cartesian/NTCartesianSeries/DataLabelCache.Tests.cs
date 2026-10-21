using Microsoft.AspNetCore.Components;
using NTComponents.Charts.Core.Series;

namespace NTComponents.Charts.Tests.Series.Cartesian;

/// <summary>Data label text is reused across frames and must follow format and value changes.</summary>
public class NTCartesianSeriesDataLabelCache_Tests : CartesianChartTestBase {
    private static readonly ChartSetup _setup = new() { XAxis = XAxisKind.Int };

    private static RenderFragment Line(Pt[] data, string format) => SeriesFragment<NTLineSeries<Pt>>(data, ("ShowDataLabels", true), ("DataLabelFormat", format), ("PointStyle", PointStyle.None));

    [Fact]
    public void ChangingTheFormatAfterRendering_DrawsTheSameLabelsAsAFreshChart() {
        var data = new[] { new Pt(1, 10m), new Pt(2, 25m), new Pt(3, 15m) };
        var changed = RenderChart(_setup, Line(data, "{0:0}"));
        using (Draw(changed, SeriesOf<NTLineSeries<Pt>>(changed))) { }
        ReRender(changed, _setup, Line(data, "{0:0.00}"));
        var fresh = RenderChart(_setup, Line(data, "{0:0.00}"));

        using var changedImage = Draw(changed, SeriesOf<NTLineSeries<Pt>>(changed));
        using var freshImage = Draw(fresh, SeriesOf<NTLineSeries<Pt>>(fresh));

        Snapshot(changedImage).Should().Equal(Snapshot(freshImage));
    }

    // The last point repeats the first value at a larger scale: "{0}" formats it as "1.50", which a value-keyed cache would draw as "1.5".
    [Fact]
    public void EqualValuesWithDifferentScales_KeepTheirOwnText() {
        var scaled = RenderChart(_setup, Line([new Pt(1, 1.5m), new Pt(2, 3m), new Pt(3, 1.50m)], "{0}"));
        var sameScale = RenderChart(_setup, Line([new Pt(1, 1.5m), new Pt(2, 3m), new Pt(3, 1.5m)], "{0}"));

        using var scaledImage = Draw(scaled, SeriesOf<NTLineSeries<Pt>>(scaled));
        using var sameScaleImage = Draw(sameScale, SeriesOf<NTLineSeries<Pt>>(sameScale));

        Snapshot(scaledImage).Should().NotEqual(Snapshot(sameScaleImage));
    }
}
