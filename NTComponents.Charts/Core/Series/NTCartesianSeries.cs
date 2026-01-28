using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using NTComponents.Charts.Core.Axes;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Linq;

namespace NTComponents.Charts.Core.Series;

public abstract class NTCartesianSeries<TData> : NTBaseSeries<TData>, ICartesianSeries where TData : class {

    /// <inheritdoc />
    public override ChartCoordinateSystem CoordinateSystem => ChartCoordinateSystem.Cartesian;

    [Parameter, EditorRequired]
    public Func<TData, decimal> YValueSelector { get; set; } = default!;

    /// <summary>
    ///     Gets or sets the X axis options.
    /// </summary>
    [Parameter]
    public NTXAxisOptions? XAxis { get; set; }

    /// <summary>
    ///    Gets or sets the Y axis options.
    /// </summary>
    [Parameter]
    public NTYAxisOptions? YAxis { get; set; }

    public NTXAxisOptions EffectiveXAxis => XAxis ?? Chart?.PrimaryXAxis ?? NTXAxisOptions.Default;
    public NTYAxisOptions EffectiveYAxis => YAxis ?? Chart?.PrimaryYAxis ?? NTYAxisOptions.Default;

    NTXAxisOptions ICartesianSeries.EffectiveXAxis => EffectiveXAxis;
    NTYAxisOptions ICartesianSeries.EffectiveYAxis => EffectiveYAxis;

    /// <inheritdoc />
    internal override TooltipInfo GetTooltipInfo(TData data) {
        var xValue = XValue.Invoke(data);
        var yValue = YValueSelector(data);
        return new TooltipInfo {
            Header = xValue?.ToString(),
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
    }

    /// <inheritdoc />
    internal override SKRect Measure(SKRect renderArea, NTRenderContext context, HashSet<object> measured) {
        if (!IsEffectivelyVisible) return renderArea;
        var rect = renderArea;
        if (EffectiveXAxis != null && EffectiveXAxis.Visible && measured.Add(EffectiveXAxis)) {
            rect = EffectiveXAxis.Measure(rect, context, Chart);
        }
        if (EffectiveYAxis != null && EffectiveYAxis.Visible && measured.Add(EffectiveYAxis)) {
            rect = EffectiveYAxis.Measure(rect, context, Chart);
        }
        return rect;
    }

    /// <inheritdoc />
    internal override void RenderAxes(NTRenderContext context, HashSet<object> rendered) {
        if (!IsEffectivelyVisible) return;
        if (EffectiveXAxis != null && EffectiveXAxis.Visible && rendered.Add(EffectiveXAxis)) {
            EffectiveXAxis.Render(context, Chart);
        }
        if (EffectiveYAxis != null && EffectiveYAxis.Visible && rendered.Add(EffectiveYAxis)) {
            EffectiveYAxis.Render(context, Chart);
        }
    }

    /// <inheritdoc />
    public override (double Min, double Max)? GetXRange() {
        if (Data == null || !Data.Any()) return null;
        if (_cachedTotalXRange.HasValue) return _cachedTotalXRange;

        var values = Data.Select(item => Chart.GetScaledXValue(XValue.Invoke(item))).ToList();
        var min = values.Min();
        var max = values.Max();

        _cachedTotalXRange = (min, max);
        return _cachedTotalXRange;
    }

    /// <inheritdoc />
    public override (decimal Min, decimal Max)? GetYRange(double? xMin = null, double? xMax = null) {
        if (Data == null || !Data.Any()) return null;

        if (!xMin.HasValue && !xMax.HasValue && _cachedTotalYRange.HasValue) {
            return _cachedTotalYRange;
        }

        var min = decimal.MaxValue;
        var max = decimal.MinValue;

        var dataToConsider = Data;
        if (xMin.HasValue && xMax.HasValue) {
            dataToConsider = dataToConsider.Where(d => {
                var x = Chart.GetScaledXValue(XValue.Invoke(d));
                return x >= xMin.Value && x <= xMax.Value;
            });
        }

        if (!dataToConsider.Any()) return null;

        if (this is NTBoxPlotSeries<TData> boxPlot) {
            foreach (var item in dataToConsider) {
                var values = boxPlot.BoXValue(item);

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
            var values = dataToConsider.Select(item => YValueSelector(item) * visibilityFactor).ToList();
            min = values.Min();
            max = values.Max();
        }

        var result = (min, max);
        if (!xMin.HasValue && !xMax.HasValue) {
            _cachedTotalYRange = result;
        }

        return result;
    }

    /// <inheritdoc />
    internal override void RegisterXValues(HashSet<object> values) {

        if (Data == null) return;
        foreach (var item in Data) {
            var val = XValue.Invoke(item);
            if (val != null) values.Add(val);
        }
    }


    /// <inheritdoc />
    internal override void RegisterYValues(HashSet<object> values) {
        if (Data == null) return;
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
    public TnTColor? DataLabelColor { get; set; }

    /// <summary>
    ///     Gets or sets whether to show a background for data labels.
    /// </summary>
    [Parameter]
    public bool ShowDataLabelBackground { get; set; } = true;

    /// <summary>
    ///     Gets or sets the background color for data labels. If null, the series' color will be used.
    /// </summary>
    [Parameter]
    public TnTColor? DataLabelBackgroundColor { get; set; }

    protected double? _viewXMin;
    protected double? _viewXMax;
    protected decimal? _viewYMin;
    protected decimal? _viewYMax;

    private (double Min, double Max)? _cachedTotalXRange;
    private (decimal Min, decimal Max)? _cachedTotalYRange;

    protected bool _isPanning;
    protected SKPoint _panStartPoint;
    protected (double Min, double Max)? _panStartXRange;
    protected (decimal Min, decimal Max)? _panStartYRange;

    public override void HandleMouseDown(MouseEventArgs e) {
        var point = new SKPoint((float)e.OffsetX * Chart.Density, (float)e.OffsetY * Chart.Density);
        if (Interactions.HasFlag(ChartInteractions.XPan) || Interactions.HasFlag(ChartInteractions.YPan)) {
            _isPanning = true;
            _panStartPoint = point;
            _panStartXRange = Chart.GetXRange(EffectiveXAxis, true);
            _panStartYRange = Chart.GetYRange(EffectiveYAxis, true);
        }
    }

    public override void HandleMouseMove(MouseEventArgs e) {
        var point = new SKPoint((float)e.OffsetX * Chart.Density, (float)e.OffsetY * Chart.Density);
        if (_isPanning && Chart.LastPlotArea != default) {
            var dx = _panStartPoint.X - point.X;
            var dy = point.Y - _panStartPoint.Y; // Y is inverted in screen coords

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
        }
    }

    public override void HandleMouseUp(MouseEventArgs e) {
        _isPanning = false;
    }

    public override void HandleMouseWheel(WheelEventArgs e) {
        if ((!Interactions.HasFlag(ChartInteractions.XZoom) && !Interactions.HasFlag(ChartInteractions.YZoom)) || Chart.LastPlotArea == default) {
            return;
        }

        var point = new SKPoint((float)e.OffsetX * Chart.Density, (float)e.OffsetY * Chart.Density);
        if (!Chart.LastPlotArea.Contains(point)) {
            return;
        }

        var zoomFactor = e.DeltaY > 0 ? 1.1 : 0.9;

        var xVal = Chart.ScaleXInverse(point.X, Chart.LastPlotArea, EffectiveXAxis);
        var yVal = Chart.ScaleYInverse(point.Y, EffectiveYAxis, Chart.LastPlotArea);

        var (xMin, xMax) = Chart.GetXRange(EffectiveXAxis, true);
        var (yMin, yMax) = Chart.GetYRange(EffectiveYAxis, true);

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
    }

    public override void ResetView() {
        _viewXMin = null;
        _viewXMax = null;
        _viewYMin = null;
        _viewYMax = null;
    }

    public override (double Min, double Max)? GetViewXRange() => (_viewXMin.HasValue && _viewXMax.HasValue) ? (_viewXMin.Value, _viewXMax.Value) : null;
    public override (decimal Min, decimal Max)? GetViewYRange() => (_viewYMin.HasValue && _viewYMax.HasValue) ? (_viewYMin.Value, _viewYMax.Value) : null;

    /// <inheritdoc />
    public override bool IsPanning => _isPanning;

    protected decimal[]? AnimationStartValues { get; set; }
    protected decimal[]? AnimationCurrentValues { get; set; }

    /// <inheritdoc />
    protected override void OnDataChanged() {
        if (AnimationCurrentValues != null) {
            AnimationStartValues = AnimationCurrentValues;
        }
        AnimationCurrentValues = null;
        _cachedTotalXRange = null;
        _cachedTotalYRange = null;
        base.OnDataChanged();
    }

    protected void RenderDataLabel(NTRenderContext context, float x, float y, decimal value, SKRect renderArea, SKColor? overrideColor = null, float? overrideFontSize = null, SKTextAlign textAlign = SKTextAlign.Center) {
        if (overrideColor == null && !ShowDataLabels) {
            return;
        }

        context.DrawDataLabel(
            Chart,
            this,
            x,
            y,
            value,
            renderArea,
            DataLabelFormat,
            overrideColor,
            overrideFontSize,
            textAlign,
            ShowDataLabelBackground,
            DataLabelBackgroundColor.HasValue ? Chart.GetThemeColor(DataLabelBackgroundColor.Value) : null);
    }

    protected void RenderPoint(NTRenderContext context, float x, float y, SKColor color, float? pointSize = null, PointShape? pointShape = null, SKColor? strokeColor = null) {
        if (PointStyle == PointStyle.None) {
            return;
        }

        var size = pointSize ?? PointSize;
        var shape = pointShape ?? PointShape ?? (PointShape)(Chart.GetSeriesIndex(this) % Enum.GetValues<PointShape>().Length);

        context.DrawPoint(
            Chart,
            this,
            x,
            y,
            color,
            PointStyle,
            size,
            shape,
            strokeColor);
    }
}
