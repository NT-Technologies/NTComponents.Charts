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
    private List<LegendItemInfo<TData>>? _items;
    private List<List<LegendItemInfo<TData>>>? _cachedRows;
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

        // Same NaN handling as Enumerable.Max: NaN widths are skipped unless every width is NaN.
        float? widest = null;
        foreach (var item in items) {
            var width = GetItemWidth(item, font, density);
            if (widest is null || width > widest || float.IsNaN(widest.Value)) {
                widest = width;
            }
        }
        var maxWidth = widest + (2 * NTChartTokens.LegendPadding * density) ?? 100 * density;
        var totalHeight = (items.Count * RowHeight * density) + (2 * NTChartTokens.LegendPadding * density);

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
            var rowHeight = RowHeight * density;
            for (var r = 0; r < rows.Count; r++) {
                var rowItems = rows[r];
                var startX = GetHorizontalLegendRowStartX(contentArea, GetRowWidth(rowItems, font, density));

                var rowTop = legendDrawArea.Top + (NTChartTokens.LegendPadding / 2 * density) + (r * rowHeight);
                var currentX = startX;

                foreach (var item in rowItems) {
                    var itemWidth = GetItemWidth(item, font, density);
                    var itemRect = new SKRect(currentX, rowTop, currentX + itemWidth, rowTop + rowHeight);
                    if (itemRect.Contains(point)) {
                        return item;
                    }

                    currentX += itemWidth + (ItemSpacing * density);
                }
            }
        }
        else if (Position is LegendPosition.Left or LegendPosition.Right) {
            var x = legendDrawArea.Left + (NTChartTokens.LegendPadding * density);
            var rowTop = legendDrawArea.Top + (NTChartTokens.LegendPadding * density);
            var items = GetItems();
            foreach (var item in items) {
                var itemWidth = GetItemWidth(item, font, density);
                var itemRect = new SKRect(x, rowTop, x + itemWidth, rowTop + (RowHeight * density));
                if (itemRect.Contains(point)) {
                    return item;
                }

                rowTop += RowHeight * density;
            }
        }
        else if (Position == LegendPosition.Floating) {
            var rect = (legendDrawArea.Width > 0 && legendDrawArea.Height > 0)
                ? legendDrawArea
                : GetFloatingRect(plotArea, density);
            var x = rect.Left + (NTChartTokens.LegendPadding * density);
            var rowTop = rect.Top + (NTChartTokens.LegendPadding * density);
            var items = GetItems();
            foreach (var item in items) {
                var itemWidth = GetItemWidth(item, font, density);
                var itemRect = new SKRect(x, rowTop, x + itemWidth, rowTop + (RowHeight * density));
                if (itemRect.Contains(point)) {
                    return item;
                }

                rowTop += RowHeight * density;
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
                    var legendHeight = (rows.Count * RowHeight * context.Density) + (NTChartTokens.LegendPadding * context.Density);

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
                        legendWidth = Math.Max(legendWidth, GetItemWidth(item, font, context.Density));
                    }
                    legendWidth += 2 * NTChartTokens.LegendPadding * context.Density;

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
            var rowHeight = RowHeight * context.Density;

            context.Canvas.Save();
            context.Canvas.ClipRect(contentArea);

            try {
                for (var r = 0; r < rows.Count; r++) {
                    var rowItems = rows[r];
                    var startX = GetHorizontalLegendRowStartX(contentArea, GetRowWidth(rowItems, font, context.Density));
                    var rowTop = legendArea.Top + (NTChartTokens.LegendPadding / 2 * context.Density) + (r * rowHeight);

                    var currentX = startX;
                    foreach (var item in rowItems) {
                        RenderItem(context, font, item, currentX, rowTop);
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
            var x = legendArea.Left + (NTChartTokens.LegendPadding * context.Density);
            var rowTop = legendArea.Top + (NTChartTokens.LegendPadding * context.Density);

            var items = GetItems();
            foreach (var item in items) {
                RenderItem(context, font, item, x, rowTop);
                rowTop += RowHeight * context.Density;
            }
        }
        else if (Position == LegendPosition.Floating) {
            var x = legendArea.Left + (NTChartTokens.LegendPadding * context.Density);
            var rowTop = legendArea.Top + (NTChartTokens.LegendPadding * context.Density);

            var items = GetItems();
            var corner = NTChartTokens.Radius(NTChartTokens.LegendCorner) * context.Density;

            var bgPaint = GetPaint(ref _backgroundPaint);
            bgPaint.Color = Chart.GetThemeColor(BackgroundColor ?? NTColor.SurfaceContainer);
            bgPaint.Style = SKPaintStyle.Fill;
            context.Canvas.DrawRoundRect(legendArea, corner, corner, bgPaint);

            var borderPaint = GetPaint(ref _borderPaint);
            borderPaint.Color = Chart.GetThemeColor(NTColor.OutlineVariant);
            borderPaint.Style = SKPaintStyle.Stroke;
            borderPaint.StrokeWidth = context.Density;
            context.Canvas.DrawRoundRect(legendArea, corner, corner, borderPaint);

            foreach (var item in items) {
                RenderItem(context, font, item, x, rowTop);
                rowTop += RowHeight * context.Density;
            }
        }

        return remainingArea;
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
