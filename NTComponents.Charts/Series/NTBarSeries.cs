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

    /// <summary>
    ///     Optional explicit minimum for the value axis when rendering bar series.
    ///     For vertical bars this affects Y-axis minimum; for horizontal bars this affects X-axis minimum.
    /// </summary>
    [Parameter]
    public decimal? ValueAxisMinimum { get; set; }

    /// <summary>
    ///     Optional callback that returns stacked segments for each bar data point.
    /// </summary>
    [Parameter]
    public Func<TData, IEnumerable<NTBarSegment>>? SegmentSelector { get; set; }

    /// <summary>
    ///     Optional callback to choose a color for each bar segment.
    /// </summary>
    [Parameter]
    public Func<NTBarSegmentColorContext<TData>, TnTColor>? SegmentColorSelector { get; set; }

    /// <summary>
    ///     Gets or sets whether non-hovered bars in this series fade when a single bar is hovered.
    /// </summary>
    [Parameter]
    public bool FadeOtherBarsOnHover { get; set; }

    /// <summary>
    ///     Gets or sets the opacity applied to non-hovered bars when <see cref="FadeOtherBarsOnHover" /> is enabled.
    /// </summary>
    [Parameter]
    public float NonHoveredBarOpacity { get; set; } = 0.15f;

    private SKPaint? _barPaint;
    private SKPaint? _highlightPaint;
    private SKPaint? _labelPaint;
    private SKPaint? _labelBgPaint;
    private SKPaint? _labelBorderPaint;
    private SKFont? _labelFont;
    private readonly List<(SKRect Rect, int Index, int? SegmentIndex, TData Data, string? SegmentLabel, decimal SegmentValue, SKColor SegmentColor)> _lastBarRects = [];
    private readonly HashSet<string> _hiddenSegmentLabels = new(StringComparer.OrdinalIgnoreCase);
    private int? _lastHoveredSegmentPointIndex;
    private int? _lastHoveredSegmentIndex;
    private string? _lastHoveredSegmentLabel;
    private decimal? _lastHoveredSegmentValue;
    private SKColor? _lastHoveredSegmentColor;

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
        var animationFactor = GetBarAnimationFactor(progress, visibility);

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

        // Horizontal bars use the X axis as the value axis, so baseline comes from X range.
        double xBase;
        if (xMin > 0) {
            xBase = xMin;
        }
        else if (xMax < 0) {
            xBase = xMax;
        }
        else {
            xBase = 0d;
        }
        var xBasePx = Chart.ScaleX(xBase, renderArea);

        var barRects = GetBarRects(renderArea, xMin, xMax, yMin, yMax, yBase, xBase, yScale, animationFactor);
        if (barRects.Count == 0) {
            return renderArea;
        }

        var seriesColor = Chart.GetSeriesColor(this);
        var seriesAlphaFactor = Math.Clamp(HoverFactor * VisibilityFactor, 0f, 1f);
        var color = ApplyAlphaFactor(seriesColor, seriesAlphaFactor);

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
            var pointAlphaFactor = GetPointAlphaFactor(i);
            var pointColor = ApplyAlphaFactor(args.Color ?? color, pointAlphaFactor);
            var value = GetBarTotalValue(item);
            var segments = GetPointSegments(item, value);

            if (SegmentSelector is null) {
                _barPaint.Color = pointColor;
                _highlightPaint.Color = pointColor.WithAlpha(255);
                DrawBar(canvas, rect, isPointHovered ? _highlightPaint : _barPaint, context.Density);
                _lastBarRects.Add((rect, i, null, item, null, value, pointColor));
            }
            else if (segments.Count > 0) {
                DrawSegmentedBar(context, renderArea, rect, item, i, value, segments, pointColor, isPointHovered, pointAlphaFactor);
            }

            if (ShowDataLabels && SegmentSelector is null) {
                var labelColor = args.DataLabelColor;
                var labelSize = args.DataLabelSize ?? DataLabelSize;

                if (Orientation == NTChartOrientation.Vertical) {
                    DrawVerticalBarLabel(context, renderArea, rect, value, pointColor, labelColor, labelSize, pointAlphaFactor);
                }
                else {
                    DrawHorizontalBarLabel(context, renderArea, rect, value, pointColor, labelColor, labelSize, xBasePx, pointAlphaFactor);
                }
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
        var values = Data.Select(item => (double)GetBarTotalValue(item)).ToList();
        var min = values.Min();
        var max = values.Max();
        return (min, max);
    }

    /// <inheritdoc />
    public override (decimal Min, decimal Max)? GetYRange(double? xMin = null, double? xMax = null) {
        if (Data == null || !Data.Any()) return null;

        if (Orientation == NTChartOrientation.Vertical) {
            var dataList = Data.ToList();
            if (dataList.Count == 0) {
                return null;
            }

            IEnumerable<TData> items = dataList;
            if (xMin.HasValue && xMax.HasValue) {
                var minX = Math.Min(xMin.Value, xMax.Value);
                var maxX = Math.Max(xMin.Value, xMax.Value);
                items = dataList.Where(item => {
                    var x = Chart.GetScaledXValue(XValue.Invoke(item));
                    return x >= minX && x <= maxX;
                });
            }

            var min = decimal.MaxValue;
            var max = decimal.MinValue;
            var hasValues = false;
            foreach (var item in items) {
                var value = GetBarTotalValue(item) * (decimal)VisibilityFactor;
                min = Math.Min(min, value);
                max = Math.Max(max, value);
                hasValues = true;
            }

            return hasValues ? (min, max) : null;
        }

        // Horizontal: Y axis is categorical, so use index positions.
        var count = Data.Count();
        if (count <= 0) {
            return (0, 1);
        }

        return (0, Math.Max(1, count - 1));
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

    private void DrawVerticalBarLabel(NTRenderContext context, SKRect renderArea, SKRect barRect, decimal value, SKColor barColor, SKColor? labelColor, float labelSize, float alphaFactor) {
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
        _labelPaint.Color = ApplyAlphaFactor(labelColor ?? Chart.GetSeriesTextColor(this), alphaFactor);

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
        _labelBgPaint.Color = ApplyAlphaFactor(barColor.WithAlpha(235), alphaFactor);
        context.Canvas.DrawRoundRect(bgRect, 4f * context.Density, 4f * context.Density, _labelBgPaint);

        _labelBorderPaint ??= new SKPaint {
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = context.Density
        };
        _labelBorderPaint.Color = ApplyAlphaFactor(Chart.GetThemeColor(TnTColor.OutlineVariant), alphaFactor);
        context.Canvas.DrawRoundRect(bgRect, 4f * context.Density, 4f * context.Density, _labelBorderPaint);

        context.Canvas.DrawText(text, centerX, baselineAbove, SKTextAlign.Center, _labelFont, _labelPaint);
    }

    private void DrawHorizontalBarLabel(NTRenderContext context, SKRect renderArea, SKRect barRect, decimal value, SKColor barColor, SKColor? labelColor, float labelSize, float xAxisBase, float alphaFactor) {
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
        _labelPaint.Color = ApplyAlphaFactor(labelColor ?? Chart.GetSeriesTextColor(this), alphaFactor);

        _labelFont.MeasureText(text, out var bounds);
        var textHeight = bounds.Height;
        var textWidth = bounds.Width;
        var textPadding = 4f * context.Density;
        var minInsideWidth = textWidth + (10f * context.Density);
        var canRenderInside = barRect.Width >= minInsideWidth;

        var baselineY = barRect.MidY + (textHeight / 2f) - (1f * context.Density);
        if (canRenderInside) {
            context.Canvas.DrawText(text, barRect.MidX, baselineY, SKTextAlign.Center, _labelFont, _labelPaint);
            return;
        }

        var isPositive = value >= 0;
        var textAlign = isPositive ? SKTextAlign.Left : SKTextAlign.Right;
        var outsideOffset = 6f * context.Density;
        var outsideX = isPositive
            ? barRect.Right + outsideOffset
            : barRect.Left - outsideOffset;

        var bgRect = isPositive
            ? new SKRect(
                outsideX - textPadding,
                baselineY - textHeight - textPadding,
                outsideX + textWidth + textPadding,
                baselineY + textPadding)
            : new SKRect(
                outsideX - textWidth - textPadding,
                baselineY - textHeight - textPadding,
                outsideX + textPadding,
                baselineY + textPadding);

        if (bgRect.Right > renderArea.Right) {
            var shiftLeft = bgRect.Right - renderArea.Right;
            bgRect.Offset(-shiftLeft, 0f);
            outsideX -= shiftLeft;
        }

        if (bgRect.Left < renderArea.Left) {
            var shiftRight = renderArea.Left - bgRect.Left;
            bgRect.Offset(shiftRight, 0f);
            outsideX += shiftRight;
        }

        _labelBgPaint ??= new SKPaint {
            IsAntialias = true,
            Style = SKPaintStyle.Fill
        };
        _labelBgPaint.Color = ApplyAlphaFactor(barColor.WithAlpha(235), alphaFactor);
        context.Canvas.DrawRoundRect(bgRect, 4f * context.Density, 4f * context.Density, _labelBgPaint);

        _labelBorderPaint ??= new SKPaint {
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = context.Density
        };
        _labelBorderPaint.Color = ApplyAlphaFactor(Chart.GetThemeColor(TnTColor.OutlineVariant), alphaFactor);
        context.Canvas.DrawRoundRect(bgRect, 4f * context.Density, 4f * context.Density, _labelBorderPaint);

        context.Canvas.DrawText(text, outsideX, baselineY, textAlign, _labelFont, _labelPaint);
    }

    private List<SKRect> GetBarRects(SKRect renderArea, double xMin, double xMax, decimal yMin, decimal yMax, decimal yBase, double xBase, NTAxisScale yScale, float animationFactor) {
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
                var yValue = GetBarTotalValue(item);
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
        var baseX = Chart.ScaleX(xBase, renderArea);

        for (var i = 0; i < horizontalCount; i++) {
            var item = dataList[i];
            var scaledY = (decimal)i;

            var centerY = Chart.ScaleY(scaledY, renderArea);
            var xValue = (double)GetBarTotalValue(item);
            var animatedXValue = xBase + ((xValue - xBase) * animationFactor);
            var valueX = Chart.ScaleX(animatedXValue, renderArea);
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

    private static float GetBarAnimationFactor(float progress, float visibility) {
        progress = Math.Clamp(progress, 0f, 1f);
        visibility = Math.Clamp(visibility, 0f, 1f);

        // Ease-out-back creates a small overshoot past 1.0 before settling.
        const float c1 = 1.35f;
        var c3 = c1 + 1f;
        var p = progress - 1f;
        var eased = 1f + (c3 * p * p * p) + (c1 * p * p);
        return eased * visibility;
    }

    internal override TooltipInfo GetTooltipInfo(TData data) {
        var baseInfo = base.GetTooltipInfo(data);
        if (SegmentSelector is null || Chart.HoveredSeries != this || Chart.HoveredPointIndex is null) {
            return ResolveTooltipInfo(data, baseInfo, Chart.HoveredPointIndex);
        }

        if (_lastHoveredSegmentPointIndex == Chart.HoveredPointIndex &&
            _lastHoveredSegmentIndex.HasValue &&
            _lastHoveredSegmentValue.HasValue) {
            var defaultInfo = new TooltipInfo {
                Header = baseInfo.Header,
                Lines = [
                    new TooltipLine {
                        Label = _lastHoveredSegmentLabel ?? $"Segment {_lastHoveredSegmentIndex.Value + 1}",
                        Value = string.Format(DataLabelFormat, _lastHoveredSegmentValue.Value),
                        Color = _lastHoveredSegmentColor ?? Chart.GetSeriesColor(this)
                    },
                    new TooltipLine {
                        Label = Title ?? "Total",
                        Value = string.Format(DataLabelFormat, GetBarTotalValue(data)),
                        Color = Chart.GetSeriesColor(this)
                    }
                ]
            };

            return ResolveTooltipInfo(
                data,
                defaultInfo,
                Chart.HoveredPointIndex,
                _lastHoveredSegmentLabel,
                _lastHoveredSegmentValue,
                _lastHoveredSegmentColor);
        }

        var nonSegmentedDefaultInfo = new TooltipInfo {
            Header = baseInfo.Header,
            Lines = [
                new TooltipLine {
                    Label = Title ?? "Total",
                    Value = string.Format(DataLabelFormat, GetBarTotalValue(data)),
                    Color = Chart.GetSeriesColor(this)
                }
            ]
        };

        return ResolveTooltipInfo(data, nonSegmentedDefaultInfo, Chart.HoveredPointIndex);
    }

    /// <inheritdoc />
    public override (int Index, TData? Data)? HitTest(SKPoint point, SKRect renderArea) {
        for (var i = _lastBarRects.Count - 1; i >= 0; i--) {
            var item = _lastBarRects[i];
            if (item.Rect.Contains(point)) {
                _lastHoveredSegmentPointIndex = item.Index;
                _lastHoveredSegmentIndex = item.SegmentIndex;
                _lastHoveredSegmentLabel = item.SegmentLabel;
                _lastHoveredSegmentValue = item.SegmentValue;
                _lastHoveredSegmentColor = item.SegmentColor;
                return (item.Index, item.Data);
            }
        }

        _lastHoveredSegmentPointIndex = null;
        _lastHoveredSegmentIndex = null;
        _lastHoveredSegmentLabel = null;
        _lastHoveredSegmentValue = null;
        _lastHoveredSegmentColor = null;

        return null;
    }

    internal override void ToggleLegendItem(LegendItemInfo<TData> item) {
        if (SegmentSelector is not null && !string.IsNullOrWhiteSpace(item.Key)) {
            var key = item.Key.Trim();
            if (!_hiddenSegmentLabels.Add(key)) {
                _hiddenSegmentLabels.Remove(key);
            }

            ResetAnimation();
            Chart?.InvalidateDataCaches();
            return;
        }

        base.ToggleLegendItem(item);
    }

    internal override IEnumerable<LegendItemInfo<TData>> GetLegendItems() {
        if (SegmentSelector is null) {
            return base.GetLegendItems();
        }

        var dataList = Data?.ToList() ?? [];
        if (dataList.Count == 0) {
            return base.GetLegendItems();
        }

        var segmentItems = new Dictionary<string, LegendItemInfo<TData>>(StringComparer.OrdinalIgnoreCase);
        var seriesColor = Chart.GetSeriesColor(this);

        for (var dataIndex = 0; dataIndex < dataList.Count; dataIndex++) {
            var item = dataList[dataIndex];
            var segments = SegmentSelector(item) ?? [];
            var segmentIndex = 0;

            foreach (var segment in segments) {
                if (Math.Abs(segment.Value) <= 0m || string.IsNullOrWhiteSpace(segment.Label)) {
                    segmentIndex++;
                    continue;
                }

                var label = segment.Label.Trim();
                if (!segmentItems.ContainsKey(label)) {
                    segmentItems[label] = new LegendItemInfo<TData> {
                        Label = label,
                        Color = ResolveSegmentColor(item, dataIndex, segment, segmentIndex, seriesColor),
                        Series = this,
                        Key = label,
                        IsVisible = Visible && !_hiddenSegmentLabels.Contains(label)
                    };
                }

                segmentIndex++;
            }
        }

        return segmentItems.Count > 0
            ? segmentItems
                .OrderBy(entry => entry.Key, StringComparer.OrdinalIgnoreCase)
                .Select(entry => entry.Value)
            : base.GetLegendItems();
    }

    internal override bool IsLegendItemHovered(LegendItemInfo<TData> item) {
        if (SegmentSelector is not null && !string.IsNullOrWhiteSpace(item.Key)) {
            var hoveredLegendKey = Chart.HoveredLegendItem?.Series == this
                ? Chart.HoveredLegendItem.Key
                : null;

            if (!string.IsNullOrWhiteSpace(hoveredLegendKey)) {
                return string.Equals(item.Key, hoveredLegendKey, StringComparison.OrdinalIgnoreCase);
            }

            if (Chart.HoveredSeries == this && !string.IsNullOrWhiteSpace(_lastHoveredSegmentLabel)) {
                return string.Equals(item.Key, _lastHoveredSegmentLabel, StringComparison.OrdinalIgnoreCase);
            }

            return false;
        }

        return base.IsLegendItemHovered(item);
    }

    internal override float GetLegendItemAlphaFactor(LegendItemInfo<TData> item) {
        var baseAlphaFactor = base.GetLegendItemAlphaFactor(item);

        if (SegmentSelector is null || string.IsNullOrWhiteSpace(item.Key)) {
            return baseAlphaFactor;
        }

        var hoveredLegendKey = Chart.HoveredLegendItem?.Series == this
            ? Chart.HoveredLegendItem.Key
            : null;

        if (string.IsNullOrWhiteSpace(hoveredLegendKey)) {
            return baseAlphaFactor;
        }

        if (string.Equals(item.Key, hoveredLegendKey, StringComparison.OrdinalIgnoreCase)) {
            return baseAlphaFactor;
        }

        return Math.Clamp(baseAlphaFactor * NonHoveredBarOpacity, 0f, 1f);
    }

    private decimal GetBarTotalValue(TData item) {
        if (SegmentSelector is null) {
            return YValueSelector(item);
        }

        decimal total = 0m;
        foreach (var segment in SegmentSelector(item) ?? []) {
            if (!IsSegmentVisible(segment)) {
                continue;
            }

            total += segment.Value;
        }

        return total;
    }

    private List<NTBarSegment> GetPointSegments(TData item, decimal totalValue) {
        if (SegmentSelector is null) {
            return [new NTBarSegment { Value = totalValue }];
        }

        var segments = (SegmentSelector(item) ?? [])
            .Where(IsSegmentVisible)
            .Where(s => Math.Abs(s.Value) > 0m)
            .ToList();

        if (segments.Count == 0) {
            return [];
        }

        return segments;
    }

    private void DrawSegmentedBar(NTRenderContext context, SKRect renderArea, SKRect rect, TData data, int dataIndex, decimal totalValue, IReadOnlyList<NTBarSegment> segments, SKColor fallbackColor, bool isPointHovered, float pointAlphaFactor) {
        if (rect.Width <= 0 || rect.Height <= 0 || segments.Count == 0) {
            return;
        }

        if (Orientation == NTChartOrientation.Vertical) {
            var totalMagnitude = segments.Sum(s => Math.Abs(s.Value));
            if (totalMagnitude <= 0m) {
                return;
            }

            var cursor = rect.Bottom;
            for (var segmentIndex = 0; segmentIndex < segments.Count; segmentIndex++) {
                var segment = segments[segmentIndex];
                var magnitude = Math.Abs(segment.Value);
                if (magnitude <= 0m) {
                    continue;
                }

                var segmentHeight = rect.Height * (float)(magnitude / totalMagnitude);
                var segmentRect = new SKRect(rect.Left, cursor - segmentHeight, rect.Right, cursor);
                cursor -= segmentHeight;

                var segmentAlphaFactor = GetSegmentAlphaFactor(pointAlphaFactor, segment.Label);
                var segmentColor = ApplyAlphaFactor(ResolveSegmentColor(data, dataIndex, segment, segmentIndex, fallbackColor), segmentAlphaFactor);
                var isSegmentHovered = isPointHovered && _lastHoveredSegmentPointIndex == dataIndex && _lastHoveredSegmentIndex == segmentIndex;
                _barPaint!.Color = segmentColor;
                _highlightPaint!.Color = segmentColor.WithAlpha(255);
                DrawBar(context.Canvas, segmentRect, isSegmentHovered ? _highlightPaint : _barPaint, context.Density);
                _lastBarRects.Add((segmentRect, dataIndex, segmentIndex, data, segment.Label, segment.Value, segmentColor));
                if (ShowDataLabels) {
                    DrawSegmentDataLabel(context, segmentRect, segment.Value, segmentColor, isHorizontal: false, segmentAlphaFactor);
                }
            }

            if (ShowDataLabels) {
                DrawSegmentTotalLabel(context, renderArea, rect, totalValue, fallbackColor, isHorizontal: false, pointAlphaFactor);
            }

            return;
        }

        var totalWidthMagnitude = segments.Sum(s => Math.Abs(s.Value));
        if (totalWidthMagnitude <= 0m) {
            return;
        }

        var cursorX = rect.Left;
        for (var segmentIndex = 0; segmentIndex < segments.Count; segmentIndex++) {
            var segment = segments[segmentIndex];
            var magnitude = Math.Abs(segment.Value);
            if (magnitude <= 0m) {
                continue;
            }

            var segmentWidth = rect.Width * (float)(magnitude / totalWidthMagnitude);
            var segmentRect = new SKRect(cursorX, rect.Top, cursorX + segmentWidth, rect.Bottom);
            cursorX += segmentWidth;

            var segmentAlphaFactor = GetSegmentAlphaFactor(pointAlphaFactor, segment.Label);
            var segmentColor = ApplyAlphaFactor(ResolveSegmentColor(data, dataIndex, segment, segmentIndex, fallbackColor), segmentAlphaFactor);
            var isSegmentHovered = isPointHovered && _lastHoveredSegmentPointIndex == dataIndex && _lastHoveredSegmentIndex == segmentIndex;
            _barPaint!.Color = segmentColor;
            _highlightPaint!.Color = segmentColor.WithAlpha(255);
            DrawBar(context.Canvas, segmentRect, isSegmentHovered ? _highlightPaint : _barPaint, context.Density);
            _lastBarRects.Add((segmentRect, dataIndex, segmentIndex, data, segment.Label, segment.Value, segmentColor));
            if (ShowDataLabels) {
                DrawSegmentDataLabel(context, segmentRect, segment.Value, segmentColor, isHorizontal: true, segmentAlphaFactor);
            }
        }

        if (ShowDataLabels) {
            DrawSegmentTotalLabel(context, renderArea, rect, totalValue, fallbackColor, isHorizontal: true, pointAlphaFactor);
        }
    }

    private void DrawSegmentDataLabel(NTRenderContext context, SKRect segmentRect, decimal value, SKColor segmentColor, bool isHorizontal, float alphaFactor) {
        var text = string.Format(DataLabelFormat, value);
        var labelSize = DataLabelSize * context.Density;

        _labelFont ??= new SKFont {
            Embolden = true,
            Typeface = context.DefaultFont.Typeface
        };
        _labelFont.Size = labelSize;

        _labelPaint ??= new SKPaint {
            IsAntialias = true
        };
        _labelPaint.Color = ApplyAlphaFactor(GetContrastTextColor(segmentColor), alphaFactor);

        _labelFont.MeasureText(text, out var bounds);
        var textWidth = bounds.Width;
        var textHeight = bounds.Height;
        var horizontalPad = 4f * context.Density;
        var minWidth = textWidth + (horizontalPad * 2f);
        var minHeight = isHorizontal
            ? textHeight + (2f * context.Density)
            : textHeight + (8f * context.Density);

        if (segmentRect.Width < minWidth || segmentRect.Height < minHeight) {
            return;
        }

        var baselineY = segmentRect.MidY + (textHeight / 2f) - (1f * context.Density);
        var centerX = isHorizontal ? segmentRect.MidX : segmentRect.MidX;
        context.Canvas.DrawText(text, centerX, baselineY, SKTextAlign.Center, _labelFont, _labelPaint);
    }

    private void DrawSegmentTotalLabel(NTRenderContext context, SKRect renderArea, SKRect barRect, decimal value, SKColor accentColor, bool isHorizontal, float alphaFactor) {
        var text = string.Format(DataLabelFormat, value);
        var labelSize = DataLabelSize * context.Density;

        _labelFont ??= new SKFont {
            Embolden = true,
            Typeface = context.DefaultFont.Typeface
        };
        _labelFont.Size = labelSize;

        _labelPaint ??= new SKPaint {
            IsAntialias = true
        };
        _labelPaint.Color = ApplyAlphaFactor(ResolveTotalLabelTextColor(accentColor), alphaFactor);

        _labelFont.MeasureText(text, out var bounds);
        var textWidth = bounds.Width;
        var textHeight = bounds.Height;
        var textPadding = 4f * context.Density;

        if (isHorizontal) {
            var baselineY = barRect.MidY + (textHeight / 2f) - (1f * context.Density);
            var textX = barRect.Right + (6f * context.Density);

            if (ShowDataLabelBackground) {
                var bgRect = new SKRect(
                    textX - textPadding,
                    baselineY - textHeight - textPadding,
                    textX + textWidth + textPadding,
                    baselineY + textPadding);

                if (bgRect.Right > renderArea.Right) {
                    var shiftLeft = bgRect.Right - renderArea.Right;
                    bgRect.Offset(-shiftLeft, 0f);
                    textX -= shiftLeft;
                }

                if (bgRect.Left < renderArea.Left) {
                    var shiftRight = renderArea.Left - bgRect.Left;
                    bgRect.Offset(shiftRight, 0f);
                    textX += shiftRight;
                }

                DrawDataLabelBackground(context, bgRect, accentColor, alphaFactor);
            }
            else if (textX + textWidth > renderArea.Right) {
                textX = Math.Max(renderArea.Left, renderArea.Right - textWidth);
            }

            context.Canvas.DrawText(text, textX, baselineY, SKTextAlign.Left, _labelFont, _labelPaint);
            return;
        }

        var baselineAbove = barRect.Top - (4f * context.Density);
        var minBaseline = renderArea.Top + textHeight + textPadding;
        if (baselineAbove < minBaseline) {
            baselineAbove = minBaseline;
        }

        var centerX = barRect.MidX;
        if (ShowDataLabelBackground) {
            var bgRect = new SKRect(
                centerX - (textWidth / 2f) - textPadding,
                baselineAbove - textHeight - textPadding,
                centerX + (textWidth / 2f) + textPadding,
                baselineAbove + textPadding);

            if (bgRect.Left < renderArea.Left) {
                bgRect.Offset(renderArea.Left - bgRect.Left, 0f);
            }

            if (bgRect.Right > renderArea.Right) {
                bgRect.Offset(renderArea.Right - bgRect.Right, 0f);
            }

            centerX = bgRect.MidX;
            DrawDataLabelBackground(context, bgRect, accentColor, alphaFactor);
        }
        else {
            centerX = Math.Clamp(centerX, renderArea.Left + (textWidth / 2f), renderArea.Right - (textWidth / 2f));
        }

        context.Canvas.DrawText(text, centerX, baselineAbove, SKTextAlign.Center, _labelFont, _labelPaint);
    }

    private void DrawDataLabelBackground(NTRenderContext context, SKRect bgRect, SKColor accentColor, float alphaFactor) {
        _labelBgPaint ??= new SKPaint {
            IsAntialias = true,
            Style = SKPaintStyle.Fill
        };
        _labelBgPaint.Color = ApplyAlphaFactor(ResolveTotalLabelBackgroundColor(accentColor), alphaFactor);
        context.Canvas.DrawRoundRect(bgRect, 4f * context.Density, 4f * context.Density, _labelBgPaint);

        _labelBorderPaint ??= new SKPaint {
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = context.Density
        };
        _labelBorderPaint.Color = ApplyAlphaFactor(Chart.GetThemeColor(TnTColor.OutlineVariant), alphaFactor);
        context.Canvas.DrawRoundRect(bgRect, 4f * context.Density, 4f * context.Density, _labelBorderPaint);
    }

    private SKColor ResolveTotalLabelTextColor(SKColor accentColor) {
        if (DataLabelColor.HasValue) {
            return Chart.GetThemeColor(DataLabelColor.Value);
        }

        return ShowDataLabelBackground
            ? GetContrastTextColor(ResolveTotalLabelBackgroundColor(accentColor))
            : Chart.GetSeriesTextColor(this);
    }

    private SKColor ResolveTotalLabelBackgroundColor(SKColor accentColor) {
        if (DataLabelBackgroundColor.HasValue) {
            return Chart.GetThemeColor(DataLabelBackgroundColor.Value);
        }

        return accentColor.WithAlpha(235);
    }

    private SKColor ResolveSegmentColor(TData data, int dataIndex, NTBarSegment segment, int segmentIndex, SKColor fallbackColor) {
        if (segment.CustomColor.HasValue) {
            return segment.CustomColor.Value;
        }

        if (segment.Color.HasValue && segment.Color.Value != TnTColor.None) {
            return Chart.GetThemeColor(segment.Color.Value);
        }

        if (SegmentColorSelector is not null) {
            var colorContext = new NTBarSegmentColorContext<TData> {
                Data = data,
                DataIndex = dataIndex,
                SegmentIndex = segmentIndex,
                SegmentLabel = segment.Label,
                SegmentValue = segment.Value
            };
            return Chart.GetThemeColor(SegmentColorSelector(colorContext));
        }

        return Chart.GetPaletteColor(segmentIndex + 1).WithAlpha(fallbackColor.Alpha);
    }

    private static SKColor GetContrastTextColor(SKColor color) {
        var luminance = (0.2126f * color.Red) + (0.7152f * color.Green) + (0.0722f * color.Blue);
        return luminance > 140f ? SKColors.Black : SKColors.White;
    }

    private float GetPointAlphaFactor(int pointIndex) {
        var alphaFactor = Math.Clamp(HoverFactor * VisibilityFactor, 0f, 1f);

        if (!FadeOtherBarsOnHover || Chart.HoveredSeries != this || !Chart.HoveredPointIndex.HasValue) {
            return alphaFactor;
        }

        if (Chart.HoveredPointIndex.Value == pointIndex) {
            return alphaFactor;
        }

        return Math.Clamp(alphaFactor * NonHoveredBarOpacity, 0f, 1f);
    }

    private float GetSegmentAlphaFactor(float baseAlphaFactor, string? segmentLabel) {
        var hoveredLegendKey = Chart.HoveredLegendItem?.Series == this
            ? Chart.HoveredLegendItem.Key
            : null;

        if (string.IsNullOrWhiteSpace(hoveredLegendKey)) {
            return baseAlphaFactor;
        }

        if (string.Equals(segmentLabel?.Trim(), hoveredLegendKey, StringComparison.OrdinalIgnoreCase)) {
            return baseAlphaFactor;
        }

        return Math.Clamp(baseAlphaFactor * NonHoveredBarOpacity, 0f, 1f);
    }

    private bool IsSegmentVisible(NTBarSegment segment) {
        if (string.IsNullOrWhiteSpace(segment.Label)) {
            return true;
        }

        return !_hiddenSegmentLabels.Contains(segment.Label.Trim());
    }

    private static SKColor ApplyAlphaFactor(SKColor color, float alphaFactor) {
        alphaFactor = Math.Clamp(alphaFactor, 0f, 1f);
        return color.WithAlpha((byte)Math.Clamp(color.Alpha * alphaFactor, 0f, 255f));
    }
}
