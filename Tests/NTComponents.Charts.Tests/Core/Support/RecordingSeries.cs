using Microsoft.AspNetCore.Components;
using NTComponents.Charts.Core;
using NTComponents.Charts.Core.Series;
using SkiaSharp;

namespace NTComponents.Charts.Tests.ChartCore;

/// <summary>
///     Cartesian series with scripted hit testing. It records every render/hit-test call so tests can assert
///     chart-level orchestration (render order, clipping area, hover/tooltip/click routing) without depending on
///     per-series geometry.
/// </summary>
public sealed class RecordingSeries : NTCartesianSeries<Pt> {
    /// <summary>Shared log that receives the series label each time it renders.</summary>
    [Parameter]
    public List<string>? Log { get; set; }

    /// <summary>Label written to <see cref="Log"/>.</summary>
    [Parameter]
    public string Label { get; set; } = "series";

    /// <summary>Scripted hit test. Returning null means no hit.</summary>
    [Parameter]
    public Func<SKPoint, SKRect, (int Index, Pt? Data)?>? Hit { get; set; }

    public void SetViewYRangeForTest(decimal min, decimal max) { _viewYMin = min; _viewYMax = max; }

    public List<SKRect> RenderAreas { get; } = [];
    public List<SKRect> HitAreas { get; } = [];
    public List<SKPoint> HitPoints { get; } = [];

    public override (int Index, Pt? Data)? HitTest(SKPoint point, SKRect renderArea) {
        HitAreas.Add(renderArea);
        HitPoints.Add(point);
        return Hit?.Invoke(point, renderArea);
    }

    public override SKRect Render(NTRenderContext context, SKRect renderArea) {
        RenderAreas.Add(renderArea);
        Log?.Add(Label);
        return renderArea;
    }
}
