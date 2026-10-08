using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using NTComponents.Charts.Core.Axes;
using SkiaSharp;

namespace NTComponents.Charts.Core.Series;

/// <summary>
///     Base class for all cartesian (X/Y axis) chart series.
/// </summary>
/// <typeparam name="TData">The type of the data items.</typeparam>
public abstract class NTCartesianSeries<TData> : NTBaseSeries<TData>, ICartesianSeries where TData : class {
    /// <summary>
    ///     Represents a pre-scaled data point used for visibility and hit-testing calculations.
    /// </summary>
    protected readonly record struct VisiblePoint(TData Data, int Index, double X);

    /// <inheritdoc />
    public override ChartCoordinateSystem CoordinateSystem => ChartCoordinateSystem.Cartesian;

    /// <summary>
    ///     Gets or sets the function that extracts the Y-axis value from a data item.
    /// </summary>
    [Parameter, EditorRequired]
    public Func<TData, decimal> YValueSelector { get; set; } = default!;

    /// <summary>
    ///    Gets or sets whether to use the secondary Y axis if available.
    /// </summary>
    [Parameter]
    public bool UseSecondaryYAxis { get; set; }

    /// <inheritdoc />
    internal override TooltipInfo GetTooltipInfo(TData data) {
        var xValue = XValue.Invoke(data);
        var yValue = YValueSelector(data);
        var header = Chart.XAxis.FormatValue(xValue, forTooltip: true);
        var defaultInfo = new TooltipInfo {
            Header = header,
            Lines =
            [
                new TooltipLine
                {
                    Label = Title ?? "Series",
                    Value = string.Format(DataLabelFormat, yValue),
                    Color = Chart.GetSeriesColor(this)
                }
            ]
        };

        return ResolveTooltipInfo(data, defaultInfo, Chart.HoveredPointIndex);
    }

    /// <inheritdoc />
    public override (double Min, double Max)? GetXRange() {
        var points = GetSortedVisiblePoints();
        if (points.Count == 0) {
            return null;
        }

        if (_cachedTotalXRange.HasValue) {
            return _cachedTotalXRange;
        }

        _cachedTotalXRange = (points[0].X, points[^1].X);
        return _cachedTotalXRange;
    }

    /// <inheritdoc />
    public override (decimal Min, decimal Max)? GetYRange(double? xMin = null, double? xMax = null) {
        var points = GetSortedVisiblePoints();
        if (points.Count == 0) {
            return null;
        }

        if (!xMin.HasValue && !xMax.HasValue && _cachedTotalYRange.HasValue) {
            return _cachedTotalYRange;
        }

        if (xMin.HasValue && xMax.HasValue) {
            var rangeMin = Math.Round(Math.Min(xMin.Value, xMax.Value), 6);
            var rangeMax = Math.Round(Math.Max(xMin.Value, xMax.Value), 6);
            if (_cachedWindowYRange.HasValue &&
                _cachedWindowYRangeKey.HasValue &&
                _cachedWindowYRangeKey.Value.Min == rangeMin &&
                _cachedWindowYRangeKey.Value.Max == rangeMax) {
                return _cachedWindowYRange;
            }
        }

        var min = decimal.MaxValue;
        var max = decimal.MinValue;

        var window = xMin.HasValue && xMax.HasValue ? GetVisibleWindow(xMin.Value, xMax.Value) : points;
        if (window.Count == 0) {
            return null;
        }

        if (this is NTBoxPlotSeries<TData> boxPlot) {
            foreach (var entry in window) {
                var values = boxPlot.BoXValue(entry.Data);
                min = Math.Min(min, values.Min);
                max = Math.Max(max, values.Max);
                if (values.Outliers != null && values.Outliers.Any()) {
                    min = Math.Min(min, values.Outliers.Min());
                    max = Math.Max(max, values.Outliers.Max());
                }
            }
        }
        else {
            var visibilityFactor = (decimal)VisibilityFactor;
            foreach (var entry in window) {
                var y = YValueSelector(entry.Data) * visibilityFactor;
                min = Math.Min(min, y);
                max = Math.Max(max, y);
            }
        }

        var result = (min, max);
        if (!xMin.HasValue && !xMax.HasValue) {
            _cachedTotalYRange = result;
        }
        else if (xMin.HasValue && xMax.HasValue) {
            _cachedWindowYRangeKey = (Math.Round(Math.Min(xMin.Value, xMax.Value), 6), Math.Round(Math.Max(xMin.Value, xMax.Value), 6));
            _cachedWindowYRange = result;
        }

        return result;
    }

    /// <inheritdoc />
    internal override void RegisterXValues(HashSet<object> values) {

        if (Data == null) {
            return;
        }

        foreach (var item in Data) {
            var val = XValue.Invoke(item);
            if (val != null) {
                values.Add(val);
            }
        }
    }

    /// <inheritdoc />
    internal override void RegisterYValues(HashSet<object> values) {
        if (Data == null) {
            return;
        }

        foreach (var item in Data) {
            values.Add(YValueSelector(item));
        }
    }

    /// <summary>
    ///    Gets or sets the style of the data points.
    /// </summary>
    [Parameter]
    public PointStyle PointStyle { get; set; } = PointStyle.Filled;

    /// <summary>
    ///     Gets or sets the shape of the data points. If null, a shape will be assigned based on the series index.
    /// </summary>
    [Parameter]
    public PointShape? PointShape { get; set; }

    /// <summary>
    ///    Gets or sets the size of the data points.
    /// </summary>
    [Parameter]
    public float PointSize { get; set; } = 8.0f;

    /// <summary>
    ///     Gets or sets whether to show data labels for each point.
    /// </summary>
    [Parameter]
    public bool ShowDataLabels { get; set; }

    /// <summary>
    ///     Gets or sets the format for the data labels.
    /// </summary>
    [Parameter]
    public string DataLabelFormat { get; set; } = "{0:0.#}";

    /// <summary>
    ///     Gets or sets the size of the data labels.
    /// </summary>
    [Parameter]
    public float DataLabelSize { get; set; } = 12.0f;

    /// <summary>
    ///     Gets or sets the color of the data labels. If null, the chart's text color will be used.
    /// </summary>
    [Parameter]
    public NTColor? DataLabelColor { get; set; }

    /// <summary>
    ///     Gets or sets whether to show a background for data labels.
    /// </summary>
    [Parameter]
    public bool ShowDataLabelBackground { get; set; } = true;

    /// <summary>
    ///     Gets or sets the background color for data labels. If null, the series' color will be used.
    /// </summary>
    [Parameter]
    public NTColor? DataLabelBackgroundColor { get; set; }

    /// <summary>The minimum X value of the current view window, set by pan or zoom interactions.</summary>
    protected double? _viewXMin;
    /// <summary>The maximum X value of the current view window, set by pan or zoom interactions.</summary>
    protected double? _viewXMax;
    /// <summary>The minimum Y value of the current view window, set by pan or zoom interactions.</summary>
    protected decimal? _viewYMin;
    /// <summary>The maximum Y value of the current view window, set by pan or zoom interactions.</summary>
    protected decimal? _viewYMax;

    private (double Min, double Max)? _cachedTotalXRange;
    private (decimal Min, decimal Max)? _cachedTotalYRange;
    private (double Min, double Max)? _cachedWindowYRangeKey;
    private (decimal Min, decimal Max)? _cachedWindowYRange;
    private List<VisiblePoint>? _cachedSortedVisiblePoints;
    private Func<TData, decimal>? _previousYValueSelector;

    private SKPaint? _pointPaint;
    private SKPaint? _labelPaint;
    private SKPaint? _labelBgPaint;
    private SKPaint? _labelBorderPaint;
    private SKFont? _labelFont;

    /// <summary>Indicates whether a pan gesture is currently in progress.</summary>
    protected bool _isPanning;
    /// <summary>The screen-space position where the current pan gesture began.</summary>
    protected SKPoint _panStartPoint;
    /// <summary>The X-axis view range captured at the start of the pan gesture.</summary>
    protected (double Min, double Max)? _panStartXRange;
    /// <summary>The Y-axis view range captured at the start of the pan gesture.</summary>
    protected (decimal Min, decimal Max)? _panStartYRange;

    /// <inheritdoc />
    public override void HandleMouseDown(MouseEventArgs e) {
        var point = new SKPoint((float)e.OffsetX * Chart.Density, (float)e.OffsetY * Chart.Density);
        if (Interactions.HasFlag(ChartInteractions.XPan) || Interactions.HasFlag(ChartInteractions.YPan)) {
            _isPanning = true;
            _panStartPoint = point;
            _panStartXRange = Chart.GetXRange(Chart.XAxis as NTAxisOptions<TData>, true);
            var yAxis = UseSecondaryYAxis ? Chart.SecondaryYAxis : Chart.YAxis;
            _panStartYRange = Chart.GetYRange(yAxis as NTAxisOptions<TData>, true);
            NotifyPanStart(new NTSeriesPanStartEventArgs<TData> {
                Series = this,
                PointerPosition = point,
                MouseEvent = e,
                ViewXRange = GetViewXRange(),
                ViewYRange = GetViewYRange()
            });
        }
    }

    /// <inheritdoc />
    public override void HandleMouseMove(MouseEventArgs e) {
        var point = new SKPoint((float)e.OffsetX * Chart.Density, (float)e.OffsetY * Chart.Density);
        if (_isPanning && Chart.LastPlotArea != default) {
            var dx = _panStartPoint.X - point.X;
            var dy = point.Y - _panStartPoint.Y;

            if (_panStartXRange.HasValue && Interactions.HasFlag(ChartInteractions.XPan)) {
                var xRangeSize = _panStartXRange.Value.Max - _panStartXRange.Value.Min;
                var dataDx = dx / Chart.LastPlotArea.Width * xRangeSize;
                _viewXMin = _panStartXRange.Value.Min + dataDx;
                _viewXMax = _panStartXRange.Value.Max + dataDx;
            }

            if (_panStartYRange.HasValue && Interactions.HasFlag(ChartInteractions.YPan)) {
                var yRangeSize = _panStartYRange.Value.Max - _panStartYRange.Value.Min;
                var dataDy = (decimal)(dy / Chart.LastPlotArea.Height) * yRangeSize;
                _viewYMin = _panStartYRange.Value.Min + dataDy;
                _viewYMax = _panStartYRange.Value.Max + dataDy;
            }

            NotifyPan(new NTSeriesPanEventArgs<TData> {
                Series = this,
                PointerPosition = point,
                MouseEvent = e,
                ViewXRange = GetViewXRange(),
                ViewYRange = GetViewYRange()
            });
        }
    }

    /// <inheritdoc />
    public override void HandleMouseUp(MouseEventArgs e) {
        if (_isPanning) {
            var point = new SKPoint((float)e.OffsetX * Chart.Density, (float)e.OffsetY * Chart.Density);
            NotifyPanEnd(new NTSeriesPanEndEventArgs<TData> {
                Series = this,
                PointerPosition = point,
                MouseEvent = e,
                ViewXRange = GetViewXRange(),
                ViewYRange = GetViewYRange()
            });
        }
        _isPanning = false;
    }

    /// <inheritdoc />
    public override void HandleMouseWheel(WheelEventArgs e) {
        if ((!Interactions.HasFlag(ChartInteractions.XZoom) && !Interactions.HasFlag(ChartInteractions.YZoom)) || Chart.LastPlotArea == default) {
            return;
        }

        var point = new SKPoint((float)e.OffsetX * Chart.Density, (float)e.OffsetY * Chart.Density);
        if (!Chart.LastPlotArea.Contains(point)) {
            return;
        }

        var zoomFactor = e.DeltaY > 0 ? 1.1 : 0.9;
        var yAxis = (UseSecondaryYAxis ? Chart.SecondaryYAxis : Chart.YAxis) as NTAxisOptions<TData>;

        var xVal = Chart.ScaleXInverse(point.X, Chart.LastPlotArea);
        var yVal = Chart.ScaleYInverse(point.Y, yAxis, Chart.LastPlotArea);

        var (xMin, xMax) = Chart.GetXRange(Chart.XAxis as NTAxisOptions<TData>, true);
        var (yMin, yMax) = Chart.GetYRange(yAxis, true);

        if (Interactions.HasFlag(ChartInteractions.XZoom)) {
            var newXRange = (xMax - xMin) * zoomFactor;
            var xPct = (xVal - xMin) / (xMax - xMin);
            _viewXMin = xVal - (newXRange * xPct);
            _viewXMax = xVal + (newXRange * (1 - xPct));
        }

        if (Interactions.HasFlag(ChartInteractions.YZoom)) {
            var newYRange = (yMax - yMin) * (decimal)zoomFactor;
            var yPct = (decimal)((double)(yVal - yMin) / (double)(yMax - yMin));
            _viewYMin = yVal - (newYRange * yPct);
            _viewYMax = yVal + (newYRange * (1 - yPct));
        }

        NotifyZoom(new NTSeriesZoomEventArgs<TData> {
            Series = this,
            PointerPosition = point,
            WheelEvent = e,
            ViewXRange = GetViewXRange(),
            ViewYRange = GetViewYRange()
        });
    }

    /// <inheritdoc />
    public override void ResetView() {
        _viewXMin = null;
        _viewXMax = null;
        _viewYMin = null;
        _viewYMax = null;
        base.ResetView();
    }

    /// <inheritdoc />
    public override (double Min, double Max)? GetViewXRange() => (_viewXMin.HasValue && _viewXMax.HasValue) ? (_viewXMin.Value, _viewXMax.Value) : null;
    /// <inheritdoc />
    public override (decimal Min, decimal Max)? GetViewYRange() => (_viewYMin.HasValue && _viewYMax.HasValue) ? (_viewYMin.Value, _viewYMax.Value) : null;

    internal void SetViewXRange(double? min, double? max) {
        _viewXMin = min;
        _viewXMax = max;
    }

    internal void SetViewYRange(decimal? min, decimal? max) {
        _viewYMin = min;
        _viewYMax = max;
    }

    /// <inheritdoc />
    public override bool IsPanning => _isPanning;

    /// <inheritdoc />
    protected override void Dispose(bool disposing) {
        if (disposing) {
            _pointPaint?.Dispose();
            _labelPaint?.Dispose();
            _labelBgPaint?.Dispose();
            _labelBorderPaint?.Dispose();
            _labelFont?.Dispose();
        }
        base.Dispose(disposing);
    }

    /// <summary>Gets or sets the animation start values used as the baseline when transitioning between data sets.</summary>
    protected decimal[]? AnimationStartValues { get; set; }

    /// <summary>Gets or sets the current interpolated animation values during an in-progress transition.</summary>
    protected decimal[]? AnimationCurrentValues { get; set; }

    /// <inheritdoc />
    protected override void OnDataChanged() {
        if (AnimationCurrentValues != null) {
            AnimationStartValues = AnimationCurrentValues;
        }
        AnimationCurrentValues = null;
        _cachedTotalXRange = null;
        _cachedTotalYRange = null;
        _cachedWindowYRangeKey = null;
        _cachedWindowYRange = null;
        _cachedSortedVisiblePoints = null;
        base.OnDataChanged();
    }

    /// <inheritdoc />
    protected override void OnParametersSet() {
        base.OnParametersSet();
        if (!ReferenceEquals(_previousYValueSelector, YValueSelector)) {
            _previousYValueSelector = YValueSelector;
            OnDataChanged();
        }
    }

    /// <summary>Returns the list of data points whose X values fall within the specified visible window, with optional overscan padding.</summary>
    /// <param name="minX">The minimum X boundary of the visible window.</param>
    /// <param name="maxX">The maximum X boundary of the visible window.</param>
    /// <param name="overscan">Number of extra points to include beyond each edge for smooth rendering.</param>
    protected List<VisiblePoint> GetVisibleWindow(double minX, double maxX, int overscan = 1) {
        var points = GetSortedVisiblePoints();
        if (points.Count == 0) {
            return [];
        }

        if (maxX < minX) {
            (minX, maxX) = (maxX, minX);
        }

        var start = LowerBound(points, minX);
        var end = UpperBound(points, maxX);

        if (end < start) {
            return [];
        }

        start = Math.Max(0, start - overscan);
        end = Math.Min(points.Count - 1, end + overscan);
        return points.GetRange(start, (end - start) + 1);
    }

    private List<VisiblePoint> GetSortedVisiblePoints() {
        if (_cachedSortedVisiblePoints is not null) {
            return _cachedSortedVisiblePoints;
        }

        var points = new List<VisiblePoint>();
        if (Data != null) {
            var index = 0;
            foreach (var item in Data) {
                points.Add(new VisiblePoint(item, index, Chart.GetScaledXValue(XValue.Invoke(item))));
                index++;
            }
        }

        points.Sort(static (a, b) => {
            var cmp = a.X.CompareTo(b.X);
            return cmp != 0 ? cmp : a.Index.CompareTo(b.Index);
        });

        _cachedSortedVisiblePoints = points;
        return _cachedSortedVisiblePoints;
    }

    private static int LowerBound(List<VisiblePoint> values, double target) {
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

    private static int UpperBound(List<VisiblePoint> values, double target) {
        var lo = 0;
        var hi = values.Count;
        while (lo < hi) {
            var mid = lo + ((hi - lo) / 2);
            if (values[mid].X <= target) {
                lo = mid + 1;
            }
            else {
                hi = mid;
            }
        }
        return lo - 1;
    }

    /// <summary>Renders a formatted data label at the specified canvas position.</summary>
    /// <param name="context">The current render context.</param>
    /// <param name="x">The X canvas coordinate for the label.</param>
    /// <param name="y">The Y canvas coordinate for the label.</param>
    /// <param name="value">The numeric value to format and display.</param>
    /// <param name="renderArea">The bounding rectangle used to keep the label within the plot area.</param>
    /// <param name="overrideColor">Optional color override; when set, the label is always drawn regardless of <c>ShowDataLabels</c>.</param>
    /// <param name="overrideFontSize">Optional font size override.</param>
    /// <param name="textAlign">The horizontal alignment of the label text.</param>
    protected void RenderDataLabel(NTRenderContext context, float x, float y, decimal value, SKRect renderArea, SKColor? overrideColor = null, float? overrideFontSize = null, SKTextAlign textAlign = SKTextAlign.Center) {
        if (overrideColor == null && !ShowDataLabels) {
            return;
        }

        var alphaFactor = Math.Clamp(HoverFactor * VisibilityFactor, 0f, 1f);
        var color = overrideColor ?? Chart.GetSeriesTextColor(this);
        color = color.WithAlpha((byte)Math.Clamp((int)(color.Alpha * alphaFactor), 0, 255));
        var size = (overrideFontSize ?? DataLabelSize) * context.Density;

        _labelFont ??= new SKFont {
            Typeface = context.DefaultFont.Typeface
        };
        _labelFont.Size = size;

        _labelPaint ??= new SKPaint {
            IsAntialias = true
        };
        _labelPaint.Color = color;

        var text = string.Format(DataLabelFormat, value);
        var textWidth = _labelFont.MeasureText(text);
        var textHeight = _labelFont.Size;

        var paddingX = 6f * context.Density;
        var paddingY = 2f * context.Density;

        var drawY = y;
        if (textAlign == SKTextAlign.Center) {
            drawY = y - ((size / 2) + (5 * context.Density));
            if (drawY - textHeight < renderArea.Top) {
                drawY = y + ((size / 2) + textHeight + (5 * context.Density));
            }
        }

        if (ShowDataLabelBackground) {
            var bgColor = DataLabelBackgroundColor.HasValue ? Chart.GetThemeColor(DataLabelBackgroundColor.Value) : Chart.GetSeriesColor(this);
            bgColor = bgColor.WithAlpha((byte)Math.Clamp((int)(bgColor.Alpha * alphaFactor), 0, 255));

            _labelBgPaint ??= new SKPaint {
                Style = SKPaintStyle.Fill,
                IsAntialias = true,
                ImageFilter = SKImageFilter.CreateDropShadow(2 * context.Density, 2 * context.Density, 4 * context.Density, 4 * context.Density, SKColors.Black.WithAlpha(80))
            };
            _labelBgPaint.Color = bgColor;

            var bgRect = textAlign switch {
                SKTextAlign.Left => new SKRect(x - paddingX, drawY - (textHeight / 2) - paddingY, x + textWidth + paddingX, drawY + (textHeight / 2) + paddingY),
                SKTextAlign.Right => new SKRect(x - textWidth - paddingX, drawY - (textHeight / 2) - paddingY, x + paddingX, drawY + (textHeight / 2) + paddingY),
                _ => new SKRect(x - (textWidth / 2) - paddingX, drawY - textHeight - paddingY, x + (textWidth / 2) + paddingX + (2 * context.Density), drawY + paddingY)
            };

            context.Canvas.DrawRoundRect(bgRect, 6 * context.Density, 6 * context.Density, _labelBgPaint);

            _labelBorderPaint ??= new SKPaint {
                Style = SKPaintStyle.Stroke,
                StrokeWidth = 1,
                IsAntialias = true
            };
            _labelBorderPaint.Color = Chart.GetThemeColor(NTColor.Outline).WithAlpha((byte)Math.Clamp((int)(255 * alphaFactor), 0, 255));
            context.Canvas.DrawRoundRect(bgRect, 6 * context.Density, 6 * context.Density, _labelBorderPaint);
        }

        context.Canvas.DrawText(text, x, drawY, textAlign, _labelFont, _labelPaint);
    }

    /// <summary>Renders a styled data point marker at the specified canvas position.</summary>
    /// <param name="context">The current render context.</param>
    /// <param name="x">The X canvas coordinate for the point.</param>
    /// <param name="y">The Y canvas coordinate for the point.</param>
    /// <param name="color">The fill or stroke color of the point.</param>
    /// <param name="pointSize">Optional size override; defaults to the series <c>PointSize</c>.</param>
    /// <param name="pointShape">Optional shape override; defaults to the series <c>PointShape</c>.</param>
    /// <param name="strokeColor">Optional stroke color for outlined point styles.</param>
    protected void RenderPoint(NTRenderContext context, float x, float y, SKColor color, float? pointSize = null, PointShape? pointShape = null, SKColor? strokeColor = null) {
        if (PointStyle == PointStyle.None) {
            return;
        }

        var size = pointSize ?? PointSize;
        var shape = pointShape ?? PointShape ?? (PointShape)(Chart.GetSeriesIndex(this) % Enum.GetValues<PointShape>().Length);

        _pointPaint ??= new SKPaint {
            IsAntialias = true
        };
        _pointPaint.Color = color;
        _pointPaint.Style = PointStyle == PointStyle.Filled ? SKPaintStyle.Fill : SKPaintStyle.Stroke;
        _pointPaint.StrokeWidth = 2 * context.Density;

        var scaledSize = size * context.Density;
        NTRenderContextExtensions.DrawPointInternal(context.Canvas, x, y, scaledSize, shape, _pointPaint);

        if (PointStyle == PointStyle.Outlined && strokeColor.HasValue) {
            _pointPaint.Color = strokeColor.Value;
            _pointPaint.Style = SKPaintStyle.Stroke;
            context.Canvas.DrawCircle(x, y, scaledSize / 2, _pointPaint);
        }
    }
}
