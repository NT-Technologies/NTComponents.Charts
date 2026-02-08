using System;
using Microsoft.AspNetCore.Components;
using SkiaSharp;
using NTComponents.Charts.Core;

namespace NTComponents.Charts.Core.Axes;

/// <summary>
///     Base class for all chart axis options.
/// </summary>
public abstract class NTAxisOptions<TData> : ComponentBase, INTAxis<TData> where TData : class {

    /// <summary>
    ///     Gets or sets the format string for axis labels.
    /// </summary>
    [Parameter]
    public string? LabelFormat { get; set; }

    /// <inheritdoc />
    public RenderOrdered RenderOrder => RenderOrdered.Axis;

    /// <summary>
    ///     Gets or sets the scale used by the axis.
    /// </summary>
    [Parameter]
    public NTAxisScale Scale { get; set; } = NTAxisScale.Linear;

    /// <summary>
    ///     Gets or sets the title of the axis.
    /// </summary>
    [Parameter]
    public string? Title { get; set; }

    /// <summary>
    ///     Gets or sets whether the axis is visible.
    /// </summary>
    [Parameter]
    public bool Visible { get; set; } = true;

    [CascadingParameter]
    protected IChart<TData> Chart { get; set; } = default!;

    public virtual void Dispose() {
        Chart?.UnregisterRenderable(this);
        GC.SuppressFinalize(this);
    }

    /// <inheritdoc />
    public virtual void Invalidate() { }

    public virtual SKRect Measure(NTRenderContext context, SKRect renderArea) => renderArea;

    public abstract SKRect Render(NTRenderContext context, SKRect renderArea);

    protected override void OnInitialized() {
        base.OnInitialized();
        Chart.RegisterRenderable(this);
    }

    // internal (double niceMin, double niceMax, double spacing) CalculateNiceScaling(double min, double max, int maxTicks = 10) { if (min == max) { max = min + 1; }

    // var range = CalculateNiceNumber(max - min, false); var tickSpacing = CalculateNiceNumber(range / (maxTicks - 1), true);

    // var niceMin = Math.Floor(min / tickSpacing) * tickSpacing; var niceMax = Math.Ceiling(max / tickSpacing) * tickSpacing;

    // return (niceMin, niceMax, tickSpacing); }

    // internal (decimal niceMin, decimal niceMax, decimal spacing) CalculateNiceScaling(decimal min, decimal max, int maxTicks = 10) { if (min == max) { max = min + 1; }

    // var range = CalculateNiceNumber(max - min, false); var tickSpacing = CalculateNiceNumber(range / (maxTicks - 1), true);

    // var niceMin = Math.Floor(min / tickSpacing) * tickSpacing; var niceMax = Math.Ceiling(max / tickSpacing) * tickSpacing;

    // return (niceMin, niceMax, tickSpacing); }

    // private double CalculateNiceNumber(double range, bool round) { var exponent = Math.Floor(Math.Log10(range)); var fraction = range / Math.Pow(10, exponent); double niceFraction;

    // if (round) { niceFraction = fraction < 1.5 ? 1 : fraction < 3 ? 2 : fraction < 7 ? 5 : 10; } else { niceFraction = fraction <= 1 ? 1 : fraction <= 2 ? 2 : fraction <= 5 ? 5 : 10; }

    // return niceFraction * Math.Pow(10, exponent); }

    // private decimal CalculateNiceNumber(decimal range, bool round) { var exponent = Math.Floor(Math.Log10((double)range)); var fraction = (double)range / Math.Pow(10, exponent); double niceFraction;

    // if (round) { niceFraction = fraction < 1.5 ? 1 : fraction < 3 ? 2 : fraction < 7 ? 5 : 10; } else { niceFraction = fraction <= 1 ? 1 : fraction <= 2 ? 2 : fraction <= 5 ? 5 : 10; }

    // return (decimal)(niceFraction * Math.Pow(10, exponent)); }
}