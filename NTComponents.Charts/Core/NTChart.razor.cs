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
public partial class NTChart<TData> : TnTDisposableComponentBase, IChart<TData> where TData : class {

    internal void RegisterLegend(NTLegend<TData> legend) {
        if (Legend is not null) {
            throw new InvalidOperationException("Only one legend can be registered to the chart.");
        }
        Legend = legend;
    }

    internal void UnregisterLegend(NTLegend<TData> legend) {
        if (ReferenceEquals(Legend, legend)) {
            Legend = null;
        }
    }

    protected override void OnParametersSet() {
        base.OnParametersSet();
        if (TitleOptions != null) {
            _title = new(this);
        }
        else {
            _title?.Dispose();
            _title = null;
        }


    }

    public INTXAxis<TData> XAxis { get; private set; } = _defaultXAxis;

    public void RegisterAxis(INTXAxis<TData> axis) {
        ArgumentNullException.ThrowIfNull(axis, nameof(axis));
        if (!ReferenceEquals(XAxis, _defaultXAxis)) {
            throw new InvalidOperationException("Only one X axis can be registered to the chart.");
        }

        XAxis = axis;
    }

    public void UnregisterAxis(INTXAxis<TData> axis) {
        if (ReferenceEquals(XAxis, axis)) {
            XAxis = _defaultXAxis;
        }
    }
    private static readonly INTYAxis<TData> _defaultYAxis = NTYAxisOptions<TData, decimal>.Default;
    private static readonly INTXAxis<TData> _defaultXAxis = NTXAxisOptions<TData, object>.Default;

    public INTYAxis<TData> YAxis { get; private set; } = _defaultYAxis;
    public INTYAxis<TData>? SecondaryYAxis { get; private set; }

    public void RegisterAxis(INTYAxis<TData> axis) {
        ArgumentNullException.ThrowIfNull(axis, nameof(axis));
        if (ReferenceEquals(YAxis, _defaultYAxis)) {
            YAxis = axis;
        }
        else if (SecondaryYAxis is null) {
            SecondaryYAxis = axis;
        }
        else {
            throw new InvalidOperationException("Only two Y axes can be registered to the chart.");
        }
    }

    public void UnregisterAxis(INTYAxis<TData> axis) {
        if (ReferenceEquals(YAxis, axis)) {
            if (SecondaryYAxis is not null) {
                YAxis = SecondaryYAxis;
                SecondaryYAxis = null;
            }
            else {
                YAxis = _defaultYAxis;
            }
        }
        else if (ReferenceEquals(SecondaryYAxis, axis)) {
            SecondaryYAxis = null;
        }
    }

    private NTTitle<TData>? _title;
    private bool _invalidate;
    private readonly Dictionary<RenderOrdered, List<IRenderable>> _renderablesByOrder = Enum.GetValues<RenderOrdered>().ToDictionary(r => r, _ => new List<IRenderable>());
    private bool _defaultAxesAttached;
    public void RegisterRenderable(IRenderable renderable) {
        ArgumentNullException.ThrowIfNull(renderable, nameof(renderable));
        var list =
        _renderablesByOrder[renderable.RenderOrder];
        if (!list.Contains(renderable)) {
            list.Add(renderable);
            _invalidate = true;
        }
    }
    public void UnregisterRenderable(IRenderable renderable) {
        _renderablesByOrder[renderable.RenderOrder].Remove(renderable);
        _invalidate = true;
    }

    public void Invalidate() {
        foreach (var renderable in _renderablesByOrder.SelectMany(kvp => kvp.Value)) {
            renderable.Invalidate();
        }
        _invalidate = false;
    }

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
    ///     Gets or sets the background color of the tooltip.
    /// </summary>
    [Parameter]
    public TnTColor TooltipBackgroundColor { get; set; } = TnTColor.SurfaceVariant;

    /// <summary>
    ///     Gets or sets the text color of the tooltip.
    /// </summary>
    [Parameter]
    public TnTColor TooltipTextColor { get; set; } = TnTColor.OnSurfaceVariant;

    public SKFont DefaultFont => _defaultFont;
    public TData? HoveredDataPoint { get; private set; }
    public int? HoveredPointIndex { get; private set; }
    public NTBaseSeries<TData>? HoveredSeries { get; private set; }

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
    public NTLegend<TData>? Legend { get; private set; }
    public SKFont RegularFont => _regularFont;
    public List<NTBaseSeries<TData>> Series { get; } = [];

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

    public SKPoint? LastMousePosition { get; private set; }
    internal SKRect LastPlotArea { get; private set; }
    private static readonly SKFont _defaultFont = new(SKTypeface.FromFamilyName("Roboto", SKFontStyleWeight.Bold, SKFontStyleWidth.Normal, SKFontStyleSlant.Upright), 12);
    private static readonly SKFont _regularFont = new(SKTypeface.FromFamilyName("Roboto", SKFontStyleWeight.Medium, SKFontStyleWidth.Normal, SKFontStyleSlant.Upright), 12);
    private readonly Dictionary<TnTColor, SKColor> _resolvedColors = [];
    private readonly Dictionary<NTBaseSeries<TData>, SKRect> _treeMapAreas = [];
    private List<object>? _cachedAllX;
    private List<object>? _cachedAllY;
    public float Density { get; private set; } = 1.0f;
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

    // Cached paints and fonts to avoid allocations in render loop
    private SKPaint? _errorPaint;
    private SKFont? _errorFont;
    private SKPaint? _debugBgPaint;
    private SKPaint? _debugTextPaint;
    private SKFont? _debugFont;
    private SKPaint? _treeMapGroupPaint;
    private SKFont? _treeMapGroupFont;

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
        await JSRuntime.DownloadFileFromStreamAsync(stream, fileName ?? $"{TitleOptions?.Title ?? "chart"}.png");
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

        if (XAxis?.IsCategorical == true) {
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

        if (originalY is DateTime dt) {
            return dt.Ticks;
        }
        return originalY is IConvertible convertible ? convertible.ToDecimal(null) : 0;
    }

    public (double Min, double Max) GetXRange(NTAxisOptions<TData>? axis, bool padded = false) {
        throw new NotImplementedException();
        //if (XAxis != null && XAxis.CachedXRange.HasValue && padded) {
        //    return XAxis.CachedXRange.Value;
        //}

        //foreach (var s in Series.OfType<NTCartesianSeries<TData>>().Where(s => s.IsEffectivelyVisible)) {
        //    var v = s.GetViewXRange();
        //    if (v.HasValue) {
        //        if (XAxis != null && padded)
        //            XAxis.CachedXRange = v.Value;
        //        return v.Value;
        //    }
        //}

        //    if (!allX.Any()) {
        //        return (0, 1);
        //    }

        //    if (!padded) {
        //        return (0, Math.Max(1, allX.Count - 1));
        //    }

        //    var catRange = Math.Max(1, allX.Count - 1);
        //    var resultCat = (-catRange * RangePadding, catRange + (catRange * RangePadding));
        //    if (XAxis != null)
        //        XAxis.CachedXRange = resultCat;
        //    return resultCat;
        //}

        //var min = (double?)(XAxis?.Min) ?? double.MaxValue;
        //var max = (double?)(XAxis?.Max) ?? double.MinValue;

        //var seriesToConsider = Series.OfType<NTCartesianSeries<TData>>().Where(s => s.IsEffectivelyVisible);

        //foreach (var s in seriesToConsider) {
        //    var seriesRange = s.GetXRange();
        //    if (seriesRange.HasValue) {
        //        if (XAxis?.Min == null)
        //            min = Math.Min(min, seriesRange.Value.Min);
        //        if (XAxis?.Max == null)
        //            max = Math.Max(max, seriesRange.Value.Max);
        //    }
        //}

        //if (min == double.MaxValue)
        //    min = 0;
        //if (max == double.MinValue)
        //    max = 1;

        //if (!padded || (XAxis?.Min != null && XAxis?.Max != null)) {
        //    var resultNoPad = (min, max);
        //    if (XAxis != null && padded)
        //        XAxis.CachedXRange = resultNoPad;
        //    return resultNoPad;
        //}

        //var (niceMin, niceMax, _) = (XAxis ?? NTXAxisOptions<TData>.Default).CalculateNiceScaling(min, max, (XAxis ?? NTXAxisOptions<TData>.Default).MaxTicks);
        //var result = (niceMin, niceMax);
        //if (XAxis != null)
        //    XAxis.CachedXRange = result;
        //return result;
    }

    public (decimal Min, decimal Max) GetYRange(NTAxisOptions<TData>? axis, bool padded = false) {
        throw new NotImplementedException();
        //if (YAxis != null && YAxis.CachedYRange.HasValue && padded) {
        //    return YAxis.CachedYRange.Value;
        //}

        //foreach (var s in Series.OfType<NTCartesianSeries<TData>>().Where(s => s.IsEffectivelyVisible)) {
        //    var v = s.GetViewYRange();
        //    if (v.HasValue) {
        //        if (YAxis != null && padded)
        //            YAxis.CachedYRange = v.Value;
        //        return v.Value;
        //    }
        //}

        //    var allY = GetAllYValues();
        //    if (!allY.Any()) {
        //        return (0, 1);
        //    }

        //    if (!padded) {
        //        return (0, Math.Max(1, allY.Count - 1));
        //    }

        //    var catRange = (decimal)Math.Max(1, allY.Count - 1);
        //    var rp = (decimal)RangePadding;
        //    var resultCat = (-catRange * rp, catRange + (catRange * rp));
        //    if (YAxis != null)
        //        YAxis.CachedYRange = resultCat;
        //    return resultCat;
        //}

        //var min = YAxis?.Min ?? decimal.MaxValue;
        //var max = YAxis?.Max ?? decimal.MinValue;

        //var seriesToConsider = Series.OfType<NTCartesianSeries<TData>>().Where(s => s.IsEffectivelyVisible);

        //foreach (var s in seriesToConsider) {
        //    var xView = s.GetViewXRange();
        //    var seriesRange = (IsXZoomEnabled && xView.HasValue)
        //        ? s.GetYRange(xView!.Value.Min, xView.Value.Max)
        //        : s.GetYRange();

        //    if (seriesRange.HasValue) {
        //        if (YAxis?.Min == null)
        //            min = Math.Min(min, seriesRange.Value.Min);
        //        if (YAxis?.Max == null)
        //            max = Math.Max(max, seriesRange.Value.Max);
        //    }
        //}

        //if (min == decimal.MaxValue)
        //    min = 0;
        //if (max == decimal.MinValue)
        //    max = 1;

        //// Bar charts should generally start at 0
        //if (YAxis?.Min == null && seriesToConsider.Any(s => s is NTBarSeries<TData>)) {
        //    min = Math.Min(0, min);
        //}

        //if (!padded || (YAxis?.Min != null && YAxis?.Max != null)) {
        //    var range = (min, max);
        //    if (YAxis != null && padded)
        //        YAxis.CachedYRange = range;
        //    return range;
        //}

        //var (niceMinY, niceMaxY, _) = (YAxis ?? NTYAxisOptions<TData>.Default).CalculateNiceScaling((double)min, (double)max, (YAxis ?? NTYAxisOptions<TData>.Default).MaxTicks);
        //var result = ((decimal)niceMinY, (decimal)niceMaxY);
        //if (YAxis != null)
        //    YAxis.CachedYRange = result;
        //return result;
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

    public float ScaleX(double x, SKRect plotArea) {

        //var (min, max) = GetXRange(XAxis, true);
        //var scale = XAxis.Scale;

        //double t;
        //if (scale == NTAxisScale.Logarithmic) {
        //    min = Math.Max(0.000001, min);
        //    max = Math.Max(min * 1.1, max);
        //    x = Math.Max(min, x);
        //    t = (Math.Log10(x) - Math.Log10(min)) / (Math.Log10(max) - Math.Log10(min));
        //}
        //else {
        //    var range = max - min;
        //    if (range <= 0) {
        //        return plotArea.Left;
        //    }
        //    t = (x - min) / range;
        //}

        //const float p = 3f; // 3 pixels of air
        //var left = plotArea.Left + p;
        //var width = plotArea.Width - (p * 2);
        //return (float)(left + (t * width));
        return 0;
    }

    public double ScaleXInverse(float coord, SKRect plotArea) {
        //var (min, max) = GetXRange(XAxis, true);
        //var scale = XAxis?.Scale ?? NTAxisScale.Linear;

        //const float p = 3f;
        //double t;
        //var left = plotArea.Left + p;
        //var width = plotArea.Width - (p * 2);
        //t = width <= 0 ? 0 : (coord - left) / width;

        //if (scale == NTAxisScale.Logarithmic) {
        //    min = Math.Max(0.000001, min);
        //    max = Math.Max(min * 1.1, max);
        //    return Math.Pow(10, Math.Log10(min) + (t * (Math.Log10(max) - Math.Log10(min))));
        //}
        //return min + (t * (max - min));
        return 0;
    }



    public float ScaleY(decimal y, SKRect plotArea) {
        //var (min, max) = GetYRange(YAxis, true);
        //var scale = YAxis?.Scale ?? NTAxisScale.Linear;

        //double t;
        //if (scale == NTAxisScale.Logarithmic) {
        //    var dMin = Math.Max(0.000001, (double)min);
        //    var dMax = Math.Max(dMin * 1.1, (double)max);
        //    var dy = Math.Max(dMin, (double)y);
        //    t = (Math.Log10(dy) - Math.Log10(dMin)) / (Math.Log10(dMax) - Math.Log10(dMin));
        //}
        //else {
        //    var range = max - min;
        //    if (range <= 0) {
        //        return plotArea.Bottom;
        //    }
        //    t = (double)((y - min) / range);
        //}

        //const float p = 3f; // 3 pixels of air
        //var bottom = plotArea.Bottom - p;
        //var height = plotArea.Height - (p * 2);
        //return (float)(bottom - (t * height));
        return 1;
    }

    /// <summary>
    ///     Converts a screen coordinate back to a data Y value.
    /// </summary>
    public decimal ScaleYInverse(float coord, NTAxisOptions<TData>? axis, SKRect plotArea) {
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

    internal void RegisterSeries(NTBaseSeries<TData> series) {
        if (!Series.Contains(series)) {
            Series.Add(series);
        }


        ValidateState();
    }



    [Parameter]
    public NTTitleOptions? TitleOptions { get; set; }

    /// <summary>
    ///    Validates the current state of the chart and its components.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when the chart state is invalid.</exception>
    private void ValidateState() {
        if (Series.Count == 0) {
            return;
        }


        if (Series.Select(s => s.CoordinateSystem).Distinct().Count() > 1) {
            throw new InvalidOperationException("All series in the chart must use the same coordinate system.");
        }

        _chartCoordSystem = Series[0].CoordinateSystem;
        if (!Series.All(s => s.CoordinateSystem == _chartCoordSystem)) {
            throw new InvalidOperationException("All series in the chart must use the same coordinate system.");
        }

        // Validate XValue types across series
        Type? commonXType = null;
        foreach (var series in Series) {
            var firstItem = series.Data?.FirstOrDefault();
            if (firstItem != null) {
                var xVal = series.XValue(firstItem);
                if (xVal != null) {
                    var currentType = xVal.GetType();
                    if (commonXType == null) {
                        commonXType = currentType;
                    }
                    else if (commonXType != currentType) {
                        throw new InvalidOperationException("All series in the chart must have the same X value type.");
                    }
                }
            }
        }

        if (_chartCoordSystem == ChartCoordinateSystem.Cartesian) {
            var cartesianSeries = Series.Select(s => s as NTCartesianSeries<TData>).Where(s => s is not null).ToArray();
            if (cartesianSeries.Length != Series.Count) {
                throw new InvalidOperationException("All series must be of type NTCartesianSeries when using Cartesian coordinate system.");
            }

            // Panning/Zooming validation
            var firstInteractions = cartesianSeries[0]!.Interactions;
            if (!cartesianSeries.All(s => s!.Interactions == firstInteractions)) {
                throw new InvalidOperationException("All cartesian series must have the same interaction configurations (Pan/Zoom flags) to ensure consistent behavior across shared axes.");
            }
        }
    }

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

    public SKColor GetThemeColor(TnTColor color) => _resolvedColors.TryGetValue(color, out var skColor) ? skColor : SKColors.Black;

    internal void UnregisterSeries(NTBaseSeries<TData> series) {
        if (Series.Contains(series)) {
            Series.Remove(series);
        }
    }

    protected override void Dispose(bool disposing) {
        if (disposing) {
            _errorPaint?.Dispose();
            _errorFont?.Dispose();
            _debugBgPaint?.Dispose();
            _debugTextPaint?.Dispose();
            _debugFont?.Dispose();
            _treeMapGroupPaint?.Dispose();
            _treeMapGroupFont?.Dispose();
        }
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
            var clickedItem = Legend.GetItemAtPoint(point, LastPlotArea, Legend.LastDrawArea, Density);

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
        if (_invalidate) {
            Invalidate();
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

        var totalArea = new SKRect(0, 0, info.Width, info.Height);
        var renderArea = new SKRect(
            left: Margin.Left * Density,
            top: Margin.Top * Density,
            right: info.Width - (Margin.Right * Density),
            bottom: info.Height - (Margin.Bottom * Density));

        var context = new NTRenderContext {
            Canvas = canvas,
            Info = info,
            TotalArea = totalArea,
            Density = Density,
            DefaultFont = _defaultFont,
            RegularFont = _regularFont,
            TextColor = GetThemeColor(TextColor),
            PlotArea = renderArea
        };

        canvas.Clear(GetThemeColor(BackgroundColor));


        foreach (var order in Enum.GetValues<RenderOrdered>().Where(x => x is RenderOrdered.Title or RenderOrdered.Legend or RenderOrdered.Axis)) {

            if (order is RenderOrdered.Axis && _chartCoordSystem is ChartCoordinateSystem.Cartesian) {
                var axisArea = renderArea;
                foreach (var renderable in _renderablesByOrder[order]) {
                    axisArea = renderable.Render(context, axisArea);
                }

                var xAxisArea = new SKRect(axisArea.Left, renderArea.Top, axisArea.Right, renderArea.Bottom);
                XAxis.Render(context, xAxisArea);

                var yAxisArea = new SKRect(renderArea.Left, axisArea.Top, renderArea.Right, axisArea.Bottom);
                YAxis.Render(context, yAxisArea);

                if (SecondaryYAxis is not null) {
                    var secondaryYAxisArea = new SKRect(renderArea.Left, axisArea.Top, renderArea.Right, axisArea.Bottom);
                    SecondaryYAxis.Render(context, secondaryYAxisArea);
                }
            }
            else {
                foreach (var renderable in _renderablesByOrder[order]) {
                    renderArea = renderable.Render(context, renderArea);
                }
            }


        }

        // Pass 9: Update cursor if needed

        if (sw != null) {
            sw.Stop();
            _lastRenderTimeMs = sw.Elapsed.TotalMilliseconds;
            RenderDebugInfo(context);
        }
    }

    private ChartCoordinateSystem _chartCoordSystem;

    private void PerformHitTesting(NTRenderContext context, SKRect plotArea) {
        if (!LastMousePosition.HasValue) {
            return;
        }

        var mousePoint = LastMousePosition.Value;

        // Check legend items for hover (if legend is present)
        if (Legend != null && Legend.Visible && Legend.Position != LegendPosition.None) {
            // Note: LastLegendDrawArea might still be useful if we want to hit test exactly where it was drawn
            // but for now let's assume it's calculated during its Render pass. 
            // In a more pure model, NTLegend would handle its own hit testing.

            if (Legend.Position == LegendPosition.Floating) {
                var rect = Legend.GetFloatingRect(plotArea, context.Density);
                if (rect.Contains(mousePoint)) {
                    _isHoveringLegend = true;
                }
            }
            // Non-floating hit testing is usually handled in OnClick or via some other mechanism currently.
        }

        // Check series/points
        if (HoveredSeries == null && plotArea.Contains(mousePoint)) {
            foreach (var series in Series.AsEnumerable().Reverse().Where(s => s.Visible)) {
                // In the decentralized model, each series should handle its own hit testing relative to plotArea
                var hit = series.HitTest(mousePoint, plotArea);
                if (hit != null) {
                    HoveredSeries = series;
                    HoveredPointIndex = hit.Value.Index;
                    HoveredDataPoint = hit.Value.Data;
                    break;
                }
            }
        }
    }


    private void RenderDebugInfo(NTRenderContext context) {
        _debugBgPaint ??= new SKPaint {
            Color = SKColors.Black.WithAlpha(160),
            Style = SKPaintStyle.Fill
        };
        context.Canvas.DrawRect(0, 0, context.Info.Width, 24 * context.Density, _debugBgPaint);

        _debugTextPaint ??= new SKPaint {
            Color = SKColors.White,
            IsAntialias = true
        };

        if (_debugFont == null) {
            var typeface = SKTypeface.FromFamilyName("monospace");
            _debugFont = new SKFont(typeface, 12 * context.Density);
        }
        _debugFont.Size = 12 * context.Density;

        context.Canvas.DrawText($"Render: {_lastRenderTimeMs:F2} ms", 10 * context.Density, 16 * context.Density, _debugFont, _debugTextPaint);
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

    private void RenderTreeMapGroupLabels(NTRenderContext context) {
        var canvas = context.Canvas;
        var treeMapSeries = Series.Where(s => s.CoordinateSystem == ChartCoordinateSystem.TreeMap && s.IsEffectivelyVisible).ToList();
        if (treeMapSeries.Count <= 1) {
            return;
        }

        _treeMapGroupPaint ??= new SKPaint {
            IsAntialias = true,
            Style = SKPaintStyle.Fill
        };

        _treeMapGroupFont ??= new SKFont {
            Embolden = true,
            Typeface = context.DefaultFont.Typeface
        };
        _treeMapGroupFont.Size = 14 * context.Density;

        foreach (var series in treeMapSeries) {
            if (!_treeMapAreas.TryGetValue(series, out var area)) {
                continue;
            }

            _treeMapGroupPaint.Color = GetSeriesTextColor(series).WithAlpha((byte)(255 * series.VisibilityFactor));

            var title = series.Title ?? "Series";
            // Measure text to make sure it fits
            var textWidth = _treeMapGroupFont.MeasureText(title);
            if (area.Width < textWidth + (10 * context.Density) || area.Height < (20 * context.Density)) {
                continue;
            }

            // Draw at top left of the area with a small offset
            canvas.DrawText(title, area.Left + (5 * context.Density), area.Top + (15 * context.Density), SKTextAlign.Left, _treeMapGroupFont, _treeMapGroupPaint);
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

    public decimal ScaleYInverse(float coord, SKRect plotArea) => throw new NotImplementedException();
    public bool HasViewRange(NTAxisOptions<TData> axis) => throw new NotImplementedException();

    private record SeriesLayoutItem(NTBaseSeries<TData> Series, decimal Value);
}