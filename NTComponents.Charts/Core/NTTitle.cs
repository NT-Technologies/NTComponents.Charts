using SkiaSharp;
using System.ComponentModel;
using System.Diagnostics;

namespace NTComponents.Charts.Core;

internal class NTTitle<TData> : IRenderable where TData : class {
    private readonly IChart<TData> _chart;
    private SKPoint _point = SKPoint.Empty;
    private SKFont _titleFont = default!;
    private SKPaint _titlePaint = default!;

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
        _titlePaint?.Dispose();
        _titleFont?.Dispose();

        _titlePaint ??= new SKPaint {
            IsAntialias = true,
            Style = SKPaintStyle.Fill,
            Color = _chart.GetThemeColor(_chart.TitleOptions!.TextColor ?? _chart.TextColor)
        };

        _titleFont ??= new SKFont {
            Embolden = true,
            Typeface = _chart.DefaultFont.Typeface,
            Size = _chart.TitleOptions!.FontSize * _chart.Density
        };
        _point = SKPoint.Empty;
    }

    public SKRect Render(NTRenderContext context, SKRect renderArea) {
        var x = renderArea.Left + (renderArea.Width / 2);
        var y = renderArea.Top + (15 * context.Density); // Slightly above center of its allotted 30dp height

        context.Canvas.DrawText(_chart.TitleOptions!.Title, x, y, SKTextAlign.Center, _titleFont, _titlePaint);
        return new SKRect(renderArea.Left, renderArea.Top + (30 * context.Density), renderArea.Right, renderArea.Bottom);
    }

}