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
public partial class NTChart<TData> : TnTDisposableComponentBase, IAsyncDisposable where TData : class {

    [Parameter]
    public TnTColor BackgroundColor { get; set; } = TnTColor.Surface;

    /// <summary>
    ///     Gets or sets the default text color for the chart (titles, labels).
    /// </summary>
    [Parameter]
    public TnTColor TextColor { get; set; } = TnTColor.OnSurface;

    /// <summary>
    ///    Gets or sets the background color of the tooltip.
    /// </summary>
    [Parameter]
    public TnTColor TooltipBackgroundColor { get; set; } = TnTColor.SurfaceVariant;

    /// <summary>
    ///    Gets or sets the text color of the tooltip.
    /// </summary>
    [Parameter]
    public TnTColor TooltipTextColor { get; set; } = TnTColor.OnSurfaceVariant;

    [Parameter]
    public bool EnableHardwareAcceleration { get; set; } = true;

    /// <summary>
    ///    Gets or sets whether to allow exporting the chart as a PNG.
    /// </summary>
    [Parameter]
    public bool AllowExport { get; set; } = true;

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

    /// <summary>
    ///     Gets or sets the margin around the chart.
    /// </summary>
    [Parameter]
    public ChartMargin Margin { get; set; } = ChartMargin.All(5);

    /// <summary>
    ///     Gets or sets the title of the chart.
    /// </summary>
    [Parameter]
    public string? Title { get; set; }

    /// <summary>
    ///    Gets or sets the padding percentage for the axis ranges (0 to 1).
    /// </summary>
    [Parameter]
    public double RangePadding { get; set; } = 0.05;

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

    private static readonly SKTypeface _defaultTypeface = SKTypeface.FromFamilyName("Roboto", SKFontStyleWeight.Bold, SKFontStyleWidth.Normal, SKFontStyleSlant.Upright);
    private static readonly SKTypeface _regularTypeface = SKTypeface.FromFamilyName("Roboto", SKFontStyleWeight.Medium, SKFontStyleWidth.Normal, SKFontStyleSlant.Upright);

    internal SKTypeface DefaultTypeface => _defaultTypeface;
    internal SKTypeface RegularTypeface => _regularTypeface;

    /// <summary>
    ///    Gets or sets the duration of the hover animation.
    /// </summary>
    [Parameter]
    public TimeSpan HoverAnimationDuration { get; set; } = TimeSpan.FromMilliseconds(250);

    [Inject]
    protected IJSRuntime JSRuntime { get; set; } = default!;

    protected SKPoint? LastMousePosition { get; private set; }

    protected SKRect LastPlotArea { get; private set; }

    protected SKRect LastLegendDrawArea { get; private set; }

    protected string CanvasStyle {
        get {
            if (HoveredSeries != null) {
                return "cursor: pointer;";
            }

            if (_isDraggingLegend || _isHoveringLegend) {
                return "cursor: move;";
            }

            return _isPanning ? "cursor: grabbing;" : (IsXPanEnabled || IsYPanEnabled) ? "cursor: grab;" : "cursor: default;";
        }
    }

    private bool _isHovering;
    private bool _isHoveringLegend;

    internal NTBaseSeries<TData>? HoveredSeries { get; private set; }

    internal bool IsXPanEnabled => Series.OfType<NTLineSeries<TData>>().Any(s => s.EnableXPan);
    internal bool IsYPanEnabled => Series.OfType<NTLineSeries<TData>>().Any(s => s.EnableYPan);
    internal bool IsXZoomEnabled => Series.OfType<NTLineSeries<TData>>().Any(s => s.EnableXZoom);
    internal bool IsYZoomEnabled => Series.OfType<NTLineSeries<TData>>().Any(s => s.EnableYZoom);

    internal bool IsXAxisDateTime {
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

    internal int? HoveredPointIndex { get; private set; }

    internal TData? HoveredDataPoint { get; private set; }

    private double? _viewXMin;
    private double? _viewXMax;
    private decimal? _viewYMin;
    private decimal? _viewYMax;

    private bool _isPanning;
    private SKPoint _panStartPoint;
    private (double Min, double Max)? _panStartXRange;
    private (decimal Min, decimal Max)? _panStartYRange;

    private bool _isDraggingLegend;
    private bool _hasDraggedLegend;
    private SKPoint _legendMouseOffset;
    private SKPoint _legendDragStartMousePos;

    internal List<NTBaseSeries<TData>> Series { get; } = [];

    internal NTLegend<TData>? Legend { get; private set; }

    private readonly Dictionary<TnTColor, SKColor> _resolvedColors = [];

    private readonly Dictionary<NTBaseSeries<TData>, SKRect> _treeMapAreas = [];

    private List<object>? _cachedAllX;

    private List<object>? _cachedAllY;

    private readonly Dictionary<(NTYAxisOptions?, bool), (decimal Min, decimal Max)> _cachedYRanges = [];

    private readonly Dictionary<(NTXAxisOptions?, bool), (double Min, double Max)> _cachedXRanges = [];

    private IJSObjectReference? _themeListener;
    private DotNetObjectReference<NTChart<TData>>? _objRef;

    private float _density = 1.0f;

    protected override async Task OnAfterRenderAsync(bool firstRender) {
        if (firstRender) {
            _density = await JSRuntime.InvokeAsync<float>("eval", "window.devicePixelRatio || 1");
            _objRef = DotNetObjectReference.Create(this);
            _themeListener = await JSRuntime.InvokeAsync<IJSObjectReference>("NTComponents.onThemeChanged", _objRef);
            await ResolveColorsAsync();
            StateHasChanged();
        }
    }

    [JSInvokable]
    public async Task OnThemeChanged() {
        await ResolveColorsAsync();
        StateHasChanged();
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

    internal SKColor GetThemeColor(TnTColor color) => _resolvedColors.TryGetValue(color, out var skColor) ? skColor : SKColors.Black;

    public async ValueTask DisposeAsync() {
        _objRef?.Dispose();
        if (_themeListener != null) {
            await _themeListener.InvokeVoidAsync("dispose");
            await _themeListener.DisposeAsync();
        }
    }

    /// <summary>
    ///     Resets the view to the default range.
    /// </summary>
    public void ResetView() {
        _viewXMin = null;
        _viewXMax = null;
        _viewYMin = null;
        _viewYMax = null;
        StateHasChanged();
    }

    protected virtual void OnClick(MouseEventArgs e) {
        if (_hasDraggedLegend) {
            _hasDraggedLegend = false;
            return;
        }

        var point = new SKPoint((float)e.OffsetX * _density, (float)e.OffsetY * _density);

        if (Legend != null && Legend.Visible && Legend.Position != LegendPosition.None) {
            var clickedItem = Legend.GetItemAtPoint(point, LastPlotArea, LastLegendDrawArea);

            if (clickedItem != null) {
                if (clickedItem.Series != null) {
                    clickedItem.Series.ToggleLegendItem(clickedItem.Index);
                    StateHasChanged();
                }
                return;
            }
        }
    }

    private float _lastWidth;
    private float _lastHeight;

    protected virtual void OnMouseMove(MouseEventArgs e) {
        LastMousePosition = new SKPoint((float)e.OffsetX * _density, (float)e.OffsetY * _density);

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

        if (_isPanning && LastPlotArea != default) {
            var currentPoint = LastMousePosition.Value;
            var dx = _panStartPoint.X - currentPoint.X;
            var dy = currentPoint.Y - _panStartPoint.Y; // Y is inverted in screen coords

            if (_panStartXRange.HasValue && IsXPanEnabled) {
                var xRangeSize = _panStartXRange.Value.Max - _panStartXRange.Value.Min;
                var dataDx = dx / LastPlotArea.Width * xRangeSize;
                _viewXMin = _panStartXRange.Value.Min + dataDx;
                _viewXMax = _panStartXRange.Value.Max + dataDx;
            }

            if (_panStartYRange.HasValue && IsYPanEnabled) {
                var yRangeSize = _panStartYRange.Value.Max - _panStartYRange.Value.Min;
                var dataDy = (decimal)(dy / LastPlotArea.Height) * yRangeSize;
                _viewYMin = _panStartYRange.Value.Min + dataDy;
                _viewYMax = _panStartYRange.Value.Max + dataDy;
            }
        }

        StateHasChanged();
    }

    protected virtual void OnMouseDown(MouseEventArgs e) {
        var point = new SKPoint((float)e.OffsetX * _density, (float)e.OffsetY * _density);

        if (Legend != null && Legend.Visible && Legend.Position == LegendPosition.Floating) {
            var rect = Legend.GetFloatingRect(LastPlotArea);
            if (rect.Contains(point)) {
                _isDraggingLegend = true;
                _hasDraggedLegend = false;
                _legendDragStartMousePos = point;
                _legendMouseOffset = new SKPoint(point.X - rect.Left, point.Y - rect.Top);
                return;
            }
        }

        if (IsXPanEnabled || IsYPanEnabled) {
            _isPanning = true;
            _panStartPoint = point;
            var primaryX = GetUniqueXAxes().FirstOrDefault();
            var primaryY = GetUniqueYAxes().FirstOrDefault();
            _panStartXRange = GetXRange(primaryX, true);
            _panStartYRange = GetYRange(primaryY, true);
        }
    }

    protected virtual void OnMouseUp(MouseEventArgs e) {
        _isPanning = false;
        _isDraggingLegend = false;
    }

    protected virtual void OnWheel(WheelEventArgs e) {
        if ((!IsXZoomEnabled && !IsYZoomEnabled) || LastPlotArea == default) {
            return;
        }

        var mousePoint = new SKPoint((float)e.OffsetX * _density, (float)e.OffsetY * _density);
        if (!LastPlotArea.Contains(mousePoint)) {
            return;
        }

        var zoomFactor = e.DeltaY > 0 ? 1.1 : 0.9;

        // Use ScaleXInverse/ScaleYInverse
        var primaryX = GetUniqueXAxes().FirstOrDefault();
        var primaryY = GetUniqueYAxes().FirstOrDefault();

        var xVal = ScaleXInverse(mousePoint.X, LastPlotArea);
        var yVal = ScaleYInverse(mousePoint.Y, primaryY!, LastPlotArea);

        var (xMin, xMax) = GetXRange(primaryX, true);
        var (yMin, yMax) = GetYRange(primaryY, true);

        if (IsXZoomEnabled) {
            var newXRange = (xMax - xMin) * zoomFactor;
            var xPct = (xVal - xMin) / (xMax - xMin);
            _viewXMin = xVal - (newXRange * xPct);
            _viewXMax = xVal + (newXRange * (1 - xPct));
        }

        if (IsYZoomEnabled) {
            var newYRange = (yMax - yMin) * (decimal)zoomFactor;
            var yPct = (decimal)((double)(yVal - yMin) / (double)(yMax - yMin));
            _viewYMin = yVal - (newYRange * yPct);
            _viewYMax = yVal + (newYRange * (1 - yPct));
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

    protected void OnPaintSurface(SKPaintGLSurfaceEventArgs e) => OnPaintSurface(e.Surface.Canvas, e.Info);

    protected void OnPaintSurface(SKPaintSurfaceEventArgs e) => OnPaintSurface(e.Surface.Canvas, e.Info);

    /// <summary>
    ///     Handles the paint surface event from the SkiaSharp view.
    /// </summary>
    protected void OnPaintSurface(SKCanvas canvas, SKImageInfo info) {
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
        _cachedYRanges.Clear();
        _cachedXRanges.Clear();

        canvas.Clear(GetThemeColor(BackgroundColor));

        if (!string.IsNullOrEmpty(Title)) {
            RenderTitle(canvas, info);
        }

        var renderArea = new SKRect(Margin.Left, Margin.Top, info.Width - Margin.Right, info.Height - Margin.Bottom);
        if (!string.IsNullOrEmpty(Title)) {
            // TODO Implement Title options
            renderArea = new SKRect(renderArea.Left, renderArea.Top + 30, renderArea.Right, renderArea.Bottom);
        }
        var plotArea = renderArea;
        var accessibleArea = renderArea; // Area available for axes and legend

        // Pass 1: Measure legend and update plotArea/accessibleArea
        SKRect legendDrawArea = default;
        if (Legend != null && Legend.Visible && Legend.Position != LegendPosition.None && Legend.Position != LegendPosition.Floating) {
            var (newPlotArea, legendArea) = Legend.Measure(plotArea);
            plotArea = newPlotArea;
            legendDrawArea = legendArea;

            // Adjust accessibleArea for axes so they don't overlap legend
            if (Legend.Position == LegendPosition.Bottom) {
                accessibleArea = new SKRect(accessibleArea.Left, accessibleArea.Top, accessibleArea.Right, legendArea.Top);
            }
            else if (Legend.Position == LegendPosition.Top) {
                accessibleArea = new SKRect(accessibleArea.Left, legendArea.Bottom, accessibleArea.Right, accessibleArea.Bottom);
            }
            else if (Legend.Position == LegendPosition.Left) {
                accessibleArea = new SKRect(legendArea.Right, accessibleArea.Top, accessibleArea.Right, accessibleArea.Bottom);
            }
            else if (Legend.Position == LegendPosition.Right) {
                accessibleArea = new SKRect(accessibleArea.Left, accessibleArea.Top, legendArea.Left, accessibleArea.Bottom);
            }
        }

        LastLegendDrawArea = legendDrawArea;

        // Pass 2: Measure series (axes etc) and update plotArea
        var measured = new HashSet<object>();
        foreach (var series in Series.Where(s => s.Visible)) {
            plotArea = series.Measure(plotArea, measured);
        }

        LastPlotArea = plotArea;
        CalculateTreeMapAreas(plotArea);

        // Pass 3: Render axes using the final plotArea and the adjusted accessibleArea
        var rendered = new HashSet<object>();
        foreach (var series in Series.Where(s => s.Visible)) {
            series.RenderAxes(canvas, plotArea, accessibleArea, rendered);
        }

        // Pass 4: Hit testing and Tooltip Prep
        if (LastMousePosition.HasValue) {
            var mousePoint = LastMousePosition.Value;

            // Check legend items for hover first (so they take precedence over lines)
            if (Legend != null && Legend.Visible && Legend.Position != LegendPosition.None) {
                var item = Legend.GetItemAtPoint(mousePoint, plotArea, legendDrawArea);
                if (item != null) {
                    HoveredSeries = item.Series;
                    HoveredPointIndex = item.Index;
                }
                else if (Legend.Position == LegendPosition.Floating) {
                    var rect = Legend.GetFloatingRect(plotArea);
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
            series.Render(canvas, seriesRenderArea);
        }
        // Render active series last (on top)
        if (HoveredSeries != null && HoveredSeries.IsEffectivelyVisible) {
            var seriesRenderArea = GetSeriesRenderArea(HoveredSeries, plotArea, renderArea);
            HoveredSeries.Render(canvas, seriesRenderArea);
        }
        canvas.Restore();

        // Pass 6: Render TreeMap group labels if needed
        RenderTreeMapGroupLabels(canvas);

        // Pass 7: Render Tooltip
        if (HoveredDataPoint != null && LastMousePosition.HasValue && HoveredSeries != null && HoveredSeries.Visible) {
            RenderTooltip(canvas, plotArea);
        }

        // Pass 8: Render legend (Now after hit testing so it can react to hovered series)
        if (Legend != null && Legend.Visible && Legend.Position != LegendPosition.None) {
            if (Legend.Position == LegendPosition.Floating) {
                Legend.Render(canvas, plotArea, renderArea);
            }
            else {
                Legend.Render(canvas, plotArea, legendDrawArea);
            }
        }

        // Pass 9: Update cursor if needed
        var currentHover = HoveredSeries != null || _isHoveringLegend || _isPanning;
        if (_isHovering != currentHover) {
            _isHovering = currentHover;
            _ = InvokeAsync(StateHasChanged);
        }
    }

    private void RenderTooltip(SKCanvas canvas, SKRect plotArea) {
        if (HoveredSeries == null || HoveredDataPoint == null || !LastMousePosition.HasValue) {
            return;
        }

        var mousePoint = LastMousePosition.Value;
        var tooltipInfo = HoveredSeries.GetTooltipInfo(HoveredDataPoint);

        if (tooltipInfo.Lines.Count == 0) {
            return;
        }

        var bgColor = GetThemeColor(HoveredSeries.TooltipBackgroundColor ?? TooltipBackgroundColor);
        var textColor = GetThemeColor(HoveredSeries.TooltipTextColor ?? TooltipTextColor);
        var subTextColor = textColor.WithAlpha(200);

        using var fontHeader = new SKFont {
            Size = 11,
            Typeface = RegularTypeface
        };
        using var fontLabel = new SKFont {
            Size = 12,
            Typeface = RegularTypeface
        };
        using var fontValue = new SKFont {
            Size = 12,
            Typeface = DefaultTypeface
        };

        var padding = 8f;
        var headerHeight = string.IsNullOrEmpty(tooltipInfo.Header) ? 0 : fontHeader.Size + 6;
        var separatorHeight = string.IsNullOrEmpty(tooltipInfo.Header) ? 0 : 6;
        var lineHeight = 18f;
        var iconSize = 8f;
        var iconSpacing = 8f;

        float maxWidth = 0;
        if (!string.IsNullOrEmpty(tooltipInfo.Header)) {
            maxWidth = fontHeader.MeasureText(tooltipInfo.Header);
        }

        foreach (var line in tooltipInfo.Lines) {
            var lineWidth = iconSize + iconSpacing + fontLabel.MeasureText(line.Label + ": ") + fontValue.MeasureText(line.Value);
            maxWidth = Math.Max(maxWidth, lineWidth);
        }

        var totalWidth = maxWidth + (padding * 2);
        var totalHeight = headerHeight + separatorHeight + (tooltipInfo.Lines.Count * lineHeight) + (padding * 2) - 2;

        var rect = new SKRect(mousePoint.X + 15, mousePoint.Y - totalHeight - 15, mousePoint.X + 15 + totalWidth, mousePoint.Y - 15);

        if (rect.Right > _lastWidth - Margin.Right) {
            rect.Offset(-(totalWidth + 30), 0);
        }
        if (rect.Top < Margin.Top) {
            rect.Offset(0, totalHeight + 30);
        }

        using var bgPaint = new SKPaint {
            Color = bgColor.WithAlpha(250),
            Style = SKPaintStyle.Fill,
            IsAntialias = true
        };
        canvas.DrawRoundRect(rect, 4, 4, bgPaint);

        using var borderPaint = new SKPaint {
            Color = GetThemeColor(TnTColor.OutlineVariant),
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 1,
            IsAntialias = true
        };
        canvas.DrawRoundRect(rect, 4, 4, borderPaint);

        var currentY = rect.Top + padding;

        if (!string.IsNullOrEmpty(tooltipInfo.Header)) {
            using var headerPaint = new SKPaint { Color = subTextColor, IsAntialias = true };
            canvas.DrawText(tooltipInfo.Header, rect.Left + padding, currentY + fontHeader.Size - 2, SKTextAlign.Left, fontHeader, headerPaint);
            currentY += headerHeight;

            using var separatorPaint = new SKPaint { Color = GetThemeColor(TnTColor.OutlineVariant), StrokeWidth = 1, IsAntialias = true };
            canvas.DrawLine(rect.Left, currentY - 4, rect.Right, currentY - 4, separatorPaint);
            currentY += separatorHeight - 4;
        }

        foreach (var line in tooltipInfo.Lines) {
            var centerX = rect.Left + padding + (iconSize / 2);
            var centerY = currentY + (lineHeight / 2) - 1;

            using var iconPaint = new SKPaint { Color = line.Color, Style = SKPaintStyle.Fill, IsAntialias = true };
            canvas.DrawCircle(centerX, centerY, iconSize / 2, iconPaint);

            var textX = rect.Left + padding + iconSize + iconSpacing;
            var textY = currentY + 14;

            using var labelPaint = new SKPaint { Color = subTextColor, IsAntialias = true };
            canvas.DrawText(line.Label + ": ", textX, textY, SKTextAlign.Left, fontLabel, labelPaint);

            var labelWidth = fontLabel.MeasureText(line.Label + ": ");

            using var valuePaint = new SKPaint { Color = textColor, IsAntialias = true };
            canvas.DrawText(line.Value, textX + labelWidth, textY, SKTextAlign.Left, fontValue, valuePaint);

            currentY += lineHeight;
        }
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

    private record SeriesLayoutItem(NTBaseSeries<TData> Series, decimal Value);

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

    private void RenderTreeMapGroupLabels(SKCanvas canvas) {
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
                Size = 14,
                Embolden = true,
                Typeface = DefaultTypeface
            };

            var title = series.Title ?? "Series";
            // Measure text to make sure it fits
            var textWidth = font.MeasureText(title);
            if (area.Width < textWidth + 10 || area.Height < 20) {
                continue;
            }

            // Draw at top left of the area with a small offset
            canvas.DrawText(title, area.Left + 5, area.Top + 15, SKTextAlign.Left, font, paint);
        }
    }

    private void RenderTitle(SKCanvas canvas, SKImageInfo info) {
        using var paint = new SKPaint {
            Color = GetThemeColor(TextColor),
            IsAntialias = true,
            Style = SKPaintStyle.Fill
        };

        using var font = new SKFont {
            Size = 20,
            Embolden = true,
            Typeface = DefaultTypeface
        };

        var x = Margin.Left + ((info.Width - Margin.Left - Margin.Right) / 2);
        var y = Margin.Top + 20;

        canvas.DrawText(Title!, x, y, SKTextAlign.Center, font, paint);
    }

    internal void AddSeries(NTBaseSeries<TData> series) {
        if (Series.Count > 0 && Series[0].CoordinateSystem != series.CoordinateSystem) {
            throw new InvalidOperationException($"Cannot combine series with different coordinate systems. Currently using {Series[0].CoordinateSystem}, but tried to add {series.CoordinateSystem}.");
        }

        if (!Series.Contains(series)) {
            Series.Add(series);
        }

        var cartesianSeries = Series.OfType<NTCartesianSeries<TData>>();
        if (cartesianSeries?.Any() == true) {
            var xAxis = cartesianSeries.First().XAxis;
            var yAxis1 = cartesianSeries.First().YAxis;
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
        var lineSeries = Series.OfType<NTLineSeries<TData>>().ToList();
        var anyInteractivity = lineSeries.Any(s => s.EnableXPan || s.EnableYPan || s.EnableXZoom || s.EnableYZoom);

        if (anyInteractivity) {
            if (Series.Count != lineSeries.Count) {
                throw new InvalidOperationException("Interactive panning and zooming is only supported when all series in the chart are line series.");
            }

            var first = lineSeries[0];
            foreach (var s in lineSeries.Skip(1)) {
                if (s.EnableXPan != first.EnableXPan || s.EnableYPan != first.EnableYPan ||
                    s.EnableXZoom != first.EnableXZoom || s.EnableYZoom != first.EnableYZoom) {
                    throw new InvalidOperationException("All line series in the chart must have the same panning and zooming configurations.");
                }
            }
        }
    }

    internal void RemoveSeries(NTBaseSeries<TData> series) {
        if (Series.Contains(series)) {
            Series.Remove(series);
        }
    }

    internal void SetLegend(NTLegend<TData> legend) => Legend = legend;

    internal void RemoveLegend(NTLegend<TData> legend) {
        if (Legend == legend) {
            Legend = null;
        }
    }

    internal int GetSeriesIndex(NTBaseSeries<TData> series) => Series.IndexOf(series);

    internal List<NTYAxisOptions> GetUniqueYAxes() {
        return Series.OfType<NTCartesianSeries<TData>>()
            .Where(s => s.IsEffectivelyVisible)
            .Select(s => s.YAxis)
            .Where(a => a != null)
            .Distinct()
            .ToList();
    }

    internal List<NTXAxisOptions> GetUniqueXAxes() {
        return Series.OfType<NTCartesianSeries<TData>>()
            .Where(s => s.IsEffectivelyVisible)
            .Select(s => s.XAxis)
            .Where(a => a != null)
            .Distinct()
            .ToList();
    }

    internal float GetBarSeriesTotalWeight(NTYAxisOptions axis) => Series.OfType<NTBarSeries<TData>>().Where(s => s.YAxis == axis).Sum(s => s.VisibilityFactor);

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

    /// <summary>
    ///    Gets whether to use a categorical scale for the X axis.
    /// </summary>
    public bool IsCategoricalX => Series.OfType<NTBarSeries<TData>>().Any(s => s.Orientation == NTChartOrientation.Vertical && s.IsEffectivelyVisible);

    /// <summary>
    ///    Gets whether to use a categorical scale for the Y axis.
    /// </summary>
    public bool IsCategoricalY => Series.OfType<NTBarSeries<TData>>().Any(s => s.Orientation == NTChartOrientation.Horizontal && s.IsEffectivelyVisible);

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

    /// <summary>
    ///    Gets the scale used for the X axis.
    /// </summary>
    public NTAxisScale GetXScale() => GetUniqueXAxes().FirstOrDefault()?.Scale ?? NTAxisScale.Linear;

    public float ScaleX(double x, SKRect plotArea) {
        var axis = GetUniqueXAxes().FirstOrDefault();
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

    public double ScaleXInverse(float coord, SKRect plotArea) {
        var axis = GetUniqueXAxes().FirstOrDefault();
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

    public float ScaleY(decimal y, NTYAxisOptions axis, SKRect plotArea) {
        var (min, max) = GetYRange(axis, true);
        var scale = axis.Scale;

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
    public decimal ScaleYInverse(float coord, NTYAxisOptions axis, SKRect plotArea) {
        var (min, max) = GetYRange(axis, true);
        var scale = axis.Scale;

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

    public (double Min, double Max) GetXRange(NTXAxisOptions? axis, bool padded = false) {
        if (_cachedXRanges.TryGetValue((axis, padded), out var cached)) {
            return cached;
        }

        if (_viewXMin.HasValue && _viewXMax.HasValue) {
            var range = (_viewXMin.Value, _viewXMax.Value);
            _cachedXRanges[(axis, padded)] = range;
            return range;
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
            var range = (-catRange * RangePadding, catRange + (catRange * RangePadding));
            _cachedXRanges[(axis, padded)] = range;
            return range;
        }

        var min = double.MaxValue;
        var max = double.MinValue;

        var seriesToConsider = Series.OfType<NTCartesianSeries<TData>>().Where(s => s.IsEffectivelyVisible);
        if (axis != null) {
            seriesToConsider = seriesToConsider.Where(s => s.XAxis == axis);
        }

        foreach (var s in seriesToConsider) {
            var seriesRange = s.GetXRange();
            if (seriesRange.HasValue) {
                min = Math.Min(min, seriesRange.Value.Min);
                max = Math.Max(max, seriesRange.Value.Max);
            }
        }

        if (min == double.MaxValue) {
            return (0, 1);
        }

        if (!padded) {
            return (min, max);
        }

        var (niceMin, niceMax, _) = CalculateNiceScaling(min, max);
        var result = (niceMin, niceMax);
        _cachedXRanges[(axis, padded)] = result;
        return result;
    }

    public (decimal Min, decimal Max) GetYRange(NTYAxisOptions? axis, bool padded = false) {
        if (_cachedYRanges.TryGetValue((axis, padded), out var cached)) {
            return cached;
        }

        if (_viewYMin.HasValue && _viewYMax.HasValue) {
            var range = (_viewYMin.Value, _viewYMax.Value);
            _cachedYRanges[(axis, padded)] = range;
            return range;
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
            var range = (-catRange * rp, catRange + (catRange * rp));
            _cachedYRanges[(axis, padded)] = range;
            return range;
        }

        var min = decimal.MaxValue;
        var max = decimal.MinValue;

        var seriesToConsider = Series.OfType<NTCartesianSeries<TData>>().Where(s => s.IsEffectivelyVisible);
        if (axis != null) {
            seriesToConsider = seriesToConsider.Where(s => s.YAxis == axis);
        }

        foreach (var s in seriesToConsider) {
            var seriesRange = (IsXZoomEnabled && _viewXMin.HasValue && _viewXMax.HasValue)
                ? s.GetYRange(_viewXMin, _viewXMax)
                : s.GetYRange();

            if (seriesRange.HasValue) {
                min = Math.Min(min, seriesRange.Value.Min);
                max = Math.Max(max, seriesRange.Value.Max);
            }
        }

        if (min == decimal.MaxValue) {
            return (0, 1);
        }

        // Bar charts should generally start at 0
        if (seriesToConsider.Any(s => s is NTBarSeries<TData>)) {
            min = Math.Min(0, min);
        }

        if (!padded) {
            var range = (min, max);
            _cachedYRanges[(axis, padded)] = range;
            return range;
        }

        var (niceMinY, niceMaxY, _) = CalculateNiceScaling((double)min, (double)max);
        var result = ((decimal)niceMinY, (decimal)niceMaxY);
        _cachedYRanges[(axis, padded)] = result;
        return result;
    }

    public (double Min, double Max, double Spacing) CalculateNiceScaling(double min, double max, int maxTicks = 5) {
        if (min == max) {
            max = min + 1;
        }

        var range = CalculateNiceNumber(max - min, false);
        var tickSpacing = CalculateNiceNumber(range / (maxTicks - 1), true);

        var niceMin = Math.Floor(min / tickSpacing) * tickSpacing;
        var niceMax = Math.Ceiling(max / tickSpacing) * tickSpacing;

        return (niceMin, niceMax, tickSpacing);
    }

    private double CalculateNiceNumber(double range, bool round) {
        var exponent = Math.Floor(Math.Log10(range));
        var fraction = range / Math.Pow(10, exponent);
        double niceFraction;

        if (round) {
            niceFraction = fraction < 1.5 ? 1 : fraction < 3 ? 2 : fraction < 7 ? 5 : 10;
        }
        else {
            niceFraction = fraction <= 1 ? 1 : fraction <= 2 ? 2 : fraction <= 5 ? 5 : 10;
        }

        return niceFraction * Math.Pow(10, exponent);
    }

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

        using var stream = data.AsStream();
        await JSRuntime.DownloadFileFromStreamAsync(stream, fileName ?? $"{Title ?? "chart"}.png");
    }

    protected override void Dispose(bool disposing) {
        base.Dispose(disposing);
    }
    protected override ValueTask DisposeAsyncCore() => base.DisposeAsyncCore();
}
