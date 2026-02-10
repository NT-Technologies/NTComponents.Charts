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

    private readonly record struct RenderPointInfo(SKPoint Point, TData Data, int Index, decimal Value);

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
        if (Data?.Any() != true) {
            return renderArea;
        }

        var yAxis = (UseSecondaryYAxis ? Chart.SecondaryYAxis : Chart.YAxis) as NTAxisOptions<TData>;
        var (xMin, xMax) = Chart.GetXRange(Chart.XAxis as NTAxisOptions<TData>, true);
        var points = GetRenderPoints(renderArea, xMin, xMax, yAxis);

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
                _linePath?.Dispose();
                _linePath = BuildPath(points.Select(p => p.Point).ToList());
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

        if (PointStyle != PointStyle.None || ShowDataLabels) {
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

                if (PointStyle != PointStyle.None) {
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
        var points = GetRenderPoints(renderArea, xMin, xMax, Chart.YAxis as NTAxisOptions<TData>);
        return points.Select(p => p.Point).ToList();
    }

    private List<RenderPointInfo> GetRenderPoints(SKRect renderArea, double xMin, double xMax, NTAxisOptions<TData>? yAxis) {
        var visibleData = GetVisibleWindow(xMin, xMax);
        if (visibleData.Count == 0) {
            return [];
        }

        if (EnableAggregation && visibleData.Count > AggregationThreshold && AggregationThreshold > 1) {
            return GetAggregatedPoints(visibleData, renderArea, xMin, xMax, yAxis);
        }

        var progress = GetAnimationProgress();
        var easedProgress = (decimal)BackEase(progress);
        var vFactor = (decimal)VisibilityFactor;

        var points = new List<RenderPointInfo>(visibleData.Count);
        foreach (var item in visibleData) {
            var targetY = YValueSelector(item.Data);
            var animatedY = (targetY * easedProgress) * vFactor * vFactor;

            var screenX = Chart.ScaleX(item.X, renderArea);
            var screenY = ScaleYForAxis(animatedY, yAxis, renderArea);
            points.Add(new RenderPointInfo(new SKPoint(screenX, screenY), item.Data, item.Index, targetY));
        }

        return points;
    }

    private List<RenderPointInfo> GetAggregatedPoints(List<VisiblePoint> visibleData, SKRect renderArea, double xMin, double xMax, NTAxisOptions<TData>? yAxis) {
        if (AggregationThreshold <= 1) {
            return [];
        }

        var bucketCount = Math.Min(AggregationThreshold, Math.Max(2, (int)renderArea.Width));
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

        var progress = GetAnimationProgress();
        var easedProgress = (decimal)BackEase(progress);
        var vFactor = (decimal)VisibilityFactor;

        var points = new List<RenderPointInfo>();
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
            else {
                representative = bucket[bucket.Count / 2];
                aggregatedY = YValueSelector(representative.Data);
                aggregatedX = representative.X;
            }

            var animatedY = (aggregatedY * easedProgress) * vFactor * vFactor;
            var screenX = Chart.ScaleX(aggregatedX, renderArea);
            var screenY = ScaleYForAxis(animatedY, yAxis, renderArea);
            points.Add(new RenderPointInfo(new SKPoint(screenX, screenY), representative.Data, representative.Index, aggregatedY));
        }

        return points;
    }

    private float ScaleYForAxis(decimal y, NTAxisOptions<TData>? yAxis, SKRect plotArea) {
        if (yAxis == null || ReferenceEquals(yAxis, Chart.YAxis)) {
            return Chart.ScaleY(y, plotArea);
        }

        var (min, max) = Chart.GetYRange(yAxis, true);
        var scale = yAxis.Scale;

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
        var points = GetRenderPoints(renderArea, xMin, xMax, yAxis);

        for (var i = 0; i < points.Count; i++) {
            var p = points[i].Point;
            var distance = Math.Sqrt(Math.Pow(p.X - point.X, 2) + Math.Pow(p.Y - point.Y, 2));
            if (distance < (PointSize * Chart.Density) + (5 * Chart.Density)) {
                return (points[i].Index, points[i].Data);
            }
        }

        if (points.Count > 1 && LineStyle != LineStyle.None) {
            _hitTestPath?.Dispose();
            _hitTestPath = BuildPath(points.Select(p => p.Point).ToList());

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
                var nearest = points
                    .OrderBy(p => Math.Abs(p.Point.X - point.X))
                    .ThenBy(p => Math.Abs(p.Point.Y - point.Y))
                    .First();
                return (nearest.Index, nearest.Data);
            }
        }

        return null;
    }
}

