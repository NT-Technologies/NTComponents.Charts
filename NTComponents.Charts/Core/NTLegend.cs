using Microsoft.AspNetCore.Components;
using SkiaSharp;
using NTComponents.Charts.Core.Series;
using System;
using System.Collections.Generic;
using System.Linq;

namespace NTComponents.Charts.Core;

/// <summary>
///     Represents the legend for a chart.
/// </summary>
public class NTLegend<TData> : ComponentBase, IDisposable where TData : class {

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
    }

    public void Dispose() {
        Chart?.RemoveLegend(this);
    }

    internal SKRect GetFloatingRect(SKRect plotArea) {
        if (Position != LegendPosition.Floating) {
            return SKRect.Empty;
        }

        using var font = new SKFont { Size = FontSize, Embolden = true, Typeface = Chart.DefaultTypeface };
        var items = Chart.Series.SelectMany(s => s.GetLegendItems()).ToList();

        var maxWidth = items.Any() ? items.Max(s => font.MeasureText(s.Label)) + IconSize + 25 : 100;
        var totalHeight = (items.Count * (FontSize + 5)) + 15;

        float x, y;
        if (FloatingOffset.HasValue) {
            x = plotArea.Left + FloatingOffset.Value.X;
            y = plotArea.Top + FloatingOffset.Value.Y;
        }
        else {
            // Default position
            x = plotArea.Right - maxWidth - 10;
            y = plotArea.Top + 10;
        }

        return new SKRect(x, y, x + maxWidth, y + totalHeight);
    }

    internal LegendItemInfo<TData>? GetItemAtPoint(SKPoint point, SKRect plotArea, SKRect legendDrawArea) {
        if (!Visible || Position == LegendPosition.None) {
            return null;
        }

        using var font = new SKFont { Size = FontSize, Embolden = true, Typeface = Chart.DefaultTypeface };

        // Handle Horizontal (Top/Bottom)

        if (Position is LegendPosition.Top or LegendPosition.Bottom) {
            var maxWidth = legendDrawArea.Width - 40;
            var rows = GetLegendRows(font, maxWidth);
            var rowHeight = FontSize + 10;
            for (var r = 0; r < rows.Count; r++) {
                var rowItems = rows[r];
                var totalRowWidth = rowItems.Sum(i => font.MeasureText(i.Label) + IconSize + 10) + ((rowItems.Count - 1) * ItemSpacing);
                var startX = plotArea.Left + ((plotArea.Width - totalRowWidth) / 2);

                var y = legendDrawArea.Top + 5 + FontSize + (r * rowHeight);
                var currentX = startX;

                foreach (var item in rowItems) {
                    var itemWidth = font.MeasureText(item.Label) + IconSize + 10;
                    var itemRect = new SKRect(currentX, y - FontSize, currentX + itemWidth, y + 5);
                    if (itemRect.Contains(point)) {
                        return item;
                    }

                    currentX += itemWidth + ItemSpacing;
                }
            }
        }
        else if (Position is LegendPosition.Left or LegendPosition.Right) {
            var x = legendDrawArea.Left + 5;
            var currentY = legendDrawArea.Top + 20;
            var items = Chart.Series.SelectMany(s => s.GetLegendItems()).ToList();
            foreach (var item in items) {
                var label = item.Label;
                var itemWidth = font.MeasureText(label) + IconSize + 10;
                var itemRect = new SKRect(x - 2, currentY - FontSize, x + itemWidth, currentY + 5);
                if (itemRect.Contains(point)) {
                    return item;
                }

                currentY += FontSize + 10;
            }
        }
        else if (Position == LegendPosition.Floating) {
            var rect = GetFloatingRect(plotArea);
            var x = rect.Left + 5;
            var y = rect.Top + 5 + FontSize;
            var items = Chart.Series.SelectMany(s => s.GetLegendItems()).ToList();
            foreach (var item in items) {
                var label = item.Label;
                var itemWidth = font.MeasureText(label) + IconSize + 10;
                var itemRect = new SKRect(x - 2, y - FontSize, x + itemWidth, y + 5);
                if (itemRect.Contains(point)) {
                    return item;
                }

                y += FontSize + 5;
            }
        }

        return null;
    }

    public (SKRect PlotArea, SKRect LegendArea) Measure(SKRect currentArea) {
        if (!Visible || Position == LegendPosition.None || Position == LegendPosition.Floating) {
            return (currentArea, SKRect.Empty);
        }

        using var font = new SKFont {
            Size = FontSize,
            Embolden = true,
            Typeface = Chart.DefaultTypeface
        };


        if (Position is LegendPosition.Top or LegendPosition.Bottom) {
            var maxWidth = currentArea.Width - 40;
            var rows = GetLegendRows(font, maxWidth);
            var legendHeight = rows.Count * (FontSize + 10) + 10;

            if (Position == LegendPosition.Top) {
                var legendArea = new SKRect(currentArea.Left, currentArea.Top, currentArea.Right, currentArea.Top + legendHeight);
                var plotArea = new SKRect(currentArea.Left, currentArea.Top + legendHeight, currentArea.Right, currentArea.Bottom);
                return (plotArea, legendArea);
            }
            else {
                var legendArea = new SKRect(currentArea.Left, currentArea.Bottom - legendHeight, currentArea.Right, currentArea.Bottom);
                var plotArea = new SKRect(currentArea.Left, currentArea.Top, currentArea.Right, currentArea.Bottom - legendHeight);
                return (plotArea, legendArea);
            }
        }
        else if (Position is LegendPosition.Left or LegendPosition.Right) {
            float legendWidth = 0;
            var items = Chart.Series.SelectMany(s => s.GetLegendItems()).ToList();
            foreach (var item in items) {
                var label = item.Label;
                legendWidth = Math.Max(legendWidth, font.MeasureText(label) + IconSize + 15);
            }
            legendWidth += 10; // padding

            if (Position == LegendPosition.Left) {
                var legendArea = new SKRect(currentArea.Left, currentArea.Top, currentArea.Left + legendWidth, currentArea.Bottom);
                var plotArea = new SKRect(currentArea.Left + legendWidth, currentArea.Top, currentArea.Right, currentArea.Bottom);
                return (plotArea, legendArea);
            }
            else {
                var legendArea = new SKRect(currentArea.Right - legendWidth, currentArea.Top, currentArea.Right, currentArea.Bottom);
                var plotArea = new SKRect(currentArea.Left, currentArea.Top, currentArea.Right - legendWidth, currentArea.Bottom);
                return (plotArea, legendArea);
            }
        }

        return (currentArea, SKRect.Empty);
    }

    public void Render(SKCanvas canvas, SKRect plotArea, SKRect targetArea) {
        if (!Visible || Position == LegendPosition.None) {
            return;
        }

        using var font = new SKFont {
            Size = FontSize,
            Embolden = true,
            Typeface = Chart.DefaultTypeface
        };

        if (Position is LegendPosition.Top or LegendPosition.Bottom) {
            var maxWidth = targetArea.Width - 40;
            var rows = GetLegendRows(font, maxWidth);
            var rowHeight = FontSize + 10;

            for (var r = 0; r < rows.Count; r++) {
                var rowItems = rows[r];
                var totalRowWidth = rowItems.Sum(i => font.MeasureText(i.Label) + IconSize + 10) + ((rowItems.Count - 1) * ItemSpacing);
                var startX = plotArea.Left + ((plotArea.Width - totalRowWidth) / 2);
                var y = targetArea.Top + 5 + FontSize + (r * rowHeight);

                var currentX = startX;
                foreach (var item in rowItems) {
                    RenderItem(canvas, font, item, currentX, y);
                    var itemWidth = font.MeasureText(item.Label) + IconSize + 10;
                    currentX += itemWidth + ItemSpacing;
                }
            }
        }
        else if (Position is LegendPosition.Left or LegendPosition.Right) {
            var x = targetArea.Left + 5;
            var currentY = targetArea.Top + 20;

            var items = Chart.Series.SelectMany(s => s.GetLegendItems()).ToList();
            foreach (var item in items) {
                RenderItem(canvas, font, item, x, currentY);
                currentY += FontSize + 10;
            }
        }
        else if (Position == LegendPosition.Floating) {
            var rect = GetFloatingRect(plotArea);
            var x = rect.Left + 5;
            var y = rect.Top + 5 + FontSize;

            var items = Chart.Series.SelectMany(s => s.GetLegendItems()).ToList();

            var bgColor = BackgroundColor == TnTColor.None ? Chart.BackgroundColor : BackgroundColor;

            using var bgPaint = new SKPaint {
                Color = Chart.GetThemeColor(bgColor).WithAlpha(200),
                Style = SKPaintStyle.Fill,
                IsAntialias = true
            };
            canvas.DrawRoundRect(rect, 4, 4, bgPaint);
            
            using var borderPaint = new SKPaint {
                Color = Chart.GetThemeColor(TnTColor.OutlineVariant),
                Style = SKPaintStyle.Stroke,
                StrokeWidth = 1,
                IsAntialias = true
            };
            canvas.DrawRoundRect(rect, 4, 4, borderPaint);

            foreach (var item in items) {
                RenderItem(canvas, font, item, x, y);
                y += FontSize + 5;
            }
        }
    }

    private void RenderItem(SKCanvas canvas, SKFont font, LegendItemInfo<TData> item, float x, float y) {
        var itemWidth = font.MeasureText(item.Label) + IconSize + 10;
        var itemRect = new SKRect(x, y - FontSize, x + itemWidth, y + 5);

        var isItemHovered = item.Index.HasValue
            ? (Chart.HoveredSeries == item.Series && Chart.HoveredPointIndex == item.Index.Value)
            : (Chart.HoveredSeries == item.Series);

        var iconColor = item.Color;
        var currentTextColor = Chart.GetThemeColor(Chart.TextColor);

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
            canvas.DrawRoundRect(itemRect, 4, 4, highlightPaint);
        }

        using var iconPaint = new SKPaint { Color = iconColor, Style = SKPaintStyle.Fill, IsAntialias = true };
        using var currentTextPaint = new SKPaint { Color = currentTextColor, IsAntialias = true };

        canvas.DrawRect(x, y - IconSize + 2, IconSize, IconSize, iconPaint);
        canvas.DrawText(item.Label, x + IconSize + 5, y, SKTextAlign.Left, font, currentTextPaint);
    }

    private List<List<LegendItemInfo<TData>>> GetLegendRows(SKFont font, float maxWidth) {
        var rows = new List<List<LegendItemInfo<TData>>>();
        var currentRow = new List<LegendItemInfo<TData>>();
        float currentRowWidth = 0;

        var items = Chart.Series.SelectMany(s => s.GetLegendItems()).ToList();

        foreach (var item in items) {
            var itemWidth = font.MeasureText(item.Label) + IconSize + 10;
            if (currentRow.Any() && currentRowWidth + ItemSpacing + itemWidth > maxWidth) {
                rows.Add(currentRow);
                currentRow = [];
                currentRowWidth = 0;
            }

            if (currentRow.Any()) {
                currentRowWidth += ItemSpacing;
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
