using NTComponents.Charts.Core;
using NTComponents.Charts.Core.Series;
using SkiaSharp;
using static NTComponents.Charts.Tests.Series.Circular.CircularChartTestBase;

namespace NTComponents.Charts.Tests.Series.Circular;

/// <summary>Minimal concrete circular series that exposes the computed slice geometry without any drawing.</summary>
public sealed class ProbeCircularSeries : NTCircularSeries<Item> {
    public ProbeCircularSeries(NTChart<Item> chart, IEnumerable<Item> data) {
        Chart = chart;
        Data = data;
        XValue = item => item.Name;
        ValueSelector = item => item.Value;
        AnimationEnabled = false;
        ApplyParameters();
    }

    public IReadOnlyList<ProbeSlice> Slices {
        get {
            CalculateSlices(new SKRect(0, 0, 100, 100));
            return SliceInfos.Select(slice => new ProbeSlice(slice.Index, slice.StartAngle, slice.SweepAngle, slice.Value, slice.Data)).ToList();
        }
    }

    public void ApplyParameters() => OnParametersSet();

    public override SKRect Render(NTRenderContext context, SKRect renderArea) => renderArea;

    public override (int Index, Item? Data)? HitTest(SKPoint point, SKRect renderArea) => null;
}
