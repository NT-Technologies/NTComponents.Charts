using System.Diagnostics;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;
using NTComponents.Charts;
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
        if (TitleOptions is null) {
            _title?.Dispose();
            _title = null;
            _lastTitleOptionsRef = null;
        }
        else {
            // Keep a single title renderable instance while title options are enabled.
            _title ??= new(this);
            if (!ReferenceEquals(_lastTitleOptionsRef, TitleOptions)) {
                _title.Invalidate();
                _lastTitleOptionsRef = TitleOptions;
            }
        }
    }

    public INTXAxis<TData> XAxis { get; private set; }

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
    private readonly INTYAxis<TData> _defaultYAxis;
    private readonly INTXAxis<TData> _defaultXAxis;

    public INTYAxis<TData> YAxis { get; private set; }
    public INTYAxis<TData>? SecondaryYAxis { get; private set; }

    public NTChart() {
        _defaultYAxis = new NTYAxisOptions<TData, decimal> {
            ValueSelector = _ => 0m
        };
        _defaultXAxis = new NTXAxisOptions<TData, object> {
            ValueSelector = _ => default!
        };

        XAxis = _defaultXAxis;
        YAxis = _defaultYAxis;
        AttachDefaultAxes();
    }

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
    private NTTitleOptions? _lastTitleOptionsRef;
    private bool _invalidate;
    private readonly Dictionary<RenderOrdered, List<IRenderable>> _renderablesByOrder = Enum.GetValues<RenderOrdered>().ToDictionary(r => r, _ => new List<IRenderable>());
    private bool _defaultAxesAttached;

    private void AttachDefaultAxes() {
        if (_defaultAxesAttached) {
            return;
        }

        if (_defaultXAxis is NTAxisOptions<TData> xAxis) {
            xAxis.AttachChart(this);
        }

        if (_defaultYAxis is NTAxisOptions<TData> yAxis) {
            yAxis.AttachChart(this);
        }

        _defaultAxesAttached = true;
    }
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
    ///     Optional chart annotations rendered over the plot area.
    /// </summary>
    [Parameter]
    public IReadOnlyList<NTChartAnnotation> Annotations { get; set; } = Array.Empty<NTChartAnnotation>();

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
    internal LegendItemInfo<TData>? HoveredLegendItem { get; private set; }
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
            if (_isDraggingLegend) {
                return "cursor: move;";
            }

            if (_isHoveringLegend) {
                return "cursor: pointer;";
            }

            if (HoveredSeries != null) {
                return "cursor: pointer;";
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
    private Dictionary<object, int>? _cachedXIndexMap;
    public float Density { get; private set; } = 1.0f;

    public NTDateGroupingLevel GetActiveDateGroupingLevel() {
        if (!IsXAxisDateTime || !XAxis.EnableAutoDateGrouping) {
            return NTDateGroupingLevel.None;
        }

        var (min, max) = GetXRange(XAxis as NTAxisOptions<TData>, true);
        var plotWidth = LastPlotArea.Width > 0f
            ? LastPlotArea.Width
            : Math.Max(1f, _lastWidth * Math.Max(0.1f, Density));

        return XAxis.ResolveDateGroupingLevel(min, max, plotWidth, Math.Max(0.1f, Density));
    }

    private bool _hasDraggedLegend;
    private bool _isDraggingLegend;
    private bool _isHoveringLegend;
    private NTBaseSeries<TData>? _lastHoverNotifiedSeries;
    private string? _lastHoverNotifiedLegendKey;
    private int? _lastHoverNotifiedPointIndex;
    private long _lastHoverHitTimestamp;
    private int _hoverMissStreak;
    private float _lastHeight;
    private double _lastRenderTimeMs;
    private float _lastWidth;
    private SKPoint _legendDragStartMousePos;
    private SKPoint _legendDragStartOffset;
    private ElementReference _interactionHost;
    private IJSObjectReference? _chartModule;
    private DotNetObjectReference<NTChart<TData>>? _objRef;
    private IJSObjectReference? _themeListener;
    private IJSObjectReference? _wheelListener;
    private long _lastUiRefreshTimestamp;
    private long _lastInteractionTimestamp;

    // Per-frame scale range caches to avoid O(points * range-calculation) cost.
    private bool _useFrameScaleCache;
    private bool _frameXRangeReady;
    private bool _framePrimaryYRangeReady;
    private bool _frameSecondaryYRangeReady;
    private (double Min, double Max) _frameXRange;
    private (decimal Min, decimal Max) _framePrimaryYRange;
    private (decimal Min, decimal Max) _frameSecondaryYRange;

    // Cached paints and fonts to avoid allocations in render loop
    private SKPaint? _errorPaint;
    private SKFont? _errorFont;
    private SKPaint? _debugBgPaint;
    private SKPaint? _debugTextPaint;
    private SKFont? _debugFont;
    private SKPaint? _treeMapGroupPaint;
    private SKFont? _treeMapGroupFont;
    private SKPaint? _annotationLinePaint;
    private SKPaint? _annotationFillPaint;
    private SKPaint? _annotationTextPaint;
    private SKPaint? _annotationLabelBgPaint;
    private SKFont? _annotationFont;

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
            _cachedXIndexMap ??= GetAllXValues()
                .Select((x, index) => new { x, index })
                .ToDictionary(item => item.x, item => item.index);

            return _cachedXIndexMap.TryGetValue(originalX, out var index) ? index : 0;
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
        var cartesianSeries = Series.OfType<NTCartesianSeries<TData>>().Where(s => s.IsEffectivelyVisible).ToList();
        if (!cartesianSeries.Any()) {
            return (0, 1);
        }

        var horizontalBars = cartesianSeries
            .OfType<NTBarSeries<TData>>()
            .Where(s => s.Orientation == NTChartOrientation.Horizontal)
            .ToList();
        var hasHorizontalBars = horizontalBars.Count > 0;

        var viewRanges = cartesianSeries
            .Select(s => s.GetViewXRange())
            .Where(r => r.HasValue)
            .Select(r => r!.Value)
            .ToList();
        var hasViewRange = viewRanges.Count > 0;

        if (XAxis.IsCategorical && !hasHorizontalBars) {
            var allX = GetAllXValues();
            if (!allX.Any()) {
                return (0, 1);
            }

            double min = 0;
            double max = Math.Max(1, allX.Count - 1);

            if (hasViewRange) {
                min = viewRanges.Min(v => v.Min);
                max = viewRanges.Max(v => v.Max);
            }

            if (!padded || hasViewRange) {
                return NormalizeRange(min, max);
            }

            var catRange = Math.Max(1, max - min);
            var padding = catRange * RangePadding;
            // Bar charts need at least half-category edge padding so first/last bars are not clipped.
            if (Series.OfType<NTBarSeries<TData>>().Any(s => s.IsEffectivelyVisible && s.Orientation == NTChartOrientation.Vertical)) {
                padding = Math.Max(padding, 0.5);
            }
            return NormalizeRange(min - padding, max + padding);
        }

        double dataMin = double.MaxValue;
        double dataMax = double.MinValue;

        foreach (var s in cartesianSeries) {
            var range = s.GetXRange();
            if (!range.HasValue) {
                continue;
            }

            dataMin = Math.Min(dataMin, range.Value.Min);
            dataMax = Math.Max(dataMax, range.Value.Max);
        }

        if (hasViewRange) {
            dataMin = viewRanges.Min(v => v.Min);
            dataMax = viewRanges.Max(v => v.Max);
        }
        else if (horizontalBars.Count > 0) {
            // Horizontal bar value axis floor: default to 0 for all-positive, otherwise data min.
            // Can be overridden by ValueAxisMinimum on NTBarSeries.
            var overrideMins = horizontalBars
                .Where(s => s.ValueAxisMinimum.HasValue)
                .Select(s => (double)s.ValueAxisMinimum!.Value)
                .ToList();

            if (overrideMins.Count > 0) {
                dataMin = overrideMins.Min();
            }
            else if (dataMin > 0) {
                dataMin = 0;
            }
        }

        if (dataMin == double.MaxValue || dataMax == double.MinValue) {
            return (0, 1);
        }

        if (!padded || hasViewRange) {
            return NormalizeRange(dataMin, dataMax);
        }

        var rangeSize = Math.Max(1e-9, dataMax - dataMin);
        var rangePadding = rangeSize * RangePadding;

        if (hasHorizontalBars) {
            if (dataMin >= 0) {
                // All-positive horizontal bars should start at the axis with no left padding.
                return NormalizeRange(dataMin, dataMax + rangePadding);
            }

            if (dataMax <= 0) {
                // All-negative horizontal bars should end at the axis with no right padding.
                return NormalizeRange(dataMin - rangePadding, dataMax);
            }
        }

        return NormalizeRange(dataMin - rangePadding, dataMax + rangePadding);
    }

    public (decimal Min, decimal Max) GetYRange(NTAxisOptions<TData>? axis, bool padded = false) {
        var useSecondaryAxis = axis is not null && SecondaryYAxis is not null && ReferenceEquals(axis, SecondaryYAxis);
        var absoluteMinimum = (axis as INTYAxis<TData>)?.AbsoluteMinimum;


        var cartesianSeries = Series
            .OfType<NTCartesianSeries<TData>>()
            .Where(s => s.IsEffectivelyVisible && s.UseSecondaryYAxis == useSecondaryAxis)
            .ToList();

        if (!cartesianSeries.Any()) {
            return (0, 1);
        }

        var hasHorizontalBars = cartesianSeries
            .OfType<NTBarSeries<TData>>()
            .Any(s => s.Orientation == NTChartOrientation.Horizontal);
        var verticalBars = cartesianSeries
            .OfType<NTBarSeries<TData>>()
            .Where(s => s.Orientation == NTChartOrientation.Vertical)
            .ToList();

        var viewRanges = cartesianSeries
            .Select(s => s.GetViewYRange())
            .Where(r => r.HasValue)
            .Select(r => r!.Value)
            .ToList();
        var hasViewRange = viewRanges.Count > 0;

        decimal min = decimal.MaxValue;
        decimal max = decimal.MinValue;

        var xAxisOptions = XAxis as NTAxisOptions<TData>;
        var hasXViewRange = xAxisOptions is not null && HasViewRange(xAxisOptions);
        var viewXRange = hasXViewRange ? GetXRange(xAxisOptions, true) : (Min: 0d, Max: 0d);

        foreach (var s in cartesianSeries) {
            var yRange = hasXViewRange ? s.GetYRange(viewXRange.Min, viewXRange.Max) : s.GetYRange();
            if (!yRange.HasValue) {
                continue;
            }

            min = Math.Min(min, yRange.Value.Min);
            max = Math.Max(max, yRange.Value.Max);
        }

        if (hasViewRange) {
            min = viewRanges.Min(v => v.Min);
            max = viewRanges.Max(v => v.Max);
        }

        // Bar chart value axis floor: default to 0 for all-positive, otherwise data min.
        // Can be overridden by ValueAxisMinimum on NTBarSeries.
        if (!hasViewRange && verticalBars.Count > 0) {
            var overrideMins = verticalBars
                .Where(s => s.ValueAxisMinimum.HasValue)
                .Select(s => s.ValueAxisMinimum!.Value)
                .ToList();

            if (overrideMins.Count > 0) {
                min = overrideMins.Min();
            }
            else if (min > 0) {
                min = 0;
            }
        }

        if (min == decimal.MaxValue || max == decimal.MinValue) {
            return (0, 1);
        }

        if (!padded || hasViewRange) {
            return NormalizeYRange(min, max, absoluteMinimum);
        }

        var rangeSize = Math.Max(0.0000001m, max - min);
        var rangePadding = rangeSize * (decimal)RangePadding;
        if (verticalBars.Count > 0) {
            // Keep the lower bound anchored for bar charts (0/data-min/override-min),
            // and only pad the top.
            return NormalizeYRange(min, max + rangePadding, absoluteMinimum);
        }

        // Horizontal bar charts use Y as the category axis and need half-category edge padding.
        if (hasHorizontalBars) {
            rangePadding = Math.Max(rangePadding, 0.5m);
        }
        return NormalizeYRange(min - rangePadding, max + rangePadding, absoluteMinimum);
    }
    [JSInvokable]
    public async Task OnThemeChanged() {
        await ResolveColorsAsync();
        Invalidate();
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

    /// <summary>
    ///     Applies an explicit X-axis view range to all visible cartesian series in the chart.
    /// </summary>
    /// <param name="min">The minimum X value for the view range, or <see langword="null" /> to clear the range.</param>
    /// <param name="max">The maximum X value for the view range, or <see langword="null" /> to clear the range.</param>
    /// <param name="resetYRange">When <see langword="true" />, clears per-series Y view ranges so they refit to the new X window.</param>
    public void SetViewXRange(double? min, double? max, bool resetYRange = true) {
        foreach (var series in Series.OfType<NTCartesianSeries<TData>>().Where(s => s.IsEffectivelyVisible)) {
            series.SetViewXRange(min, max);
            if (resetYRange) {
                series.SetViewYRange(null, null);
            }
        }

        StateHasChanged();
    }

    public float ScaleX(double x, SKRect plotArea) {
        var (min, max) = GetScaleXRange();
        var scale = (XAxis as NTAxisOptions<TData>)?.Scale ?? NTAxisScale.Linear;

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

        const float p = 3f;
        var left = plotArea.Left + p;
        var width = plotArea.Width - (p * 2);
        return (float)(left + (t * width));
    }

    public double ScaleXInverse(float coord, SKRect plotArea) {
        var (min, max) = GetScaleXRange();
        var scale = (XAxis as NTAxisOptions<TData>)?.Scale ?? NTAxisScale.Linear;

        const float p = 3f;
        var left = plotArea.Left + p;
        var width = plotArea.Width - (p * 2);
        var t = width <= 0 ? 0 : (coord - left) / width;

        if (scale == NTAxisScale.Logarithmic) {
            min = Math.Max(0.000001, min);
            max = Math.Max(min * 1.1, max);
            return Math.Pow(10, Math.Log10(min) + (t * (Math.Log10(max) - Math.Log10(min))));
        }

        return min + (t * (max - min));
    }



    public float ScaleY(decimal y, SKRect plotArea) => ScaleY(y, YAxis as NTAxisOptions<TData>, plotArea);

    private float ScaleY(decimal y, NTAxisOptions<TData>? axis, SKRect plotArea) {
        var (min, max) = GetScaleYRange(axis);
        var absoluteMinimum = (axis as INTYAxis<TData>)?.AbsoluteMinimum;
        var scale = axis?.Scale ?? NTAxisScale.Linear;

        if (absoluteMinimum.HasValue && y < absoluteMinimum.Value) {
            y = absoluteMinimum.Value;
        }

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

        var (topPadding, bottomPadding) = GetVerticalRenderPadding(min);
        var bottom = plotArea.Bottom - bottomPadding;
        var height = plotArea.Height - topPadding - bottomPadding;
        return (float)(bottom - (t * height));
    }

    /// <summary>
    ///     Converts a screen coordinate back to a data Y value.
    /// </summary>
    public decimal ScaleYInverse(float coord, NTAxisOptions<TData>? axis, SKRect plotArea) {
        var (min, max) = GetScaleYRange(axis);
        var scale = axis?.Scale ?? NTAxisScale.Linear;

        double t;
        var (topPadding, bottomPadding) = GetVerticalRenderPadding(min);
        var bottom = plotArea.Bottom - bottomPadding;
        var height = plotArea.Height - topPadding - bottomPadding;
        t = height <= 0 ? 0 : (bottom - coord) / height;

        if (scale == NTAxisScale.Logarithmic) {
            var dMin = Math.Max(0.000001, (double)min);
            var dMax = Math.Max(dMin * 1.1, (double)max);
            return (decimal)Math.Pow(10, Math.Log10(dMin) + (t * (Math.Log10(dMax) - Math.Log10(dMin))));
        }
        return min + ((decimal)t * (max - min));
    }

    private static (float Top, float Bottom) GetVerticalRenderPadding(decimal min) {
        const float topPadding = 3f;
        const float defaultBottomPadding = 3f;
        const float zeroFloorBottomPadding = 9f;

        return min == 0m
            ? (topPadding, zeroFloorBottomPadding)
            : (topPadding, defaultBottomPadding);
    }

    internal void RegisterSeries(NTBaseSeries<TData> series) {
        if (!Series.Contains(series)) {
            Series.Add(series);
            InvalidateDataCaches();
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

            // Horizontal bar charts can only be combined with other horizontal bar series.
            var barSeries = cartesianSeries.OfType<NTBarSeries<TData>>().ToList();
            var hasHorizontalBars = barSeries.Any(s => s.Orientation == NTChartOrientation.Horizontal);
            if (hasHorizontalBars) {
                var onlyHorizontalBars = (barSeries.Count == Series.Count) &&
                                         barSeries.All(s => s.Orientation == NTChartOrientation.Horizontal);
                if (!onlyHorizontalBars) {
                    throw new InvalidOperationException("Horizontal bar series can only be combined with other horizontal bar series.");
                }
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
            InvalidateDataCaches();
        }
    }

    internal void InvalidateDataCaches() {
        _cachedAllX = null;
        _cachedAllY = null;
        _cachedXIndexMap = null;
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
            _annotationLinePaint?.Dispose();
            _annotationFillPaint?.Dispose();
            _annotationTextPaint?.Dispose();
            _annotationLabelBgPaint?.Dispose();
            _annotationFont?.Dispose();
        }
        base.Dispose(disposing);
    }

    protected override async ValueTask DisposeAsyncCore() {
        _objRef?.Dispose();
        if (_wheelListener != null) {
            await _wheelListener.InvokeVoidAsync("dispose");
            await _wheelListener.DisposeAsync();
        }
        if (_chartModule != null) {
            await _chartModule.DisposeAsync();
        }
        if (_themeListener != null) {
            await _themeListener.InvokeVoidAsync("dispose");
            await _themeListener.DisposeAsync();
        }
    }

    protected override async Task OnAfterRenderAsync(bool firstRender) {
        if (firstRender) {
            Density = await JSRuntime.InvokeAsync<float>("eval", "window.devicePixelRatio || 1");
            _chartModule = await JSRuntime.InvokeAsync<IJSObjectReference>("import", "./_content/NTComponents.Charts/NTComponents.Charts.lib.module.js");
            _objRef = DotNetObjectReference.Create(this);
            _themeListener = await JSRuntime.InvokeAsync<IJSObjectReference>("NTComponents.onThemeChanged", _objRef);
            _wheelListener = await _chartModule.InvokeAsync<IJSObjectReference>(
                "registerWheelHandler",
                _interactionHost,
                _objRef,
                IsXZoomEnabled || IsYZoomEnabled);
            await ResolveColorsAsync();
            StateHasChanged();
        }
    }

    [JSInvokable]
    public void OnNativeWheel(NTNativeWheelEventArgs e) {
        OnWheel(new WheelEventArgs {
            OffsetX = e.OffsetX,
            OffsetY = e.OffsetY,
            DeltaX = e.DeltaX,
            DeltaY = e.DeltaY,
            CtrlKey = e.CtrlKey,
            ShiftKey = e.ShiftKey,
            AltKey = e.AltKey,
            MetaKey = e.MetaKey,
            Type = "wheel"
        });
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
                if (clickedItem.InteractsWithChart && clickedItem.Series != null) {
                    clickedItem.Series.ToggleLegendItem(clickedItem);
                    RequestUiRefresh(force: true);
                }
                return;
            }
        }

        var hit = HitTestSeriesAtPoint(point, LastPlotArea);
        if (hit.Series is not null) {
            var clickArgs = new NTSeriesClickEventArgs<TData> {
                Series = hit.Series,
                PointIndex = hit.Index,
                DataPoint = hit.Data,
                PointerPosition = point,
                MouseEvent = e
            };

            var refreshRequested = false;
            var suppressPointClick = false;
            if (hit.Series is NTLineSeries<TData> lineSeries) {
                refreshRequested = lineSeries.HandleDateGroupClick(clickArgs, out suppressPointClick);
            }

            if (!suppressPointClick) {
                hit.Series.NotifyClick(clickArgs);
            }

            if (refreshRequested) {
                RequestUiRefresh(force: true);
            }
        }
    }

    protected virtual void OnMouseDown(MouseEventArgs e) {
        var point = new SKPoint((float)e.OffsetX * Density, (float)e.OffsetY * Density);

        if (Legend != null && Legend.Visible && Legend.Position == LegendPosition.Floating) {
            var rect = Legend.LastDrawArea.Width > 0 && Legend.LastDrawArea.Height > 0
                ? Legend.LastDrawArea
                : Legend.GetFloatingRect(LastPlotArea, Density);
            if (rect.Contains(point)) {
                _isDraggingLegend = true;
                _hasDraggedLegend = false;
                _legendDragStartMousePos = point;
                _legendDragStartOffset = Legend.FloatingOffset
                    ?? new SKPoint(rect.Left - LastPlotArea.Left, rect.Top - LastPlotArea.Top);
                return;
            }
        }

        if (_chartCoordSystem == ChartCoordinateSystem.TreeMap && _treeMapAreas.Count > 0) {
            for (var i = Series.Count - 1; i >= 0; i--) {
                var series = Series[i];
                if (!series.IsEffectivelyVisible) {
                    continue;
                }

                if (!_treeMapAreas.TryGetValue(series, out var seriesArea) ||
                    seriesArea.Width <= 0 ||
                    seriesArea.Height <= 0 ||
                    !seriesArea.Contains(point)) {
                    continue;
                }

                series.HandleMouseDown(e);
                break;
            }
        }
        else {
            var visibleSeries = Series.Where(s => s.IsEffectivelyVisible).ToList();
            var callbackLeaderIndex = FindInteractionCallbackLeaderIndex(visibleSeries, ChartInteractions.XPan | ChartInteractions.YPan);
            for (var i = 0; i < visibleSeries.Count; i++) {
                var series = visibleSeries[i];
                series.SuppressInteractionCallbacks = i != callbackLeaderIndex;
                series.HandleMouseDown(e);
            }

            foreach (var series in visibleSeries) {
                series.SuppressInteractionCallbacks = false;
            }
        }

        _lastInteractionTimestamp = Stopwatch.GetTimestamp();
        RequestUiRefresh();
    }

    protected virtual void OnMouseMove(MouseEventArgs e) {
        var nextMousePosition = new SKPoint((float)e.OffsetX * Density, (float)e.OffsetY * Density);
        var hadLastMousePosition = LastMousePosition.HasValue;
        LastMousePosition = nextMousePosition;

        if (_isDraggingLegend && Legend != null && LastPlotArea != default) {
            var currentPoint = nextMousePosition;

            // Check for a small movement threshold to allow clicking
            if (!_hasDraggedLegend) {
                var dx = currentPoint.X - _legendDragStartMousePos.X;
                var dy = currentPoint.Y - _legendDragStartMousePos.Y;
                if (Math.Sqrt((dx * dx) + (dy * dy)) > 5) {
                    _hasDraggedLegend = true;
                }
            }

            if (_hasDraggedLegend) {
                var deltaX = currentPoint.X - _legendDragStartMousePos.X;
                var deltaY = currentPoint.Y - _legendDragStartMousePos.Y;
                var newX = _legendDragStartOffset.X + deltaX;
                var newY = _legendDragStartOffset.Y + deltaY;
                Legend.FloatingOffset = new SKPoint(newX, newY);
                _lastInteractionTimestamp = Stopwatch.GetTimestamp();
                RequestUiRefresh();
            }
            return;
        }

        var visibleSeries = Series.Where(s => s.IsEffectivelyVisible).ToList();
        var callbackLeaderIndex = FindInteractionCallbackLeaderIndex(visibleSeries, ChartInteractions.XPan | ChartInteractions.YPan);
        for (var i = 0; i < visibleSeries.Count; i++) {
            var series = visibleSeries[i];
            series.SuppressInteractionCallbacks = i != callbackLeaderIndex;
            series.HandleMouseMove(e);
        }

        foreach (var series in visibleSeries) {
            series.SuppressInteractionCallbacks = false;
        }

        _lastInteractionTimestamp = Stopwatch.GetTimestamp();
        if (!hadLastMousePosition || Series.Any(s => s.IsPanning)) {
            RequestUiRefresh();
        }
    }

    protected virtual void OnMouseOut(MouseEventArgs e) {
        if (_lastHoverNotifiedSeries is not null) {
            _lastHoverNotifiedSeries.NotifyHoverLeave(new NTSeriesHoverLeaveEventArgs<TData> {
                Series = _lastHoverNotifiedSeries,
                PointIndex = _lastHoverNotifiedPointIndex
            });
            _lastHoverNotifiedSeries = null;
            _lastHoverNotifiedPointIndex = null;
        }

        LastMousePosition = null;
        HoveredSeries = null;
        HoveredPointIndex = null;
        HoveredDataPoint = null;
        HoveredLegendItem = null;
        _isHoveringLegend = false;
        _hoverMissStreak = 0;
        RequestUiRefresh(force: true);
    }

    protected virtual void OnMouseUp(MouseEventArgs e) {
        var visibleSeries = Series.Where(s => s.IsEffectivelyVisible).ToList();
        var callbackLeaderIndex = FindInteractionCallbackLeaderIndex(visibleSeries, ChartInteractions.XPan | ChartInteractions.YPan);
        for (var i = 0; i < visibleSeries.Count; i++) {
            var series = visibleSeries[i];
            series.SuppressInteractionCallbacks = i != callbackLeaderIndex;
            series.HandleMouseUp(e);
        }

        foreach (var series in visibleSeries) {
            series.SuppressInteractionCallbacks = false;
        }
        _isDraggingLegend = false;
        _lastInteractionTimestamp = Stopwatch.GetTimestamp();
        RequestUiRefresh();
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

        _isHoveringLegend = false;
        _treeMapAreas.Clear();

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

        _useFrameScaleCache = true;
        _frameXRangeReady = false;
        _framePrimaryYRangeReady = false;
        _frameSecondaryYRangeReady = false;
        try {
            foreach (var order in Enum.GetValues<RenderOrdered>()) {
                if (order is RenderOrdered.Axis && _chartCoordSystem is ChartCoordinateSystem.Cartesian) {
                    var plotArea = renderArea;

                    if (YAxis is NTAxisOptions<TData> primaryYAxis) {
                        plotArea = primaryYAxis.Measure(context, plotArea);
                    }
                    if (SecondaryYAxis is NTAxisOptions<TData> secondaryYAxisMeasure) {
                        plotArea = secondaryYAxisMeasure.Measure(context, plotArea);
                    }
                    if (XAxis is NTAxisOptions<TData> xAxisMeasure) {
                        plotArea = xAxisMeasure.Measure(context, plotArea);
                    }
                    var xAxisArea = new SKRect(plotArea.Left, plotArea.Top, plotArea.Right, renderArea.Bottom);

                    context.PlotArea = plotArea;
                    LastPlotArea = plotArea;

                    XAxis.Render(context, xAxisArea);

                    var yAxisArea = new SKRect(renderArea.Left, plotArea.Top, plotArea.Right, plotArea.Bottom);
                    YAxis.Render(context, yAxisArea);

                    if (SecondaryYAxis is not null) {
                        var secondaryYAxisArea = new SKRect(plotArea.Left, plotArea.Top, renderArea.Right, plotArea.Bottom);
                        SecondaryYAxis.Render(context, secondaryYAxisArea);
                    }

                    renderArea = plotArea;
                    continue;
                }

                if (order is RenderOrdered.Series) {
                    context.PlotArea = renderArea;
                    LastPlotArea = renderArea;

                    if (_chartCoordSystem == ChartCoordinateSystem.TreeMap) {
                        CalculateTreeMapAreas(renderArea);
                    }

                    PerformHitTesting(context, renderArea);

                    var renderSeries = _renderablesByOrder[order]
                        .OfType<NTBaseSeries<TData>>()
                        .Where(s => s.IsEffectivelyVisible)
                        .ToList();

                    if (HoveredSeries is not null && renderSeries.Count > 1) {
                        var hoveredIndex = renderSeries.IndexOf(HoveredSeries);
                        if (hoveredIndex >= 0 && hoveredIndex < renderSeries.Count - 1) {
                            var hovered = renderSeries[hoveredIndex];
                            renderSeries.RemoveAt(hoveredIndex);
                            renderSeries.Add(hovered);
                        }
                    }

                    foreach (var series in renderSeries) {

                        var seriesArea = GetSeriesRenderArea(series, renderArea, totalArea);
                        if (seriesArea.Width <= 0 || seriesArea.Height <= 0) {
                            continue;
                        }

                        if (_chartCoordSystem == ChartCoordinateSystem.Cartesian) {
                            canvas.Save();
                            canvas.ClipRect(seriesArea);
                            series.Render(context, seriesArea);
                            canvas.Restore();
                        }
                        else {
                            series.Render(context, seriesArea);
                        }
                    }

                    if (_chartCoordSystem == ChartCoordinateSystem.TreeMap) {
                        RenderTreeMapGroupLabels(context);
                    }

                    continue;
                }

                if (order is RenderOrdered.Annotation) {
                    context.PlotArea = renderArea;
                    LastPlotArea = renderArea;
                    RenderAnnotations(context, renderArea);
                    continue;
                }

                foreach (var renderable in _renderablesByOrder[order]) {
                    renderArea = renderable.Render(context, renderArea);
                }
            }
        }
        finally {
            _useFrameScaleCache = false;
        }

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

        // Skip hover hit-testing while panning to avoid expensive per-frame path checks.
        if (Series.Any(s => s.IsPanning)) {
            return;
        }

        var mousePoint = LastMousePosition.Value;

        // Check legend items for hover (if legend is present)
        if (Legend != null && Legend.Visible && Legend.Position != LegendPosition.None) {
            if (Legend.LastDrawArea.Contains(mousePoint)) {
                _isHoveringLegend = true;
            }

            var hoveredLegendItem = Legend.GetItemAtPoint(mousePoint, plotArea, Legend.LastDrawArea, Density);
            if (hoveredLegendItem?.Series is not null && hoveredLegendItem.InteractsWithChart) {
                SetHoveredState(
                    hoveredLegendItem.Series,
                    hoveredLegendItem.Index,
                    GetLegendHoverDataPoint(hoveredLegendItem),
                    mousePoint,
                    hoveredLegendItem);
                return;
            }
        }

        var (hoveredSeries, hoveredPointIndex, hoveredDataPoint) = HitTestSeriesAtPoint(mousePoint, plotArea);

        if (hoveredSeries != null) {
            SetHoveredState(hoveredSeries, hoveredPointIndex, hoveredDataPoint, mousePoint);
            return;
        }

        // Suppress transient tooltip flicker from occasional one-frame hit-test misses.
        if (HoveredSeries != null && plotArea.Contains(mousePoint)) {
            _hoverMissStreak++;
            var elapsedTicks = Stopwatch.GetTimestamp() - _lastHoverHitTimestamp;
            var graceTicks = Stopwatch.Frequency / 20L; // ~50 ms
            if (_hoverMissStreak < 2 && elapsedTicks <= graceTicks) {
                return;
            }
        }

        ClearHoveredState(mousePoint);
    }

    private void SetHoveredState(NTBaseSeries<TData> hoveredSeries, int? hoveredPointIndex, TData? hoveredDataPoint, SKPoint mousePoint, LegendItemInfo<TData>? hoveredLegendItem = null) {
        HoveredSeries = hoveredSeries;
        HoveredPointIndex = hoveredPointIndex;
        HoveredDataPoint = hoveredDataPoint;
        HoveredLegendItem = hoveredLegendItem;
        _hoverMissStreak = 0;
        _lastHoverHitTimestamp = Stopwatch.GetTimestamp();

        var hoveredLegendKey = hoveredLegendItem?.Key;
        if (!ReferenceEquals(_lastHoverNotifiedSeries, hoveredSeries) ||
            _lastHoverNotifiedPointIndex != hoveredPointIndex ||
            !string.Equals(_lastHoverNotifiedLegendKey, hoveredLegendKey, StringComparison.Ordinal)) {
            if (_lastHoverNotifiedSeries is not null) {
                _lastHoverNotifiedSeries.NotifyHoverLeave(new NTSeriesHoverLeaveEventArgs<TData> {
                    Series = _lastHoverNotifiedSeries,
                    PointIndex = _lastHoverNotifiedPointIndex
                });
            }

            hoveredSeries.NotifyHoverEnter(new NTSeriesHoverEnterEventArgs<TData> {
                Series = hoveredSeries,
                PointIndex = hoveredPointIndex,
                DataPoint = hoveredDataPoint,
                PointerPosition = mousePoint
            });

            _lastHoverNotifiedSeries = hoveredSeries;
            _lastHoverNotifiedPointIndex = hoveredPointIndex;
            _lastHoverNotifiedLegendKey = hoveredLegendKey;
        }
    }

    private void ClearHoveredState(SKPoint mousePoint) {
        HoveredSeries = null;
        HoveredPointIndex = null;
        HoveredDataPoint = null;
        HoveredLegendItem = null;
        _hoverMissStreak = 0;

        if (_lastHoverNotifiedSeries is not null) {
            _lastHoverNotifiedSeries.NotifyHoverLeave(new NTSeriesHoverLeaveEventArgs<TData> {
                Series = _lastHoverNotifiedSeries,
                PointIndex = _lastHoverNotifiedPointIndex,
                PointerPosition = mousePoint
            });
            _lastHoverNotifiedSeries = null;
            _lastHoverNotifiedPointIndex = null;
            _lastHoverNotifiedLegendKey = null;
        }
    }

    private static TData? GetLegendHoverDataPoint(LegendItemInfo<TData> legendItem) {
        if (legendItem.Series is null || !legendItem.Index.HasValue || legendItem.Index.Value < 0) {
            return null;
        }

        return legendItem.Series.Data.ElementAtOrDefault(legendItem.Index.Value);
    }

    private (NTBaseSeries<TData>? Series, int? Index, TData? Data) HitTestSeriesAtPoint(SKPoint point, SKRect plotArea) {
        var canHitTestGlobalPlot = plotArea.Contains(point);
        for (var i = Series.Count - 1; i >= 0; i--) {
            var series = Series[i];
            if (!series.IsEffectivelyVisible) {
                continue;
            }

            (int Index, TData? Data)? hit = null;
            if (_chartCoordSystem == ChartCoordinateSystem.TreeMap) {
                if (!_treeMapAreas.TryGetValue(series, out var treeMapArea) ||
                    treeMapArea.Width <= 0 ||
                    treeMapArea.Height <= 0 ||
                    !treeMapArea.Contains(point)) {
                    continue;
                }

                hit = series.HitTest(point, treeMapArea);
            }
            else {
                if (!canHitTestGlobalPlot) {
                    break;
                }

                hit = series.HitTest(point, plotArea);
            }

            if (hit is not null) {
                return (series, hit.Value.Index, hit.Value.Data);
            }
        }

        return (null, null, null);
    }

    private void RenderAnnotations(NTRenderContext context, SKRect plotArea) {
        if (Annotations.Count == 0 || plotArea.Width <= 0 || plotArea.Height <= 0) {
            return;
        }

        InitializeAnnotationPaints(context);

        foreach (var annotation in Annotations) {
            var renderCustomOnly = annotation.Type == NTChartAnnotationType.Custom || _chartCoordSystem != ChartCoordinateSystem.Cartesian;
            if (renderCustomOnly) {
                InvokeCustomAnnotationRenderer(context, plotArea, annotation);
                continue;
            }

            var opacity = Math.Clamp(annotation.Opacity, 0f, 1f);
            var strokeColor = ResolveAnnotationColor(annotation.StrokeColor, opacity);
            var fillThemeColor = annotation.FillColor ?? annotation.StrokeColor;
            var fillOpacity = annotation.FillColor.HasValue ? opacity : opacity * 0.18f;
            var fillColor = ResolveAnnotationColor(fillThemeColor, fillOpacity);
            var textBaseColor = annotation.TextColor is TnTColor annotationTextColor && annotationTextColor != TnTColor.None
                ? GetThemeColor(annotationTextColor)
                : GetThemeColor(TextColor);
            var textColor = textBaseColor.WithAlpha((byte)Math.Clamp((int)(255f * opacity), 0, 255));
            var labelBackground = fillColor.WithAlpha((byte)Math.Clamp(Math.Max((int)fillColor.Alpha, 140), 0, 255));

            _annotationLinePaint!.Color = strokeColor;
            _annotationLinePaint.StrokeWidth = Math.Max(1f, annotation.StrokeWidth * context.Density);
            _annotationFillPaint!.Color = fillColor;

            var hasClip = annotation.ClipToPlotArea;
            if (hasClip) {
                context.Canvas.Save();
                context.Canvas.ClipRect(plotArea);
            }

            try {
                switch (annotation.Type) {
                    case NTChartAnnotationType.XLine: {
                        var x = ScaleAnnotationX(annotation.X, plotArea);
                        if (!x.HasValue) {
                            break;
                        }

                        DrawAnnotationLine(context.Canvas, new SKPoint(x.Value, plotArea.Top), new SKPoint(x.Value, plotArea.Bottom), annotation, context.Density);
                        DrawAnnotationLabel(context, annotation, annotation.Label, x.Value + (annotation.LabelOffsetX * context.Density), plotArea.Top + (14f * context.Density) + (annotation.LabelOffsetY * context.Density), textColor, labelBackground, SKTextAlign.Center);
                        break;
                    }

                    case NTChartAnnotationType.XRange: {
                        var x1 = ScaleAnnotationX(annotation.X, plotArea);
                        var x2 = ScaleAnnotationX(annotation.X2, plotArea);
                        if (!x1.HasValue || !x2.HasValue) {
                            break;
                        }

                        var left = Math.Min(x1.Value, x2.Value);
                        var right = Math.Max(x1.Value, x2.Value);
                        var rangeRect = new SKRect(left, plotArea.Top, right, plotArea.Bottom);
                        context.Canvas.DrawRect(rangeRect, _annotationFillPaint);
                        DrawAnnotationLine(context.Canvas, new SKPoint(left, plotArea.Top), new SKPoint(left, plotArea.Bottom), annotation, context.Density);
                        DrawAnnotationLine(context.Canvas, new SKPoint(right, plotArea.Top), new SKPoint(right, plotArea.Bottom), annotation, context.Density);
                        DrawAnnotationLabel(context, annotation, annotation.Label, rangeRect.MidX + (annotation.LabelOffsetX * context.Density), plotArea.Top + (14f * context.Density) + (annotation.LabelOffsetY * context.Density), textColor, labelBackground, SKTextAlign.Center);
                        break;
                    }

                    case NTChartAnnotationType.YLine: {
                        var y = ScaleAnnotationY(annotation.Y, annotation.UseSecondaryYAxis, plotArea);
                        if (!y.HasValue) {
                            break;
                        }

                        DrawAnnotationLine(context.Canvas, new SKPoint(plotArea.Left, y.Value), new SKPoint(plotArea.Right, y.Value), annotation, context.Density);
                        DrawAnnotationLabel(context, annotation, annotation.Label, plotArea.Left + (8f * context.Density) + (annotation.LabelOffsetX * context.Density), y.Value - (6f * context.Density) + (annotation.LabelOffsetY * context.Density), textColor, labelBackground, SKTextAlign.Left);
                        break;
                    }

                    case NTChartAnnotationType.YRange: {
                        var y1 = ScaleAnnotationY(annotation.Y, annotation.UseSecondaryYAxis, plotArea);
                        var y2 = ScaleAnnotationY(annotation.Y2, annotation.UseSecondaryYAxis, plotArea);
                        if (!y1.HasValue || !y2.HasValue) {
                            break;
                        }

                        var top = Math.Min(y1.Value, y2.Value);
                        var bottom = Math.Max(y1.Value, y2.Value);
                        var rangeRect = new SKRect(plotArea.Left, top, plotArea.Right, bottom);
                        context.Canvas.DrawRect(rangeRect, _annotationFillPaint);
                        DrawAnnotationLine(context.Canvas, new SKPoint(plotArea.Left, top), new SKPoint(plotArea.Right, top), annotation, context.Density);
                        DrawAnnotationLine(context.Canvas, new SKPoint(plotArea.Left, bottom), new SKPoint(plotArea.Right, bottom), annotation, context.Density);
                        DrawAnnotationLabel(context, annotation, annotation.Label, plotArea.Left + (8f * context.Density) + (annotation.LabelOffsetX * context.Density), top + (14f * context.Density) + (annotation.LabelOffsetY * context.Density), textColor, labelBackground, SKTextAlign.Left);
                        break;
                    }

                    case NTChartAnnotationType.Point: {
                        var x = ScaleAnnotationX(annotation.X, plotArea);
                        var y = ScaleAnnotationY(annotation.Y, annotation.UseSecondaryYAxis, plotArea);
                        if (!x.HasValue || !y.HasValue) {
                            break;
                        }

                        var markerRadius = Math.Max(2f, annotation.MarkerSize * context.Density);
                        context.Canvas.DrawCircle(x.Value, y.Value, markerRadius, _annotationFillPaint);
                        context.Canvas.DrawCircle(x.Value, y.Value, markerRadius, _annotationLinePaint);
                        DrawAnnotationLabel(context, annotation, annotation.Label, x.Value + (annotation.LabelOffsetX * context.Density), y.Value - markerRadius - (8f * context.Density) + (annotation.LabelOffsetY * context.Density), textColor, labelBackground, SKTextAlign.Center);
                        break;
                    }

                    case NTChartAnnotationType.Text: {
                        var x = ScaleAnnotationX(annotation.X, plotArea);
                        var y = ScaleAnnotationY(annotation.Y, annotation.UseSecondaryYAxis, plotArea);
                        if (!x.HasValue || !y.HasValue) {
                            break;
                        }

                        DrawAnnotationLabel(context, annotation, annotation.Label, x.Value + (annotation.LabelOffsetX * context.Density), y.Value + (annotation.LabelOffsetY * context.Density), textColor, labelBackground, SKTextAlign.Center);
                        break;
                    }
                }
            }
            finally {
                if (hasClip) {
                    context.Canvas.Restore();
                }
            }

            if (annotation.CustomRenderer is not null) {
                InvokeCustomAnnotationRenderer(context, plotArea, annotation);
            }
        }
    }

    private void InitializeAnnotationPaints(NTRenderContext context) {
        _annotationLinePaint ??= new SKPaint {
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            StrokeCap = SKStrokeCap.Round,
            StrokeJoin = SKStrokeJoin.Round
        };

        _annotationFillPaint ??= new SKPaint {
            IsAntialias = true,
            Style = SKPaintStyle.Fill
        };

        _annotationTextPaint ??= new SKPaint {
            IsAntialias = true,
            Style = SKPaintStyle.Fill
        };

        _annotationLabelBgPaint ??= new SKPaint {
            IsAntialias = true,
            Style = SKPaintStyle.Fill
        };

        _annotationFont ??= new SKFont {
            Typeface = context.RegularFont.Typeface
        };
    }

    private void InvokeCustomAnnotationRenderer(NTRenderContext context, SKRect plotArea, NTChartAnnotation annotation) {
        if (annotation.CustomRenderer is null) {
            return;
        }

        var hasClip = annotation.ClipToPlotArea;
        if (hasClip) {
            context.Canvas.Save();
            context.Canvas.ClipRect(plotArea);
        }

        try {
            annotation.CustomRenderer(new NTChartAnnotationRenderContext {
                Canvas = context.Canvas,
                PlotArea = plotArea,
                Density = context.Density,
                Annotation = annotation,
                ScaleX = value => ScaleAnnotationX(value, plotArea),
                ScaleY = (value, useSecondary) => ScaleAnnotationY(value, useSecondary, plotArea),
                ResolveThemeColor = GetThemeColor
            });
        }
        finally {
            if (hasClip) {
                context.Canvas.Restore();
            }
        }
    }

    private void DrawAnnotationLine(SKCanvas canvas, SKPoint start, SKPoint end, NTChartAnnotation annotation, float density) {
        if (_annotationLinePaint is null) {
            return;
        }

        if (annotation.DashLength > 0f) {
            var dash = Math.Max(1f, annotation.DashLength * density);
            using var effect = SKPathEffect.CreateDash(new[] { dash, dash }, 0f);
            _annotationLinePaint.PathEffect = effect;
            canvas.DrawLine(start, end, _annotationLinePaint);
            _annotationLinePaint.PathEffect = null;
            return;
        }

        canvas.DrawLine(start, end, _annotationLinePaint);
    }

    private void DrawAnnotationLabel(
        NTRenderContext context,
        NTChartAnnotation annotation,
        string? text,
        float x,
        float y,
        SKColor textColor,
        SKColor backgroundColor,
        SKTextAlign align) {
        if (string.IsNullOrWhiteSpace(text) || _annotationTextPaint is null || _annotationLabelBgPaint is null || _annotationFont is null) {
            return;
        }

        _annotationTextPaint.Color = textColor;
        _annotationFont.Size = Math.Max(8f, annotation.FontSize * context.Density);

        var textWidth = _annotationFont.MeasureText(text);
        var textHeight = _annotationFont.Size;
        var padX = 6f * context.Density;
        var padY = 3f * context.Density;

        var left = align switch {
            SKTextAlign.Left => x - padX,
            SKTextAlign.Right => x - textWidth - padX,
            _ => x - (textWidth / 2f) - padX
        };
        var top = y - textHeight - padY;
        var rect = new SKRect(left, top, left + textWidth + (padX * 2f), top + textHeight + (padY * 2f));

        _annotationLabelBgPaint.Color = backgroundColor;
        context.Canvas.DrawRoundRect(rect, 4f * context.Density, 4f * context.Density, _annotationLabelBgPaint);

        var textX = align switch {
            SKTextAlign.Left => rect.Left + padX,
            SKTextAlign.Right => rect.Right - padX,
            _ => rect.MidX
        };
        var baseline = rect.Top + padY + _annotationFont.Size;
        context.Canvas.DrawText(text, textX, baseline, align, _annotationFont, _annotationTextPaint);
    }

    private float? ScaleAnnotationX(object? value, SKRect plotArea) {
        if (value is null) {
            return null;
        }

        var scaled = GetScaledXValue(value);
        if (double.IsNaN(scaled) || double.IsInfinity(scaled)) {
            return null;
        }

        return ScaleX(scaled, plotArea);
    }

    private float? ScaleAnnotationY(object? value, bool useSecondaryYAxis, SKRect plotArea) {
        if (value is null) {
            return null;
        }

        var scaled = GetScaledYValue(value);
        var axis = useSecondaryYAxis && SecondaryYAxis is NTAxisOptions<TData> secondary
            ? secondary
            : YAxis as NTAxisOptions<TData>;

        return ScaleY(scaled, axis, plotArea);
    }

    private SKColor ResolveAnnotationColor(TnTColor color, float opacity) {
        var resolved = GetThemeColor(color);
        var alpha = (byte)Math.Clamp((int)(resolved.Alpha * Math.Clamp(opacity, 0f, 1f)), 0, 255);
        return resolved.WithAlpha(alpha);
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
        var visibleSeries = Series.Where(s => s.IsEffectivelyVisible).ToList();
        var callbackLeaderIndex = FindInteractionCallbackLeaderIndex(visibleSeries, ChartInteractions.XZoom | ChartInteractions.YZoom);
        for (var i = 0; i < visibleSeries.Count; i++) {
            var series = visibleSeries[i];
            series.SuppressInteractionCallbacks = i != callbackLeaderIndex;
            series.HandleMouseWheel(e);
        }

        foreach (var series in visibleSeries) {
            series.SuppressInteractionCallbacks = false;
        }
        _lastInteractionTimestamp = Stopwatch.GetTimestamp();
        RequestUiRefresh();
    }

    private static int FindInteractionCallbackLeaderIndex(IReadOnlyList<NTBaseSeries<TData>> visibleSeries, ChartInteractions interactionMask) {
        if (visibleSeries.Count == 0) {
            return -1;
        }

        for (var i = 0; i < visibleSeries.Count; i++) {
            if ((visibleSeries[i].Interactions & interactionMask) != ChartInteractions.None) {
                return i;
            }
        }

        return 0;
    }

    private void CalculateTreeMapAreas(SKRect plotArea) {
        _treeMapAreas.Clear();
        var treeMapSeries = Series.Where(s => s.CoordinateSystem == ChartCoordinateSystem.TreeMap && s.IsEffectivelyVisible).ToList();
        if (!treeMapSeries.Any()) {
            return;
        }

        var activeDrillSeries = treeMapSeries
            .OfType<ITreeMapDrillableSeries>()
            .FirstOrDefault(s => s.IsInDrilldown);
        if (activeDrillSeries is NTBaseSeries<TData> focusedSeries) {
            _treeMapAreas[focusedSeries] = plotArea;
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
                if (_treeMapAreas.Count > 1) {
                    // Shave off top for series title
                    return new SKRect(area.Left, area.Top + 20, area.Right, area.Bottom);
                }
                return area;
            }

            return SKRect.Empty;
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

    public decimal ScaleYInverse(float coord, SKRect plotArea) => ScaleYInverse(coord, YAxis as NTAxisOptions<TData>, plotArea);

    private (double Min, double Max) GetScaleXRange() {
        if (!_useFrameScaleCache) {
            return GetXRange(XAxis as NTAxisOptions<TData>, true);
        }

        if (!_frameXRangeReady) {
            _frameXRange = GetXRange(XAxis as NTAxisOptions<TData>, true);
            _frameXRangeReady = true;
        }

        return _frameXRange;
    }

    private (decimal Min, decimal Max) GetScaleYRange(NTAxisOptions<TData>? axis) {
        if (!_useFrameScaleCache) {
            return GetYRange(axis, true);
        }

        var isSecondary = axis is not null && SecondaryYAxis is not null && ReferenceEquals(axis, SecondaryYAxis);
        if (isSecondary) {
            if (!_frameSecondaryYRangeReady) {
                _frameSecondaryYRange = GetYRange(axis, true);
                _frameSecondaryYRangeReady = true;
            }

            return _frameSecondaryYRange;
        }

        if (!_framePrimaryYRangeReady) {
            _framePrimaryYRange = GetYRange(axis, true);
            _framePrimaryYRangeReady = true;
        }

        return _framePrimaryYRange;
    }

    private void RequestUiRefresh(bool force = false) {
        var now = Stopwatch.GetTimestamp();
        var sinceLast = now - _lastUiRefreshTimestamp;
        var minInterval = Stopwatch.Frequency / 30L; // cap UI rerenders to ~30 FPS during interaction.

        if (!force && sinceLast < minInterval) {
            return;
        }

        _lastUiRefreshTimestamp = now;
        StateHasChanged();
    }

    public sealed class NTNativeWheelEventArgs {
        public double OffsetX { get; init; }
        public double OffsetY { get; init; }
        public double DeltaX { get; init; }
        public double DeltaY { get; init; }
        public bool CtrlKey { get; init; }
        public bool ShiftKey { get; init; }
        public bool AltKey { get; init; }
        public bool MetaKey { get; init; }
    }

    public bool HasViewRange(NTAxisOptions<TData> axis) {
        if (ReferenceEquals(axis, XAxis)) {
            return Series.OfType<NTCartesianSeries<TData>>().Where(s => s.IsEffectivelyVisible).Any(s => s.GetViewXRange().HasValue);
        }

        var isSecondary = SecondaryYAxis is not null && ReferenceEquals(axis, SecondaryYAxis);
        return Series
            .OfType<NTCartesianSeries<TData>>()
            .Where(s => s.IsEffectivelyVisible && s.UseSecondaryYAxis == isSecondary)
            .Any(s => s.GetViewYRange().HasValue);
    }

    private static (double Min, double Max) NormalizeRange(double min, double max) {
        if (double.IsNaN(min) || double.IsNaN(max) || double.IsInfinity(min) || double.IsInfinity(max)) {
            return (0, 1);
        }

        if (max < min) {
            (min, max) = (max, min);
        }

        if (Math.Abs(max - min) <= double.Epsilon) {
            var delta = Math.Abs(min) > 1 ? Math.Abs(min * 0.01) : 1;
            return (min - delta, max + delta);
        }

        return (min, max);
    }

    private static (decimal Min, decimal Max) NormalizeRange(decimal min, decimal max) {
        if (max < min) {
            (min, max) = (max, min);
        }

        if (max == min) {
            var delta = Math.Abs(min) > 1 ? Math.Abs(min * 0.01m) : 1m;
            return (min - delta, max + delta);
        }

        return (min, max);
    }

    private static (decimal Min, decimal Max) NormalizeYRange(decimal min, decimal max, decimal? absoluteMinimum) {
        if (absoluteMinimum.HasValue) {
            min = Math.Max(min, absoluteMinimum.Value);
            max = Math.Max(max, absoluteMinimum.Value);
        }

        if (max < min) {
            (min, max) = (max, min);
        }

        if (max == min) {
            var delta = Math.Abs(min) > 1 ? Math.Abs(min * 0.01m) : 1m;
            return absoluteMinimum.HasValue
                ? (min, max + delta)
                : (min - delta, max + delta);
        }

        return (min, max);
    }

    private record SeriesLayoutItem(NTBaseSeries<TData> Series, decimal Value);
}
