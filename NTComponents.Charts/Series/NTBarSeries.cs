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
    private readonly record struct BarGeometryKey(
        SKRect Area,
        double XMin,
        double XMax,
        decimal YMin,
        decimal YMax,
        decimal YBase,
        double XBase,
        NTAxisScale Scale,
        NTChartOrientation Orientation,
        float AnimationFactor,
        float Density,
        int SeriesCount,
        int SeriesIndex,
        Func<TData, decimal> ValueSelector,
        Func<TData, object> XValue);

    private readonly record struct BarEntry(decimal Total, NTBarSegment[]? Segments, List<NTBarSegment>? DrawnSegments);
    /// <summary>
    ///     Gets or sets the corner radius for the bars.
    /// </summary>
    [Parameter]
    public float CornerRadius { get; set; } = NTChartTokens.Radius(NTCornerRadius.ExtraSmall);

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
    public Func<NTBarSegmentColorContext<TData>, NTColor>? SegmentColorSelector { get; set; }

    /// <summary>
    ///     Gets or sets whether non-hovered bars in this series fade when a single bar is hovered.
    /// </summary>
    [Parameter]
    public bool FadeOtherBarsOnHover { get; set; }

    /// <summary>
    ///     Gets or sets the opacity applied to non-hovered bars when <see cref="FadeOtherBarsOnHover" /> is enabled.
    /// </summary>
    [Parameter]
    public float NonHoveredBarOpacity { get; set; } = NTChartTokens.DeEmphasisOpacity;

    private SKPaint? _barPaint;
    private SKPaint? _highlightPaint;
    private SKRoundRect? _barRoundRect;
    private readonly SKPoint[] _barRadii = new SKPoint[4];
    // Buffers are reused: the snapshot is valid while _dataSnapshotValid, the geometry while _cachedBarGeometryKey is set, and
    // _barEntries until the data, a selector or the segment visibility changes (see ClearBarCaches).
    private readonly List<TData> _dataSnapshot = [];
    private bool _dataSnapshotValid;
    private readonly List<SKRect> _cachedBarRects = [];
    private readonly List<decimal> _cachedBarValues = [];
    private BarGeometryKey? _cachedBarGeometryKey;
    private readonly Dictionary<TData, BarEntry> _barEntries = new(ReferenceEqualityComparer.Instance);
    private Func<TData, decimal>? _cachedValueSelector;
    private Func<TData, object>? _cachedXSelector;
    private Func<TData, IEnumerable<NTBarSegment>>? _cachedSegmentSelector;
    private NTChart<TData>? _themeColorChart;
    private Func<NTColor, SKColor>? _themeColorSelector;
    private readonly List<(SKRect Rect, int Index, int? SegmentIndex, TData Data, string? SegmentLabel, decimal SegmentValue, SKColor SegmentColor)> _lastBarRects = [];
    private readonly HashSet<string> _hiddenSegmentLabels = new(StringComparer.OrdinalIgnoreCase);
    private int? _lastHoveredSegmentPointIndex;
    private int? _lastHoveredSegmentIndex;
    private string? _lastHoveredSegmentLabel;
    private decimal? _lastHoveredSegmentValue;
    private SKColor? _lastHoveredSegmentColor;

    /// <inheritdoc />
    protected override void OnDataChanged() {
        base.OnDataChanged();
        _dataSnapshotValid = false;
        ClearBarCaches();
    }

    /// <inheritdoc />
    protected override void OnParametersSet() {
        var valueSelector = YValueSelector;
        var xSelector = XValue;
        var segmentSelector = SegmentSelector;
        base.OnParametersSet();
        if ((_cachedValueSelector is not null && !ReferenceEquals(_cachedValueSelector, valueSelector)) ||
            (_cachedXSelector is not null && !ReferenceEquals(_cachedXSelector, xSelector)) ||
            !ReferenceEquals(_cachedSegmentSelector, segmentSelector)) {
            ClearBarCaches();
        }
        _cachedValueSelector = valueSelector;
        _cachedXSelector = xSelector;
        _cachedSegmentSelector = segmentSelector;
    }

    private void ClearBarCaches() {
        _cachedBarGeometryKey = null;
        _barEntries.Clear();
    }

    private List<TData> GetDataSnapshot() {
        if (!_dataSnapshotValid) {
            _dataSnapshot.Clear();
            if (Data is not null) {
                _dataSnapshot.AddRange(Data);
            }
            _dataSnapshotValid = true;
        }

        return _dataSnapshot;
    }

    private (int Count, int Index) GetVisibleBarSeriesLayout() {
        var count = 0;
        var index = 0;
        foreach (var series in Chart.Series) {
            if (series is NTBarSeries<TData> bar && bar.IsEffectivelyVisible && bar.Orientation == Orientation) {
                if (ReferenceEquals(bar, this)) {
                    index = count;
                }
                count++;
            }
        }

        return count == 0 ? (1, 0) : (count, index);
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing) {
        if (disposing) {
            _barPaint?.Dispose();
            _highlightPaint?.Dispose();
            _barRoundRect?.Dispose();
        }
        base.Dispose(disposing);
    }

    /// <inheritdoc />
    public override SKRect Render(NTRenderContext context, SKRect renderArea) {
        _lastBarRects.Clear();
        var dataList = GetDataSnapshot();
        if (dataList.Count == 0) {
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
        var animationFactor = EaseAnimation(progress) * Math.Clamp(visibility, 0f, 1f);

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

        var barRects = GetBarRects(dataList, renderArea, xMin, xMax, yMin, yMax, yBase, xBase, yScale, animationFactor);
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

        for (var i = 0; i < barRects.Count && i < dataList.Count; i++) {
            var item = dataList[i];
            NTDataPointRenderArgs<TData>? args = null;
            var pointColor = color;
            if (OnDataPointRender is { } onDataPointRender) {
                args = new NTDataPointRenderArgs<TData> {
                    Data = item,
                    Index = i,
                    Color = color,
                    GetThemeColor = GetThemeColorSelector()
                };
                onDataPointRender(args);
                pointColor = args.Color ?? color;
            }

            var rect = barRects[i];
            var isPointHovered = Chart.HoveredSeries == this && Chart.HoveredPointIndex == i;
            var pointAlphaFactor = GetPointAlphaFactor(i);
            pointColor = ApplyAlphaFactor(pointColor, pointAlphaFactor);
            var value = _cachedBarValues.Count > 0 && SegmentSelector is null ? _cachedBarValues[i] : GetBarTotalValue(item);

            if (SegmentSelector is null) {
                _barPaint.Color = pointColor;
                _highlightPaint.Color = pointColor.WithAlpha(255);
                DrawBar(canvas, rect, isPointHovered ? _highlightPaint : _barPaint, context.Density, GetValueEnd(value), isPointHovered ? GetStateLayerColor(pointColor) : null);
                _lastBarRects.Add((rect, i, null, item, null, value, pointColor));
            }
            else {
                var segments = GetPointSegments(item);
                if (segments.Count > 0) {
                    DrawSegmentedBar(context, renderArea, rect, item, i, value, segments, pointColor, isPointHovered, pointAlphaFactor);
                }
            }

            if (ShowDataLabels && SegmentSelector is null) {
                var labelColor = args?.DataLabelColor;
                var labelSize = args?.DataLabelSize ?? DataLabelSize;

                if (Orientation == NTChartOrientation.Vertical) {
                    DrawVerticalBarLabel(context, renderArea, rect, value, pointColor, labelColor, labelSize);
                }
                else {
                    DrawHorizontalBarLabel(context, renderArea, rect, value, pointColor, labelColor, labelSize);
                }
            }
        }

        return renderArea;
    }

    /// <inheritdoc />
    public override (double Min, double Max)? GetXRange() {
        var dataList = GetDataSnapshot();
        if (dataList.Count == 0) return null;

        if (Orientation == NTChartOrientation.Vertical) {
            return base.GetXRange();
        }

        // Horizontal: X axis shows values (YValueSelector)
        var min = double.MaxValue;
        var max = double.MinValue;
        foreach (var item in dataList) {
            var value = (double)GetBarTotalValue(item);
            min = Math.Min(min, value);
            max = Math.Max(max, value);
        }
        return (min, max);
    }

    /// <inheritdoc />
    public override (decimal Min, decimal Max)? GetYRange(double? xMin = null, double? xMax = null) {
        var dataList = GetDataSnapshot();
        if (dataList.Count == 0) return null;

        if (Orientation == NTChartOrientation.Vertical) {
            var windowed = xMin.HasValue && xMax.HasValue;
            var minX = windowed ? Math.Min(xMin!.Value, xMax!.Value) : 0d;
            var maxX = windowed ? Math.Max(xMin!.Value, xMax!.Value) : 0d;

            var min = decimal.MaxValue;
            var max = decimal.MinValue;
            var hasValues = false;
            foreach (var item in dataList) {
                if (windowed) {
                    var x = Chart.GetScaledXValue(XValue.Invoke(item));
                    if (x < minX || x > maxX) {
                        continue;
                    }
                }

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
    internal override void RegisterXValues(List<object> values, HashSet<object> seen) {
        if (Data == null || Orientation == NTChartOrientation.Horizontal) return;
        base.RegisterXValues(values, seen);
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

    // Inset between a bar edge and a label drawn inside or beside it.
    private const float LabelInset = 4f;

    private enum BarEnd { None, Top, Bottom, Left, Right }

    /// <summary>
    ///     Fills a bar, rounding only the corners of its value end (<paramref name="end" />), then draws the Material 3 state layer when
    ///     <paramref name="stateLayer" /> is set. The radius is clamped to half the bar thickness and to its length. The round rect and radii are reused.
    /// </summary>
    private void DrawBar(SKCanvas canvas, SKRect rect, SKPaint paint, float density, BarEnd end, SKColor? stateLayer = null) {
        var horizontal = end is BarEnd.Left or BarEnd.Right;
        var radius = Math.Min(CornerRadius * density, Math.Min((horizontal ? rect.Height : rect.Width) / 2f, horizontal ? rect.Width : rect.Height));
        // A sub-pixel radius is invisible, and a plain rect is cheaper to rasterize than a per-corner round rect.
        if (radius < 1f || end == BarEnd.None) {
            canvas.DrawRect(rect, paint);
            if (stateLayer is { } plainLayer) {
                canvas.DrawRect(rect, GetStateLayerPaint(plainLayer));
            }

            return;
        }

        var corner = new SKPoint(radius, radius);
        _barRadii[0] = end is BarEnd.Top or BarEnd.Left ? corner : default;
        _barRadii[1] = end is BarEnd.Top or BarEnd.Right ? corner : default;
        _barRadii[2] = end is BarEnd.Bottom or BarEnd.Right ? corner : default;
        _barRadii[3] = end is BarEnd.Bottom or BarEnd.Left ? corner : default;
        _barRoundRect ??= new SKRoundRect();
        _barRoundRect.SetRectRadii(rect, _barRadii);
        canvas.DrawRoundRect(_barRoundRect, paint);
        if (stateLayer is { } layer) {
            canvas.DrawRoundRect(_barRoundRect, GetStateLayerPaint(layer));
        }
    }

    /// <summary>The on-color state layer for a hovered bar filled with <paramref name="fill" />: hover opacity, or pressed opacity while the pointer is down.</summary>
    private SKColor GetStateLayerColor(SKColor fill) => NTSeriesGeometry.WithFactor(NTSeriesGeometry.LegibleTextColor(Chart.GetSeriesTextColor(this), fill, Chart.GetThemeColor(Chart.BackgroundColor)), StateLayerOpacity * VisibilityFactor);

    private BarEnd GetValueEnd(decimal value) => Orientation == NTChartOrientation.Vertical ? (value >= 0 ? BarEnd.Top : BarEnd.Bottom) : (value >= 0 ? BarEnd.Right : BarEnd.Left);

    /// <summary>Only the outermost segment (the one touching the bar's value end) is rounded; segments stack from the bottom or left.</summary>
    private BarEnd GetSegmentEnd(decimal total, int segmentIndex, int segmentCount) => segmentIndex == (total >= 0 ? segmentCount - 1 : 0) ? GetValueEnd(total) : BarEnd.None;

    private void DrawVerticalBarLabel(NTRenderContext context, SKRect renderArea, SKRect barRect, decimal value, SKColor barColor, SKColor? labelColor, float labelSize) {
        var text = FormatDataLabel(value);
        var sizePx = labelSize * context.Density;
        var boxHeight = NTRenderContextExtensions.LabelBoxHeight(sizePx, context.Density);
        var inset = LabelInset * context.Density;

        if (barRect.Height >= boxHeight + (2f * inset)) {
            DrawLabelText(context, text, barRect.MidX, barRect.Top + inset + (boxHeight / 2f), SKTextAlign.Center, sizePx, labelColor, null, barColor);
            return;
        }

        // If the bar is too small, render above with a background for readability.
        var background = GetLabelBackground(barColor);
        var centerY = Math.Max(barRect.Top - inset - (boxHeight / 2f), renderArea.Top + (boxHeight / 2f));
        DrawLabelText(context, text, barRect.MidX, centerY, SKTextAlign.Center, sizePx, labelColor, background, background, renderArea);
    }

    private void DrawHorizontalBarLabel(NTRenderContext context, SKRect renderArea, SKRect barRect, decimal value, SKColor barColor, SKColor? labelColor, float labelSize) {
        var text = FormatDataLabel(value);
        var sizePx = labelSize * context.Density;
        var inset = LabelInset * context.Density;

        if (barRect.Width >= MeasureLabelText(text, sizePx) + (2f * inset)) {
            DrawLabelText(context, text, barRect.MidX, barRect.MidY, SKTextAlign.Center, sizePx, labelColor, null, barColor);
            return;
        }

        var isPositive = value >= 0;
        var background = GetLabelBackground(barColor);
        var anchorX = isPositive ? barRect.Right + inset : barRect.Left - inset;
        DrawLabelText(context, text, anchorX, barRect.MidY, isPositive ? SKTextAlign.Left : SKTextAlign.Right, sizePx, labelColor, background, background, renderArea);
    }

    private List<SKRect> GetBarRects(IReadOnlyList<TData> dataList, SKRect renderArea, double xMin, double xMax, decimal yMin, decimal yMax, decimal yBase, double xBase, NTAxisScale yScale, float animationFactor) {
        if (dataList.Count == 0) {
            return [];
        }

        var (seriesCount, seriesIndex) = GetVisibleBarSeriesLayout();
        var key = new BarGeometryKey(renderArea, xMin, xMax, yMin, yMax, yBase, xBase, yScale, Orientation, animationFactor, Chart.Density, seriesCount, seriesIndex, YValueSelector, XValue);
        if (_cachedBarGeometryKey == key) {
            return _cachedBarRects;
        }

        _cachedBarGeometryKey = null;
        var rects = _cachedBarRects;
        rects.Clear();
        rects.EnsureCapacity(dataList.Count);
        _cachedBarValues.Clear();
        var values = SegmentSelector is null ? _cachedBarValues : null;

        if (Orientation == NTChartOrientation.Vertical) {
            var count = dataList.Count;
            var slotWidth = renderArea.Width / Math.Max(1, count);
            var spacing = Math.Max(1f * Chart.Density, slotWidth * 0.08f);
            var clusterWidth = Math.Max(2f, slotWidth - spacing);
            var interSeriesGap = seriesCount > 1 ? Math.Min(2f * Chart.Density, clusterWidth * 0.04f) : 0f;
            var totalGap = interSeriesGap * Math.Max(0, seriesCount - 1);
            var barWidth = Math.Max(1f, (clusterWidth - totalGap) / seriesCount);
            var baseY = ScaleYFast(yBase, yMin, yMax, yScale, renderArea);

            for (var i = 0; i < count; i++) {
                var item = dataList[i];
                var xValue = Chart.GetScaledXValue(XValue.Invoke(item));
                var barX = Chart.ScaleX(xValue, renderArea);
                var yValue = GetBarTotalValue(item);
                values?.Add(yValue);
                var animatedValue = yBase + ((yValue - yBase) * (decimal)animationFactor);
                var y = ScaleYFast(animatedValue, yMin, yMax, yScale, renderArea);
                var clusterLeft = barX - (clusterWidth / 2f);
                var left = clusterLeft + (seriesIndex * (barWidth + interSeriesGap));
                var right = left + barWidth;

                var top = Math.Min(y, baseY);
                var bottom = Math.Max(y, baseY);
                rects.Add(new SKRect(left, top, right, bottom));
            }

            _cachedBarGeometryKey = key;
            return rects;
        }

        // Horizontal bars: value maps to X, category/index maps to Y.
        var horizontalCount = dataList.Count;
        var hSlot = renderArea.Height / Math.Max(1, horizontalCount);
        var spacingY = Math.Max(1f * Chart.Density, hSlot * 0.08f);
        var clusterHeight = Math.Max(2f, hSlot - spacingY);
        var interSeriesGapY = seriesCount > 1 ? Math.Min(2f * Chart.Density, clusterHeight * 0.04f) : 0f;
        var totalGapY = interSeriesGapY * Math.Max(0, seriesCount - 1);
        var barHeight = Math.Max(1f, (clusterHeight - totalGapY) / seriesCount);
        var baseX = Chart.ScaleX(xBase, renderArea);

        for (var i = 0; i < horizontalCount; i++) {
            var item = dataList[i];
            var scaledY = (decimal)i;

            var centerY = Chart.ScaleY(scaledY, renderArea);
            var xValue = (double)GetBarTotalValue(item);
            values?.Add((decimal)xValue);
            var animatedXValue = xBase + ((xValue - xBase) * animationFactor);
            var valueX = Chart.ScaleX(animatedXValue, renderArea);
            var clusterTop = centerY - (clusterHeight / 2f);
            var top = clusterTop + (seriesIndex * (barHeight + interSeriesGapY));
            var bottom = top + barHeight;

            var left = Math.Min(baseX, valueX);
            var right = Math.Max(baseX, valueX);
            rects.Add(new SKRect(left, top, right, bottom));
        }

        _cachedBarGeometryKey = key;
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

    internal override TooltipInfo GetTooltipInfo(TData data) {
        var baseInfo = base.GetTooltipInfo(data);
        if (SegmentSelector is null || Chart.HoveredSeries != this || Chart.HoveredPointIndex is null) {
            return baseInfo;
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

            ClearBarCaches();
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

        if (Data is null) {
            return base.GetLegendItems();
        }

        var segmentItems = new Dictionary<string, LegendItemInfo<TData>>(StringComparer.OrdinalIgnoreCase);
        var seriesColor = Chart.GetSeriesColor(this);

        var dataIndex = -1;
        foreach (var item in Data) {
            dataIndex++;
            var segments = GetBarEntry(item).Segments ?? [];
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

    private decimal GetBarTotalValue(TData item) => GetBarEntry(item).Total;

    private List<NTBarSegment> GetPointSegments(TData item) => GetBarEntry(item).DrawnSegments ?? [];

    /// <summary>Evaluates the value or segment selector once per item; the total sums visible segments and the drawn list keeps the visible non-zero ones.</summary>
    private BarEntry GetBarEntry(TData item) {
        if (_barEntries.TryGetValue(item, out var entry)) {
            return entry;
        }

        if (SegmentSelector is null) {
            entry = new BarEntry(YValueSelector(item), null, null);
        }
        else {
            var segments = SegmentSelector(item)?.ToArray() ?? [];
            var total = 0m;
            var drawn = new List<NTBarSegment>(segments.Length);
            foreach (var segment in segments) {
                if (!IsSegmentVisible(segment)) {
                    continue;
                }

                total += segment.Value;
                if (Math.Abs(segment.Value) > 0m) {
                    drawn.Add(segment);
                }
            }
            entry = new BarEntry(total, segments, drawn);
        }

        _barEntries.Add(item, entry);
        return entry;
    }

    private static decimal SumMagnitudes(IReadOnlyList<NTBarSegment> segments) {
        var total = 0m;
        for (var i = 0; i < segments.Count; i++) {
            total += Math.Abs(segments[i].Value);
        }

        return total;
    }

    private Func<NTColor, SKColor> GetThemeColorSelector() {
        if (!ReferenceEquals(_themeColorChart, Chart)) {
            _themeColorChart = Chart;
            _themeColorSelector = Chart.GetThemeColor;
        }

        return _themeColorSelector!;
    }

    private void DrawSegmentedBar(NTRenderContext context, SKRect renderArea, SKRect rect, TData data, int dataIndex, decimal totalValue, IReadOnlyList<NTBarSegment> segments, SKColor fallbackColor, bool isPointHovered, float pointAlphaFactor) {
        if (rect.Width <= 0 || rect.Height <= 0 || segments.Count == 0) {
            return;
        }

        if (Orientation == NTChartOrientation.Vertical) {
            var totalMagnitude = SumMagnitudes(segments);
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
                DrawBar(context.Canvas, segmentRect, isSegmentHovered ? _highlightPaint : _barPaint, context.Density, GetSegmentEnd(totalValue, segmentIndex, segments.Count), isSegmentHovered ? GetStateLayerColor(segmentColor) : null);
                _lastBarRects.Add((segmentRect, dataIndex, segmentIndex, data, segment.Label, segment.Value, segmentColor));
                if (ShowDataLabels) {
                    DrawSegmentDataLabel(context, segmentRect, segment.Value, segmentColor, isHorizontal: false);
                }
            }

            if (ShowDataLabels) {
                DrawSegmentTotalLabel(context, renderArea, rect, totalValue, fallbackColor, isHorizontal: false);
            }

            return;
        }

        var totalWidthMagnitude = SumMagnitudes(segments);
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
            DrawBar(context.Canvas, segmentRect, isSegmentHovered ? _highlightPaint : _barPaint, context.Density, GetSegmentEnd(totalValue, segmentIndex, segments.Count), isSegmentHovered ? GetStateLayerColor(segmentColor) : null);
            _lastBarRects.Add((segmentRect, dataIndex, segmentIndex, data, segment.Label, segment.Value, segmentColor));
            if (ShowDataLabels) {
                DrawSegmentDataLabel(context, segmentRect, segment.Value, segmentColor, isHorizontal: true);
            }
        }

        if (ShowDataLabels) {
            DrawSegmentTotalLabel(context, renderArea, rect, totalValue, fallbackColor, isHorizontal: true);
        }
    }

    private void DrawSegmentDataLabel(NTRenderContext context, SKRect segmentRect, decimal value, SKColor segmentColor, bool isHorizontal) {
        var text = FormatDataLabel(value);
        var sizePx = DataLabelSize * context.Density;
        var inset = LabelInset * context.Density;
        var minHeight = isHorizontal ? NTRenderContextExtensions.LabelLineHeight(sizePx) : NTRenderContextExtensions.LabelBoxHeight(sizePx, context.Density);
        if (segmentRect.Width < MeasureLabelText(text, sizePx) + (2f * inset) || segmentRect.Height < minHeight) {
            return;
        }

        DrawLabelText(context, text, segmentRect.MidX, segmentRect.MidY, SKTextAlign.Center, sizePx, null, null, segmentColor);
    }

    private void DrawSegmentTotalLabel(NTRenderContext context, SKRect renderArea, SKRect barRect, decimal value, SKColor accentColor, bool isHorizontal) {
        var text = FormatDataLabel(value);
        var sizePx = DataLabelSize * context.Density;
        var inset = LabelInset * context.Density;
        var background = GetLabelBackground(accentColor);

        if (isHorizontal) {
            DrawLabelText(context, text, barRect.Right + inset, barRect.MidY, SKTextAlign.Left, sizePx, null, background, background, renderArea);
            return;
        }

        var boxHeight = NTRenderContextExtensions.LabelBoxHeight(sizePx, context.Density);
        DrawLabelText(context, text, barRect.MidX, Math.Max(barRect.Top - inset - (boxHeight / 2f), renderArea.Top + (boxHeight / 2f)), SKTextAlign.Center, sizePx, null, background, background, renderArea);
    }

    private SKColor ResolveSegmentColor(TData data, int dataIndex, NTBarSegment segment, int segmentIndex, SKColor fallbackColor) {
        if (segment.CustomColor.HasValue) {
            return segment.CustomColor.Value;
        }

        if (segment.Color.HasValue) {
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
