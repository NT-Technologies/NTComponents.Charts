using Microsoft.AspNetCore.Components;
using NTComponents.Charts.Core;
using NTComponents.Charts.Core.Axes;
using NTComponents.Charts.Core.Series;
using SkiaSharp;

namespace NTComponents.Charts;

/// <summary>
///     Represents a bar series in a cartesian chart.
/// </summary>
/// <typeparam name="TData">The type of the data.</typeparam>
public class NTBarSeries<TData> : NTCartesianSeries<TData> where TData : class {
    /// <summary>
    ///     Gets or sets the corner radius for the bars.
    /// </summary>
    [Parameter]
    public float CornerRadius { get; set; } = 0.0f;

    /// <summary>
    ///    Gets or sets the orientation of the chart (Vertical or Horizontal).
    /// </summary>
    [Parameter]
    public NTChartOrientation Orientation { get; set; } = NTChartOrientation.Vertical;

    private SKPaint? _barPaint;
    private SKPaint? _highlightPaint;
    private SKPaint? _labelPaint;
    private SKPaint? _labelBgPaint;
    private SKPaint? _labelBorderPaint;
    private SKFont? _labelFont;
    private readonly List<(SKRect Rect, int Index, TData Data)> _lastBarRects = [];

    private (int Count, int Index) GetVisibleBarSeriesLayout() {
        var series = Chart.Series
            .OfType<NTBarSeries<TData>>()
            .Where(s => s.IsEffectivelyVisible && s.Orientation == Orientation)
            .ToList();

        if (series.Count == 0) {
            return (1, 0);
        }

        var index = series.IndexOf(this);
        if (index < 0) {
            index = 0;
        }

        return (series.Count, index);
    }

    protected override void Dispose(bool disposing) {
        if (disposing) {
            _barPaint?.Dispose();
            _highlightPaint?.Dispose();
            _labelPaint?.Dispose();
            _labelBgPaint?.Dispose();
            _labelBorderPaint?.Dispose();
            _labelFont?.Dispose();
        }
        base.Dispose(disposing);
    }

    /// <inheritdoc />
    public override SKRect Render(NTRenderContext context, SKRect renderArea) {
        _lastBarRects.Clear();
        if (Data == null || !Data.Any()) {
            return renderArea;
        }

        var canvas = context.Canvas;
        var xAxis = Chart.XAxis as NTAxisOptions<TData>;
        var yAxis = (UseSecondaryYAxis ? Chart.SecondaryYAxis : Chart.YAxis) as NTAxisOptions<TData>;
        var (xMin, xMax) = Chart.GetXRange(xAxis, true);
        var (yMin, yMax) = Chart.GetYRange(yAxis, true);
        var yScale = yAxis?.Scale ?? NTAxisScale.Linear;
        var progress = GetAnimationProgress();
        var visibility = VisibilityFactor;
        var animationFactor = Math.Clamp(progress * visibility, 0f, 1f);

        decimal yBase;
        if (yMin > 0) {
            yBase = yMin;
        }
        else if (yMax < 0) {
            yBase = yMax;
        }
        else {
            yBase = 0m;
        }

        var barRects = GetBarRects(renderArea, xMin, xMax, yMin, yMax, yBase, yScale, animationFactor);
        if (barRects.Count == 0) {
            return renderArea;
        }

        var color = Chart.GetSeriesColor(this);
        var alphaFactor = HoverFactor * VisibilityFactor;
        color = color.WithAlpha((byte)(color.Alpha * alphaFactor));

        _barPaint ??= new SKPaint {
            IsAntialias = true,
            Style = SKPaintStyle.Fill
        };
        _highlightPaint ??= new SKPaint {
            IsAntialias = true,
            Style = SKPaintStyle.Fill
        };

        var dataList = Data.ToList();
        for (var i = 0; i < barRects.Count && i < dataList.Count; i++) {
            var item = dataList[i];
            var args = new NTDataPointRenderArgs<TData> {
                Data = item,
                Index = i,
                Color = color,
                GetThemeColor = Chart.GetThemeColor
            };
            OnDataPointRender?.Invoke(args);

            var rect = barRects[i];
            var isPointHovered = Chart.HoveredSeries == this && Chart.HoveredPointIndex == i;
            var pointColor = args.Color ?? color;

            _barPaint.Color = pointColor;
            _highlightPaint.Color = pointColor.WithAlpha(255);
            DrawBar(canvas, rect, isPointHovered ? _highlightPaint : _barPaint, context.Density);
            _lastBarRects.Add((rect, i, item));

            // Bar charts always render value labels: inside at the top when possible.
            var labelColor = args.DataLabelColor;
            var labelSize = args.DataLabelSize ?? DataLabelSize;
            var value = YValueSelector(item);

            if (Orientation == NTChartOrientation.Vertical) {
                DrawVerticalBarLabel(context, renderArea, rect, value, pointColor, labelColor, labelSize);
            }
            else {
                var labelX = value >= 0 ? rect.Right + (5 * context.Density) : rect.Left - (5 * context.Density);
                var textAlign = value >= 0 ? SKTextAlign.Left : SKTextAlign.Right;
                RenderDataLabel(context, labelX, rect.MidY + (labelSize / 2), value, renderArea, labelColor, labelSize, textAlign);
            }
        }

        return renderArea;
    }

    /// <inheritdoc />
    public override (double Min, double Max)? GetXRange() {
        if (Data == null || !Data.Any()) return null;

        if (Orientation == NTChartOrientation.Vertical) {
            return base.GetXRange();
        }

        // Horizontal: X axis shows values (YValueSelector)
        var values = Data.Select(item => (double)YValueSelector(item)).ToList();
        var min = Math.Min(0, values.Min());
        var max = values.Max();
        return (min, max);
    }

    /// <inheritdoc />
    public override (decimal Min, decimal Max)? GetYRange(double? xMin = null, double? xMax = null) {
        if (Data == null || !Data.Any()) return null;

        if (Orientation == NTChartOrientation.Vertical) {
            return base.GetYRange(xMin, xMax);
        }


        // Horizontal: Y axis shows categories (XValue)
        var dataList = Data.ToList();
        var values = new List<decimal>();
        for (int i = 0; i < dataList.Count; i++) {
            var val = XValue.Invoke(dataList[i]);
            var scaled = (decimal)Chart.GetScaledYValue(val);

            if (scaled == 0 && val != null && !(val is IConvertible)) {
                // If not categorical and we got 0 for a non-numeric value, use index
                values.Add(i);
            }
            else {
                values.Add(scaled);
            }
        }

        var min = values.Any() ? values.Min() : 0;
        var max = values.Any() ? values.Max() : 1;
        return (min, max);
    }


    /// <inheritdoc />
    internal override void RegisterXValues(HashSet<object> values) {
        if (Data == null || Orientation == NTChartOrientation.Horizontal) return;
        base.RegisterXValues(values);
    }

    /// <inheritdoc />
    internal override void RegisterYValues(HashSet<object> values) {
        if (Data == null) return;
        if (Orientation == NTChartOrientation.Vertical) {
            base.RegisterYValues(values);
        }
        else {
            // Horizontal: register categories for the Y axis
            foreach (var item in Data) {
                var val = XValue.Invoke(item);
                if (val != null) values.Add(val);
            }
        }

    }

    private void DrawBar(SKCanvas canvas, SKRect rect, SKPaint paint, float density) {
        if (CornerRadius > 0) {
            var scaledRadius = CornerRadius * density;
            canvas.DrawRoundRect(rect, scaledRadius, scaledRadius, paint);
        }
        else {
            canvas.DrawRect(rect, paint);
        }
    }

    private void DrawVerticalBarLabel(NTRenderContext context, SKRect renderArea, SKRect barRect, decimal value, SKColor barColor, SKColor? labelColor, float labelSize) {
        var text = string.Format(DataLabelFormat, value);
        var sizePx = labelSize * context.Density;

        _labelFont ??= new SKFont {
            Embolden = true,
            Typeface = context.DefaultFont.Typeface
        };
        _labelFont.Size = sizePx;

        _labelPaint ??= new SKPaint {
            IsAntialias = true
        };
        _labelPaint.Color = labelColor ?? Chart.GetSeriesTextColor(this);

        _labelFont.MeasureText(text, out var bounds);
        var textHeight = bounds.Height;
        var textWidth = bounds.Width;
        var textPadding = 4f * context.Density;
        var minInsideHeight = textHeight + (8f * context.Density);
        var canRenderInside = barRect.Height >= minInsideHeight;

        var centerX = barRect.MidX;
        if (canRenderInside) {
            var baselineY = barRect.Top + textHeight + (3f * context.Density);
            context.Canvas.DrawText(text, centerX, baselineY, SKTextAlign.Center, _labelFont, _labelPaint);
            return;
        }

        // If the bar is too small, render above with a background for readability.
        var baselineAbove = barRect.Top - (4f * context.Density);
        var minBaseline = renderArea.Top + textHeight + textPadding;
        if (baselineAbove < minBaseline) {
            baselineAbove = minBaseline;
        }

        var bgRect = new SKRect(
            centerX - (textWidth / 2f) - textPadding,
            baselineAbove - textHeight - textPadding,
            centerX + (textWidth / 2f) + textPadding,
            baselineAbove + textPadding);

        _labelBgPaint ??= new SKPaint {
            IsAntialias = true,
            Style = SKPaintStyle.Fill
        };
        _labelBgPaint.Color = barColor.WithAlpha(235);
        context.Canvas.DrawRoundRect(bgRect, 4f * context.Density, 4f * context.Density, _labelBgPaint);

        _labelBorderPaint ??= new SKPaint {
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = context.Density
        };
        _labelBorderPaint.Color = Chart.GetThemeColor(TnTColor.OutlineVariant);
        context.Canvas.DrawRoundRect(bgRect, 4f * context.Density, 4f * context.Density, _labelBorderPaint);

        context.Canvas.DrawText(text, centerX, baselineAbove, SKTextAlign.Center, _labelFont, _labelPaint);
    }

    private List<SKRect> GetBarRects(SKRect renderArea, double xMin, double xMax, decimal yMin, decimal yMax, decimal yBase, NTAxisScale yScale, float animationFactor) {
        var dataList = Data?.ToList();
        if (dataList == null || dataList.Count == 0) {
            return [];
        }

        var rects = new List<SKRect>(dataList.Count);

        if (Orientation == NTChartOrientation.Vertical) {
            var count = dataList.Count;
            var slotWidth = renderArea.Width / Math.Max(1, count);
            var spacing = Math.Max(1f * Chart.Density, slotWidth * 0.08f);
            var clusterWidth = Math.Max(2f, slotWidth - spacing);
            var (seriesCount, seriesIndex) = GetVisibleBarSeriesLayout();
            var interSeriesGap = seriesCount > 1 ? Math.Min(2f * Chart.Density, clusterWidth * 0.04f) : 0f;
            var totalGap = interSeriesGap * Math.Max(0, seriesCount - 1);
            var barWidth = Math.Max(1f, (clusterWidth - totalGap) / seriesCount);
            var baseY = ScaleYFast(yBase, yMin, yMax, yScale, renderArea);

            for (var i = 0; i < count; i++) {
                var item = dataList[i];
                var xValue = Chart.GetScaledXValue(XValue.Invoke(item));
                var barX = Chart.ScaleX(xValue, renderArea);
                var yValue = YValueSelector(item);
                var animatedValue = yBase + ((yValue - yBase) * (decimal)animationFactor);
                var y = ScaleYFast(animatedValue, yMin, yMax, yScale, renderArea);
                var clusterLeft = barX - (clusterWidth / 2f);
                var left = clusterLeft + (seriesIndex * (barWidth + interSeriesGap));
                var right = left + barWidth;

                var top = Math.Min(y, baseY);
                var bottom = Math.Max(y, baseY);
                rects.Add(new SKRect(left, top, right, bottom));
            }

            return rects;
        }

        // Horizontal bars: value maps to X, category/index maps to Y.
        var horizontalCount = dataList.Count;
        var hSlot = renderArea.Height / Math.Max(1, horizontalCount);
        var spacingY = Math.Max(1f * Chart.Density, hSlot * 0.08f);
        var clusterHeight = Math.Max(2f, hSlot - spacingY);
        var (hSeriesCount, hSeriesIndex) = GetVisibleBarSeriesLayout();
        var interSeriesGapY = hSeriesCount > 1 ? Math.Min(2f * Chart.Density, clusterHeight * 0.04f) : 0f;
        var totalGapY = interSeriesGapY * Math.Max(0, hSeriesCount - 1);
        var barHeight = Math.Max(1f, (clusterHeight - totalGapY) / hSeriesCount);
        var baseX = Chart.ScaleX((double)yBase, renderArea);

        for (var i = 0; i < horizontalCount; i++) {
            var item = dataList[i];
            var yValueRaw = XValue.Invoke(item);
            var scaledY = Chart.GetScaledYValue(yValueRaw);
            if (scaledY == 0m && yValueRaw is not null && yValueRaw is not IConvertible) {
                scaledY = i;
            }

            var centerY = Chart.ScaleY(scaledY, renderArea);
            var valueX = Chart.ScaleX((double)YValueSelector(item), renderArea);
            var clusterTop = centerY - (clusterHeight / 2f);
            var top = clusterTop + (hSeriesIndex * (barHeight + interSeriesGapY));
            var bottom = top + barHeight;

            var left = Math.Min(baseX, valueX);
            var right = Math.Max(baseX, valueX);
            rects.Add(new SKRect(left, top, right, bottom));
        }

        return rects;
    }

    private static float ScaleYFast(decimal y, decimal min, decimal max, NTAxisScale scale, SKRect plotArea) {
        double t;
        if (scale == NTAxisScale.Logarithmic) {
            var dMin = Math.Max(0.000001, (double)min);
            var dMax = Math.Max(dMin * 1.1, (double)max);
            var dy = Math.Max(dMin, (double)y);
            t = (Math.Log10(dy) - Math.Log10(dMin)) / (Math.Log10(dMax) - Math.Log10(dMin));
        }
        else {
            var range = max - min;
            if (range <= 0) {
                return plotArea.Bottom;
            }
            t = (double)((y - min) / range);
        }

        const float p = 3f;
        var bottom = plotArea.Bottom - p;
        var height = plotArea.Height - (p * 2);
        return (float)(bottom - (t * height));
    }

    /// <inheritdoc />
    public override (int Index, TData? Data)? HitTest(SKPoint point, SKRect renderArea) {
        for (var i = _lastBarRects.Count - 1; i >= 0; i--) {
            var item = _lastBarRects[i];
            if (item.Rect.Contains(point)) {
                return (item.Index, item.Data);
            }
        }

        return null;
    }
}
