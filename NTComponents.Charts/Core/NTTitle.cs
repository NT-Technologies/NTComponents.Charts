using SkiaSharp;
using System.ComponentModel;

namespace NTComponents.Charts.Core;

internal class NTTitle<TData> : IRenderable where TData : class {
    private readonly IChart<TData> _chart;
    private SKFont? _titleFont;
    private SKPaint? _titlePaint;
    private float _fontSize;
    private SKTypeface? _typeface;
    private SKColor _textColor;

    public NTTitle(IChart<TData> chart) {
        ArgumentNullException.ThrowIfNull(chart, nameof(chart));
        _chart = chart;

        if (_chart.TitleOptions is null) {
            throw new InvalidOperationException("Cannot instantiate a Title without TitleOptions.");
        }
        _chart.RegisterRenderable(this);
    }

    [EditorBrowsable(EditorBrowsableState.Never)]
    public RenderOrdered RenderOrder => RenderOrdered.Title;

    public void Dispose() {
        _titlePaint?.Dispose();
        _titleFont?.Dispose();
        _chart.UnregisterRenderable(this);
    }

    public void Invalidate() {
        var options = _chart.TitleOptions!;
        var typeface = _chart.DefaultFont.Typeface;
        var fontSize = options.FontSize * _chart.Density;
        var textColor = _chart.GetThemeColor(options.TextColor ?? _chart.TextColor);

        _titlePaint ??= new SKPaint { IsAntialias = true, Style = SKPaintStyle.Fill };
        _titleFont ??= new SKFont();
        _titlePaint.Color = textColor;
        _titleFont.Typeface = typeface;
        _titleFont.Size = fontSize;
        _typeface = typeface;
        _fontSize = fontSize;
        _textColor = textColor;
    }

    public SKRect Render(NTRenderContext context, SKRect renderArea) {
        if (_titleFont is null || _titlePaint is null || !ReferenceEquals(_typeface, _chart.DefaultFont.Typeface) || _fontSize != _chart.TitleOptions!.FontSize * context.Density || _textColor != _chart.GetThemeColor(_chart.TitleOptions.TextColor ?? _chart.TextColor)) {
            Invalidate();
        }

        var x = renderArea.Left + (renderArea.Width / 2);
        var y = renderArea.Top + (15 * context.Density); // Slightly above center of its allotted 30dp height

        context.Canvas.DrawText(_chart.TitleOptions!.Title, x, y, SKTextAlign.Center, _titleFont!, _titlePaint!);
        return new SKRect(renderArea.Left, renderArea.Top + (30 * context.Density), renderArea.Right, renderArea.Bottom);
    }

}
