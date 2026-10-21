using Microsoft.AspNetCore.Components;
using SkiaSharp;
using System.ComponentModel;

namespace NTComponents.Charts.Core;

/// <summary>
///     Component for configuring chart tooltips.
/// </summary>
public class NTTooltip<TData> : ComponentBase, IRenderable, IDisposable where TData : class {

    /// <summary>
    ///     Gets or sets the parent chart instance provided via cascading parameter.
    /// </summary>
    [CascadingParameter]
    protected IChart<TData> Chart { get; set; } = default!;

    /// <summary>
    ///     Gets or sets whether tooltips are enabled.
    /// </summary>
    [Parameter]
    public bool Enabled { get; set; } = true;

    /// <summary>
    ///     Gets or sets the background color of the tooltip.
    /// </summary>
    [Parameter]
    public NTColor? BackgroundColor { get; set; }

    /// <summary>
    ///     Gets or sets the text color of the tooltip.
    /// </summary>
    [Parameter]
    public NTColor? TextColor { get; set; }

    /// <summary>
    ///     Gets or sets whether tooltip line labels are rendered.
    /// </summary>
    [Parameter]
    public bool ShowSeriesLabels { get; set; } = true;

    /// <inheritdoc />
    [EditorBrowsable(EditorBrowsableState.Never)]
    public RenderOrdered RenderOrder => RenderOrdered.Tooltip;

    private readonly record struct Run(SKTextBlob Blob, float X, float Y);

    private readonly List<Run> _runs = [];
    private readonly List<(float Y, SKColor Color)> _keys = [];
    private readonly List<TooltipLine> _cachedLines = [];
    private readonly List<string> _wrapped = [];
    private string? _cachedHeader;
    private bool _cachedShowLabels;
    private float _cachedDensity;
    private bool _hasLayout;
    private float _width;
    private float _height;
    private float _fontDensity;
    private SKFont? _headerFont;
    private SKFont? _labelFont;
    private SKFont? _valueFont;
    private SKPaint? _bgPaint;
    private SKPaint? _keyShadowPaint;
    private SKPaint? _ambientShadowPaint;
    private SKPaint? _textPaint;
    private SKPaint? _iconPaint;

    /// <inheritdoc />
    protected override void OnInitialized() {
        base.OnInitialized();
        if (Chart is null) {
            return;
        }
        Chart.RegisterRenderable(this);
    }

    /// <inheritdoc />
    public void Dispose() {
        Chart?.UnregisterRenderable(this);
        Invalidate();
    }

    /// <inheritdoc />
    public void Invalidate() {
        ClearLayout();
        _bgPaint?.Dispose();
        _bgPaint = null;
        _keyShadowPaint?.MaskFilter?.Dispose();
        _keyShadowPaint?.Dispose();
        _keyShadowPaint = null;
        _ambientShadowPaint?.MaskFilter?.Dispose();
        _ambientShadowPaint?.Dispose();
        _ambientShadowPaint = null;
        _textPaint?.Dispose();
        _textPaint = null;
        _iconPaint?.Dispose();
        _iconPaint = null;
        _headerFont?.Dispose();
        _headerFont = null;
        _labelFont?.Dispose();
        _labelFont = null;
        _valueFont?.Dispose();
        _valueFont = null;
        _fontDensity = 0;
    }

    /// <inheritdoc />
    public SKRect Render(NTRenderContext context, SKRect renderArea) {
        if (!Enabled || Chart.HoveredDataPoint == null || Chart.LastMousePosition == null || Chart.HoveredSeries == null || !Chart.HoveredSeries.Visible) {
            return renderArea;
        }

        var tooltipInfo = Chart.HoveredSeries.GetTooltipInfo(Chart.HoveredDataPoint);
        if (tooltipInfo.Lines.Count == 0) {
            return renderArea;
        }

        var canvas = context.Canvas;
        var density = context.Density;
        var mousePoint = Chart.LastMousePosition.Value;

        EnsureResources(density);
        if (!IsCurrent(tooltipInfo, density)) {
            Build(tooltipInfo, density);
        }

        var offset = NTChartTokens.TooltipCursorOffset * density;
        var rect = new SKRect(mousePoint.X + offset, mousePoint.Y - _height - offset, mousePoint.X + offset + _width, mousePoint.Y - offset);
        if (rect.Right > context.Info.Width - (Chart.Margin.Right * density)) {
            rect.Offset(-(_width + (2 * offset)), 0);
        }
        if (rect.Top < Chart.Margin.Top * density) {
            rect.Offset(0, _height + (2 * offset));
        }

        // Elevation level 2: a tight key shadow plus a wider ambient shadow, drawn beneath the container.
        var corner = NTChartTokens.Radius(NTChartTokens.TooltipCorner) * density;
        var spread = NTChartTokens.AmbientShadowSpread * density;
        var shadowColor = Chart.GetThemeColor(NTColor.Shadow);
        _keyShadowPaint!.Color = shadowColor.WithAlpha((byte)(255 * NTChartTokens.KeyShadowAlpha));
        var keyRect = rect;
        keyRect.Offset(0, NTChartTokens.KeyShadowY * density);
        canvas.DrawRoundRect(keyRect, corner, corner, _keyShadowPaint);
        _ambientShadowPaint!.Color = shadowColor.WithAlpha((byte)(255 * NTChartTokens.AmbientShadowAlpha));
        var ambientRect = rect;
        ambientRect.Offset(0, NTChartTokens.AmbientShadowY * density);
        ambientRect.Inflate(spread, spread);
        canvas.DrawRoundRect(ambientRect, corner + spread, corner + spread, _ambientShadowPaint);

        _bgPaint!.Color = Chart.GetThemeColor(BackgroundColor ?? Chart.HoveredSeries.TooltipBackgroundColor ?? Chart.TooltipBackgroundColor);
        canvas.DrawRoundRect(rect, corner, corner, _bgPaint);

        _textPaint!.Color = Chart.GetThemeColor(TextColor ?? Chart.HoveredSeries.TooltipTextColor ?? Chart.TooltipTextColor);
        var left = rect.Left + (NTChartTokens.TooltipPaddingInline * density);
        var top = rect.Top + (NTChartTokens.TooltipPaddingBlock * density);

        var keyRadius = NTChartTokens.TooltipKeySize * density / 2;
        foreach (var (y, color) in _keys) {
            _iconPaint!.Color = color;
            canvas.DrawCircle(left + keyRadius, top + y, keyRadius, _iconPaint);
        }

        foreach (var run in _runs) {
            canvas.DrawText(run.Blob, left + run.X, top + run.Y, _textPaint);
        }

        return renderArea;
    }

    private void EnsureResources(float density) {
        if (_fontDensity != density) {
            ClearLayout();
            _headerFont ??= new SKFont { Typeface = NTChartTokens.Typeface(NTTypography.TitleSmall) };
            _labelFont ??= new SKFont { Typeface = NTChartTokens.Typeface(NTTypography.BodyMedium) };
            _valueFont ??= new SKFont { Typeface = NTChartTokens.Typeface(NTTypography.BodyMedium, emphasized: true) };
            _headerFont.Size = NTChartTokens.Size(NTTypography.TitleSmall) * density;
            _labelFont.Size = NTChartTokens.Size(NTTypography.BodyMedium) * density;
            _valueFont.Size = NTChartTokens.Size(NTTypography.BodyMedium) * density;

            _keyShadowPaint?.MaskFilter?.Dispose();
            _keyShadowPaint ??= new SKPaint { IsAntialias = true };
            _keyShadowPaint.MaskFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, NTChartTokens.KeyShadowBlur / 2 * density);
            _ambientShadowPaint?.MaskFilter?.Dispose();
            _ambientShadowPaint ??= new SKPaint { IsAntialias = true };
            _ambientShadowPaint.MaskFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, NTChartTokens.AmbientShadowBlur / 2 * density);
            _fontDensity = density;
        }
        _bgPaint ??= new SKPaint { Style = SKPaintStyle.Fill, IsAntialias = true };
        _textPaint ??= new SKPaint { IsAntialias = true };
        _iconPaint ??= new SKPaint { Style = SKPaintStyle.Fill, IsAntialias = true };
    }

    // The series rebuilds the tooltip payload every frame, so the shaped text is reused only while its content is unchanged.
    private bool IsCurrent(TooltipInfo info, float density) {
        if (!_hasLayout || _cachedDensity != density || _cachedShowLabels != ShowSeriesLabels || _cachedHeader != info.Header || _cachedLines.Count != info.Lines.Count) {
            return false;
        }
        for (var i = 0; i < _cachedLines.Count; i++) {
            var cached = _cachedLines[i];
            var line = info.Lines[i];
            if (cached.Label != line.Label || cached.Value != line.Value || cached.Color != line.Color) {
                return false;
            }
        }
        return true;
    }

    private void Build(TooltipInfo info, float density) {
        ClearLayout();
        _cachedHeader = info.Header;
        _cachedLines.AddRange(info.Lines);
        _cachedDensity = density;
        _cachedShowLabels = ShowSeriesLabels;
        _hasLayout = true;

        var available = (NTChartTokens.TooltipMaxWidth - (2 * NTChartTokens.TooltipPaddingInline)) * density;
        var headerRow = NTChartTokens.LineHeight(NTTypography.TitleSmall) * density;
        var row = NTChartTokens.LineHeight(NTTypography.BodyMedium) * density;
        var indent = (NTChartTokens.TooltipKeySize + NTChartTokens.TooltipKeyGap) * density;
        var y = 0f;
        var width = 0f;

        if (!string.IsNullOrEmpty(info.Header)) {
            y = Wrap(info.Header, _headerFont!, 0, y, headerRow, available, ref width) +NTChartTokens.TooltipContentGap * density;
        }

        foreach (var line in info.Lines) {
            var label = ShowSeriesLabels && !string.IsNullOrWhiteSpace(line.Label) ? line.Label + ": " : string.Empty;
            var value = line.Value ?? string.Empty;
            _keys.Add((y + (row / 2), line.Color));

            if (_labelFont!.MeasureText(label) + _valueFont!.MeasureText(value) <= available - indent) {
                var labelWidth = AddRun(label, _labelFont, indent, y, row);
                width = Math.Max(width, indent + labelWidth + AddRun(value, _valueFont, indent + labelWidth, y, row));
                y += row;
                continue;
            }

            // Too wide for one row: wrap the label, then the value beneath it, both indented past the key.
            y = Wrap(label.TrimEnd(), _labelFont, indent, y, row, available - indent, ref width);
            y = Wrap(value, _valueFont, indent, y, row, available - indent, ref width);
        }

        _width = width + (2 * NTChartTokens.TooltipPaddingInline * density);
        _height = y + (2 * NTChartTokens.TooltipPaddingBlock * density);
    }

    private float Wrap(string text, SKFont font, float x, float y, float row, float maxWidth, ref float width) {
        _wrapped.Clear();
        Wrap(text, font, maxWidth, _wrapped);
        foreach (var part in _wrapped) {
            width = Math.Max(width, x + AddRun(part, font, x, y, row));
            y += row;
        }
        return y;
    }

    // Adds a shaped text run centered in a line starting at rowTop and returns its measured width.
    private float AddRun(string text, SKFont font, float x, float rowTop, float rowHeight) {
        if (text.Length == 0) {
            return 0f;
        }
        var blob = SKTextBlob.Create(text, font);
        if (blob is not null) {
            _runs.Add(new Run(blob, x, NTChartTokens.TextBaseline(rowTop, rowHeight, font.Size)));
        }
        return font.MeasureText(text);
    }

    // Greedy word wrap; a single word wider than maxWidth is split by characters.
    private static void Wrap(string text, SKFont font, float maxWidth, List<string> lines) {
        while (text.Length > 0) {
            var fit = (int)font.BreakText(text, maxWidth);
            if (fit >= text.Length) {
                lines.Add(text);
                return;
            }
            fit = Math.Max(fit, 1);
            var space = text.LastIndexOf(' ', fit - 1);
            var take = space > 0 ? space : fit;
            lines.Add(text[..take]);
            text = text[take..].TrimStart(' ');
        }
    }

    private void ClearLayout() {
        foreach (var run in _runs) {
            run.Blob.Dispose();
        }
        _runs.Clear();
        _keys.Clear();
        _cachedLines.Clear();
        _cachedHeader = null;
        _hasLayout = false;
    }
}
