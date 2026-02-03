using Microsoft.AspNetCore.Components;
using NTComponents.Charts.Core.Axes;
using NTComponents.Charts.Core;
using NTComponents.Charts.Core.Series;
using SkiaSharp;

namespace NTComponents.Charts;

/// <summary>
///     Represents a line series in a cartesian chart.
/// </summary>
public class NTLineSeries<TData> : NTCartesianSeries<TData> where TData : class {

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

    private SKPaint? _linePaint;
    private SKPaint? _segmentPaint;
    private SKPath? _linePath;
    private SKPaint? _hitTestPaint;
    private SKPath? _hitTestPath;
    private SKPath? _hitTestStrokePath;

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

    /// <inheritdoc />
    public override SKRect Render(NTRenderContext context, SKRect renderArea) {
        var canvas = context.Canvas;
        if (Data == null || !Data.Any()) {
            return renderArea;
        }

        var (xMin, xMax) = Chart.GetXRange(EffectiveXAxis, true);
        var (yMin, yMax) = Chart.GetYRange(EffectiveYAxis, true);

        var points = GetPoints(renderArea, xMin, xMax, yMin, yMax);

        var isHovered = Chart.HoveredSeries == this;
        var color = Chart.GetSeriesColor(this);
        var visibilityFactor = VisibilityFactor;
        var hoverFactor = HoverFactor;

        color = color.WithAlpha((byte)(color.Alpha * hoverFactor * visibilityFactor));

        _linePaint ??= new SKPaint {
            Style = SKPaintStyle.Stroke,
            IsAntialias = true,
            StrokeCap = SKStrokeCap.Round,
            StrokeJoin = SKStrokeJoin.Round
        };
        _linePaint.Color = color;
        _linePaint.StrokeWidth = StrokeWidth;

        if (LineStyle == LineStyle.Dashed) {
            _linePaint.PathEffect = SKPathEffect.CreateDash([10, 5], 0);
        }
        else {
            _linePaint.PathEffect = null;
        }

        if (LineStyle != LineStyle.None && points.Count > 1) {
            if (OnDataPointRender == null) {
                _linePath?.Dispose();
                _linePath = BuildPath(points);
                canvas.DrawPath(_linePath, _linePaint);
            }
            else {
                // Draw segment by segment to allow for per-point line styling
                var dataList = Data.ToList();
                for (var i = 1; i < points.Count; i++) {
                    var args = new NTDataPointRenderArgs<TData> {
                        Data = dataList[i],
                        Index = i,
                        Color = color,
                        LineStyle = LineStyle,
                        StrokeWidth = StrokeWidth,
                        GetThemeColor = Chart.GetThemeColor
                    };
                    OnDataPointRender.Invoke(args);

                    var segmentColor = args.Color ?? color;
                    var segmentStyle = args.LineStyle ?? LineStyle;
                    var segmentWidth = args.StrokeWidth ?? StrokeWidth;

                    if (segmentStyle == LineStyle.None) continue;

                    _segmentPaint ??= new SKPaint {
                        Style = SKPaintStyle.Stroke,
                        IsAntialias = true,
                        StrokeCap = SKStrokeCap.Round,
                        StrokeJoin = SKStrokeJoin.Round
                    };
                    _segmentPaint.Color = segmentColor;
                    _segmentPaint.StrokeWidth = segmentWidth;

                    if (segmentStyle == LineStyle.Dashed) {
                        _segmentPaint.PathEffect = SKPathEffect.CreateDash([10, 5], 0);
                    }
                    else {
                        _segmentPaint.PathEffect = null;
                    }

                    // For now, segment-based styling only supports straight lines
                    canvas.DrawLine(points[i - 1], points[i], _segmentPaint);
                }
            }
        }

        if (PointStyle != PointStyle.None || ShowDataLabels) {
            var dataList = Data.ToList();
            for (var i = 0; i < points.Count; i++) {
                var item = dataList[i];
                var args = new NTDataPointRenderArgs<TData> {
                    Data = item,
                    Index = i,
                    Color = color,
                    PointSize = PointSize,
                    PointShape = PointShape,
                    GetThemeColor = Chart.GetThemeColor
                };
                OnDataPointRender?.Invoke(args);

                var point = points[i];
                var isPointHovered = Chart.HoveredSeries == this && Chart.HoveredPointIndex == i;

                var pointColor = args.Color ?? color;
                var pointStrokeColor = args.StrokeColor ?? pointColor;
                var currentPointSize = args.PointSize ?? PointSize;
                var currentPointShape = args.PointShape ?? PointShape;

                if (isPointHovered) {
                    pointColor = pointColor.WithAlpha(255);
                    pointStrokeColor = pointStrokeColor.WithAlpha(255);
                    currentPointSize *= 1.5f;
                }

                if (PointStyle != PointStyle.None) {
                    RenderPoint(context, point.X, point.Y, pointColor, currentPointSize, currentPointShape, pointStrokeColor);
                }

                if (ShowDataLabels || isPointHovered) {
                    var labelColor = args.DataLabelColor;
                    var labelSize = args.DataLabelSize ?? DataLabelSize;
                    RenderDataLabel(context, point.X, point.Y, YValueSelector(dataList[i]), renderArea, labelColor, labelSize);
                }
            }
        }

        return renderArea;
    }

    protected List<SKPoint> GetPoints(SKRect renderArea, double xMin, double xMax, decimal yMin, decimal yMax) {
        var dataList = Data.ToList();
        var points = new List<SKPoint>();
        var progress = GetAnimationProgress();
        var easedProgress = (decimal)BackEase(progress);

        // Filter and aggregate if needed
        if (EnableAggregation && dataList.Count > AggregationThreshold) {
            var visibleData = new List<(TData Data, int Index)>();
            for (int i = 0; i < dataList.Count; i++) {
                var xVal = Chart.GetScaledXValue(XValue.Invoke(dataList[i]));
                if (xVal >= xMin - (xMax - xMin) * 0.1 && xVal <= xMax + (xMax - xMin) * 0.1) {
                    visibleData.Add((dataList[i], i));
                }
            }

            if (visibleData.Count > AggregationThreshold) {
                return GetAggregatedPoints(visibleData, renderArea, xMin, xMax, yMin, yMax);
            }

            // If visible count is small, fall back to normal rendering for those points
            dataList = visibleData.Select(x => x.Data).ToList();
        }

        if (AnimationCurrentValues == null || AnimationCurrentValues.Length != dataList.Count) {
            AnimationCurrentValues = new decimal[dataList.Count];
        }

        for (var i = 0; i < dataList.Count; i++) {
            var originalX = XValue.Invoke(dataList[i]);
            var xValue = Chart.GetScaledXValue(originalX);
            var targetYValue = YValueSelector(dataList[i]);


            var startYValue = (AnimationStartValues != null && i < AnimationStartValues.Length)
                ? AnimationStartValues[i]
                : yMin;

            var currentYValue = startYValue + ((targetYValue - startYValue) * easedProgress);

            // We use VisibilityFactor^2 for the value to ensure it shrinks to zero 
            // even as the axis range (which uses linear VisibilityFactor) also shrinks.
            var vFactor = (decimal)VisibilityFactor;
            currentYValue *= vFactor * vFactor;
            AnimationCurrentValues[i] = currentYValue;

            var screenXCoord = Chart.ScaleX(xValue, renderArea, EffectiveXAxis);
            var screenYCoord = Chart.ScaleY(currentYValue, EffectiveYAxis, renderArea);

            points.Add(new SKPoint(screenXCoord, screenYCoord));
        }
        return points;
    }

    private List<SKPoint> GetAggregatedPoints(List<(TData Data, int Index)> visibleData, SKRect renderArea, double xMin, double xMax, decimal yMin, decimal yMax) {
        var points = new List<SKPoint>();
        if (AggregationThreshold <= 0) return points;

        var buckets = new List<(TData Data, int Index)>[AggregationThreshold];
        for (int i = 0; i < buckets.Length; i++) buckets[i] = [];

        double range = xMax - xMin;
        if (range <= 0) range = 1;

        foreach (var item in visibleData) {
            var xVal = Chart.GetScaledXValue(XValue?.Invoke(item.Data));
            int bucketIndex = (int)((xVal - xMin) / range * (AggregationThreshold - 1));
            bucketIndex = Math.Clamp(bucketIndex, 0, AggregationThreshold - 1);
            buckets[bucketIndex].Add(item);
        }

        var vFactor = (decimal)VisibilityFactor;

        foreach (var bucket in buckets) {
            if (bucket.Count == 0) continue;

            decimal aggregatedY = 0;
            double aggregatedX = 0;

            if (AggregationMode == AggregationMode.Average) {
                aggregatedY = bucket.Average(x => YValueSelector!(x.Data));
                aggregatedX = bucket.Average(x => Chart.GetScaledXValue(XValue?.Invoke(x.Data)));
            }

            else if (AggregationMode == AggregationMode.Sum) {
                aggregatedY = bucket.Sum(x => YValueSelector!(x.Data));
                aggregatedX = bucket.Average(x => Chart.GetScaledXValue(XValue?.Invoke(x.Data)));
            }

            else if (AggregationMode == AggregationMode.Min) {
                aggregatedY = bucket.Min(x => YValueSelector!(x.Data));
                var best = bucket.First(x => YValueSelector!(x.Data) == aggregatedY);
                aggregatedX = Chart.GetScaledXValue(XValue?.Invoke(best.Data));
            }

            else if (AggregationMode == AggregationMode.Max) {
                aggregatedY = bucket.Max(x => YValueSelector!(x.Data));
                var best = bucket.First(x => YValueSelector!(x.Data) == aggregatedY);
                aggregatedX = Chart.GetScaledXValue(XValue?.Invoke(best.Data));
            }

            else {
                var mid = bucket[bucket.Count / 2];
                aggregatedY = YValueSelector!(mid.Data);
                aggregatedX = Chart.GetScaledXValue(XValue?.Invoke(mid.Data));
            }


            aggregatedY *= vFactor * vFactor;

            var screenX = Chart.ScaleX(aggregatedX, renderArea, EffectiveXAxis);
            var screenY = Chart.ScaleY(aggregatedY, EffectiveYAxis, renderArea);
            points.Add(new SKPoint(screenX, screenY));
        }

        return points;
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

        var (xMin, xMax) = Chart.GetXRange(EffectiveXAxis, true);
        var (yMin, yMax) = Chart.GetYRange(EffectiveYAxis, true);
        var points = GetPoints(renderArea, xMin, xMax, yMin, yMax);
        var dataList = Data.ToList();

        for (var i = 0; i < points.Count; i++) {
            var p = points[i];
            var distance = Math.Sqrt(Math.Pow(p.X - point.X, 2) + Math.Pow(p.Y - point.Y, 2));
            if (distance < PointSize + 5) {
                return (i, dataList[i]);
            }
        }

        if (points.Count > 1 && LineStyle != LineStyle.None) {
            _hitTestPath?.Dispose();
            _hitTestPath = BuildPath(points);

            _hitTestPaint ??= new SKPaint {
                Style = SKPaintStyle.Stroke,
                StrokeCap = SKStrokeCap.Round,
                StrokeJoin = SKStrokeJoin.Round
            };
            _hitTestPaint.StrokeWidth = StrokeWidth + 10; // Wider tolerance for hovering

            _hitTestStrokePath ??= new SKPath();
            _hitTestStrokePath.Reset();

            _hitTestPaint.GetFillPath(_hitTestPath, _hitTestStrokePath);

            if (_hitTestStrokePath.Contains(point.X, point.Y)) {
                return (-1, null);
            }
        }

        return null;
    }
}
