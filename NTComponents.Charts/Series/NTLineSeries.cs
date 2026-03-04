using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using NTComponents.Charts.Core.Axes;
using NTComponents.Charts.Core;
using NTComponents.Charts.Core.Series;
using SkiaSharp;

namespace NTComponents.Charts;

/// <summary>
///     Represents a line series in a cartesian chart.
/// </summary>
public class NTLineSeries<TData> : NTCartesianSeries<TData> where TData : class {
    private const int EdgeCullBufferTicks = 2;
    private const float EdgeCullBufferPixels = 16f;

    private readonly record struct RenderPointInfo(SKPoint Point, TData Data, int Index, decimal Value);

    private readonly record struct RenderCacheKey(
        double XMin,
        double XMax,
        float Left,
        float Top,
        float Right,
        float Bottom,
        float Density,
        float Progress,
        float Visibility,
        bool Aggregated,
        int AggregationThreshold,
        AggregationMode AggregationMode,
        NTDateGroupingLevel DateGroupingLevel,
        bool UseSecondaryAxis,
        decimal YMin,
        decimal YMax,
        NTAxisScale YScale,
        LineInterpolation Interpolation);

    /// <summary>
    ///     Gets or sets the width of the line.
    /// </summary>
    [Parameter]
    public float StrokeWidth { get; set; } = 2.0f;

    /// <summary>
    ///    Gets or sets the style of the line.
    /// </summary>
    [Parameter]
    public LineStyle LineStyle { get; set; } = LineStyle.Solid;

    /// <summary>
    ///    Gets or sets the interpolation type for the line.
    /// </summary>
    [Parameter]
    public LineInterpolation Interpolation { get; set; } = LineInterpolation.Straight;

    /// <summary>
    ///    Gets or sets whether data aggregation is enabled when zoomed out.
    /// </summary>
    [Parameter]
    public bool EnableAggregation { get; set; }

    /// <summary>
    ///    Gets or sets the threshold (number of points) above which aggregation is triggered.
    /// </summary>
    [Parameter]
    public int AggregationThreshold { get; set; } = 500;

    /// <summary>
    ///    Gets or sets the aggregation mode.
    /// </summary>
    [Parameter]
    public AggregationMode AggregationMode { get; set; } = AggregationMode.Average;

    /// <summary>
    ///    Gets or sets the callback invoked when a grouped date point (year/month) is clicked.
    /// </summary>
    [Parameter]
    public EventCallback<NTSeriesDateGroupClickEventArgs<TData>> OnDateGroupClick { get; set; }

    /// <summary>
    ///    Gets or sets whether clicking a grouped date point (year/month) zooms the X axis to that group range.
    /// </summary>
    [Parameter]
    public bool ZoomToDateGroupOnClick { get; set; } = true;

    private SKPaint? _linePaint;
    private SKPaint? _segmentPaint;
    private SKPath? _linePath;
    private RenderCacheKey? _linePathKey;
    private SKPaint? _hitTestPaint;
    private SKPath? _hitTestPath;
    private RenderCacheKey? _hitTestPathKey;
    private SKPath? _hitTestStrokePath;

    private List<RenderPointInfo>? _cachedRenderPoints;
    private RenderCacheKey? _cachedRenderKey;
    private (double MinX, double MaxX, NTDateGroupingLevel Level, AggregationMode Mode, decimal MinY, decimal MaxY)? _cachedGroupedRange;

    protected override void Dispose(bool disposing) {
        if (disposing) {
            _linePaint?.Dispose();
            _segmentPaint?.Dispose();
            _linePath?.Dispose();
            _hitTestPaint?.Dispose();
            _hitTestPath?.Dispose();
            _hitTestStrokePath?.Dispose();
        }
        base.Dispose(disposing);
    }

    protected override void OnDataChanged() {
        _cachedRenderPoints = null;
        _cachedRenderKey = null;
        _linePathKey = null;
        _hitTestPathKey = null;
        _cachedGroupedRange = null;
        base.OnDataChanged();
    }

    public override void HandleMouseWheel(WheelEventArgs e) {
        base.HandleMouseWheel(e);

        // Keep Y fitted to current X zoom window on every X zoom event.
        // This preserves visibility for aggregation modes like Sum where magnitude changes with bucket size.
        if (!Interactions.HasFlag(ChartInteractions.XZoom) || _isPanning) {
            return;
        }

        var viewXRange = GetViewXRange();
        if (!viewXRange.HasValue) {
            return;
        }

        var yRange = GetYRange(viewXRange.Value.Min, viewXRange.Value.Max);
        if (!yRange.HasValue) {
            return;
        }

        var min = yRange.Value.Min;
        var max = yRange.Value.Max;
        if (max <= min) {
            var delta = Math.Abs(min) > 1m ? Math.Abs(min * 0.01m) : 1m;
            min -= delta;
            max += delta;
        }

        var paddingRatio = (decimal)Math.Max(0, Chart.RangePadding);
        var padding = (max - min) * paddingRatio;
        var paddedMin = min - padding;
        var paddedMax = max + padding;

        if (TryGetZoomAnchorY(e, viewXRange.Value, out var anchorY)) {
            var halfSpan = Math.Max(paddedMax - anchorY, anchorY - paddedMin);
            if (halfSpan <= 0) {
                halfSpan = Math.Abs(anchorY) > 1m ? Math.Abs(anchorY * 0.01m) : 1m;
            }

            _viewYMin = anchorY - halfSpan;
            _viewYMax = anchorY + halfSpan;
            return;
        }

        _viewYMin = paddedMin;
        _viewYMax = paddedMax;
    }

    internal bool HandleDateGroupClick(NTSeriesClickEventArgs<TData> clickArgs, out bool suppressPointClick) {
        suppressPointClick = false;
        if (clickArgs.DataPoint is null || !Chart.IsXAxisDateTime || !Chart.XAxis.EnableAutoDateGrouping) {
            return false;
        }

        var plotWidth = Chart.LastPlotArea.Width > 0f ? Chart.LastPlotArea.Width : 1200f;
        var density = Math.Max(0.1f, Chart.Density);
        var (xMin, xMax) = Chart.GetXRange(Chart.XAxis as NTAxisOptions<TData>, true);
        var groupingLevel = Chart.XAxis.ResolveDateGroupingLevel(xMin, xMax, plotWidth, density);
        if (groupingLevel is not (NTDateGroupingLevel.Year or NTDateGroupingLevel.Month)) {
            return false;
        }

        if (!TryResolveDate(clickArgs.DataPoint, out var clickedDate)) {
            return false;
        }

        var (rangeStart, rangeEnd) = GetDateGroupRange(clickedDate, groupingLevel);
        suppressPointClick = OnDateGroupClick.HasDelegate || ZoomToDateGroupOnClick;
        var zoomApplied = false;
        if (ZoomToDateGroupOnClick) {
            _viewXMin = rangeStart.Ticks;
            _viewXMax = rangeEnd.Ticks;
            // Let chart Y axis re-fit to the newly selected X window.
            _viewYMin = null;
            _viewYMax = null;
            zoomApplied = true;
        }

        if (OnDateGroupClick.HasDelegate) {
            var args = new NTSeriesDateGroupClickEventArgs<TData> {
                Series = this,
                PointIndex = clickArgs.PointIndex,
                DataPoint = clickArgs.DataPoint,
                ClickedDate = clickedDate,
                GroupDate = rangeStart,
                GroupingLevel = groupingLevel,
                RangeStart = rangeStart,
                RangeEnd = rangeEnd,
                ZoomApplied = zoomApplied,
                PointerPosition = clickArgs.PointerPosition,
                MouseEvent = clickArgs.MouseEvent
            };

            _ = InvokeAsync(() => OnDateGroupClick.InvokeAsync(args));
        }

        return zoomApplied;
    }

    private bool TryGetZoomAnchorY(WheelEventArgs e, (double Min, double Max) viewXRange, out decimal anchorY) {
        anchorY = 0m;
        if (Chart.LastPlotArea == default) {
            return false;
        }

        var pointer = new SKPoint((float)e.OffsetX * Chart.Density, (float)e.OffsetY * Chart.Density);
        if (!Chart.LastPlotArea.Contains(pointer)) {
            return false;
        }

        var xValue = Chart.ScaleXInverse(pointer.X, Chart.LastPlotArea);
        return TryGetRepresentativeYAtX(xValue, viewXRange.Min, viewXRange.Max, out anchorY);
    }

    private bool TryResolveDate(TData dataPoint, out DateTime date) {
        var raw = XValue.Invoke(dataPoint);
        if (raw is DateTime dt) {
            date = dt;
            return true;
        }

        if (raw is DateTimeOffset dto) {
            date = dto.DateTime;
            return true;
        }

        if (raw is DateOnly dateOnly) {
            date = dateOnly.ToDateTime(TimeOnly.MinValue);
            return true;
        }

        date = default;
        return false;
    }

    private static (DateTime Start, DateTime End) GetDateGroupRange(DateTime date, NTDateGroupingLevel groupingLevel) {
        if (groupingLevel == NTDateGroupingLevel.Year) {
            var start = new DateTime(date.Year, 1, 1, 0, 0, 0, date.Kind);
            var end = date.Year == DateTime.MaxValue.Year ? DateTime.MaxValue : start.AddYears(1).AddTicks(-1);
            return (start, end);
        }

        var monthStart = new DateTime(date.Year, date.Month, 1, 0, 0, 0, date.Kind);
        var monthEnd = (date.Year == DateTime.MaxValue.Year && date.Month == 12)
            ? DateTime.MaxValue
            : monthStart.AddMonths(1).AddTicks(-1);
        return (monthStart, monthEnd);
    }

    private bool TryGetRepresentativeYAtX(double xValue, double xMin, double xMax, out decimal yValue) {
        var plotWidth = Chart is NTChart<TData> ntChart && ntChart.LastPlotArea.Width > 0f
            ? ntChart.LastPlotArea.Width
            : 1200f;
        var density = Math.Max(0.1f, Chart.Density);
        var axisDateGroupingLevel = Chart.XAxis.ResolveDateGroupingLevel(xMin, xMax, plotWidth, density);
        var useAxisDateGrouping = Chart.IsXAxisDateTime
            && Chart.XAxis.EnableAutoDateGrouping
            && axisDateGroupingLevel is NTDateGroupingLevel.Year or NTDateGroupingLevel.Month;

        var (cullMinX, cullMaxX) = ExpandCullRange(xMin, xMax, plotWidth, density, useAxisDateGrouping, axisDateGroupingLevel);
        var window = useAxisDateGrouping
            ? GetVisibleWindowIncludingGroupedBuckets(cullMinX, cullMaxX, axisDateGroupingLevel)
            : GetVisibleWindow(cullMinX, cullMaxX);
        if (window.Count == 0) {
            yValue = 0m;
            return false;
        }

        if (useAxisDateGrouping && TryGetBucketAggregatedY(window, GetDateBucketKey(xValue, axisDateGroupingLevel), xValue, axisDateGroupingLevel, out yValue)) {
            return true;
        }

        var idx = LowerBoundVisible(window, xValue);
        var chosen = 0;
        if (idx <= 0) {
            chosen = 0;
        }
        else if (idx >= window.Count) {
            chosen = window.Count - 1;
        }
        else {
            chosen = Math.Abs(window[idx].X - xValue) < Math.Abs(window[idx - 1].X - xValue) ? idx : idx - 1;
        }
        yValue = YValueSelector(window[chosen].Data);
        return true;
    }

    private bool TryGetBucketAggregatedY(List<VisiblePoint> window, long bucketKey, double xValue, NTDateGroupingLevel groupingLevel, out decimal aggregatedY) {
        var hasExactBucket = false;
        long nearestBucket = 0;
        var nearestDistance = long.MaxValue;

        foreach (var point in window) {
            var bucket = GetDateBucketKey(point.X, groupingLevel);
            if (bucket == bucketKey) {
                hasExactBucket = true;
                break;
            }

            var dist = Math.Abs(bucket - bucketKey);
            if (dist < nearestDistance) {
                nearestDistance = dist;
                nearestBucket = bucket;
            }
        }

        var selectedBucket = hasExactBucket ? bucketKey : nearestBucket;
        if (!hasExactBucket && nearestDistance == long.MaxValue) {
            aggregatedY = 0m;
            return false;
        }

        var mode = AggregationMode;
        var count = 0;
        var sum = 0m;
        var min = decimal.MaxValue;
        var max = decimal.MinValue;
        var representativeValue = 0m;
        var representativeDistance = double.MaxValue;
        var medianValues = mode == AggregationMode.Median ? new List<decimal>(32) : null;

        foreach (var point in window) {
            if (GetDateBucketKey(point.X, groupingLevel) != selectedBucket) {
                continue;
            }

            var value = YValueSelector(point.Data);
            count++;
            sum += value;
            if (value < min) {
                min = value;
            }
            if (value > max) {
                max = value;
            }

            var distance = Math.Abs(point.X - xValue);
            if (distance < representativeDistance) {
                representativeDistance = distance;
                representativeValue = value;
            }

            medianValues?.Add(value);
        }

        if (count == 0) {
            aggregatedY = 0m;
            return false;
        }

        aggregatedY = mode switch {
            AggregationMode.Sum => sum,
            AggregationMode.Min => min,
            AggregationMode.Max => max,
            AggregationMode.Median => CalculateMedian(medianValues!),
            AggregationMode.None => representativeValue,
            _ => sum / count
        };
        return true;
    }

    private static int LowerBoundVisible(List<VisiblePoint> values, double target) {
        var lo = 0;
        var hi = values.Count;
        while (lo < hi) {
            var mid = lo + ((hi - lo) / 2);
            if (values[mid].X < target) {
                lo = mid + 1;
            }
            else {
                hi = mid;
            }
        }
        return lo;
    }

    public override (decimal Min, decimal Max)? GetYRange(double? xMin = null, double? xMax = null) {
        if (!Chart.IsXAxisDateTime || !Chart.XAxis.EnableAutoDateGrouping) {
            return base.GetYRange(xMin, xMax);
        }

        var xRange = (xMin.HasValue && xMax.HasValue)
            ? (Math.Min(xMin.Value, xMax.Value), Math.Max(xMin.Value, xMax.Value))
            : GetXRange();
        if (!xRange.HasValue) {
            return base.GetYRange(xMin, xMax);
        }

        var plotWidth = Chart is NTChart<TData> ntChart && ntChart.LastPlotArea.Width > 0f
            ? ntChart.LastPlotArea.Width
            : 1200f;
        var level = Chart.XAxis.ResolveDateGroupingLevel(xRange.Value.Min, xRange.Value.Max, plotWidth, Math.Max(0.1f, Chart.Density));
        if (level is not (NTDateGroupingLevel.Year or NTDateGroupingLevel.Month)) {
            return base.GetYRange(xMin, xMax);
        }

        if (_cachedGroupedRange.HasValue &&
            Math.Abs(_cachedGroupedRange.Value.MinX - xRange.Value.Min) < 0.000001 &&
            Math.Abs(_cachedGroupedRange.Value.MaxX - xRange.Value.Max) < 0.000001 &&
            _cachedGroupedRange.Value.Level == level &&
            _cachedGroupedRange.Value.Mode == AggregationMode) {
            return (_cachedGroupedRange.Value.MinY, _cachedGroupedRange.Value.MaxY);
        }

        var window = GetVisibleWindowIncludingGroupedBuckets(xRange.Value.Min, xRange.Value.Max, level);
        if (window.Count == 0) {
            return base.GetYRange(xMin, xMax);
        }

        var groupedRange = GetGroupedDateYRangeForWindow(window, level);
        if (!groupedRange.HasValue) {
            return base.GetYRange(xMin, xMax);
        }

        _cachedGroupedRange = (xRange.Value.Min, xRange.Value.Max, level, AggregationMode, groupedRange.Value.Min, groupedRange.Value.Max);
        return groupedRange.Value;
    }

    private (decimal Min, decimal Max)? GetGroupedDateYRangeForWindow(List<VisiblePoint> window, NTDateGroupingLevel groupingLevel) {
        if (window.Count == 0) {
            return null;
        }

        var mode = AggregationMode;
        var medianValues = mode == AggregationMode.Median ? new List<decimal>(32) : null;

        decimal rangeMin = decimal.MaxValue;
        decimal rangeMax = decimal.MinValue;
        var currentBucket = GetDateBucketKey(window[0].X, groupingLevel);

        var count = 0;
        var sum = 0m;
        var bucketMin = decimal.MaxValue;
        var bucketMax = decimal.MinValue;
        var representativeValue = 0m;

        void ResetBucketState(VisiblePoint seed) {
            count = 0;
            sum = 0m;
            bucketMin = decimal.MaxValue;
            bucketMax = decimal.MinValue;
            representativeValue = YValueSelector(seed.Data);
            medianValues?.Clear();
        }

        void Accumulate(VisiblePoint item) {
            var value = YValueSelector(item.Data);
            count++;
            sum += value;
            if (value < bucketMin) {
                bucketMin = value;
            }
            if (value > bucketMax) {
                bucketMax = value;
            }
            if ((count & 1) == 1) {
                representativeValue = value;
            }
            medianValues?.Add(value);
        }

        void FlushBucket() {
            if (count == 0) {
                return;
            }

            var aggregated = mode switch {
                AggregationMode.Sum => sum,
                AggregationMode.Min => bucketMin,
                AggregationMode.Max => bucketMax,
                AggregationMode.Median => CalculateMedian(medianValues!),
                AggregationMode.None => representativeValue,
                _ => sum / count
            };

            rangeMin = Math.Min(rangeMin, aggregated);
            rangeMax = Math.Max(rangeMax, aggregated);
        }

        ResetBucketState(window[0]);
        foreach (var point in window) {
            var bucket = GetDateBucketKey(point.X, groupingLevel);
            if (bucket != currentBucket) {
                FlushBucket();
                currentBucket = bucket;
                ResetBucketState(point);
            }

            Accumulate(point);
        }

        FlushBucket();
        if (rangeMin == decimal.MaxValue || rangeMax == decimal.MinValue) {
            return null;
        }

        return (rangeMin, rangeMax);
    }

    /// <inheritdoc />
    public override SKRect Render(NTRenderContext context, SKRect renderArea) {
        if (Data?.Any() != true) {
            return renderArea;
        }

        var yAxis = (UseSecondaryYAxis ? Chart.SecondaryYAxis : Chart.YAxis) as NTAxisOptions<TData>;
        var (xMin, xMax) = Chart.GetXRange(Chart.XAxis as NTAxisOptions<TData>, true);
        var points = GetRenderPoints(renderArea, xMin, xMax, yAxis, context.Density, out var renderKey);

        if (points.Count == 0) {
            return renderArea;
        }

        var canvas = context.Canvas;
        var color = Chart.GetSeriesColor(this);
        var alphaFactor = HoverFactor * VisibilityFactor;
        color = color.WithAlpha((byte)(color.Alpha * alphaFactor));

        _linePaint ??= new SKPaint {
            Style = SKPaintStyle.Stroke,
            IsAntialias = true,
            StrokeCap = SKStrokeCap.Round,
            StrokeJoin = SKStrokeJoin.Round
        };
        _linePaint.Color = color;
        _linePaint.StrokeWidth = StrokeWidth * context.Density;
        _linePaint.PathEffect = LineStyle == LineStyle.Dashed
            ? SKPathEffect.CreateDash([10 * context.Density, 5 * context.Density], 0)
            : null;

        if (LineStyle != LineStyle.None && points.Count > 1) {
            if (OnDataPointRender == null) {
                if (_linePath is null || !_linePathKey.HasValue || !_linePathKey.Value.Equals(renderKey)) {
                    _linePath?.Dispose();
                    _linePath = BuildPath(points.Select(p => p.Point).ToList());
                    _linePathKey = renderKey;
                }
                canvas.DrawPath(_linePath, _linePaint);
            }
            else {
                for (var i = 1; i < points.Count; i++) {
                    var args = new NTDataPointRenderArgs<TData> {
                        Data = points[i].Data,
                        Index = points[i].Index,
                        Color = color,
                        LineStyle = LineStyle,
                        StrokeWidth = StrokeWidth,
                        GetThemeColor = Chart.GetThemeColor
                    };
                    OnDataPointRender.Invoke(args);

                    var segmentColor = args.Color ?? color;
                    var segmentStyle = args.LineStyle ?? LineStyle;
                    var segmentWidth = (args.StrokeWidth ?? StrokeWidth) * context.Density;

                    if (segmentStyle == LineStyle.None) {
                        continue;
                    }

                    _segmentPaint ??= new SKPaint {
                        Style = SKPaintStyle.Stroke,
                        IsAntialias = true,
                        StrokeCap = SKStrokeCap.Round,
                        StrokeJoin = SKStrokeJoin.Round
                    };
                    _segmentPaint.Color = segmentColor;
                    _segmentPaint.StrokeWidth = segmentWidth;
                    _segmentPaint.PathEffect = segmentStyle == LineStyle.Dashed
                        ? SKPathEffect.CreateDash([10 * context.Density, 5 * context.Density], 0)
                        : null;

                    canvas.DrawLine(points[i - 1].Point, points[i].Point, _segmentPaint);
                }
            }
        }

        var renderPointMarkers = PointStyle != PointStyle.None && points.Count <= 600;
        if (renderPointMarkers || ShowDataLabels || Chart.HoveredSeries == this) {
            for (var i = 0; i < points.Count; i++) {
                var rp = points[i];
                var args = new NTDataPointRenderArgs<TData> {
                    Data = rp.Data,
                    Index = rp.Index,
                    Color = color,
                    PointSize = PointSize,
                    PointShape = PointShape,
                    GetThemeColor = Chart.GetThemeColor
                };
                OnDataPointRender?.Invoke(args);

                var isPointHovered = Chart.HoveredSeries == this && Chart.HoveredPointIndex == rp.Index;
                var pointColor = args.Color ?? color;
                var pointStrokeColor = args.StrokeColor ?? pointColor;
                var currentPointSize = (args.PointSize ?? PointSize) * context.Density;
                var currentPointShape = args.PointShape ?? PointShape;

                if (isPointHovered) {
                    pointColor = pointColor.WithAlpha(255);
                    pointStrokeColor = pointStrokeColor.WithAlpha(255);
                    currentPointSize *= 1.5f;
                }

                if (renderPointMarkers) {
                    RenderPoint(context, rp.Point.X, rp.Point.Y, pointColor, currentPointSize / context.Density, currentPointShape, pointStrokeColor);
                }

                if (ShowDataLabels || isPointHovered) {
                    var labelColor = args.DataLabelColor;
                    var labelSize = args.DataLabelSize ?? DataLabelSize;
                    RenderDataLabel(context, rp.Point.X, rp.Point.Y, rp.Value, renderArea, labelColor, labelSize);
                }
            }
        }

        return renderArea;
    }

    protected List<SKPoint> GetPoints(SKRect renderArea, double xMin, double xMax, decimal yMin, decimal yMax) {
        var points = GetRenderPoints(renderArea, xMin, xMax, Chart.YAxis as NTAxisOptions<TData>, Chart.Density, out _);
        return points.Select(p => p.Point).ToList();
    }

    private List<RenderPointInfo> GetRenderPoints(SKRect renderArea, double xMin, double xMax, NTAxisOptions<TData>? yAxis, float density, out RenderCacheKey key) {
        var (yMin, yMax) = Chart.GetYRange(yAxis, true);
        var yScale = yAxis?.Scale ?? NTAxisScale.Linear;
        var progress = GetAnimationProgress();
        var visibility = VisibilityFactor;
        var xAxis = Chart.XAxis;
        var axisDateGroupingLevel = xAxis.ResolveDateGroupingLevel(xMin, xMax, renderArea.Width, density);
        var useAxisDateGrouping = Chart.IsXAxisDateTime
            && xAxis.EnableAutoDateGrouping
            && axisDateGroupingLevel is NTDateGroupingLevel.Year or NTDateGroupingLevel.Month;
        var shouldAggregate = (EnableAggregation || useAxisDateGrouping) && AggregationThreshold > 1;
        var pixelCapacity = GetPixelCapacity(renderArea);
        var effectiveAggregationThreshold = Math.Max(AggregationThreshold, pixelCapacity);

        key = new RenderCacheKey(
            Math.Round(xMin, 6),
            Math.Round(xMax, 6),
            renderArea.Left,
            renderArea.Top,
            renderArea.Right,
            renderArea.Bottom,
            density,
            (float)Math.Round(progress, 3),
            (float)Math.Round(visibility, 3),
            shouldAggregate,
            AggregationThreshold,
            AggregationMode,
            axisDateGroupingLevel,
            UseSecondaryYAxis,
            decimal.Round(yMin, 6),
            decimal.Round(yMax, 6),
            yScale,
            Interpolation);

        if (_cachedRenderPoints is not null && _cachedRenderKey.HasValue && _cachedRenderKey.Value.Equals(key)) {
            return _cachedRenderPoints;
        }

        var (cullMinX, cullMaxX) = ExpandCullRange(xMin, xMax, renderArea.Width, density, useAxisDateGrouping, axisDateGroupingLevel);
        var visibleData = useAxisDateGrouping
            ? GetVisibleWindowIncludingGroupedBuckets(cullMinX, cullMaxX, axisDateGroupingLevel)
            : GetVisibleWindow(cullMinX, cullMaxX);
        if (visibleData.Count == 0) {
            _cachedRenderPoints = [];
            _cachedRenderKey = key;
            return _cachedRenderPoints;
        }

        var easedProgress = (decimal)BackEase(progress);
        var vFactor = (decimal)visibility;

        if (useAxisDateGrouping) {
            _cachedRenderPoints = GetAxisGroupedDatePoints(visibleData, renderArea, xMin, xMax, yMin, yMax, yScale, easedProgress, vFactor, axisDateGroupingLevel);
        }
        else if (shouldAggregate && visibleData.Count > effectiveAggregationThreshold) {
            _cachedRenderPoints = GetAggregatedPoints(visibleData, renderArea, xMin, xMax, yMin, yMax, yScale, easedProgress, vFactor, pixelCapacity);
        }
        else {
            var points = new List<RenderPointInfo>(visibleData.Count);
            foreach (var item in visibleData) {
                var targetY = YValueSelector(item.Data);
                var animatedY = (targetY * easedProgress) * vFactor * vFactor;

                var screenX = ScaleXFast(item.X, xMin, xMax, renderArea);
                var screenY = ScaleYFast(animatedY, yMin, yMax, yScale, renderArea);
                points.Add(new RenderPointInfo(new SKPoint(screenX, screenY), item.Data, item.Index, targetY));
            }

            _cachedRenderPoints = points;
        }

        _cachedRenderKey = key;
        return _cachedRenderPoints;
    }

    private List<RenderPointInfo> GetAxisGroupedDatePoints(
        List<VisiblePoint> visibleData,
        SKRect renderArea,
        double xMin,
        double xMax,
        decimal yMin,
        decimal yMax,
        NTAxisScale yScale,
        decimal easedProgress,
        decimal vFactor,
        NTDateGroupingLevel groupingLevel) {

        if (visibleData.Count == 0) {
            return [];
        }

        var points = new List<RenderPointInfo>(Math.Min(visibleData.Count, 1024));
        var mode = AggregationMode;
        var useMedian = mode == AggregationMode.Median;
        var medianValues = useMedian ? new List<decimal>(32) : null;

        var currentBucket = GetDateBucketKey(visibleData[0].X, groupingLevel);
        var count = 0;
        var sum = 0m;
        var min = decimal.MaxValue;
        var max = decimal.MinValue;
        var representative = visibleData[0];
        var minPoint = visibleData[0];
        var maxPoint = visibleData[0];
        var cullBufferData = GetCullBufferDataUnits(xMin, xMax, renderArea.Width, Chart.Density, true, groupingLevel);

        void ResetBucketState(VisiblePoint seed) {
            count = 0;
            sum = 0m;
            min = decimal.MaxValue;
            max = decimal.MinValue;
            representative = seed;
            minPoint = seed;
            maxPoint = seed;
            medianValues?.Clear();
        }

        void Accumulate(VisiblePoint item) {
            var value = YValueSelector(item.Data);
            count++;
            sum += value;

            if (value < min) {
                min = value;
                minPoint = item;
            }
            if (value > max) {
                max = value;
                maxPoint = item;
            }

            if ((count & 1) == 1) {
                representative = item;
            }

            medianValues?.Add(value);
        }

        void FlushBucket() {
            if (count == 0) {
                return;
            }

            decimal aggregatedY;
            VisiblePoint selected;

            if (mode == AggregationMode.Sum) {
                aggregatedY = sum;
                selected = representative;
            }
            else if (mode == AggregationMode.Min) {
                aggregatedY = min;
                selected = minPoint;
            }
            else if (mode == AggregationMode.Max) {
                aggregatedY = max;
                selected = maxPoint;
            }
            else if (mode == AggregationMode.Median) {
                aggregatedY = CalculateMedian(medianValues!);
                selected = representative;
            }
            else if (mode == AggregationMode.None) {
                aggregatedY = YValueSelector(representative.Data);
                selected = representative;
            }
            else {
                aggregatedY = sum / count;
                selected = representative;
            }

            var animatedY = (aggregatedY * easedProgress) * vFactor * vFactor;
            var renderX = GetVisibleBucketAnchorX(currentBucket, groupingLevel, xMin, xMax, cullBufferData);
            var screenX = ScaleXFast(renderX, xMin, xMax, renderArea);
            var screenY = ScaleYFast(animatedY, yMin, yMax, yScale, renderArea);
            points.Add(new RenderPointInfo(new SKPoint(screenX, screenY), selected.Data, selected.Index, aggregatedY));
        }

        ResetBucketState(visibleData[0]);
        foreach (var item in visibleData) {
            var bucket = GetDateBucketKey(item.X, groupingLevel);
            if (bucket != currentBucket) {
                FlushBucket();
                currentBucket = bucket;
                ResetBucketState(item);
            }

            Accumulate(item);
        }

        FlushBucket();
        return points;
    }

    private static double GetVisibleBucketAnchorX(long bucketKey, NTDateGroupingLevel groupingLevel, double xMin, double xMax, double cullBufferData) {
        if (xMax < xMin) {
            (xMin, xMax) = (xMax, xMin);
        }

        var bufferedMin = xMin - cullBufferData;
        var bufferedMax = xMax + cullBufferData;
        var (bucketStart, bucketEnd) = GetDateBucketRange(bucketKey, groupingLevel);
        var visibleStart = Math.Max(bucketStart, bufferedMin);
        var visibleEnd = Math.Min(bucketEnd, bufferedMax);

        // Keep grouped points visible until the entire bucket is off-canvas by
        // anchoring to the center of the currently visible overlap.
        if (visibleStart <= visibleEnd) {
            return visibleStart + ((visibleEnd - visibleStart) * 0.5d);
        }

        return bucketKey;
    }

    private static long GetDateBucketKey(double xValue, NTDateGroupingLevel groupingLevel) {
        var ticks = (long)Math.Round(xValue);
        ticks = Math.Clamp(ticks, DateTime.MinValue.Ticks, DateTime.MaxValue.Ticks);
        var dt = new DateTime(ticks);

        return groupingLevel switch {
            NTDateGroupingLevel.Year => new DateTime(dt.Year, 7, 1).Ticks,
            NTDateGroupingLevel.Month => new DateTime(dt.Year, dt.Month, 1).AddDays(14).Ticks,
            _ => dt.Ticks
        };
    }

    private List<VisiblePoint> GetVisibleWindowIncludingGroupedBuckets(double minX, double maxX, NTDateGroupingLevel groupingLevel) {
        if (maxX < minX) {
            (minX, maxX) = (maxX, minX);
        }

        var firstBucket = GetDateBucketKey(minX, groupingLevel);
        var lastBucket = GetDateBucketKey(maxX, groupingLevel);
        var (bucketStart, _) = GetDateBucketRange(firstBucket, groupingLevel);
        var (_, bucketEnd) = GetDateBucketRange(lastBucket, groupingLevel);

        var inclusiveStart = Math.BitDecrement(bucketStart);
        var inclusiveEnd = Math.BitIncrement(bucketEnd);
        var window = GetVisibleWindow(inclusiveStart, inclusiveEnd, overscan: 0);
        if (window.Count > 0) {
            return window;
        }

        // If no points are inside the computed bucket span (for example sparse data),
        // fall back to the nearest visible point window so interactions remain stable.
        return GetVisibleWindow(minX, maxX, overscan: 1);
    }

    private static (double Min, double Max) ExpandCullRange(
        double minX,
        double maxX,
        float renderWidth,
        float density,
        bool useAxisDateGrouping,
        NTDateGroupingLevel groupingLevel) {
        if (maxX < minX) {
            (minX, maxX) = (maxX, minX);
        }

        var buffer = GetCullBufferDataUnits(minX, maxX, renderWidth, density, useAxisDateGrouping, groupingLevel);
        return (minX - buffer, maxX + buffer);
    }

    private static double GetCullBufferDataUnits(
        double minX,
        double maxX,
        float renderWidth,
        float density,
        bool useAxisDateGrouping,
        NTDateGroupingLevel groupingLevel) {
        if (useAxisDateGrouping && groupingLevel is NTDateGroupingLevel.Year or NTDateGroupingLevel.Month or NTDateGroupingLevel.Day) {
            return GetGroupedTickSpan(groupingLevel, minX, maxX) * EdgeCullBufferTicks;
        }

        var range = Math.Abs(maxX - minX);
        if (range <= 0 || renderWidth <= 0) {
            return 0;
        }

        var dataPerPixel = range / Math.Max(1f, renderWidth);
        var pixelBuffer = Math.Max(1f, EdgeCullBufferPixels * Math.Max(0.1f, density));
        return dataPerPixel * pixelBuffer;
    }

    private static double GetGroupedTickSpan(NTDateGroupingLevel groupingLevel, double minX, double maxX) {
        var mid = (long)Math.Round(minX + ((maxX - minX) * 0.5d));
        mid = Math.Clamp(mid, DateTime.MinValue.Ticks, DateTime.MaxValue.Ticks);
        var dt = new DateTime(mid);

        if (groupingLevel == NTDateGroupingLevel.Year) {
            var start = new DateTime(dt.Year, 1, 1);
            var end = dt.Year == DateTime.MaxValue.Year
                ? DateTime.MaxValue
                : new DateTime(dt.Year + 1, 1, 1);
            return Math.Max(1d, end.Ticks - start.Ticks);
        }

        if (groupingLevel == NTDateGroupingLevel.Month) {
            var start = new DateTime(dt.Year, dt.Month, 1);
            var end = (dt.Year == DateTime.MaxValue.Year && dt.Month == 12)
                ? DateTime.MaxValue
                : start.AddMonths(1);
            return Math.Max(1d, end.Ticks - start.Ticks);
        }

        var dayStart = dt.Date;
        var dayEnd = dayStart == DateTime.MaxValue.Date ? DateTime.MaxValue : dayStart.AddDays(1);
        return Math.Max(1d, dayEnd.Ticks - dayStart.Ticks);
    }

    private static (double Start, double End) GetDateBucketRange(long bucketKey, NTDateGroupingLevel groupingLevel) {
        var ticks = Math.Clamp(bucketKey, DateTime.MinValue.Ticks, DateTime.MaxValue.Ticks);
        var dt = new DateTime(ticks);

        if (groupingLevel == NTDateGroupingLevel.Year) {
            var start = new DateTime(dt.Year, 1, 1);
            var end = dt.Year == DateTime.MaxValue.Year
                ? DateTime.MaxValue
                : new DateTime(dt.Year + 1, 1, 1).AddTicks(-1);
            return (start.Ticks, end.Ticks);
        }

        if (groupingLevel == NTDateGroupingLevel.Month) {
            var start = new DateTime(dt.Year, dt.Month, 1);
            var end = (dt.Year == DateTime.MaxValue.Year && dt.Month == 12)
                ? DateTime.MaxValue
                : start.AddMonths(1).AddTicks(-1);
            return (start.Ticks, end.Ticks);
        }

        var dayStart = dt.Date;
        var dayEnd = dayStart == DateTime.MaxValue.Date ? DateTime.MaxValue : dayStart.AddDays(1).AddTicks(-1);
        return (dayStart.Ticks, dayEnd.Ticks);
    }

    private static int GetPixelCapacity(SKRect renderArea) {
        // Allow slightly more than one point per pixel to preserve detail on dense data.
        return Math.Max(2, (int)MathF.Ceiling(renderArea.Width * 1.25f));
    }

    private List<RenderPointInfo> GetAggregatedPoints(
        List<VisiblePoint> visibleData,
        SKRect renderArea,
        double xMin,
        double xMax,
        decimal yMin,
        decimal yMax,
        NTAxisScale yScale,
        decimal easedProgress,
        decimal vFactor,
        int pixelCapacity) {

        var bucketCount = Math.Min(visibleData.Count, pixelCapacity);
        var buckets = new List<VisiblePoint>[bucketCount];
        for (var i = 0; i < buckets.Length; i++) {
            buckets[i] = [];
        }

        var range = xMax - xMin;
        if (range <= 0) {
            range = 1;
        }

        foreach (var item in visibleData) {
            var bucketIndex = (int)((item.X - xMin) / range * (bucketCount - 1));
            bucketIndex = Math.Clamp(bucketIndex, 0, bucketCount - 1);
            buckets[bucketIndex].Add(item);
        }

        var points = new List<RenderPointInfo>(bucketCount);
        foreach (var bucket in buckets) {
            if (bucket.Count == 0) {
                continue;
            }

            decimal aggregatedY;
            double aggregatedX;
            VisiblePoint representative;

            if (AggregationMode == AggregationMode.Average) {
                aggregatedY = bucket.Average(x => YValueSelector(x.Data));
                aggregatedX = bucket.Average(x => x.X);
                representative = bucket[bucket.Count / 2];
            }
            else if (AggregationMode == AggregationMode.Sum) {
                aggregatedY = bucket.Sum(x => YValueSelector(x.Data));
                aggregatedX = bucket.Average(x => x.X);
                representative = bucket[bucket.Count / 2];
            }
            else if (AggregationMode == AggregationMode.Min) {
                representative = bucket.MinBy(x => YValueSelector(x.Data));
                aggregatedY = YValueSelector(representative.Data);
                aggregatedX = representative.X;
            }
            else if (AggregationMode == AggregationMode.Max) {
                representative = bucket.MaxBy(x => YValueSelector(x.Data));
                aggregatedY = YValueSelector(representative.Data);
                aggregatedX = representative.X;
            }
            else if (AggregationMode == AggregationMode.Median) {
                representative = bucket[bucket.Count / 2];
                aggregatedY = CalculateMedian(bucket.Select(x => YValueSelector(x.Data)));
                aggregatedX = bucket.Average(x => x.X);
            }
            else {
                representative = bucket[bucket.Count / 2];
                aggregatedY = YValueSelector(representative.Data);
                aggregatedX = representative.X;
            }

            var animatedY = (aggregatedY * easedProgress) * vFactor * vFactor;
            var screenX = ScaleXFast(aggregatedX, xMin, xMax, renderArea);
            var screenY = ScaleYFast(animatedY, yMin, yMax, yScale, renderArea);
            points.Add(new RenderPointInfo(new SKPoint(screenX, screenY), representative.Data, representative.Index, aggregatedY));
        }

        return points;
    }

    private static decimal CalculateMedian(IEnumerable<decimal> values) {
        var sorted = values.OrderBy(v => v).ToList();
        if (sorted.Count == 0) {
            return 0m;
        }

        var mid = sorted.Count / 2;
        if (sorted.Count % 2 == 1) {
            return sorted[mid];
        }

        return (sorted[mid - 1] + sorted[mid]) / 2m;
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

    private static float ScaleXFast(double x, double min, double max, SKRect plotArea) {
        var range = max - min;
        if (range <= 0) {
            return plotArea.Left;
        }

        var t = (x - min) / range;
        const float p = 3f;
        var left = plotArea.Left + p;
        var width = plotArea.Width - (p * 2);
        return (float)(left + (t * width));
    }

    protected SKPath BuildPath(List<SKPoint> points) {
        var path = new SKPath();
        if (points.Count < 2) {
            return path;
        }

        path.MoveTo(points[0]);

        if (Interpolation == LineInterpolation.Straight) {
            for (var i = 1; i < points.Count; i++) {
                path.LineTo(points[i]);
            }
        }
        else if (Interpolation == LineInterpolation.Step) {
            for (var i = 1; i < points.Count; i++) {
                path.LineTo(points[i - 1].X, points[i].Y);
                path.LineTo(points[i]);
            }
        }
        else if (Interpolation == LineInterpolation.Curved) {
            for (var i = 0; i < points.Count - 1; i++) {
                var p0 = points[Math.Max(i - 1, 0)];
                var p1 = points[i];
                var p2 = points[i + 1];
                var p3 = points[Math.Min(i + 2, points.Count - 1)];

                var cp1 = new SKPoint(p1.X + ((p2.X - p0.X) / 6), p1.Y + ((p2.Y - p0.Y) / 6));
                var cp2 = new SKPoint(p2.X - ((p3.X - p1.X) / 6), p2.Y - ((p3.Y - p1.Y) / 6));

                path.CubicTo(cp1, cp2, p2);
            }
        }
        else if (Interpolation == LineInterpolation.Smoothed) {
            var n = points.Count;
            if (n > 2) {
                var slopes = new float[n - 1];
                for (var i = 0; i < n - 1; i++) {
                    slopes[i] = (points[i + 1].Y - points[i].Y) / (points[i + 1].X - points[i].X + 1e-6f);
                }

                var tangents = new float[n];
                for (var i = 1; i < n - 1; i++) {
                    if (Math.Sign(slopes[i - 1]) != Math.Sign(slopes[i])) {
                        tangents[i] = 0;
                    }
                    else {
                        var dxPrev = points[i].X - points[i - 1].X;
                        var dxCurr = points[i + 1].X - points[i].X;
                        var w1 = (2 * dxCurr) + dxPrev;
                        var w2 = dxCurr + (2 * dxPrev);

                        tangents[i] = Math.Abs(slopes[i - 1]) > 1e-6f && Math.Abs(slopes[i]) > 1e-6f ? (w1 + w2) / ((w1 / slopes[i - 1]) + (w2 / slopes[i])) : 0;
                    }
                }
                tangents[0] = slopes[0];
                tangents[n - 1] = slopes[n - 2];

                for (var i = 0; i < n - 1; i++) {
                    var xSpan = (points[i + 1].X - points[i].X) / 3f;
                    var cp1 = new SKPoint(points[i].X + xSpan, points[i].Y + (tangents[i] * xSpan));
                    var cp2 = new SKPoint(points[i + 1].X - xSpan, points[i + 1].Y - (tangents[i + 1] * xSpan));
                    path.CubicTo(cp1, cp2, points[i + 1]);
                }
            }
            else {
                path.LineTo(points[1]);
            }
        }

        return path;
    }

    /// <inheritdoc />
    public override (int Index, TData? Data)? HitTest(SKPoint point, SKRect renderArea) {
        if (Data == null || !Data.Any()) {
            return null;
        }

        var yAxis = (UseSecondaryYAxis ? Chart.SecondaryYAxis : Chart.YAxis) as NTAxisOptions<TData>;
        var (xMin, xMax) = Chart.GetXRange(Chart.XAxis as NTAxisOptions<TData>, true);
        var points = GetRenderPoints(renderArea, xMin, xMax, yAxis, Chart.Density, out var renderKey);

        for (var i = 0; i < points.Count; i++) {
            var p = points[i].Point;
            var dx = p.X - point.X;
            var dy = p.Y - point.Y;
            var distanceSq = (dx * dx) + (dy * dy);
            var threshold = (PointSize * Chart.Density) + (5 * Chart.Density);
            if (distanceSq < (threshold * threshold)) {
                return (points[i].Index, points[i].Data);
            }
        }

        if (points.Count > 1 && LineStyle != LineStyle.None) {
            if (_hitTestPath is null || !_hitTestPathKey.HasValue || !_hitTestPathKey.Value.Equals(renderKey)) {
                _hitTestPath?.Dispose();
                _hitTestPath = BuildPath(points.Select(p => p.Point).ToList());
                _hitTestPathKey = renderKey;
            }

            _hitTestPaint ??= new SKPaint {
                Style = SKPaintStyle.Stroke,
                StrokeCap = SKStrokeCap.Round,
                StrokeJoin = SKStrokeJoin.Round
            };
            _hitTestPaint.StrokeWidth = (StrokeWidth * Chart.Density) + (10 * Chart.Density);

            _hitTestStrokePath ??= new SKPath();
            _hitTestStrokePath.Reset();

            _hitTestPaint.GetFillPath(_hitTestPath, _hitTestStrokePath);

            if (_hitTestStrokePath.Contains(point.X, point.Y)) {
                var nearestIdx = -1;
                var nearestDistSq = double.MaxValue;
                for (var i = 0; i < points.Count; i++) {
                    var dx = points[i].Point.X - point.X;
                    var dy = points[i].Point.Y - point.Y;
                    var distSq = (dx * dx) + (dy * dy);
                    if (distSq < nearestDistSq) {
                        nearestDistSq = distSq;
                        nearestIdx = i;
                    }
                }

                if (nearestIdx >= 0) {
                    return (points[nearestIdx].Index, points[nearestIdx].Data);
                }
            }
        }

        return null;
    }
}
