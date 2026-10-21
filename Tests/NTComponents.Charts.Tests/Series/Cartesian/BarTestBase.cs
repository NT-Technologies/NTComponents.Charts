using NTComponents.Charts.Core;
using SkiaSharp;

namespace NTComponents.Charts.Tests.Series.Cartesian;

/// <summary>Independent re-statement of the bar layout rules used to verify rendered/hit-tested bar geometry.</summary>
public abstract class BarTestBase : CartesianChartTestBase {
    protected static readonly (string, object) Primary = ("Color", NTColor.Primary);
    protected static readonly (string, object) Secondary = ("Color", NTColor.Secondary);
    protected static readonly (string, object) Horizontal = ("Orientation", NTChartOrientation.Horizontal);

    private static float YFast(decimal y, decimal min, decimal max) => (float)(SurfaceHeight - 3 - ((double)((y - min) / (max - min)) * (SurfaceHeight - 6)));

    /// <summary>Layout of the slot a bar occupies: width of the cluster and the per-series bar width.</summary>
    protected static (float ClusterWidth, float BarWidth, float Gap) Cluster(float slot, int seriesCount) {
        var spacing = Math.Max(1f, slot * 0.08f);
        var cluster = Math.Max(2f, slot - spacing);
        var gap = seriesCount > 1 ? Math.Min(2f, cluster * 0.04f) : 0f;
        var barWidth = Math.Max(1f, (cluster - (gap * (seriesCount - 1))) / seriesCount);
        return (cluster, barWidth, gap);
    }

    /// <summary>Expected rectangle of vertical bar <paramref name="index" /> of <paramref name="count" /> for the series at <paramref name="seriesIndex" />.</summary>
    protected static SKRect VerticalBar(NTChart<Pt> chart, int index, int count, decimal value, int seriesIndex = 0, int seriesCount = 1, decimal? baseValue = null) {
        var (yMin, yMax) = ChartYRange(chart, padded: true);
        var (cluster, barWidth, gap) = Cluster(Area.Width / count, seriesCount);
        var centerX = chart.ScaleX(index, Area);
        var left = centerX - (cluster / 2f) + (seriesIndex * (barWidth + gap));
        var baseY = YFast(baseValue ?? (yMin > 0 ? yMin : yMax < 0 ? yMax : 0m), yMin, yMax);
        var top = YFast(value, yMin, yMax);
        return new SKRect(left, Math.Min(top, baseY), left + barWidth, Math.Max(top, baseY));
    }

    /// <summary>Expected rectangle of horizontal bar <paramref name="index" /> of <paramref name="count" /> for the series at <paramref name="seriesIndex" />.</summary>
    protected static SKRect HorizontalBar(NTChart<Pt> chart, int index, int count, decimal value, int seriesIndex = 0, int seriesCount = 1) {
        var (xMin, xMax) = ChartXRange(chart, padded: true);
        var xBase = xMin > 0 ? xMin : xMax < 0 ? xMax : 0d;
        var slot = Area.Height / count;
        var (cluster, barHeight, gap) = Cluster(slot, seriesCount);
        var centerY = chart.ScaleY(index, Area);
        var top = centerY - (cluster / 2f) + (seriesIndex * (barHeight + gap));
        var baseX = chart.ScaleX(xBase, Area);
        var valueX = chart.ScaleX((double)value, Area);
        return new SKRect(Math.Min(baseX, valueX), top, Math.Max(baseX, valueX), top + barHeight);
    }

    protected static SKPoint Center(SKRect rect) => new(rect.MidX, rect.MidY);

    protected static SKRect Shrink(SKRect rect, float amount) => new(rect.Left + amount, rect.Top + amount, rect.Right - amount, rect.Bottom - amount);
}
