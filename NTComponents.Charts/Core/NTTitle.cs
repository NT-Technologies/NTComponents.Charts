using SkiaSharp;
using System.Diagnostics;

namespace NTComponents.Charts.Core;

internal class NTTitle : IRenderable {
    private readonly IChart _chart;
    private SKPoint _point = SKPoint.Empty;
    private SKFont _titleFont = default!;
    private SKPaint _titlePaint = default!;

    public NTTitle(IChart chart) {
        ArgumentNullException.ThrowIfNull(chart, nameof(chart));
        _chart = chart;

        if(_chart.TitleOptions is null) {
            throw new InvalidOperationException("Cannot instantiate a Title without TitleOptions.");
        }
        _chart.RegisterRenderable(this);
    }

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
        if (_point == SKPoint.Empty) {
            var x = (_chart.Margin.Left * _chart.Density) + ((context.Info.Width - (_chart.Margin.Left * _chart.Density) - (_chart.Margin.Right * _chart.Density)) / 2);
            var y = (_chart.Margin.Top * _chart.Density) + (20 * _chart.Density);
            _point = new(x, y);
        }

        context.Canvas.DrawText(_chart.TitleOptions!.Title, _point.X, _point.Y, SKTextAlign.Center, _titleFont, _titlePaint);
        return new SKRect(renderArea.Left, renderArea.Top + (30 * context.Density), renderArea.Right, renderArea.Bottom);
    }
}