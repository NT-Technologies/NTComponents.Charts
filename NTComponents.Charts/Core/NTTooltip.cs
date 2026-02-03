using Microsoft.AspNetCore.Components;
using SkiaSharp;
using System.ComponentModel;

namespace NTComponents.Charts.Core;

/// <summary>
///     Component for configuring chart tooltips.
/// </summary>
public class NTTooltip : ComponentBase, IRenderable, IDisposable {

    [CascadingParameter]
    protected IAxisChart Chart { get; set; } = default!;

    /// <summary>
    ///     Gets or sets whether tooltips are enabled.
    /// </summary>
    [Parameter]
    public bool Enabled { get; set; } = true;

    /// <summary>
    ///     Gets or sets the background color of the tooltip.
    /// </summary>
    [Parameter]
    public TnTColor? BackgroundColor { get; set; }

    /// <summary>
    ///     Gets or sets the text color of the tooltip.
    /// </summary>
    [Parameter]
    public TnTColor? TextColor { get; set; }

    [EditorBrowsable(EditorBrowsableState.Never)]
    public RenderOrdered RenderOrder => RenderOrdered.Tooltip;

    private SKPaint? _bgPaint;
    private SKPaint? _borderPaint;
    private SKFont? _headerFont;
    private SKFont? _labelFont;
    private SKFont? _valueFont;
    private SKPaint? _headerPaint;
    private SKPaint? _separatorPaint;
    private SKPaint? _iconPaint;
    private SKPaint? _labelPaint;
    private SKPaint? _valuePaint;

    protected override void OnInitialized() {
        base.OnInitialized();
        if (Chart is null) {
            return;
        }
        Chart.SetTooltip(this);
        Chart.RegisterRenderable(this);
    }

    public void Dispose() {
        Chart?.UnregisterRenderable(this);
        _bgPaint?.Dispose();
        _borderPaint?.Dispose();
        _headerFont?.Dispose();
        _labelFont?.Dispose();
        _valueFont?.Dispose();
        _headerPaint?.Dispose();
        _separatorPaint?.Dispose();
        _iconPaint?.Dispose();
        _labelPaint?.Dispose();
        _valuePaint?.Dispose();
    }

    public void Invalidate() {
        _bgPaint?.Dispose();
        _bgPaint = null;
        _borderPaint?.Dispose();
        _borderPaint = null;
        _headerFont?.Dispose();
        _headerFont = null;
        _labelFont?.Dispose();
        _labelFont = null;
        _valueFont?.Dispose();
        _valueFont = null;
        _headerPaint?.Dispose();
        _headerPaint = null;
        _separatorPaint?.Dispose();
        _separatorPaint = null;
        _iconPaint?.Dispose();
        _iconPaint = null;
        _labelPaint?.Dispose();
        _labelPaint = null;
        _valuePaint?.Dispose();
        _valuePaint = null;
    }

    public SKRect Render(NTRenderContext context, SKRect renderArea) {
        if (!Enabled || Chart.HoveredDataPoint == null || Chart.LastMousePosition == null || Chart.HoveredSeries == null || !Chart.HoveredSeries.Visible) {
            return renderArea;
        }

        var canvas = context.Canvas;
        var mousePoint = Chart.LastMousePosition.Value;
        var tooltipInfo = Chart.HoveredSeries.GetTooltipInfo(Chart.HoveredDataPoint);

        if (tooltipInfo.Lines.Count == 0) {
            return renderArea;
        }

        var bgColor = Chart.GetThemeColor(BackgroundColor ?? Chart.HoveredSeries.TooltipBackgroundColor ?? Chart.TooltipBackgroundColor);
        var textColor = Chart.GetThemeColor(TextColor ?? Chart.HoveredSeries.TooltipTextColor ?? Chart.TooltipTextColor);
        var subTextColor = textColor.WithAlpha(200);

        _headerFont ??= new SKFont { Typeface = context.RegularFont.Typeface, Size = 11 * context.Density };
        _labelFont ??= new SKFont { Typeface = context.RegularFont.Typeface, Size = 12 * context.Density };
        _valueFont ??= new SKFont { Typeface = context.DefaultFont.Typeface, Size = 12 * context.Density };

        var padding = 8f * context.Density;
        var headerHeight = string.IsNullOrEmpty(tooltipInfo.Header) ? 0 : _headerFont.Size + (6 * context.Density);
        var separatorHeight = string.IsNullOrEmpty(tooltipInfo.Header) ? 0 : 6 * context.Density;
        var lineHeight = 18f * context.Density;
        var iconSize = 8f * context.Density;
        var iconSpacing = 8f * context.Density;

        float maxWidth = 0;
        if (!string.IsNullOrEmpty(tooltipInfo.Header)) {
            maxWidth = _headerFont.MeasureText(tooltipInfo.Header);
        }

        foreach (var line in tooltipInfo.Lines) {
            var lineWidth = iconSize + iconSpacing + _labelFont.MeasureText(line.Label + ": ") + _valueFont.MeasureText(line.Value);
            maxWidth = Math.Max(maxWidth, lineWidth);
        }

        var totalWidth = maxWidth + (padding * 2);
        var totalHeight = headerHeight + separatorHeight + (tooltipInfo.Lines.Count * lineHeight) + (padding * 2) - (2 * context.Density);

        var rect = new SKRect(mousePoint.X + (15 * context.Density), mousePoint.Y - totalHeight - (15 * context.Density), mousePoint.X + (15 * context.Density) + totalWidth, mousePoint.Y - (15 * context.Density));

        if (rect.Right > context.Info.Width - (Chart.Margin.Right * context.Density)) {
            rect.Offset(-(totalWidth + (30 * context.Density)), 0);
        }
        if (rect.Top < Chart.Margin.Top * context.Density) {
            rect.Offset(0, totalHeight + (30 * context.Density));
        }

        _bgPaint ??= new SKPaint { Style = SKPaintStyle.Fill, IsAntialias = true };
        _bgPaint.Color = bgColor.WithAlpha(250);
        canvas.DrawRoundRect(rect, 4 * context.Density, 4 * context.Density, _bgPaint);

        _borderPaint ??= new SKPaint { Style = SKPaintStyle.Stroke, StrokeWidth = 1, IsAntialias = true };
        _borderPaint.Color = Chart.GetThemeColor(TnTColor.OutlineVariant);
        canvas.DrawRoundRect(rect, 4 * context.Density, 4 * context.Density, _borderPaint);

        var currentY = rect.Top + padding;

        if (!string.IsNullOrEmpty(tooltipInfo.Header)) {
            _headerPaint ??= new SKPaint { IsAntialias = true, Color = subTextColor };
            canvas.DrawText(tooltipInfo.Header, rect.Left + padding, currentY + _headerFont.Size - (2 * context.Density), SKTextAlign.Left, _headerFont, _headerPaint);
            currentY += headerHeight;

            _separatorPaint ??= new SKPaint { StrokeWidth = 1, IsAntialias = true, Color = Chart.GetThemeColor(TnTColor.OutlineVariant) };
            canvas.DrawLine(rect.Left, currentY - (4 * context.Density), rect.Right, currentY - (4 * context.Density), _separatorPaint);
            currentY += separatorHeight - (4 * context.Density);
        }

        foreach (var line in tooltipInfo.Lines) {
            var centerX = rect.Left + padding + (iconSize / 2);
            var centerY = currentY + (lineHeight / 2) - (1 * context.Density);

            _iconPaint ??= new SKPaint { Style = SKPaintStyle.Fill, IsAntialias = true };
            _iconPaint.Color = line.Color;
            canvas.DrawCircle(centerX, centerY, iconSize / 2, _iconPaint);

            var textX = rect.Left + padding + iconSize + iconSpacing;
            var textY = currentY + (14 * context.Density);

            _labelPaint ??= new SKPaint { IsAntialias = true, Color = subTextColor };
            canvas.DrawText(line.Label + ": ", textX, textY, SKTextAlign.Left, _labelFont, _labelPaint);

            var labelWidth = _labelFont.MeasureText(line.Label + ": ");

            _valuePaint ??= new SKPaint { IsAntialias = true, Color = textColor };
            canvas.DrawText(line.Value, textX + labelWidth, textY, SKTextAlign.Left, _valueFont, _valuePaint);

            currentY += lineHeight;
        }

        return renderArea;
    }
}
