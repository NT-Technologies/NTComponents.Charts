using Microsoft.AspNetCore.Components;
using SkiaSharp;

namespace NTComponents.Charts.Core;

/// <summary>
///     Represents the legend for a chart.
/// </summary>
public class NTLegend<TData> : ComponentBase, IRenderable where TData : class {

    /// <summary>
    ///     Gets or sets the parent <see cref="NTChart{TData}"/> instance provided via cascading parameter.
    /// </summary>
    [CascadingParameter]
    protected NTChart<TData> Chart { get; set; } = default!;

    /// <summary>
    ///    Gets or sets the position of the legend.
    /// </summary>
    [Parameter]
    public LegendPosition Position { get; set; } = LegendPosition.Bottom;

    /// <summary>
    ///     Gets or sets the font size for the legend text.
    /// </summary>
    [Parameter]
    public float FontSize { get; set; } = 12.0f;

    /// <summary>
    ///     Gets or sets the size of the legend icon (square).
    /// </summary>
    [Parameter]
    public float IconSize { get; set; } = 12.0f;

    /// <summary>
    ///     Gets or sets the spacing between legend items.
    /// </summary>
    [Parameter]
    public float ItemSpacing { get; set; } = NTChartTokens.LegendItemSpacing;

    /// <summary>
    ///     Gets or sets whether the legend is visible.
    /// </summary>
    [Parameter]
    public bool Visible { get; set; } = true;

    /// <summary>
    ///     Gets or sets the background color for the legend.
    ///     Currently only applied when <see cref="Position"/> is <see cref="LegendPosition.Floating"/>.
    ///     If null, uses the opaque <see cref="NTColor.SurfaceContainer"/> role.
    /// </summary>
    [Parameter]
    public NTColor? BackgroundColor { get; set; }

    /// <summary>
    ///     Gets or sets the most item rows a <see cref="LegendPosition.Top"/> or <see cref="LegendPosition.Bottom"/> legend
    ///     shows. Items that need more rows fill these rows column by column and scroll sideways with the mouse wheel. Side
    ///     and floating legends scroll vertically once they run out of height.
    /// </summary>
    [Parameter]
    public int MaxRows { get; set; } = 2;

    /// <summary>
    ///    Gets or sets the current offset when Position is Floating.
    /// </summary>
    public SKPoint? FloatingOffset { get; set; }

    /// <summary>
    ///     Gets the area, in device pixels, that scrolls the legend under the mouse wheel, or <see cref="SKRect.Empty"/>
    ///     when every item is already visible.
    /// </summary>
    public SKRect ScrollRegion => Overflow > 0.5f ? LastDrawArea : SKRect.Empty;

    /// <summary>
    ///     Scrolls the legend items when <paramref name="point"/> is over a legend that overflows.
    /// </summary>
    /// <param name="point">The pointer position in device pixels.</param>
    /// <param name="deltaX">The horizontal scroll distance in device pixels; positive scrolls toward later items.</param>
    /// <param name="deltaY">The vertical scroll distance in device pixels; positive scrolls toward later items.</param>
    /// <returns><see langword="true"/> when the legend handled the scroll.</returns>
    public bool ScrollBy(SKPoint point, float deltaX, float deltaY) {
        if (!ScrollRegion.Contains(point)) {
            return false;
        }

        // Most mice only have a vertical wheel, so it also scrolls a sideways legend.
        var delta = _scrollsHorizontally && Math.Abs(deltaX) > Math.Abs(deltaY) ? deltaX : deltaY;
        _scrollOffset = Math.Clamp(_scrollOffset + delta, 0f, Overflow);
        return true;
    }

    /// <inheritdoc />
    protected override void OnInitialized() {
        base.OnInitialized();
        if (Chart is null) {
            throw new ArgumentNullException(nameof(Chart), $"Legend must be used within a {nameof(NTChart<TData>)}.");
        }
        Chart.RegisterLegend(this);
        Chart.RegisterRenderable(this);
    }

    /// <inheritdoc />
    public void Dispose() {
        _font?.Dispose();
        _iconPaint?.Dispose();
        _textPaint?.Dispose();
        _highlightPaint?.Dispose();
        _backgroundPaint?.Dispose();
        _borderPaint?.Dispose();
        _scrollbarPaint?.Dispose();
        _itemRects.Clear();
        _items = null;
        _cachedRows = null;
        ClearItemMeasurements();
        Chart?.UnregisterLegend(this);
        Chart?.UnregisterRenderable(this);
    }

    /// <inheritdoc />
    public RenderOrdered RenderOrder => RenderOrdered.Legend;

    /// <inheritdoc />
    public void Invalidate() {
        _items = null;
        _cachedRows = null;
        ClearItemMeasurements();
    }

    private SKPaint? _iconPaint;
    private SKPaint? _textPaint;
    private SKPaint? _highlightPaint;
    private SKPaint? _backgroundPaint;
    private SKPaint? _borderPaint;
    private SKPaint? _scrollbarPaint;
    private List<LegendItemInfo<TData>>? _items;
    private List<List<LegendItemInfo<TData>>>? _cachedRows;
    // The box of each item (in item order) from the last render in unscrolled content coordinates, the visible window
    // onto them, the items' extent along the scroll direction, and the scroll position. Items are looked up when needed
    // because a series may refresh them mid-frame.
    private readonly List<SKRect> _itemRects = [];
    private SKRect _viewport;
    private bool _scrollsHorizontally;
    private float _contentSize;
    private float _scrollOffset;
    private readonly Dictionary<LegendItemInfo<TData>, float> _itemWidths = [];
    // Pre-shaped labels: drawing a string through Skia creates a text blob per call, every frame.
    private readonly Dictionary<LegendItemInfo<TData>, SKTextBlob?> _labelBlobs = [];
    private float _cachedRowsWidth;
    private float _cachedRowsDensity;
    private float _cachedRowsFontSize;
    private float _cachedRowsIconSize;
    private float _cachedRowsSpacing;
    private float _fontDensity;

    // Row height in dp: the 24dp minimum touch target, grown for larger fonts.
    private float RowHeight => Math.Max(NTChartTokens.LegendRowHeight, FontSize + 8);

    // Inset the hover state layer extends past the swatch and the label.
    private static float ItemInset => NTChartTokens.LegendPadding / 2;

    private SKFont GetFont(float density) {
        _font ??= new SKFont { Typeface = NTChartTokens.Typeface(NTTypography.LabelMedium) };
        if (_fontDensity != density || _font.Size != FontSize * density) {
            _font.Size = FontSize * density;
            _fontDensity = density;
            _cachedRows = null;
            ClearItemMeasurements();
        }
        return _font;
    }

    private List<LegendItemInfo<TData>> GetItems() => _items ??= Chart.Series.SelectMany(s => s.GetLegendItems()).ToList();

    private static SKPaint GetPaint(ref SKPaint? paint) => paint ??= new SKPaint { IsAntialias = true };

    private bool _hasParameters;
    private LegendPosition _lastPosition;
    private float _lastFontSize;
    private float _lastIconSize;
    private float _lastItemSpacing;
    private bool _lastVisible;
    private NTColor? _lastBackgroundColor;
    private SKPoint? _lastFloatingOffset;
    private int _lastMaxRows;

    /// <inheritdoc />
    protected override void OnParametersSet() {
        if (_hasParameters && (_lastPosition != Position || _lastFontSize != FontSize || _lastIconSize != IconSize || _lastItemSpacing != ItemSpacing || _lastVisible != Visible || _lastBackgroundColor != BackgroundColor || _lastFloatingOffset != FloatingOffset || _lastMaxRows != MaxRows)) {
            Invalidate();
            Chart?.RequestDraw(true);
        }
        _lastPosition = Position;
        _lastFontSize = FontSize;
        _lastIconSize = IconSize;
        _lastItemSpacing = ItemSpacing;
        _lastVisible = Visible;
        _lastBackgroundColor = BackgroundColor;
        _lastFloatingOffset = FloatingOffset;
        _lastMaxRows = MaxRows;
        _hasParameters = true;
    }

    internal SKRect GetFloatingRect(SKRect plotArea, float density = 1f) {
        if (Position != LegendPosition.Floating) {
            return SKRect.Empty;
        }

        var font = GetFont(density);
        var items = GetItems();

        // Same NaN handling as Enumerable.Max: NaN widths are skipped unless every width is NaN.
        float? widest = null;
        foreach (var item in items) {
            var width = GetItemWidth(item, font, density);
            if (widest is null || width > widest || float.IsNaN(widest.Value)) {
                widest = width;
            }
        }
        var maxWidth = widest + (2 * NTChartTokens.LegendPadding * density) ?? 100 * density;
        // Items past what fits in the plot area (at least one row) scroll.
        var rowHeight = RowHeight * density;
        var contentHeight = Math.Min(items.Count * rowHeight, Math.Max(rowHeight, plotArea.Height - (4 * NTChartTokens.LegendPadding * density)));
        var totalHeight = contentHeight + (2 * NTChartTokens.LegendPadding * density);

        float x, y;
        if (FloatingOffset.HasValue) {
            x = plotArea.Left + FloatingOffset.Value.X;
            y = plotArea.Top + FloatingOffset.Value.Y;
        }
        else {
            // Default position
            x = plotArea.Right - maxWidth - (NTChartTokens.LegendPadding * density);
            y = plotArea.Top + (NTChartTokens.LegendPadding * density);
        }

        return new SKRect(x, y, x + maxWidth, y + totalHeight);
    }

    internal LegendItemInfo<TData>? GetItemAtPoint(SKPoint point) {
        if (!_viewport.Contains(point)) {
            return null;
        }

        var scroll = ScrollVector;
        var contentPoint = new SKPoint(point.X + scroll.X, point.Y + scroll.Y);
        var index = _itemRects.FindIndex(rect => rect.Contains(contentPoint));
        var items = GetItems();
        return index >= 0 && index < items.Count ? items[index] : null;
    }

    private SKFont? _font;
    internal SKRect LastDrawArea { get; private set; }

    // How far the items extend past the visible window; positive when the legend scrolls.
    private float Overflow => _contentSize - (_scrollsHorizontally ? _viewport.Width : _viewport.Height);

    private SKPoint ScrollVector => _scrollsHorizontally ? new SKPoint(_scrollOffset, 0) : new SKPoint(0, _scrollOffset);

    /// <summary>
    /// Calculates the area required to display the legend based on its position and the available space within the
    /// chart.
    /// </summary>
    /// <remarks>The measured area is adjusted according to the legend's position (top, bottom, left, or
    /// right) and the available space, ensuring the legend is properly sized and positioned within the chart. Items that
    /// do not fit (past <see cref="MaxRows"/> rows, or past the available height) scroll. If the legend is not visible
    /// or its position is not supported, an empty rectangle is returned.</remarks>
    /// <param name="context">The rendering context that provides font settings, density scaling, and other information necessary for accurate
    /// measurement.</param>
    /// <param name="renderArea">The rectangle that defines the current available area for the legend within the chart layout.</param>
    /// <returns>A rectangle representing the remaining area after the legend has been rendered. Returns <paramref name="renderArea"/> if the legend is
    /// not visible or its position is set to None.</returns>
    public SKRect Render(NTRenderContext context, SKRect renderArea) {
        _itemRects.Clear();
        _viewport = SKRect.Empty;
        _scrollsHorizontally = false;
        _contentSize = 0;
        if (!Visible || Position == LegendPosition.None) {
            LastDrawArea = SKRect.Empty;
            return renderArea;
        }

        var density = context.Density;
        var font = GetFont(density);
        var padding = NTChartTokens.LegendPadding * density;
        var rowHeight = RowHeight * density;
        var legendArea = SKRect.Empty;
        var remainingArea = renderArea;

        switch (Position) {
            case LegendPosition.Top:
            case LegendPosition.Bottom:
                var contentArea = GetHorizontalLegendContentArea(renderArea);
                var rows = GetLegendRows(font, GetHorizontalLegendMaxWidth(contentArea, density), density);
                var maxRows = Math.Max(1, MaxRows);
                var legendHeight = (Math.Min(rows.Count, maxRows) * rowHeight) + padding;

                if (Position == LegendPosition.Top) {
                    legendArea = new SKRect(contentArea.Left, renderArea.Top, contentArea.Right, renderArea.Top + legendHeight);
                    remainingArea = new SKRect(renderArea.Left, renderArea.Top + legendHeight, renderArea.Right, renderArea.Bottom);
                }
                else {
                    legendArea = new SKRect(contentArea.Left, renderArea.Bottom - legendHeight, contentArea.Right, renderArea.Bottom);
                    remainingArea = new SKRect(renderArea.Left, renderArea.Top, renderArea.Right, renderArea.Bottom - legendHeight);
                }

                _viewport = new SKRect(legendArea.Left, legendArea.Top + (padding / 2), legendArea.Right, legendArea.Bottom - (padding / 2));
                if (rows.Count > maxRows) {
                    LayoutColumns(maxRows, font, density);
                    break;
                }
                for (var r = 0; r < rows.Count; r++) {
                    var x = GetHorizontalLegendRowStartX(contentArea, GetRowWidth(rows[r], font, density));
                    var rowTop = _viewport.Top + (r * rowHeight);
                    foreach (var item in rows[r]) {
                        var itemWidth = GetItemWidth(item, font, density);
                        _itemRects.Add(new SKRect(x, rowTop, x + itemWidth, rowTop + rowHeight));
                        x += itemWidth + (ItemSpacing * density);
                    }
                }
                break;

            case LegendPosition.Left:
            case LegendPosition.Right:
                float legendWidth = 0;
                foreach (var item in GetItems()) {
                    legendWidth = Math.Max(legendWidth, GetItemWidth(item, font, density));
                }
                legendWidth += 2 * padding;

                if (Position == LegendPosition.Left) {
                    legendArea = new SKRect(renderArea.Left, renderArea.Top, renderArea.Left + legendWidth, renderArea.Bottom);
                    remainingArea = new SKRect(renderArea.Left + legendWidth, renderArea.Top, renderArea.Right, renderArea.Bottom);
                }
                else {
                    legendArea = new SKRect(renderArea.Right - legendWidth, renderArea.Top, renderArea.Right, renderArea.Bottom);
                    remainingArea = new SKRect(renderArea.Left, renderArea.Top, renderArea.Right - legendWidth, renderArea.Bottom);
                }
                LayoutColumn(legendArea, font, density);
                break;

            case LegendPosition.Floating:
                legendArea = GetFloatingRect(context.PlotArea, density);
                LayoutColumn(legendArea, font, density);

                var corner = NTChartTokens.Radius(NTChartTokens.LegendCorner) * density;
                var bgPaint = GetPaint(ref _backgroundPaint);
                bgPaint.Color = Chart.GetThemeColor(BackgroundColor ?? NTColor.SurfaceContainer);
                bgPaint.Style = SKPaintStyle.Fill;
                context.Canvas.DrawRoundRect(legendArea, corner, corner, bgPaint);

                var borderPaint = GetPaint(ref _borderPaint);
                borderPaint.Color = Chart.GetThemeColor(NTColor.OutlineVariant);
                borderPaint.Style = SKPaintStyle.Stroke;
                borderPaint.StrokeWidth = density;
                context.Canvas.DrawRoundRect(legendArea, corner, corner, borderPaint);
                break;
        }

        LastDrawArea = legendArea;
        if (!_scrollsHorizontally) {
            _contentSize = _itemRects.Count == 0 ? 0 : _itemRects[^1].Bottom - _viewport.Top;
        }
        _scrollOffset = Math.Clamp(_scrollOffset, 0f, Math.Max(0f, Overflow));

        var scroll = ScrollVector;
        context.Canvas.Save();
        context.Canvas.ClipRect(_viewport);
        context.Canvas.Translate(-scroll.X, -scroll.Y);
        try {
            var items = GetItems();
            for (var i = 0; i < _itemRects.Count; i++) {
                // Items scrolled out of view are clipped anyway; skipping them keeps long legends cheap to draw.
                var shown = _itemRects[i];
                shown.Offset(-scroll.X, -scroll.Y);
                if (shown.IntersectsWith(_viewport)) {
                    RenderItem(context, font, items[i], _itemRects[i].Left, _itemRects[i].Top);
                }
            }
        }
        finally {
            context.Canvas.Restore();
        }

        RenderScrollbar(context);
        return remainingArea;
    }

    // Stacks every item in one column inside the legend's padding.
    private void LayoutColumn(SKRect legendArea, SKFont font, float density) {
        var padding = NTChartTokens.LegendPadding * density;
        var rowHeight = RowHeight * density;
        _viewport = new SKRect(legendArea.Left, legendArea.Top + padding, legendArea.Right, legendArea.Bottom - padding);
        var x = legendArea.Left + padding;
        var rowTop = _viewport.Top;
        foreach (var item in GetItems()) {
            _itemRects.Add(new SKRect(x, rowTop, x + GetItemWidth(item, font, density), rowTop + rowHeight));
            rowTop += rowHeight;
        }
    }

    // Fills the rows column by column, each column as wide as its widest item, so the extra items scroll sideways.
    private void LayoutColumns(int rows, SKFont font, float density) {
        var padding = NTChartTokens.LegendPadding * density;
        var rowHeight = RowHeight * density;
        var items = GetItems();
        var x = _viewport.Left + (padding / 2);
        float columnWidth = 0;
        for (var i = 0; i < items.Count; i++) {
            if (i > 0 && i % rows == 0) {
                x += columnWidth + (ItemSpacing * density);
                columnWidth = 0;
            }
            var itemWidth = GetItemWidth(items[i], font, density);
            var rowTop = _viewport.Top + (i % rows * rowHeight);
            _itemRects.Add(new SKRect(x, rowTop, x + itemWidth, rowTop + rowHeight));
            columnWidth = Math.Max(columnWidth, itemWidth);
        }
        _scrollsHorizontally = true;
        _contentSize = x + columnWidth + (padding / 2) - _viewport.Left;
    }

    // A thin thumb on the legend's trailing edge (its bottom when it scrolls sideways) shows that the items scroll and
    // where the view is.
    private void RenderScrollbar(NTRenderContext context) {
        if (Overflow <= 0.5f) {
            return;
        }

        var density = context.Density;
        var thickness = 4 * density;
        var length = _scrollsHorizontally ? _viewport.Width : _viewport.Height;
        var thumb = Math.Max(16 * density, length * length / _contentSize);
        var start = (length - thumb) * (_scrollOffset / Overflow);
        var rect = _scrollsHorizontally
            ? SKRect.Create(_viewport.Left + start, LastDrawArea.Bottom - thickness, thumb, thickness)
            : SKRect.Create(LastDrawArea.Right - (2 * density) - thickness, _viewport.Top + start, thickness, thumb);

        var paint = GetPaint(ref _scrollbarPaint);
        paint.Color = NTChartTokens.SecondaryTextColor(Chart).WithAlpha((byte)(255 * NTChartTokens.DeEmphasisOpacity));
        paint.Style = SKPaintStyle.Fill;
        context.Canvas.DrawRoundRect(rect, thickness / 2, thickness / 2, paint);
    }

    private void RenderItem(NTRenderContext context, SKFont font, LegendItemInfo<TData> item, float x, float rowTop) {
        var canvas = context.Canvas;
        var density = context.Density;

        var itemWidth = GetItemWidth(item, font, density);
        var rowHeight = RowHeight * density;
        var itemRect = new SKRect(x, rowTop, x + itemWidth, rowTop + rowHeight);

        // Only the swatch fades while another series is hovered; the label keeps its contrast. Hidden items are disabled content.
        var iconColor = item.Color;
        var textColor = NTChartTokens.SecondaryTextColor(Chart);
        if (item.Series != null) {
            iconColor = iconColor.WithAlpha((byte)(iconColor.Alpha * item.Series.GetLegendItemAlphaFactor(item)));
        }

        if (!item.IsVisible) {
            iconColor = iconColor.WithAlpha((byte)(iconColor.Alpha * NTChartTokens.DeEmphasisOpacity));
            textColor = textColor.WithAlpha((byte)(textColor.Alpha * NTChartTokens.DeEmphasisOpacity));
        }

        if (item.Series?.IsLegendItemHovered(item) ?? false) {
            var corner = Math.Min(NTChartTokens.Radius(NTChartTokens.LegendCorner) * density, rowHeight / 2);
            var highlightPaint = GetPaint(ref _highlightPaint);
            highlightPaint.Color = NTChartTokens.SecondaryTextColor(Chart).WithAlpha((byte)(255 * NTChartTokens.HoverOpacity));
            highlightPaint.Style = SKPaintStyle.Fill;
            canvas.DrawRoundRect(itemRect, corner, corner, highlightPaint);
        }

        var iconPaint = GetPaint(ref _iconPaint);
        iconPaint.Color = iconColor;
        iconPaint.Style = SKPaintStyle.Fill;
        var currentTextPaint = GetPaint(ref _textPaint);
        currentTextPaint.Color = textColor;
        currentTextPaint.Style = SKPaintStyle.Fill;

        var iconSize = IconSize * density;
        var swatchLeft = x + (ItemInset * density);
        canvas.DrawCircle(swatchLeft + (iconSize / 2), rowTop + (rowHeight / 2), iconSize / 2, iconPaint);
        if (GetLabelBlob(item, font) is { } label) {
            canvas.DrawText(label, swatchLeft + iconSize + (NTChartTokens.LegendSwatchGap * density), NTChartTokens.TextBaseline(rowTop, rowHeight, font.Size), currentTextPaint);
        }
    }

    private List<List<LegendItemInfo<TData>>> GetLegendRows(SKFont font, float maxWidth, float density) {
        if (_cachedRows is not null && _cachedRowsWidth == maxWidth && _cachedRowsDensity == density && _cachedRowsFontSize == FontSize && _cachedRowsIconSize == IconSize && _cachedRowsSpacing == ItemSpacing) {
            return _cachedRows;
        }

        var rows = new List<List<LegendItemInfo<TData>>>();
        var currentRow = new List<LegendItemInfo<TData>>();
        float currentRowWidth = 0;

        var items = GetItems();

        foreach (var item in items) {
            var itemWidth = GetItemWidth(item, font, density);
            if (currentRow.Any() && currentRowWidth + (ItemSpacing * density) + itemWidth > maxWidth) {
                rows.Add(currentRow);
                currentRow = [];
                currentRowWidth = 0;
            }

            if (currentRow.Any()) {
                currentRowWidth += ItemSpacing * density;
            }

            currentRow.Add(item);
            currentRowWidth += itemWidth;
        }

        if (currentRow.Any()) {
            rows.Add(currentRow);
        }

        _cachedRows = rows;
        _cachedRowsWidth = maxWidth;
        _cachedRowsDensity = density;
        _cachedRowsFontSize = FontSize;
        _cachedRowsIconSize = IconSize;
        _cachedRowsSpacing = ItemSpacing;
        return rows;
    }

    private float GetRowWidth(List<LegendItemInfo<TData>> rowItems, SKFont font, float density) {
        // Accumulate in double like Enumerable.Sum so row positions stay pixel-identical.
        var width = 0d;
        foreach (var item in rowItems) {
            width += GetItemWidth(item, font, density);
        }
        return (float)width + ((rowItems.Count - 1) * (ItemSpacing * density));
    }

    private SKTextBlob? GetLabelBlob(LegendItemInfo<TData> item, SKFont font) {
        if (!_labelBlobs.TryGetValue(item, out var blob)) {
            blob = SKTextBlob.Create(item.Label, font);
            _labelBlobs[item] = blob;
        }
        return blob;
    }

    private void ClearItemMeasurements() {
        _itemWidths.Clear();
        foreach (var blob in _labelBlobs.Values) {
            blob?.Dispose();
        }
        _labelBlobs.Clear();
    }

    private float GetItemWidth(LegendItemInfo<TData> item, SKFont font, float density) => _itemWidths.TryGetValue(item, out var width) ? width : _itemWidths[item] = font.MeasureText(item.Label) + ((IconSize + NTChartTokens.LegendSwatchGap + (2 * ItemInset)) * density);

    private SKRect GetHorizontalLegendContentArea(SKRect area) {
        if (Position != LegendPosition.Bottom || Chart.LastPlotArea.Width <= 0) {
            return area;
        }

        return new SKRect(Chart.LastPlotArea.Left, area.Top, Chart.LastPlotArea.Right, area.Bottom);
    }

    private float GetHorizontalLegendMaxWidth(SKRect area, float density) {
        return Math.Max(1f, area.Width - (NTChartTokens.LegendPadding * density));
    }

    private float GetHorizontalLegendRowStartX(SKRect area, float rowWidth) {
        return area.Left + Math.Max(0f, (area.Width - rowWidth) / 2f);
    }
}
