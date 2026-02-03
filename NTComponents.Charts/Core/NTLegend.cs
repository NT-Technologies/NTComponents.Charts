using Microsoft.AspNetCore.Components;
using SkiaSharp;

namespace NTComponents.Charts.Core;

/// <summary>
///     Represents the legend for a chart.
/// </summary>
public class NTLegend<TData> : ComponentBase, IRenderable where TData : class {

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
    ///     If None, uses the chart's background color with some transparency.
    /// </summary>
    [Parameter]
    public TnTColor BackgroundColor { get; set; } = TnTColor.None;

    /// <summary>
    ///    Gets or sets the current offset when Position is Floating.
    /// </summary>
    public SKPoint? FloatingOffset { get; set; }

    protected override void OnInitialized() {
        base.OnInitialized();
        if (Chart is null) {
            throw new ArgumentNullException(nameof(Chart), $"Legend must be used within a {nameof(NTChart<TData>)}.");
        }
        Chart.SetLegend(this);
        Chart.RegisterRenderable(this);
    }

    public void Dispose() {
        _font?.Dispose();
        Chart?.RemoveLegend(this);
        Chart?.UnregisterRenderable(this);
    }

    public RenderOrdered RenderOrder => RenderOrdered.Legend;

    public void Invalidate() {
        _font?.Dispose();
        _font = null;
    }
    internal SKRect GetFloatingRect(SKRect plotArea, float density = 1f) {
        if (Position != LegendPosition.Floating) {
            return SKRect.Empty;
        }

        using var font = new SKFont { Size = FontSize * density, Embolden = true, Typeface = Chart.DefaultFont.Typeface };
        var items = Chart.Series.SelectMany(s => s.GetLegendItems()).ToList();

        var maxWidth = items.Any() ? items.Max(s => font.MeasureText(s.Label)) + (IconSize * density) + (25 * density) : 100 * density;
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

        using var font = new SKFont { Size = FontSize * density, Embolden = true, Typeface = Chart.DefaultFont.Typeface };

        // Handle Horizontal (Top/Bottom)

        if (Position is LegendPosition.Top or LegendPosition.Bottom) {
            var maxWidth = legendDrawArea.Width - (40 * density);
            var rows = GetLegendRows(font, maxWidth, density);
            var rowHeight = (FontSize + 10) * density;
            for (var r = 0; r < rows.Count; r++) {
                var rowItems = rows[r];
                var totalRowWidth = rowItems.Sum(i => font.MeasureText(i.Label) + (IconSize * density) + (10 * density)) + ((rowItems.Count - 1) * (ItemSpacing * density));
                var startX = plotArea.Left + ((plotArea.Width - totalRowWidth) / 2);

                var y = legendDrawArea.Top + (5 * density) + (FontSize * density) + (r * rowHeight);
                var currentX = startX;

                foreach (var item in rowItems) {
                    var itemWidth = font.MeasureText(item.Label) + (IconSize * density) + (10 * density);
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
            var items = Chart.Series.SelectMany(s => s.GetLegendItems()).ToList();
            foreach (var item in items) {
                var label = item.Label;
                var itemWidth = font.MeasureText(label) + (IconSize * density) + (10 * density);
                var itemRect = new SKRect(x - (2 * density), currentY - (FontSize * density), x + itemWidth, currentY + (5 * density));
                if (itemRect.Contains(point)) {
                    return item;
                }

                currentY += (FontSize + 10) * density;
            }
        }
        else if (Position == LegendPosition.Floating) {
            var rect = GetFloatingRect(plotArea, density);
            var x = rect.Left + (5 * density);
            var y = rect.Top + (5 * density) + (FontSize * density);
            var items = Chart.Series.SelectMany(s => s.GetLegendItems()).ToList();
            foreach (var item in items) {
                var label = item.Label;
                var itemWidth = font.MeasureText(label) + (IconSize * density) + (10 * density);
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

        _font ??= new SKFont {
            Size = FontSize * context.Density,
            Embolden = true,
            Typeface = Chart.DefaultFont.Typeface
        };

        SKRect legendArea = SKRect.Empty;
        SKRect remainingArea = renderArea;

        if (Position != LegendPosition.Floating) {
            switch (Position) {
                case LegendPosition.Top:
                case LegendPosition.Bottom:
                    var maxWidth = renderArea.Width - (40 * context.Density);
                    var rows = GetLegendRows(_font, maxWidth, context.Density);
                    var legendHeight = (rows.Count * ((FontSize + 10) * context.Density)) + (10 * context.Density);

                    if (Position == LegendPosition.Top) {
                        legendArea = new SKRect(renderArea.Left, renderArea.Top, renderArea.Right, renderArea.Top + legendHeight);
                        remainingArea = new SKRect(renderArea.Left, renderArea.Top + legendHeight, renderArea.Right, renderArea.Bottom);
                    }
                    else {
                        legendArea = new SKRect(renderArea.Left, renderArea.Bottom - legendHeight, renderArea.Right, renderArea.Bottom);
                        remainingArea = new SKRect(renderArea.Left, renderArea.Top, renderArea.Right, renderArea.Bottom - legendHeight);
                    }
                    break;

                case LegendPosition.Left:
                case LegendPosition.Right:
                    float legendWidth = 0;
                    var items = Chart.Series.SelectMany(s => s.GetLegendItems()).ToList();
                    foreach (var item in items) {
                        var label = item.Label;
                        legendWidth = Math.Max(legendWidth, _font.MeasureText(label) + (IconSize * context.Density) + (15 * context.Density));
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
            var maxWidth = legendArea.Width - (40 * context.Density);
            var rows = GetLegendRows(_font!, maxWidth, context.Density);
            var rowHeight = (FontSize + 10) * context.Density;

            for (var r = 0; r < rows.Count; r++) {
                var rowItems = rows[r];
                var totalRowWidth = rowItems.Sum(i => _font!.MeasureText(i.Label) + (IconSize * context.Density) + (10 * context.Density)) + ((rowItems.Count - 1) * (ItemSpacing * context.Density));
                var startX = legendArea.Left + ((legendArea.Width - totalRowWidth) / 2);
                var y = legendArea.Top + (5 * context.Density) + (FontSize * context.Density) + (r * rowHeight);

                var currentX = startX;
                foreach (var item in rowItems) {
                    RenderItem(context, _font!, item, currentX, y);
                    var itemWidth = _font!.MeasureText(item.Label) + (IconSize * context.Density) + (10 * context.Density);
                    currentX += itemWidth + (ItemSpacing * context.Density);
                }
            }
        }
        else if (Position is LegendPosition.Left or LegendPosition.Right) {
            var x = legendArea.Left + (5 * context.Density);
            var currentY = legendArea.Top + (20 * context.Density);

            var items = Chart.Series.SelectMany(s => s.GetLegendItems()).ToList();
            foreach (var item in items) {
                RenderItem(context, _font!, item, x, currentY);
                currentY += (FontSize + 10) * context.Density;
            }
        }
        else if (Position == LegendPosition.Floating) {
            var x = legendArea.Left + (5 * context.Density);
            var y = legendArea.Top + (5 * context.Density) + (FontSize * context.Density);

            var items = Chart.Series.SelectMany(s => s.GetLegendItems()).ToList();
            var bgColor = BackgroundColor == TnTColor.None ? Chart.BackgroundColor : BackgroundColor;

            using var bgPaint = new SKPaint {
                Color = Chart.GetThemeColor(bgColor).WithAlpha(200),
                Style = SKPaintStyle.Fill,
                IsAntialias = true
            };
            context.Canvas.DrawRoundRect(legendArea, 4 * context.Density, 4 * context.Density, bgPaint);

            using var borderPaint = new SKPaint {
                Color = Chart.GetThemeColor(TnTColor.OutlineVariant),
                Style = SKPaintStyle.Stroke,
                StrokeWidth = 1 * context.Density,
                IsAntialias = true
            };
            context.Canvas.DrawRoundRect(legendArea, 4 * context.Density, 4 * context.Density, borderPaint);

            foreach (var item in items) {
                RenderItem(context, _font!, item, x, y);
                y += (FontSize + 5) * context.Density;
            }
        }

        return remainingArea;
    }

    private void RenderItem(NTRenderContext context, SKFont font, LegendItemInfo<TData> item, float x, float y) {
        var canvas = context.Canvas;
        var density = context.Density;

        var itemWidth = font.MeasureText(item.Label) + (IconSize * density) + (10 * density);
        var itemRect = new SKRect(x, y - (FontSize * density), x + itemWidth, y + (5 * density));

        var isItemHovered = item.Index.HasValue
            ? (Chart.HoveredSeries == item.Series && Chart.HoveredPointIndex == item.Index.Value)
            : (Chart.HoveredSeries == item.Series);

        var iconColor = item.Color;
        var currentTextColor = context.TextColor;

        if (item.Series != null) {
            var hoverFactor = item.Series.HoverFactor;
            iconColor = iconColor.WithAlpha((byte)(iconColor.Alpha * hoverFactor));
            currentTextColor = currentTextColor.WithAlpha((byte)(currentTextColor.Alpha * hoverFactor));
        }

        if (!item.IsVisible) {
            iconColor = iconColor.WithAlpha((byte)(iconColor.Alpha * 0.3f));
            currentTextColor = currentTextColor.WithAlpha((byte)(currentTextColor.Alpha * 0.3f));
        }

        if (isItemHovered) {
            using var highlightPaint = new SKPaint { Color = item.Color.WithAlpha(40), Style = SKPaintStyle.Fill, IsAntialias = true };
            canvas.DrawRoundRect(itemRect, 4 * density, 4 * density, highlightPaint);
        }

        using var iconPaint = new SKPaint { Color = iconColor, Style = SKPaintStyle.Fill, IsAntialias = true };
        using var currentTextPaint = new SKPaint { Color = currentTextColor, IsAntialias = true };

        canvas.DrawRect(x, y - (IconSize * density) + (2 * density), IconSize * density, IconSize * density, iconPaint);
        canvas.DrawText(item.Label, x + (IconSize * density) + (5 * density), y, SKTextAlign.Left, font, currentTextPaint);
    }

    private List<List<LegendItemInfo<TData>>> GetLegendRows(SKFont font, float maxWidth, float density) {
        var rows = new List<List<LegendItemInfo<TData>>>();
        var currentRow = new List<LegendItemInfo<TData>>();
        float currentRowWidth = 0;

        var items = Chart.Series.SelectMany(s => s.GetLegendItems()).ToList();

        foreach (var item in items) {
            var itemWidth = font.MeasureText(item.Label) + (IconSize * density) + (10 * density);
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

        return rows;
    }
}
