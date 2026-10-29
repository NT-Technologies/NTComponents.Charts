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
    ///    Gets or sets the position of the legend. Labels wrap onto more lines so that no item, and no left, right
    ///    or floating legend, is wider than 30% of the chart width.
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
    ///     shows. Items that need more rows fill these rows column by column and scroll sideways with the mouse wheel; a
    ///     wrapped label takes several lines of a column but never makes the legend taller than these rows. Side
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
    // Pre-shaped label lines and their widest line: drawing a string through Skia creates a text blob per call, every frame.
    private readonly Dictionary<LegendItemInfo<TData>, (SKTextBlob?[] Lines, float Width)> _labels = [];
    // The label text width that legend labels wrap to; infinity until the first render measures the canvas.
    private float _wrapWidth = float.PositiveInfinity;
    private int _maxLines = MaxLabelLines;
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

    // Labels wrap so an item, and a whole side or floating legend, stays within this share of the canvas width.
    private const float MaxLegendWidthRatio = 0.3f;
    private const int MaxLabelLines = 8;

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
        var extraLines = 0;
        foreach (var item in items) {
            var width = GetItemWidth(item, font, density);
            if (widest is null || width > widest || float.IsNaN(widest.Value)) {
                widest = width;
            }
            extraLines += GetLabel(item, font).Lines.Length - 1;
        }
        var maxWidth = widest + (2 * NTChartTokens.LegendPadding * density) ?? 100 * density;
        // Items past what fits in the plot area (at least one row) scroll.
        var rowHeight = RowHeight * density;
        var contentHeight = Math.Min((items.Count * rowHeight) + (extraLines * font.Spacing), Math.Max(rowHeight, plotArea.Height - (4 * NTChartTokens.LegendPadding * density)));
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
        UpdateLabelLayout(context.TotalArea.Width, font, density);
        var legendArea = SKRect.Empty;
        var remainingArea = renderArea;

        switch (Position) {
            case LegendPosition.Top:
            case LegendPosition.Bottom:
                var contentArea = GetHorizontalLegendContentArea(renderArea);
                var rows = GetLegendRows(font, GetHorizontalLegendMaxWidth(contentArea, density), density);
                // The legend is at most MaxRows rows tall; when the rows, grown for wrapped labels, need more, the items
                // fill that height column by column instead.
                var bandHeight = GetBandHeight(density);
                var rowHeights = GetRowHeights(rows, font, density);
                var flowsInColumns = rowHeights.Sum() > bandHeight + 0.5f;
                var legendHeight = (flowsInColumns ? bandHeight : rowHeights.Sum()) + padding;

                if (Position == LegendPosition.Top) {
                    legendArea = new SKRect(contentArea.Left, renderArea.Top, contentArea.Right, renderArea.Top + legendHeight);
                    remainingArea = new SKRect(renderArea.Left, renderArea.Top + legendHeight, renderArea.Right, renderArea.Bottom);
                }
                else {
                    legendArea = new SKRect(contentArea.Left, renderArea.Bottom - legendHeight, contentArea.Right, renderArea.Bottom);
                    remainingArea = new SKRect(renderArea.Left, renderArea.Top, renderArea.Right, renderArea.Bottom - legendHeight);
                }

                _viewport = new SKRect(legendArea.Left, legendArea.Top + (padding / 2), legendArea.Right, legendArea.Bottom - (padding / 2));
                if (flowsInColumns) {
                    LayoutColumns(font, density);
                    break;
                }
                var rowTop = _viewport.Top;
                for (var r = 0; r < rows.Count; r++) {
                    var x = GetHorizontalLegendRowStartX(contentArea, GetRowWidth(rows[r], font, density));
                    foreach (var item in rows[r]) {
                        var itemWidth = GetItemWidth(item, font, density);
                        _itemRects.Add(new SKRect(x, rowTop, x + itemWidth, rowTop + GetItemHeight(item, font, density)));
                        x += itemWidth + (ItemSpacing * density);
                    }
                    rowTop += rowHeights[r];
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
                    RenderItem(context, font, items[i], _itemRects[i]);
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
        _viewport = new SKRect(legendArea.Left, legendArea.Top + padding, legendArea.Right, legendArea.Bottom - padding);
        var x = legendArea.Left + padding;
        var rowTop = _viewport.Top;
        foreach (var item in GetItems()) {
            var itemHeight = GetItemHeight(item, font, density);
            _itemRects.Add(new SKRect(x, rowTop, x + GetItemWidth(item, font, density), rowTop + itemHeight));
            rowTop += itemHeight;
        }
    }

    // Stacks items down the viewport's height column by column, each column as wide as its widest item, so the extra
    // items scroll sideways.
    private void LayoutColumns(SKFont font, float density) {
        var padding = NTChartTokens.LegendPadding * density;
        var x = _viewport.Left + (padding / 2);
        var rowTop = _viewport.Top;
        float columnWidth = 0;
        foreach (var item in GetItems()) {
            var itemHeight = GetItemHeight(item, font, density);
            if (rowTop > _viewport.Top && rowTop + itemHeight > _viewport.Bottom + 0.5f) {
                x += columnWidth + (ItemSpacing * density);
                columnWidth = 0;
                rowTop = _viewport.Top;
            }
            var itemWidth = GetItemWidth(item, font, density);
            _itemRects.Add(new SKRect(x, rowTop, x + itemWidth, rowTop + itemHeight));
            columnWidth = Math.Max(columnWidth, itemWidth);
            rowTop += itemHeight;
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

    // Draws an item in its laid-out box: the swatch beside the first line, wrapped lines below it.
    private void RenderItem(NTRenderContext context, SKFont font, LegendItemInfo<TData> item, SKRect itemRect) {
        var canvas = context.Canvas;
        var density = context.Density;
        var x = itemRect.Left;
        var rowTop = itemRect.Top;
        var rowHeight = RowHeight * density;

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
        var textX = swatchLeft + iconSize + (NTChartTokens.LegendSwatchGap * density);
        var baseline = NTChartTokens.TextBaseline(rowTop, rowHeight, font.Size);
        var lines = GetLabel(item, font).Lines;
        for (var i = 0; i < lines.Length; i++) {
            if (lines[i] is { } line) {
                canvas.DrawText(line, textX, baseline + (i * font.Spacing), currentTextPaint);
            }
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

    private (SKTextBlob?[] Lines, float Width) GetLabel(LegendItemInfo<TData> item, SKFont font) {
        if (!_labels.TryGetValue(item, out var label)) {
            if (float.IsPositiveInfinity(_wrapWidth)) {
                label = ([SKTextBlob.Create(item.Label, font)], font.MeasureText(item.Label));
            }
            else {
                var lines = NTLabelText.Wrap(font, item.Label, _wrapWidth, _maxLines);
                label = (lines.Select(line => SKTextBlob.Create(line, font)).ToArray(), lines.Max(line => font.MeasureText(line)));
            }
            _labels[item] = label;
        }
        return label;
    }

    // Sets how wide and, for top and bottom legends, how many lines a label may wrap to; a top or bottom item never
    // grows taller than the legend's MaxRows rows.
    private void UpdateLabelLayout(float canvasWidth, SKFont font, float density) {
        var wrapWidth = Math.Max(1f, (canvasWidth * MaxLegendWidthRatio) - (2 * NTChartTokens.LegendPadding * density) - GetItemWidth(0f, density));
        var maxLines = Position is LegendPosition.Top or LegendPosition.Bottom
            ? Math.Clamp(1 + (int)((GetBandHeight(density) - (RowHeight * density)) / font.Spacing), 1, MaxLabelLines)
            : MaxLabelLines;
        if (wrapWidth != _wrapWidth || maxLines != _maxLines) {
            _wrapWidth = wrapWidth;
            _maxLines = maxLines;
            _cachedRows = null;
            ClearItemMeasurements();
        }
    }

    // The most height a top or bottom legend's items take: MaxRows rows.
    private float GetBandHeight(float density) => Math.Max(1, MaxRows) * RowHeight * density;

    // How tall each row of a top or bottom legend is: its tallest item.
    private float[] GetRowHeights(List<List<LegendItemInfo<TData>>> rows, SKFont font, float density) {
        var heights = new float[rows.Count];
        for (var r = 0; r < rows.Count; r++) {
            foreach (var item in rows[r]) {
                heights[r] = Math.Max(heights[r], GetItemHeight(item, font, density));
            }
        }
        return heights;
    }

    // An item's height: one row, plus a line for each extra wrapped line of its label.
    private float GetItemHeight(LegendItemInfo<TData> item, SKFont font, float density) => (RowHeight * density) + ((GetLabel(item, font).Lines.Length - 1) * font.Spacing);

    private void ClearItemMeasurements() {
        foreach (var label in _labels.Values) {
            foreach (var line in label.Lines) {
                line?.Dispose();
            }
        }
        _labels.Clear();
    }

    private float GetItemWidth(LegendItemInfo<TData> item, SKFont font, float density) => GetItemWidth(GetLabel(item, font).Width, density);

    // An item's width: the swatch, its gap to the label, the label, and the hover inset on both ends.
    private float GetItemWidth(float labelWidth, float density) => labelWidth + ((IconSize + NTChartTokens.LegendSwatchGap + (2 * ItemInset)) * density);

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
