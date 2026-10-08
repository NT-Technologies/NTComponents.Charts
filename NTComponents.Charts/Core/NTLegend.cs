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
    public float ItemSpacing { get; set; } = 15.0f;

    /// <summary>
    ///     Gets or sets whether the legend is visible.
    /// </summary>
    [Parameter]
    public bool Visible { get; set; } = true;

    /// <summary>
    ///     Gets or sets the background color for the legend.
    ///     Currently only applied when <see cref="Position"/> is <see cref="LegendPosition.Floating"/>.
    ///     If null, uses the chart's background color with some transparency.
    /// </summary>
    [Parameter]
    public NTColor? BackgroundColor { get; set; }

    /// <summary>
    ///    Gets or sets the current offset when Position is Floating.
    /// </summary>
    public SKPoint? FloatingOffset { get; set; }

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
        _items = null;
        _cachedRows = null;
        _itemWidths.Clear();
        Chart?.UnregisterLegend(this);
        Chart?.UnregisterRenderable(this);
    }

    /// <inheritdoc />
    public RenderOrdered RenderOrder => RenderOrdered.Legend;

    /// <inheritdoc />
    public void Invalidate() {
        _items = null;
        _cachedRows = null;
        _itemWidths.Clear();
    }

    private SKPaint? _iconPaint;
    private SKPaint? _textPaint;
    private SKPaint? _highlightPaint;
    private SKPaint? _backgroundPaint;
    private SKPaint? _borderPaint;
    private List<LegendItemInfo<TData>>? _items;
    private List<List<LegendItemInfo<TData>>>? _cachedRows;
    private readonly Dictionary<LegendItemInfo<TData>, float> _itemWidths = [];
    private float _cachedRowsWidth;
    private float _cachedRowsDensity;
    private float _cachedRowsFontSize;
    private float _cachedRowsIconSize;
    private float _cachedRowsSpacing;
    private float _fontDensity;
    private SKTypeface? _fontTypeface;

    private SKFont GetFont(float density) {
        var typeface = Chart.DefaultFont.Typeface;
        if (_font is null) {
            _font = new SKFont();
        }
        if (_fontDensity != density || !ReferenceEquals(_fontTypeface, typeface) || _font.Size != FontSize * density) {
            _font.Size = FontSize * density;
            _font.Typeface = typeface;
            _fontDensity = density;
            _fontTypeface = typeface;
            _cachedRows = null;
            _itemWidths.Clear();
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

    /// <inheritdoc />
    protected override void OnParametersSet() {
        if (_hasParameters && (_lastPosition != Position || _lastFontSize != FontSize || _lastIconSize != IconSize || _lastItemSpacing != ItemSpacing || _lastVisible != Visible || _lastBackgroundColor != BackgroundColor || _lastFloatingOffset != FloatingOffset)) {
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
        _hasParameters = true;
    }

    internal SKRect GetFloatingRect(SKRect plotArea, float density = 1f) {
        if (Position != LegendPosition.Floating) {
            return SKRect.Empty;
        }

        var font = GetFont(density);
        var items = GetItems();

        var maxWidth = items.Any() ? items.Max(s => GetItemWidth(s, font, density)) + (15 * density) : 100 * density;
        var totalHeight = (items.Count * ((FontSize + 5) * density)) + (15 * density);

        float x, y;
        if (FloatingOffset.HasValue) {
            x = plotArea.Left + FloatingOffset.Value.X;
            y = plotArea.Top + FloatingOffset.Value.Y;
        }
        else {
            // Default position
            x = plotArea.Right - maxWidth - (10 * density);
            y = plotArea.Top + (10 * density);
        }

        return new SKRect(x, y, x + maxWidth, y + totalHeight);
    }

    internal LegendItemInfo<TData>? GetItemAtPoint(SKPoint point, SKRect plotArea, SKRect legendDrawArea, float density) {
        if (!Visible || Position == LegendPosition.None) {
            return null;
        }

        var font = GetFont(density);

        // Handle Horizontal (Top/Bottom)

        if (Position is LegendPosition.Top or LegendPosition.Bottom) {
            var contentArea = GetHorizontalLegendContentArea(legendDrawArea);
            var maxWidth = GetHorizontalLegendMaxWidth(contentArea, density);
            var rows = GetLegendRows(font, maxWidth, density);
            var rowHeight = (FontSize + 10) * density;
            for (var r = 0; r < rows.Count; r++) {
                var rowItems = rows[r];
                var totalRowWidth = rowItems.Sum(i => GetItemWidth(i, font, density)) + ((rowItems.Count - 1) * (ItemSpacing * density));
                var startX = GetHorizontalLegendRowStartX(contentArea, totalRowWidth);

                var y = legendDrawArea.Top + (5 * density) + (FontSize * density) + (r * rowHeight);
                var currentX = startX;

                foreach (var item in rowItems) {
                    var itemWidth = GetItemWidth(item, font, density);
                    var itemRect = new SKRect(currentX, y - (FontSize * density), currentX + itemWidth, y + (5 * density));
                    if (itemRect.Contains(point)) {
                        return item;
                    }

                    currentX += itemWidth + (ItemSpacing * density);
                }
            }
        }
        else if (Position is LegendPosition.Left or LegendPosition.Right) {
            var x = legendDrawArea.Left + (5 * density);
            var currentY = legendDrawArea.Top + (20 * density);
            var items = GetItems();
            foreach (var item in items) {
                var itemWidth = GetItemWidth(item, font, density);
                var itemRect = new SKRect(x - (2 * density), currentY - (FontSize * density), x + itemWidth, currentY + (5 * density));
                if (itemRect.Contains(point)) {
                    return item;
                }

                currentY += (FontSize + 10) * density;
            }
        }
        else if (Position == LegendPosition.Floating) {
            var rect = (legendDrawArea.Width > 0 && legendDrawArea.Height > 0)
                ? legendDrawArea
                : GetFloatingRect(plotArea, density);
            var x = rect.Left + (5 * density);
            var y = rect.Top + (5 * density) + (FontSize * density);
            var items = GetItems();
            foreach (var item in items) {
                var itemWidth = GetItemWidth(item, font, density);
                var itemRect = new SKRect(x - (2 * density), y - (FontSize * density), x + itemWidth, y + (5 * density));
                if (itemRect.Contains(point)) {
                    return item;
                }

                y += (FontSize + 5) * density;
            }
        }

        return null;
    }

    private SKFont? _font;
    internal SKRect LastDrawArea { get; private set; }

    /// <summary>
    /// Calculates the area required to display the legend based on its position and the available space within the
    /// chart.
    /// </summary>
    /// <remarks>The measured area is adjusted according to the legend's position (top, bottom, left, or
    /// right) and the available space, ensuring the legend is properly sized and positioned within the chart. If the
    /// legend is not visible or its position is not supported, an empty rectangle is returned.</remarks>
    /// <param name="context">The rendering context that provides font settings, density scaling, and other information necessary for accurate
    /// measurement.</param>
    /// <param name="renderArea">The rectangle that defines the current available area for the legend within the chart layout.</param>
    /// <returns>A rectangle representing the remaining area after the legend has been rendered. Returns <paramref name="renderArea"/> if the legend is
    /// not visible or its position is set to None.</returns>
    public SKRect Render(NTRenderContext context, SKRect renderArea) {
        if (!Visible || Position == LegendPosition.None) {
            LastDrawArea = SKRect.Empty;
            return renderArea;
        }

        var font = GetFont(context.Density);

        SKRect legendArea = SKRect.Empty;
        SKRect remainingArea = renderArea;

        if (Position != LegendPosition.Floating) {
            switch (Position) {
                case LegendPosition.Top:
                case LegendPosition.Bottom:
                    var contentArea = GetHorizontalLegendContentArea(renderArea);
                    var maxWidth = GetHorizontalLegendMaxWidth(contentArea, context.Density);
                    var rows = GetLegendRows(font, maxWidth, context.Density);
                    var legendHeight = (rows.Count * ((FontSize + 10) * context.Density)) + (10 * context.Density);

                    if (Position == LegendPosition.Top) {
                        legendArea = new SKRect(contentArea.Left, renderArea.Top, contentArea.Right, renderArea.Top + legendHeight);
                        remainingArea = new SKRect(renderArea.Left, renderArea.Top + legendHeight, renderArea.Right, renderArea.Bottom);
                    }
                    else {
                        legendArea = new SKRect(contentArea.Left, renderArea.Bottom - legendHeight, contentArea.Right, renderArea.Bottom);
                        remainingArea = new SKRect(renderArea.Left, renderArea.Top, renderArea.Right, renderArea.Bottom - legendHeight);
                    }
                    break;

                case LegendPosition.Left:
                case LegendPosition.Right:
                    float legendWidth = 0;
                    var items = GetItems();
                    foreach (var item in items) {
                        legendWidth = Math.Max(legendWidth, GetItemWidth(item, font, context.Density) + (5 * context.Density));
                    }
                    legendWidth += 10 * context.Density;

                    if (Position == LegendPosition.Left) {
                        legendArea = new SKRect(renderArea.Left, renderArea.Top, renderArea.Left + legendWidth, renderArea.Bottom);
                        remainingArea = new SKRect(renderArea.Left + legendWidth, renderArea.Top, renderArea.Right, renderArea.Bottom);
                    }
                    else {
                        legendArea = new SKRect(renderArea.Right - legendWidth, renderArea.Top, renderArea.Right, renderArea.Bottom);
                        remainingArea = new SKRect(renderArea.Left, renderArea.Top, renderArea.Right - legendWidth, renderArea.Bottom);
                    }
                    break;
            }
        }
        else {
            legendArea = GetFloatingRect(context.PlotArea, context.Density);
        }

        LastDrawArea = legendArea;

        // Now draw using the logic from old Render but with legendArea
        if (Position is LegendPosition.Top or LegendPosition.Bottom) {
            var contentArea = GetHorizontalLegendContentArea(legendArea);
            var maxWidth = GetHorizontalLegendMaxWidth(contentArea, context.Density);
            var rows = GetLegendRows(font, maxWidth, context.Density);
            var rowHeight = (FontSize + 10) * context.Density;

            context.Canvas.Save();
            context.Canvas.ClipRect(contentArea);

            try {
                for (var r = 0; r < rows.Count; r++) {
                    var rowItems = rows[r];
                    var totalRowWidth = rowItems.Sum(i => GetItemWidth(i, font, context.Density)) + ((rowItems.Count - 1) * (ItemSpacing * context.Density));
                    var startX = GetHorizontalLegendRowStartX(contentArea, totalRowWidth);
                    var y = legendArea.Top + (5 * context.Density) + (FontSize * context.Density) + (r * rowHeight);

                    var currentX = startX;
                    foreach (var item in rowItems) {
                        RenderItem(context, font, item, currentX, y);
                        var itemWidth = GetItemWidth(item, font, context.Density);
                        currentX += itemWidth + (ItemSpacing * context.Density);
                    }
                }
            }
            finally {
                context.Canvas.Restore();
            }
        }
        else if (Position is LegendPosition.Left or LegendPosition.Right) {
            var x = legendArea.Left + (5 * context.Density);
            var currentY = legendArea.Top + (20 * context.Density);

            var items = GetItems();
            foreach (var item in items) {
                RenderItem(context, font, item, x, currentY);
                currentY += (FontSize + 10) * context.Density;
            }
        }
        else if (Position == LegendPosition.Floating) {
            var x = legendArea.Left + (5 * context.Density);
            var y = legendArea.Top + (5 * context.Density) + (FontSize * context.Density);

            var items = GetItems();
            var bgColor = BackgroundColor ?? Chart.BackgroundColor;

            var bgPaint = GetPaint(ref _backgroundPaint);
            bgPaint.Color = Chart.GetThemeColor(bgColor).WithAlpha(200);
            bgPaint.Style = SKPaintStyle.Fill;
            context.Canvas.DrawRoundRect(legendArea, 4 * context.Density, 4 * context.Density, bgPaint);

            var borderPaint = GetPaint(ref _borderPaint);
            borderPaint.Color = Chart.GetThemeColor(NTColor.OutlineVariant);
            borderPaint.Style = SKPaintStyle.Stroke;
            borderPaint.StrokeWidth = context.Density;
            context.Canvas.DrawRoundRect(legendArea, 4 * context.Density, 4 * context.Density, borderPaint);

            foreach (var item in items) {
                RenderItem(context, font, item, x, y);
                y += (FontSize + 5) * context.Density;
            }
        }

        return remainingArea;
    }

    private void RenderItem(NTRenderContext context, SKFont font, LegendItemInfo<TData> item, float x, float y) {
        var canvas = context.Canvas;
        var density = context.Density;

        var itemWidth = GetItemWidth(item, font, density);
        var itemRect = new SKRect(x, y - (FontSize * density), x + itemWidth, y + (5 * density));

        var isItemHovered = item.Series?.IsLegendItemHovered(item) ?? false;

        var iconColor = item.Color;
        var currentTextColor = context.TextColor;

        if (item.Series != null) {
            var hoverFactor = item.Series.GetLegendItemAlphaFactor(item);
            iconColor = iconColor.WithAlpha((byte)(iconColor.Alpha * hoverFactor));
            currentTextColor = currentTextColor.WithAlpha((byte)(currentTextColor.Alpha * hoverFactor));
        }

        if (!item.IsVisible) {
            iconColor = iconColor.WithAlpha((byte)(iconColor.Alpha * 0.3f));
            currentTextColor = currentTextColor.WithAlpha((byte)(currentTextColor.Alpha * 0.3f));
        }

        if (isItemHovered) {
            var highlightPaint = GetPaint(ref _highlightPaint);
            highlightPaint.Color = item.Color.WithAlpha(40);
            highlightPaint.Style = SKPaintStyle.Fill;
            canvas.DrawRoundRect(itemRect, 4 * density, 4 * density, highlightPaint);
        }

        var iconPaint = GetPaint(ref _iconPaint);
        iconPaint.Color = iconColor;
        iconPaint.Style = SKPaintStyle.Fill;
        var currentTextPaint = GetPaint(ref _textPaint);
        currentTextPaint.Color = currentTextColor;
        currentTextPaint.Style = SKPaintStyle.Fill;

        canvas.DrawRect(x, y - (IconSize * density) + (2 * density), IconSize * density, IconSize * density, iconPaint);
        canvas.DrawText(item.Label, x + (IconSize * density) + (5 * density), y, SKTextAlign.Left, font, currentTextPaint);
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

    private float GetItemWidth(LegendItemInfo<TData> item, SKFont font, float density) => _itemWidths.TryGetValue(item, out var width) ? width : _itemWidths[item] = font.MeasureText(item.Label) + (IconSize * density) + (10 * density);

    private SKRect GetHorizontalLegendContentArea(SKRect area) {
        if (Position != LegendPosition.Bottom || Chart.LastPlotArea.Width <= 0) {
            return area;
        }

        return new SKRect(Chart.LastPlotArea.Left, area.Top, Chart.LastPlotArea.Right, area.Bottom);
    }

    private float GetHorizontalLegendMaxWidth(SKRect area, float density) {
        return Math.Max(1f, area.Width - (10 * density));
    }

    private float GetHorizontalLegendRowStartX(SKRect area, float rowWidth) {
        return area.Left + Math.Max(0f, (area.Width - rowWidth) / 2f);
    }
}
