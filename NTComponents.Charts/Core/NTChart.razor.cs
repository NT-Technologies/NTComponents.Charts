using System.Diagnostics;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;
using NTComponents.Charts.Core.Axes;
using NTComponents.Charts.Core.Series;
using NTComponents.Core;
using NTComponents.Ext;
using SkiaSharp;
using SkiaSharp.Views.Blazor;

namespace NTComponents.Charts.Core;

/// <summary>
///     The base class for all charts in the NTComponents.Charts library.
/// </summary>
[CascadingTypeParameter(nameof(TData))]
public partial class NTChart<TData> : TnTDisposableComponentBase, IAxisChart where TData : class {

    /// <summary>
    ///     Gets or sets whether to allow exporting the chart as a PNG.
    /// </summary>
    [Parameter]
    public bool AllowExport { get; set; } = true;

    [Parameter]
    public TnTColor BackgroundColor { get; set; } = TnTColor.Surface;

    /// <summary>
    ///     Gets or sets whether to show debug information, such as render time per frame.
    /// </summary>
    [Parameter]
    public bool DebugView { get; set; }

    /// <summary>
    ///     Gets or sets the child content.
    /// </summary>
    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    /// <inheritdoc />
    public override string? ElementClass => CssClassBuilder.Create("nt-chart")
        .AddFromAdditionalAttributes(AdditionalAttributes)
        .Build();

    /// <inheritdoc />
    public override string? ElementStyle => CssStyleBuilder.Create()
        .AddFromAdditionalAttributes(AdditionalAttributes)
        .Build();

    [Parameter]
    public bool EnableHardwareAcceleration { get; set; } = true;

    /// <summary>
    ///     Gets or sets the duration of the hover animation.
    /// </summary>
    [Parameter]
    public TimeSpan HoverAnimationDuration { get; set; } = TimeSpan.FromMilliseconds(250);

    /// <summary>
    ///     Gets whether to use a categorical scale for the X axis.
    /// </summary>
    public bool IsCategoricalX => Series.OfType<NTBarSeries<TData>>().Any(s => s.Orientation == NTChartOrientation.Vertical && s.IsEffectivelyVisible);

    /// <summary>
    ///     Gets whether to use a categorical scale for the Y axis.
    /// </summary>
    public bool IsCategoricalY => Series.OfType<NTBarSeries<TData>>().Any(s => s.Orientation == NTChartOrientation.Horizontal && s.IsEffectivelyVisible);

    /// <summary>
    ///     Gets or sets the margin around the chart.
    /// </summary>
    [Parameter]
    public ChartMargin Margin { get; set; } = ChartMargin.All(5);

    /// <summary>
    ///     Gets or sets the default color palette for the chart.
    /// </summary>
    [Parameter]
    public List<(TnTColor Background, TnTColor Text)> Palette { get; set; } =
    [
        (TnTColor.PrimaryFixed, TnTColor.OnPrimaryFixed),
        (TnTColor.SecondaryFixed, TnTColor.OnSecondaryFixed),
        (TnTColor.TertiaryFixed, TnTColor.OnTertiaryFixed),
        (TnTColor.Primary, TnTColor.OnPrimary),
        (TnTColor.Secondary, TnTColor.OnSecondary),
        (TnTColor.Tertiary, TnTColor.OnTertiary)
    ];

    /// <summary>
    ///     Gets or sets the padding percentage for the axis ranges (0 to 1).
    /// </summary>
    [Parameter]
    public double RangePadding { get; set; } = 0.05;

    /// <summary>
    ///     Gets or sets the default text color for the chart (titles, labels).
    /// </summary>
    [Parameter]
    public TnTColor TextColor { get; set; } = TnTColor.OnSurface;

    /// <summary>
    ///     Gets or sets the title of the chart.
    /// </summary>
    [Parameter]
    public string? Title { get; set; }

    /// <summary>
    ///     Gets or sets the background color of the tooltip.
    /// </summary>
    [Parameter]
    public TnTColor TooltipBackgroundColor { get; set; } = TnTColor.SurfaceVariant;

    /// <summary>
    ///     Gets or sets the text color of the tooltip.
    /// </summary>
    [Parameter]
    public TnTColor TooltipTextColor { get; set; } = TnTColor.OnSurfaceVariant;

    internal SKFont DefaultFont => _defaultFont;
    internal TData? HoveredDataPoint { get; private set; }
    internal int? HoveredPointIndex { get; private set; }
    internal NTBaseSeries<TData>? HoveredSeries { get; private set; }

    public bool IsXAxisDateTime {
        get {
            foreach (var s in Series) {
                var first = s.Data?.Cast<TData>().FirstOrDefault();
                if (first != null && s.XValue(first) is DateTime) {
                    return true;
                }
            }
            return false;
        }
    }

    internal bool IsXPanEnabled => Series.Any(s => s.Interactions.HasFlag(ChartInteractions.XPan));
    internal bool IsXZoomEnabled => Series.Any(s => s.Interactions.HasFlag(ChartInteractions.XZoom));
    internal bool IsYPanEnabled => Series.Any(s => s.Interactions.HasFlag(ChartInteractions.YPan));
    internal bool IsYZoomEnabled => Series.Any(s => s.Interactions.HasFlag(ChartInteractions.YZoom));
    internal NTLegend<TData>? Legend { get; private set; }
    internal SKFont RegularFont => _regularFont;
    internal List<NTBaseSeries<TData>> Series { get; } = [];

    protected string CanvasStyle {
        get {
            if (HoveredSeries != null) {
                return "cursor: pointer;";
            }

            if (_isDraggingLegend || _isHoveringLegend) {
                return "cursor: move;";
            }

            return Series.Any(s => s.IsPanning) ? "cursor: grabbing;" : (IsXPanEnabled || IsYPanEnabled) ? "cursor: grab;" : "cursor: default;";
        }
    }

    [Inject]
    protected IJSRuntime JSRuntime { get; set; } = default!;

    internal SKRect LastLegendDrawArea { get; private set; }
    internal SKPoint? LastMousePosition { get; private set; }
    internal SKRect LastPlotArea { get; private set; }
    private static readonly SKFont _defaultFont = new(SKTypeface.FromFamilyName("Roboto", SKFontStyleWeight.Bold, SKFontStyleWidth.Normal, SKFontStyleSlant.Upright), 12);
    private static readonly SKFont _regularFont = new(SKTypeface.FromFamilyName("Roboto", SKFontStyleWeight.Medium, SKFontStyleWidth.Normal, SKFontStyleSlant.Upright), 12);
    private readonly Dictionary<TnTColor, SKColor> _resolvedColors = [];
    private readonly Dictionary<NTBaseSeries<TData>, SKRect> _treeMapAreas = [];
    private List<object>? _cachedAllX;
    private List<object>? _cachedAllY;
    internal float Density { get; private set; } = 1.0f;
    private bool _hasDraggedLegend;
    private bool _isDraggingLegend;
    private bool _isHovering;
    private bool _isHoveringLegend;
    private float _lastHeight;
    private double _lastRenderTimeMs;
    private float _lastWidth;
    private SKPoint _legendDragStartMousePos;
    private SKPoint _legendMouseOffset;
    private DotNetObjectReference<NTChart<TData>>? _objRef;
    private IJSObjectReference? _themeListener;

    /// <summary>
    ///     Exports the current chart as a PNG image.
    /// </summary>
    /// <param name="fileName">The name of the file to download. Defaults to "[Title].png" or "chart.png".</param>
    /// <returns>A <see cref="Task" /> representing the export operation.</returns>
    public async Task ExportAsPngAsync(string? fileName = null) {
        if (_lastWidth <= 0 || _lastHeight <= 0) {
            return;
        }

        var info = new SKImageInfo((int)_lastWidth, (int)_lastHeight);
        using var surface = SKSurface.Create(info);
        if (surface == null) {
            return;
        }

        OnPaintSurface(surface.Canvas, info);

        using var image = surface.Snapshot();
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        if (data == null) {
            return;
        }

        await using var stream = data.AsStream();
        await JSRuntime.DownloadFileFromStreamAsync(stream, fileName ?? $"{Title ?? "chart"}.png");
    }

    /// <summary>
    ///     Returns a list of all unique X values across all cartesian series, sorted.
    /// </summary>
    public List<object> GetAllXValues() {
        if (_cachedAllX != null) {
            return _cachedAllX;
        }

        var allX = new HashSet<object>();
        foreach (var s in Series.Where(s => s.IsEffectivelyVisible)) {
            s.RegisterXValues(allX);
        }

        _cachedAllX = [.. allX.OrderBy(x => x)];
        return _cachedAllX;
    }

    /// <summary>
    ///     Returns a list of all unique Y values across all cartesian series, sorted.
    /// </summary>
    public List<object> GetAllYValues() {
        if (_cachedAllY != null) {
            return _cachedAllY;
        }

        var allY = new HashSet<object>();
        foreach (var s in Series.Where(s => s.IsEffectivelyVisible)) {
            s.RegisterYValues(allY);
        }

        _cachedAllY = allY.OrderBy(y => y).ToList();
        return _cachedAllY;
    }

    public double GetScaledXValue(object? originalX) {
        if (originalX == null) {
            return 0;
        }

        if (IsCategoricalX) {
            var allX = GetAllXValues();
            var index = allX.IndexOf(originalX);
            return index >= 0 ? index : 0;
        }

        if (originalX is DateTime dt) {
            return dt.Ticks;
        }
        return originalX is IConvertible convertible ? convertible.ToDouble(null) : 0;
    }

    public decimal GetScaledYValue(object? originalY) {
        if (originalY == null) {
            return 0;
        }

        if (IsCategoricalY) {
            var allY = GetAllYValues();
            var index = allY.IndexOf(originalY);
            return index >= 0 ? index : 0;
        }

        if (originalY is DateTime dt) {
            return dt.Ticks;
        }
        return originalY is IConvertible convertible ? convertible.ToDecimal(null) : 0;
    }

    public (double Min, double Max) GetXRange(NTAxisOptions? axis, bool padded = false) {
        var uniqueAxes = GetUniqueXAxes();
        var effectiveAxis = axis ?? uniqueAxes.FirstOrDefault();

        if (effectiveAxis != null && effectiveAxis.CachedXRange.HasValue && padded) {
            return effectiveAxis.CachedXRange.Value;
        }

        foreach (var s in Series.OfType<NTCartesianSeries<TData>>().Where(s => s.IsEffectivelyVisible)) {
            if (ReferenceEquals(s.EffectiveXAxis, effectiveAxis)) {
                var v = s.GetViewXRange();
                if (v.HasValue) {
                    if (effectiveAxis != null && padded) effectiveAxis.CachedXRange = v.Value;
                    return v.Value;
                }
            }
        }

        if (IsCategoricalX) {
            var allX = GetAllXValues();
            if (!allX.Any()) {
                return (0, 1);
            }

            if (!padded) {
                return (0, Math.Max(1, allX.Count - 1));
            }

            var catRange = Math.Max(1, allX.Count - 1);
            var resultCat = (-catRange * RangePadding, catRange + (catRange * RangePadding));
            if (effectiveAxis != null) effectiveAxis.CachedXRange = resultCat;
            return resultCat;
        }

        var min = (double?)(effectiveAxis?.Min) ?? double.MaxValue;
        var max = (double?)(effectiveAxis?.Max) ?? double.MinValue;

        var seriesToConsider = Series.OfType<NTCartesianSeries<TData>>().Where(s => s.IsEffectivelyVisible && ReferenceEquals(s.EffectiveXAxis, effectiveAxis));

        foreach (var s in seriesToConsider) {
            var seriesRange = s.GetXRange();
            if (seriesRange.HasValue) {
                if (effectiveAxis?.Min == null) min = Math.Min(min, seriesRange.Value.Min);
                if (effectiveAxis?.Max == null) max = Math.Max(max, seriesRange.Value.Max);
            }
        }

        if (min == double.MaxValue) min = 0;
        if (max == double.MinValue) max = 1;

        if (!padded || (effectiveAxis?.Min != null && effectiveAxis?.Max != null)) {
            var resultNoPad = (min, max);
            if (effectiveAxis != null && padded) effectiveAxis.CachedXRange = resultNoPad;
            return resultNoPad;
        }

        var (niceMin, niceMax, _) = (effectiveAxis ?? NTXAxisOptions.Default).CalculateNiceScaling(min, max, (effectiveAxis ?? NTXAxisOptions.Default).MaxTicks);
        var result = (niceMin, niceMax);
        if (effectiveAxis != null) effectiveAxis.CachedXRange = result;
        return result;
    }

    /// <summary>
    ///     Gets the scale used for the X axis.
    /// </summary>
    public NTAxisScale GetXScale() => GetUniqueXAxes().FirstOrDefault()?.Scale ?? NTAxisScale.Linear;

    public (decimal Min, decimal Max) GetYRange(NTAxisOptions? axis, bool padded = false) {
        var uniqueAxes = GetUniqueYAxes();
        var effectiveAxis = axis ?? uniqueAxes.FirstOrDefault();

        if (effectiveAxis != null && effectiveAxis.CachedYRange.HasValue && padded) {
            return effectiveAxis.CachedYRange.Value;
        }

        foreach (var s in Series.OfType<NTCartesianSeries<TData>>().Where(s => s.IsEffectivelyVisible)) {
            if (ReferenceEquals(s.EffectiveYAxis, effectiveAxis)) {
                var v = s.GetViewYRange();
                if (v.HasValue) {
                    if (effectiveAxis != null && padded) effectiveAxis.CachedYRange = v.Value;
                    return v.Value;
                }
            }
        }

        if (IsCategoricalY) {
            var allY = GetAllYValues();
            if (!allY.Any()) {
                return (0, 1);
            }

            if (!padded) {
                return (0, Math.Max(1, allY.Count - 1));
            }

            var catRange = (decimal)Math.Max(1, allY.Count - 1);
            var rp = (decimal)RangePadding;
            var resultCat = (-catRange * rp, catRange + (catRange * rp));
            if (effectiveAxis != null) effectiveAxis.CachedYRange = resultCat;
            return resultCat;
        }

        var min = effectiveAxis?.Min ?? decimal.MaxValue;
        var max = effectiveAxis?.Max ?? decimal.MinValue;

        var seriesToConsider = Series.OfType<NTCartesianSeries<TData>>().Where(s => s.IsEffectivelyVisible && ReferenceEquals(s.EffectiveYAxis, effectiveAxis));

        foreach (var s in seriesToConsider) {
            var xView = s.GetViewXRange();
            var seriesRange = (IsXZoomEnabled && xView.HasValue)
                ? s.GetYRange(xView!.Value.Min, xView.Value.Max)
                : s.GetYRange();

            if (seriesRange.HasValue) {
                if (effectiveAxis?.Min == null) min = Math.Min(min, seriesRange.Value.Min);
                if (effectiveAxis?.Max == null) max = Math.Max(max, seriesRange.Value.Max);
            }
        }

        if (min == decimal.MaxValue) min = 0;
        if (max == decimal.MinValue) max = 1;

        // Bar charts should generally start at 0
        if (effectiveAxis?.Min == null && seriesToConsider.Any(s => s is NTBarSeries<TData>)) {
            min = Math.Min(0, min);
        }

        if (!padded || (effectiveAxis?.Min != null && effectiveAxis?.Max != null)) {
            var range = (min, max);
            if (effectiveAxis != null && padded) effectiveAxis.CachedYRange = range;
            return range;
        }

        var (niceMinY, niceMaxY, _) = (effectiveAxis ?? NTYAxisOptions.Default).CalculateNiceScaling((double)min, (double)max, (effectiveAxis ?? NTYAxisOptions.Default).MaxTicks);
        var result = ((decimal)niceMinY, (decimal)niceMaxY);
        if (effectiveAxis != null) effectiveAxis.CachedYRange = result;
        return result;
    }

    [JSInvokable]
    public async Task OnThemeChanged() {
        await ResolveColorsAsync();
        StateHasChanged();
    }

    /// <summary>
    ///     Resets the view to the default range.
    /// </summary>
    public void ResetView() {
        foreach (var s in Series) {
            s.ResetView();
        }
        StateHasChanged();
    }

    public float ScaleX(double x, SKRect plotArea, NTAxisOptions? axis = null) {
        axis ??= GetUniqueXAxes().FirstOrDefault();
        var (min, max) = GetXRange(axis, true);
        var scale = axis?.Scale ?? NTAxisScale.Linear;

        double t;
        if (scale == NTAxisScale.Logarithmic) {
            min = Math.Max(0.000001, min);
            max = Math.Max(min * 1.1, max);
            x = Math.Max(min, x);
            t = (Math.Log10(x) - Math.Log10(min)) / (Math.Log10(max) - Math.Log10(min));
        }
        else {
            var range = max - min;
            if (range <= 0) {
                return plotArea.Left;
            }
            t = (x - min) / range;
        }

        const float p = 3f; // 3 pixels of air
        var left = plotArea.Left + p;
        var width = plotArea.Width - (p * 2);
        return (float)(left + (t * width));
    }

    public double ScaleXInverse(float coord, SKRect plotArea, NTAxisOptions? axis = null) {
        axis ??= GetUniqueXAxes().FirstOrDefault();
        var (min, max) = GetXRange(axis, true);
        var scale = axis?.Scale ?? NTAxisScale.Linear;

        const float p = 3f;
        double t;
        var left = plotArea.Left + p;
        var width = plotArea.Width - (p * 2);
        t = width <= 0 ? 0 : (coord - left) / width;

        if (scale == NTAxisScale.Logarithmic) {
            min = Math.Max(0.000001, min);
            max = Math.Max(min * 1.1, max);
            return Math.Pow(10, Math.Log10(min) + (t * (Math.Log10(max) - Math.Log10(min))));
        }
        return min + (t * (max - min));
    }

    public float ScaleY(decimal y, NTAxisOptions? axis, SKRect plotArea) {
        var (min, max) = GetYRange(axis, true);
        var scale = axis?.Scale ?? NTAxisScale.Linear;

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

        const float p = 3f; // 3 pixels of air
        var bottom = plotArea.Bottom - p;
        var height = plotArea.Height - (p * 2);
        return (float)(bottom - (t * height));
    }

    /// <summary>
    ///     Converts a screen coordinate back to a data Y value.
    /// </summary>
    public decimal ScaleYInverse(float coord, NTAxisOptions? axis, SKRect plotArea) {
        var (min, max) = GetYRange(axis, true);
        var scale = axis?.Scale ?? NTAxisScale.Linear;

        const float p = 3f;
        double t;
        var bottom = plotArea.Bottom - p;
        var height = plotArea.Height - (p * 2);
        t = height <= 0 ? 0 : (bottom - coord) / height;

        if (scale == NTAxisScale.Logarithmic) {
            var dMin = Math.Max(0.000001, (double)min);
            var dMax = Math.Max(dMin * 1.1, (double)max);
            return (decimal)Math.Pow(10, Math.Log10(dMin) + (t * (Math.Log10(dMax) - Math.Log10(dMin))));
        }
        return min + ((decimal)t * (max - min));
    }

    internal void AddSeries(NTBaseSeries<TData> series) {
        if (!Series.Contains(series)) {
            Series.Add(series);
        }
    }

    internal void SetXAxisOptions(NTAxisOptions options) {
        _xAxisComponent = options;
    }

    internal void SetYAxisOptions(NTAxisOptions options) {
        _yAxisComponent = options;
    }

    internal void SetRadialAxisOptions(NTAxisOptions options) {
        _radialAxisComponent = options;
    }

    // IAxisChart implementation
    void IAxisChart.SetXAxisOptions(NTAxisOptions options) => SetXAxisOptions(options);
    void IAxisChart.SetYAxisOptions(NTAxisOptions options) => SetYAxisOptions(options);
    void IAxisChart.SetRadialAxisOptions(NTAxisOptions options) => SetRadialAxisOptions(options);
    void IAxisChart.SetTooltip(NTTooltip tooltip) => SetTooltip(tooltip);

    public NTXAxisOptions? PrimaryXAxis => _xAxisComponent as NTXAxisOptions;
    public NTYAxisOptions? PrimaryYAxis => _yAxisComponent as NTYAxisOptions;
    public NTRadialAxisOptions? PrimaryRadialAxis => _radialAxisComponent as NTRadialAxisOptions;

    NTXAxisOptions? IAxisChart.PrimaryXAxis => PrimaryXAxis;
    NTYAxisOptions? IAxisChart.PrimaryYAxis => PrimaryYAxis;
    NTRadialAxisOptions? IAxisChart.PrimaryRadialAxis => PrimaryRadialAxis;

    List<NTXAxisOptions> IAxisChart.GetUniqueXAxes() => GetUniqueXAxes();
    List<NTYAxisOptions> IAxisChart.GetUniqueYAxes() => GetUniqueYAxes();

    (double Min, double Max) IAxisChart.GetXRange(NTAxisOptions? axis, bool padded) => GetXRange(axis, padded);
    (decimal Min, decimal Max) IAxisChart.GetYRange(NTAxisOptions? axis, bool padded) => GetYRange(axis, padded);

    bool IAxisChart.HasViewRange(NTAxisOptions axis) {
        foreach (var s in Series.OfType<NTCartesianSeries<TData>>().Where(s => s.IsEffectivelyVisible)) {
            if (ReferenceEquals(s.EffectiveXAxis, axis) && s.GetViewXRange().HasValue) return true;
            if (ReferenceEquals(s.EffectiveYAxis, axis) && s.GetViewYRange().HasValue) return true;
        }
        return false;
    }

    float IAxisChart.ScaleX(double x, SKRect plotArea, NTAxisOptions? axis) => ScaleX(x, plotArea, axis);
    float IAxisChart.ScaleY(decimal y, NTAxisOptions? axis, SKRect plotArea) => ScaleY(y, axis, plotArea);

    SKFont IChart.DefaultFont => DefaultFont;
    SKFont IChart.RegularFont => RegularFont;
    SKColor IChart.GetThemeColor(TnTColor color) => GetThemeColor(color);
    float IChart.Density => Density;

    internal void SetTooltip(NTTooltip tooltip) {
        _tooltipComponent = tooltip;
    }

    private NTAxisOptions? _xAxisComponent;
    private NTAxisOptions? _yAxisComponent;
    private NTAxisOptions? _radialAxisComponent;
    private NTTooltip? _tooltipComponent;

    /// <summary>
    ///    Validates the current state of the chart and its components.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when the chart state is invalid.</exception>
    protected virtual void ValidateState() {
        if (Series.Count == 0) return;

        var firstCoordinateSystem = Series[0].CoordinateSystem;
        foreach (var s in Series) {
            if (s.CoordinateSystem != firstCoordinateSystem) {
                throw new InvalidOperationException($"Cannot combine series with different coordinate systems. Currently using {firstCoordinateSystem}, but tried to add {s.CoordinateSystem}.");
            }
        }

        if (firstCoordinateSystem == ChartCoordinateSystem.Cartesian) {
            var cartesianSeries = Series.OfType<NTCartesianSeries<TData>>().ToList();
            if (cartesianSeries.Any()) {
                var xAxis = cartesianSeries[0].XAxis;
                var yAxis1 = cartesianSeries[0].YAxis;
                NTYAxisOptions? yAxis2 = null;

                foreach (var s in cartesianSeries.Skip(1)) {
                    if (!ReferenceEquals(s.XAxis, xAxis)) {
                        throw new InvalidOperationException("All cartesian series must share the same X axis.");
                    }
                    if (!ReferenceEquals(s.YAxis, yAxis1)) {
                        if (yAxis2 is null) {
                            yAxis2 = s.YAxis;
                        }
                        else if (!ReferenceEquals(s.YAxis, yAxis2)) {
                            throw new InvalidOperationException("Cartesian series can only use up to two different Y axes.");
                        }
                    }
                }
            }

            // Panning/Zooming validation
            var interactiveSeries = Series.Where(s => s.Interactions != ChartInteractions.None).ToList();

            if (interactiveSeries.Any()) {
                var first = interactiveSeries[0];
                foreach (var s in Series.Where(s => s.IsEffectivelyVisible)) {
                    if (s.Interactions != first.Interactions) {
                        throw new InvalidOperationException("All visible series in the chart must have the same interaction configurations (Pan/Zoom flags) to ensure consistent behavior across shared axes.");
                    }
                }
            }
        }
    }

    internal float GetBarSeriesOffsetWeight(NTBarSeries<TData> series) {
        float weight = 0;
        foreach (var s in Series.OfType<NTBarSeries<TData>>()) {
            if (s == series) {
                return weight;
            }

            if (s.YAxis == series.YAxis) {
                weight += s.VisibilityFactor;
            }
        }
        return weight;
    }

    internal float GetBarSeriesTotalWeight(NTYAxisOptions? axis) => Series.OfType<NTBarSeries<TData>>().Where(s => s.YAxis == axis).Sum(s => s.VisibilityFactor);

    internal SKColor GetPaletteColor(int index) => _resolvedColors.TryGetValue(Palette[index % Palette.Count].Background, out var skColor) ? skColor : SKColors.Gray;

    internal SKColor GetPaletteTextColor(int index) {
        var color = Palette[index % Palette.Count].Text;
        if (_resolvedColors.TryGetValue(color, out var skColor)) {
            return skColor;
        }

        var bgColor = GetPaletteColor(index);
        var luminance = (0.2126f * bgColor.Red) + (0.7152f * bgColor.Green) + (0.0722f * bgColor.Blue);
        return luminance > 128 ? SKColors.Black : SKColors.White;
    }

    internal SKColor GetSeriesColor(NTBaseSeries<TData> series) {
        var color = series.Color ?? TnTColor.None;
        if (color == TnTColor.None) {
            var index = GetSeriesIndex(series);
            if (index >= 0) {
                color = Palette[index % Palette.Count].Background;
            }
        }

        return _resolvedColors.TryGetValue(color, out var skColor) ? skColor : SKColors.Gray;
    }

    internal int GetSeriesIndex(NTBaseSeries<TData> series) => Series.IndexOf(series);

    internal SKColor GetSeriesTextColor(NTBaseSeries<TData> series) {
        if (series.TextColor.HasValue && series.TextColor != TnTColor.None) {
            return GetThemeColor(series.TextColor.Value);
        }

        var index = GetSeriesIndex(series);
        if (index >= 0) {
            var color = Palette[index % Palette.Count].Text;
            if (_resolvedColors.TryGetValue(color, out var skColor)) {
                return skColor;
            }
        }

        // Final fallback: Use black or white based on series background color brightness
        var bgColor = GetSeriesColor(series);
        var luminance = (0.2126f * bgColor.Red) + (0.7152f * bgColor.Green) + (0.0722f * bgColor.Blue);
        return luminance > 128 ? SKColors.Black : SKColors.White;
    }

    internal SKColor GetThemeColor(TnTColor color) => _resolvedColors.TryGetValue(color, out var skColor) ? skColor : SKColors.Black;

    internal List<NTXAxisOptions> GetUniqueXAxes() {
        var axes = Series.OfType<NTCartesianSeries<TData>>()
            .Where(s => s.IsEffectivelyVisible)
            .Select(s => s.XAxis)
            .OfType<NTXAxisOptions>()
            .Distinct()
            .ToList();

        if (_xAxisComponent != null && _xAxisComponent is NTXAxisOptions x && !axes.Contains(x)) {
            axes.Add(x);
        }

        if (axes.Count == 0) {
            axes.Add(NTXAxisOptions.Default);
        }

        return axes;
    }

    internal List<NTYAxisOptions> GetUniqueYAxes() {
        var axes = Series.OfType<NTCartesianSeries<TData>>()
            .Where(s => s.IsEffectivelyVisible)
            .Select(s => s.YAxis)
            .OfType<NTYAxisOptions>()
            .Distinct()
            .ToList();

        if (_yAxisComponent != null && _yAxisComponent is NTYAxisOptions y && !axes.Contains(y)) {
            axes.Add(y);
        }

        if (axes.Count == 0) {
            axes.Add(NTYAxisOptions.Default);
        }

        return axes;
    }

    internal void RemoveLegend(NTLegend<TData> legend) {
        if (Legend == legend) {
            Legend = null;
        }
    }

    internal void RemoveSeries(NTBaseSeries<TData> series) {
        if (Series.Contains(series)) {
            Series.Remove(series);
        }
    }

    internal void SetLegend(NTLegend<TData> legend) => Legend = legend;

    protected override void Dispose(bool disposing) {
        base.Dispose(disposing);
    }

    protected override async ValueTask DisposeAsyncCore() {
        _objRef?.Dispose();
        if (_themeListener != null) {
            await _themeListener.InvokeVoidAsync("dispose");
            await _themeListener.DisposeAsync();
        }
    }

    protected override async Task OnAfterRenderAsync(bool firstRender) {
        if (firstRender) {
            Density = await JSRuntime.InvokeAsync<float>("eval", "window.devicePixelRatio || 1");
            _objRef = DotNetObjectReference.Create(this);
            _themeListener = await JSRuntime.InvokeAsync<IJSObjectReference>("NTComponents.onThemeChanged", _objRef);
            await ResolveColorsAsync();
            StateHasChanged();
        }
    }

    protected virtual void OnClick(MouseEventArgs e) {
        if (_hasDraggedLegend) {
            _hasDraggedLegend = false;
            return;
        }

        var point = new SKPoint((float)e.OffsetX * Density, (float)e.OffsetY * Density);

        if (Legend != null && Legend.Visible && Legend.Position != LegendPosition.None) {
            var clickedItem = Legend.GetItemAtPoint(point, LastPlotArea, LastLegendDrawArea, Density);

            if (clickedItem != null) {
                if (clickedItem.Series != null) {
                    clickedItem.Series.ToggleLegendItem(clickedItem.Index);
                    StateHasChanged();
                }
                return;
            }
        }
    }

    protected virtual void OnMouseDown(MouseEventArgs e) {
        var point = new SKPoint((float)e.OffsetX * Density, (float)e.OffsetY * Density);

        if (Legend != null && Legend.Visible && Legend.Position == LegendPosition.Floating) {
            var rect = Legend.GetFloatingRect(LastPlotArea, Density);
            if (rect.Contains(point)) {
                _isDraggingLegend = true;
                _hasDraggedLegend = false;
                _legendDragStartMousePos = point;
                _legendMouseOffset = new SKPoint(point.X - rect.Left, point.Y - rect.Top);
                return;
            }
        }

        foreach (var s in Series.Where(s => s.IsEffectivelyVisible)) {
            s.HandleMouseDown(e);
        }
    }

    protected virtual void OnMouseMove(MouseEventArgs e) {
        LastMousePosition = new SKPoint((float)e.OffsetX * Density, (float)e.OffsetY * Density);

        if (_isDraggingLegend && Legend != null && LastPlotArea != default) {
            var currentPoint = LastMousePosition.Value;

            // Check for a small movement threshold to allow clicking
            if (!_hasDraggedLegend) {
                var dx = currentPoint.X - _legendDragStartMousePos.X;
                var dy = currentPoint.Y - _legendDragStartMousePos.Y;
                if (Math.Sqrt((dx * dx) + (dy * dy)) > 5) {
                    _hasDraggedLegend = true;
                }
            }

            if (_hasDraggedLegend) {
                var newX = currentPoint.X - _legendMouseOffset.X - LastPlotArea.Left;
                var newY = currentPoint.Y - _legendMouseOffset.Y - LastPlotArea.Top;
                Legend.FloatingOffset = new SKPoint(newX, newY);
                StateHasChanged();
            }
            return;
        }

        foreach (var s in Series.Where(s => s.IsEffectivelyVisible)) {
            s.HandleMouseMove(e);
        }

        StateHasChanged();
    }

    protected virtual void OnMouseOut(MouseEventArgs e) {
        LastMousePosition = null;
        HoveredSeries = null;
        HoveredPointIndex = null;
        HoveredDataPoint = null;
        _isHovering = false;
        _isHoveringLegend = false;
        StateHasChanged();
    }

    protected virtual void OnMouseUp(MouseEventArgs e) {
        foreach (var s in Series.Where(s => s.IsEffectivelyVisible)) {
            s.HandleMouseUp(e);
        }
        _isDraggingLegend = false;
    }

    protected void OnPaintSurface(SKPaintGLSurfaceEventArgs e) => OnPaintSurface(e.Surface.Canvas, e.Info);

    protected void OnPaintSurface(SKPaintSurfaceEventArgs e) => OnPaintSurface(e.Surface.Canvas, e.Info);

    /// <summary>
    ///     Handles the paint surface event from the SkiaSharp view.
    /// </summary>
    protected void OnPaintSurface(SKCanvas canvas, SKImageInfo info) {
        try {
            ValidateState();
        }
        catch (Exception ex) {
            // Render error instead of crashing the surface
            using var paint = new SKPaint { Color = SKColors.Red, IsAntialias = true };
            using var font = new SKFont { Size = 14 * Density };
            canvas.Clear(SKColors.White);
            canvas.DrawText($"Chart Error: {ex.Message}", 10 * Density, 30 * Density, font, paint);
            return;
        }

        var sw = DebugView ? Stopwatch.StartNew() : null;

        _lastWidth = info.Width;
        _lastHeight = info.Height;

        // Reset hover state and cache for this frame calculation
        HoveredSeries = null;
        HoveredPointIndex = null;
        HoveredDataPoint = null;
        _isHoveringLegend = false;
        _treeMapAreas.Clear();
        _cachedAllX = null;
        _cachedAllY = null;

        foreach (var axis in GetUniqueXAxes()) {
            axis.ClearCache();
        }
        foreach (var axis in GetUniqueYAxes()) {
            axis.ClearCache();
        }

        var totalArea = new SKRect(0, 0, info.Width, info.Height);
        var context = new NTRenderContext(
            canvas,
            info,
            totalArea,
            Density,
            _defaultFont,
            _regularFont,
            GetThemeColor(TextColor));

        canvas.Clear(GetThemeColor(BackgroundColor));

        if (!string.IsNullOrEmpty(Title)) {
            RenderTitle(context);
        }

        var renderArea = new SKRect(
            Margin.Left * context.Density,
            Margin.Top * context.Density,
            info.Width - (Margin.Right * context.Density),
            info.Height - (Margin.Bottom * context.Density));

        if (!string.IsNullOrEmpty(Title)) {
            // TODO Implement Title options
            renderArea = new SKRect(renderArea.Left, renderArea.Top + (30 * context.Density), renderArea.Right, renderArea.Bottom);
        }
        var plotArea = renderArea;
        var accessibleArea = renderArea; // Area available for axes and legend

        // Pass 1: Measure legend and update plotArea/accessibleArea
        SKRect legendDrawArea = default;
        if (Legend?.Visible == true && Legend.Position != LegendPosition.None && Legend.Position != LegendPosition.Floating) {
            legendDrawArea = Legend.Measure(plotArea, context);

            if (legendDrawArea != SKRect.Empty) {
                // Adjust plotArea
                if (Legend.Position == LegendPosition.Bottom) {
                    plotArea = new SKRect(plotArea.Left, plotArea.Top, plotArea.Right, legendDrawArea.Top);
                }
                else if (Legend.Position == LegendPosition.Top) {
                    plotArea = new SKRect(plotArea.Left, legendDrawArea.Bottom, plotArea.Right, plotArea.Bottom);
                }
                else if (Legend.Position == LegendPosition.Left) {
                    plotArea = new SKRect(legendDrawArea.Right, plotArea.Top, plotArea.Right, plotArea.Bottom);
                }
                else if (Legend.Position == LegendPosition.Right) {
                    plotArea = new SKRect(plotArea.Left, plotArea.Top, legendDrawArea.Left, plotArea.Bottom);
                }
            }

            // Adjust accessibleArea for axes so they don't overlap legend
            if (Legend.Position == LegendPosition.Bottom) {
                accessibleArea = new SKRect(accessibleArea.Left, accessibleArea.Top, accessibleArea.Right, legendDrawArea.Top);
            }
            else if (Legend.Position == LegendPosition.Top) {
                accessibleArea = new SKRect(accessibleArea.Left, legendDrawArea.Bottom, accessibleArea.Right, accessibleArea.Bottom);
            }
            else if (Legend.Position == LegendPosition.Left) {
                accessibleArea = new SKRect(legendDrawArea.Right, accessibleArea.Top, accessibleArea.Right, accessibleArea.Bottom);
            }
            else if (Legend.Position == LegendPosition.Right) {
                accessibleArea = new SKRect(accessibleArea.Left, accessibleArea.Top, legendDrawArea.Left, accessibleArea.Bottom);
            }
        }

        LastLegendDrawArea = legendDrawArea;
        context.PlotArea = plotArea;

        // Pass 2: Measure series (axes etc) and update plotArea
        var measured = new HashSet<object>();
        if (_xAxisComponent != null && _xAxisComponent.Visible) {
            plotArea = _xAxisComponent.Measure(plotArea, context, this);
            measured.Add(_xAxisComponent);
        }
        if (_yAxisComponent != null && _yAxisComponent.Visible) {
            plotArea = _yAxisComponent.Measure(plotArea, context, this);
            measured.Add(_yAxisComponent);
        }
        if (_radialAxisComponent != null && _radialAxisComponent.Visible) {
            plotArea = _radialAxisComponent.Measure(plotArea, context, this);
            measured.Add(_radialAxisComponent);
        }

        foreach (var series in Series.Where(s => s.Visible)) {
            plotArea = series.Measure(plotArea, context, measured);
        }

        LastPlotArea = plotArea;
        context.PlotArea = plotArea;
        CalculateTreeMapAreas(plotArea);

        // Pass 3: Render axes using the final plotArea and the adjusted accessibleArea
        var rendered = new HashSet<object>();
        if (_xAxisComponent != null && _xAxisComponent.Visible) {
            _xAxisComponent.Render(context, this);
            rendered.Add(_xAxisComponent);
        }
        if (_yAxisComponent != null && _yAxisComponent.Visible) {
            _yAxisComponent.Render(context, this);
            rendered.Add(_yAxisComponent);
        }
        if (_radialAxisComponent != null && _radialAxisComponent.Visible) {
            _radialAxisComponent.Render(context, this);
            rendered.Add(_radialAxisComponent);
        }

        foreach (var series in Series.Where(s => s.Visible)) {
            series.RenderAxes(context, rendered);
        }

        // Pass 4: Hit testing and Tooltip Prep
        if (LastMousePosition.HasValue) {
            var mousePoint = LastMousePosition.Value;

            // Check legend items for hover first (so they take precedence over lines)
            if (Legend != null && Legend.Visible && Legend.Position != LegendPosition.None) {
                var item = Legend.GetItemAtPoint(mousePoint, plotArea, legendDrawArea, context.Density);
                if (item != null) {
                    HoveredSeries = item.Series;
                    HoveredPointIndex = item.Index;
                }
                else if (Legend.Position == LegendPosition.Floating) {
                    var rect = Legend.GetFloatingRect(plotArea, context.Density);
                    if (rect.Contains(mousePoint)) {
                        _isHoveringLegend = true;
                    }
                }
            }

            // Check series/points if legend wasn't hit
            if (HoveredSeries == null && plotArea.Contains(mousePoint)) {
                foreach (var series in Series.AsEnumerable().Reverse().Where(s => s.Visible)) {
                    var seriesRenderArea = GetSeriesRenderArea(series, plotArea, renderArea);
                    var hit = series.HitTest(mousePoint, seriesRenderArea);
                    if (hit != null) {
                        HoveredSeries = series;
                        HoveredPointIndex = hit.Value.Index;
                        HoveredDataPoint = hit.Value.Data;
                        break;
                    }
                }
            }
        }

        // Pass 5: Render Series
        canvas.Save();

        // Inflate the clip rect slightly so we don't cutoff line thickness or point markers
        var clipArea = plotArea;
        clipArea.Inflate(2, 2);
        canvas.ClipRect(clipArea);

        // Render inactive series first
        foreach (var series in Series.Where(s => s != HoveredSeries && s.IsEffectivelyVisible)) {
            var seriesRenderArea = GetSeriesRenderArea(series, plotArea, renderArea);
            series.Render(context, seriesRenderArea);
        }
        // Render active series last (on top)
        if (HoveredSeries != null && HoveredSeries.IsEffectivelyVisible) {
            var seriesRenderArea = GetSeriesRenderArea(HoveredSeries, plotArea, renderArea);
            HoveredSeries.Render(context, seriesRenderArea);
        }
        canvas.Restore();

        // Pass 6: Render TreeMap group labels if needed
        RenderTreeMapGroupLabels(context);

        // Pass 7: Render Tooltip
        if (HoveredDataPoint != null && LastMousePosition.HasValue && HoveredSeries != null && HoveredSeries.Visible) {
            RenderTooltip(context, plotArea);
        }

        // Pass 8: Render legend (Now after hit testing so it can react to hovered series)
        if (Legend != null && Legend.Visible && Legend.Position != LegendPosition.None) {
            if (Legend.Position == LegendPosition.Floating) {
                Legend.Render(context, plotArea, renderArea);
            }
            else {
                Legend.Render(context, plotArea, legendDrawArea);
            }
        }

        // Pass 9: Update cursor if needed
        var currentHover = HoveredSeries != null || _isHoveringLegend || Series.Any(s => s.IsPanning);
        if (_isHovering != currentHover) {
            _isHovering = currentHover;
            _ = InvokeAsync(StateHasChanged);
        }

        if (sw != null) {
            sw.Stop();
            _lastRenderTimeMs = sw.Elapsed.TotalMilliseconds;
            RenderDebugInfo(context);
        }
    }

    private void RenderDebugInfo(NTRenderContext context) {
        using var paint = new SKPaint {
            Color = SKColors.Black.WithAlpha(160),
            Style = SKPaintStyle.Fill
        };
        context.Canvas.DrawRect(0, 0, context.Info.Width, 24 * context.Density, paint);

        using var textPaint = new SKPaint {
            Color = SKColors.White,
            IsAntialias = true
        };

        using var typeface = SKTypeface.FromFamilyName("monospace");
        using var font = new SKFont(typeface, 12 * context.Density);

        context.Canvas.DrawText($"Render: {_lastRenderTimeMs:F2} ms", 10 * context.Density, 16 * context.Density, font, textPaint);
    }

    protected virtual void OnWheel(WheelEventArgs e) {
        foreach (var s in Series.Where(s => s.IsEffectivelyVisible)) {
            s.HandleMouseWheel(e);
        }

        StateHasChanged();
    }

    private void CalculateTreeMapAreas(SKRect plotArea) {
        _treeMapAreas.Clear();
        var treeMapSeries = Series.Where(s => s.CoordinateSystem == ChartCoordinateSystem.TreeMap && s.IsEffectivelyVisible).ToList();
        if (!treeMapSeries.Any()) {
            return;
        }

        if (treeMapSeries.Count == 1) {
            _treeMapAreas[treeMapSeries[0]] = plotArea;
            return;
        }

        var seriesData = treeMapSeries.Select(s => new SeriesLayoutItem(s, s.GetTotalValue())).ToList();
        var totalValue = seriesData.Sum(s => s.Value);
        if (totalValue <= 0) {
            totalValue = seriesData.Count;
            seriesData = treeMapSeries.Select(s => new SeriesLayoutItem(s, 1.0m)).ToList();
        }

        PartitionArea(seriesData, plotArea, totalValue, true);
    }

    private SKRect GetSeriesRenderArea(NTBaseSeries<TData> series, SKRect plotArea, SKRect totalArea) {
        if (series.CoordinateSystem == ChartCoordinateSystem.TreeMap) {
            if (_treeMapAreas.TryGetValue(series, out var area)) {
                var treeMapSeries = Series.Where(s => s.CoordinateSystem == ChartCoordinateSystem.TreeMap && s.IsEffectivelyVisible).ToList();
                if (treeMapSeries.Count > 1) {
                    // Shave off top for series title
                    return new SKRect(area.Left, area.Top + 20, area.Right, area.Bottom);
                }
                return area;
            }
        }

        if (series.CoordinateSystem == ChartCoordinateSystem.Circular &&
            Legend != null && Legend.Visible && (Legend.Position == LegendPosition.Left || Legend.Position == LegendPosition.Right)) {
            var centerX = totalArea.MidX;
            var centerY = totalArea.MidY;

            var dx = Math.Min(centerX - plotArea.Left, plotArea.Right - centerX);
            var dy = Math.Min(centerY - plotArea.Top, plotArea.Bottom - centerY);

            return new SKRect(centerX - dx, centerY - dy, centerX + dx, centerY + dy);
        }
        return plotArea;
    }

    private void PartitionArea(List<SeriesLayoutItem> items, SKRect area, decimal totalValue, bool horizontal) {
        if (!items.Any()) {
            return;
        }

        if (items.Count == 1) {
            _treeMapAreas[items[0].Series] = area;
            return;
        }

        var mid = items.Count / 2;
        var leftItems = items.Take(mid).ToList();
        var rightItems = items.Skip(mid).ToList();

        var leftValue = leftItems.Sum(x => x.Value);
        var rightValue = rightItems.Sum(x => x.Value);
        var total = leftValue + rightValue;

        if (total <= 0) {
            return;
        }

        if (horizontal) {
            var leftWidth = (float)(area.Width * (float)(leftValue / total));
            var leftArea = new SKRect(area.Left, area.Top, area.Left + leftWidth, area.Bottom);
            var rightArea = new SKRect(area.Left + leftWidth, area.Top, area.Right, area.Bottom);
            PartitionArea(leftItems, leftArea, leftValue, !horizontal);
            PartitionArea(rightItems, rightArea, rightValue, !horizontal);
        }
        else {
            var topHeight = (float)(area.Height * (float)(leftValue / total));
            var topArea = new SKRect(area.Left, area.Top, area.Right, area.Top + topHeight);
            var bottomArea = new SKRect(area.Left, area.Top + topHeight, area.Right, area.Bottom);
            PartitionArea(leftItems, topArea, leftValue, !horizontal);
            PartitionArea(rightItems, bottomArea, rightValue, !horizontal);
        }
    }

    private void RenderTitle(NTRenderContext context) {
        using var paint = new SKPaint {
            Color = GetThemeColor(TextColor),
            IsAntialias = true,
            Style = SKPaintStyle.Fill
        };

        using var font = new SKFont {
            Size = 20 * context.Density,
            Embolden = true,
            Typeface = context.DefaultFont.Typeface
        };

        var x = (Margin.Left * context.Density) + ((context.Info.Width - (Margin.Left * context.Density) - (Margin.Right * context.Density)) / 2);
        var y = (Margin.Top * context.Density) + (20 * context.Density);

        context.Canvas.DrawText(Title!, x, y, SKTextAlign.Center, font, paint);
    }

    private void RenderTooltip(NTRenderContext context, SKRect plotArea) {
        if (HoveredSeries == null || HoveredDataPoint == null || !LastMousePosition.HasValue) {
            return;
        }

        if (_tooltipComponent != null && !_tooltipComponent.Enabled) {
            return;
        }

        var canvas = context.Canvas;
        var mousePoint = LastMousePosition.Value;
        var tooltipInfo = HoveredSeries.GetTooltipInfo(HoveredDataPoint);

        if (tooltipInfo.Lines.Count == 0) {
            return;
        }

        var bgColor = GetThemeColor(_tooltipComponent?.BackgroundColor ?? HoveredSeries.TooltipBackgroundColor ?? TooltipBackgroundColor);
        var textColor = GetThemeColor(_tooltipComponent?.TextColor ?? HoveredSeries.TooltipTextColor ?? TooltipTextColor);
        var subTextColor = textColor.WithAlpha(200);

        using var fontHeader = new SKFont {
            Size = 11 * context.Density,
            Typeface = context.RegularFont.Typeface
        };
        using var fontLabel = new SKFont {
            Size = 12 * context.Density,
            Typeface = context.RegularFont.Typeface
        };
        using var fontValue = new SKFont {
            Size = 12 * context.Density,
            Typeface = context.DefaultFont.Typeface
        };

        var padding = 8f * context.Density;
        var headerHeight = string.IsNullOrEmpty(tooltipInfo.Header) ? 0 : fontHeader.Size + (6 * context.Density);
        var separatorHeight = string.IsNullOrEmpty(tooltipInfo.Header) ? 0 : 6 * context.Density;
        var lineHeight = 18f * context.Density;
        var iconSize = 8f * context.Density;
        var iconSpacing = 8f * context.Density;

        float maxWidth = 0;
        if (!string.IsNullOrEmpty(tooltipInfo.Header)) {
            maxWidth = fontHeader.MeasureText(tooltipInfo.Header);
        }

        foreach (var line in tooltipInfo.Lines) {
            var lineWidth = iconSize + iconSpacing + fontLabel.MeasureText(line.Label + ": ") + fontValue.MeasureText(line.Value);
            maxWidth = Math.Max(maxWidth, lineWidth);
        }

        var totalWidth = maxWidth + (padding * 2);
        var totalHeight = headerHeight + separatorHeight + (tooltipInfo.Lines.Count * lineHeight) + (padding * 2) - (2 * context.Density);

        var rect = new SKRect(mousePoint.X + (15 * context.Density), mousePoint.Y - totalHeight - (15 * context.Density), mousePoint.X + (15 * context.Density) + totalWidth, mousePoint.Y - (15 * context.Density));

        if (rect.Right > _lastWidth - (Margin.Right * context.Density)) {
            rect.Offset(-(totalWidth + (30 * context.Density)), 0);
        }
        if (rect.Top < Margin.Top * context.Density) {
            rect.Offset(0, totalHeight + (30 * context.Density));
        }

        using var bgPaint = new SKPaint {
            Color = bgColor.WithAlpha(250),
            Style = SKPaintStyle.Fill,
            IsAntialias = true
        };
        canvas.DrawRoundRect(rect, 4 * context.Density, 4 * context.Density, bgPaint);

        using var borderPaint = new SKPaint {
            Color = GetThemeColor(TnTColor.OutlineVariant),
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 1,
            IsAntialias = true
        };
        canvas.DrawRoundRect(rect, 4 * context.Density, 4 * context.Density, borderPaint);

        var currentY = rect.Top + padding;

        if (!string.IsNullOrEmpty(tooltipInfo.Header)) {
            using var headerPaint = new SKPaint { Color = subTextColor, IsAntialias = true };
            canvas.DrawText(tooltipInfo.Header, rect.Left + padding, currentY + fontHeader.Size - (2 * context.Density), SKTextAlign.Left, fontHeader, headerPaint);
            currentY += headerHeight;

            using var separatorPaint = new SKPaint { Color = GetThemeColor(TnTColor.OutlineVariant), StrokeWidth = 1, IsAntialias = true };
            canvas.DrawLine(rect.Left, currentY - (4 * context.Density), rect.Right, currentY - (4 * context.Density), separatorPaint);
            currentY += separatorHeight - (4 * context.Density);
        }

        foreach (var line in tooltipInfo.Lines) {
            var centerX = rect.Left + padding + (iconSize / 2);
            var centerY = currentY + (lineHeight / 2) - (1 * context.Density);

            using var iconPaint = new SKPaint { Color = line.Color, Style = SKPaintStyle.Fill, IsAntialias = true };
            canvas.DrawCircle(centerX, centerY, iconSize / 2, iconPaint);

            var textX = rect.Left + padding + iconSize + iconSpacing;
            var textY = currentY + (14 * context.Density);

            using var labelPaint = new SKPaint { Color = subTextColor, IsAntialias = true };
            canvas.DrawText(line.Label + ": ", textX, textY, SKTextAlign.Left, fontLabel, labelPaint);

            var labelWidth = fontLabel.MeasureText(line.Label + ": ");

            using var valuePaint = new SKPaint { Color = textColor, IsAntialias = true };
            canvas.DrawText(line.Value, textX + labelWidth, textY, SKTextAlign.Left, fontValue, valuePaint);

            currentY += lineHeight;
        }
    }

    private void RenderTreeMapGroupLabels(NTRenderContext context) {
        var canvas = context.Canvas;
        var treeMapSeries = Series.Where(s => s.CoordinateSystem == ChartCoordinateSystem.TreeMap && s.IsEffectivelyVisible).ToList();
        if (treeMapSeries.Count <= 1) {
            return;
        }

        foreach (var series in treeMapSeries) {
            if (!_treeMapAreas.TryGetValue(series, out var area)) {
                continue;
            }

            using var paint = new SKPaint {
                Color = GetSeriesTextColor(series).WithAlpha((byte)(255 * series.VisibilityFactor)),
                IsAntialias = true,
                Style = SKPaintStyle.Fill
            };

            using var font = new SKFont {
                Size = 14 * context.Density,
                Embolden = true,
                Typeface = context.DefaultFont.Typeface
            };

            var title = series.Title ?? "Series";
            // Measure text to make sure it fits
            var textWidth = font.MeasureText(title);
            if (area.Width < textWidth + (10 * context.Density) || area.Height < (20 * context.Density)) {
                continue;
            }

            // Draw at top left of the area with a small offset
            canvas.DrawText(title, area.Left + (5 * context.Density), area.Top + (15 * context.Density), SKTextAlign.Left, font, paint);
        }
    }

    private async Task ResolveColorsAsync() {
        var colorsToResolve = Enum.GetValues<TnTColor>().ToList();
        foreach (var color in colorsToResolve) {
            if (color is TnTColor.None or TnTColor.Transparent) {
                _resolvedColors[color] = SKColors.Transparent;
                continue;
            }
            if (color == TnTColor.Black) {
                _resolvedColors[color] = SKColors.Black;
                continue;
            }
            if (color == TnTColor.White) {
                _resolvedColors[color] = SKColors.White;
                continue;
            }

            var hex = await JSRuntime.InvokeAsync<string>("NTComponents.getColorValueFromEnumName", color.ToString());
            if (!string.IsNullOrEmpty(hex) && SKColor.TryParse(hex, out var skColor)) {
                _resolvedColors[color] = skColor;
            }
            else {
                // Fallback for primary/secondary etc if not found in CSS
                _resolvedColors[color] = color switch {
                    TnTColor.Primary => SKColors.RoyalBlue,
                    TnTColor.Secondary => SKColors.Gray,
                    _ => SKColors.Gray
                };
            }
        }
    }

    private record SeriesLayoutItem(NTBaseSeries<TData> Series, decimal Value);
}