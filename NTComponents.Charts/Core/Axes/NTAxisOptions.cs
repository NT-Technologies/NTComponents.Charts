using System;
using Microsoft.AspNetCore.Components;
using SkiaSharp;
using NTComponents.Charts.Core;

namespace NTComponents.Charts.Core.Axes;

/// <summary>
///     Base class for all chart axis options.
/// </summary>
public abstract class NTAxisOptions<TData> : ComponentBase, IRenderable where TData : class {

    [CascadingParameter]
    protected IChart<TData> Chart { get; set; } = default!;

    public virtual void Dispose() {
        Chart?.UnregisterRenderable(this);
        GC.SuppressFinalize(this);
    }

    protected override void OnInitialized() {
        base.OnInitialized();
        Chart.RegisterRenderable(this);
    }

    /// <summary>
    ///    Gets or sets the title of the axis.
    /// </summary>
    [Parameter]
    public string? Title { get; set; }

    /// <summary>
    ///    Gets or sets the format string for axis labels.
    /// </summary>
    [Parameter]
    public string? LabelFormat { get; set; }

    /// <summary>
    ///    Gets or sets whether the axis is visible.
    /// </summary>
    [Parameter]
    public bool Visible { get; set; } = true;


    /// <summary>
    ///    Gets or sets the scale used by the axis.
    /// </summary>
    [Parameter]
    public NTAxisScale Scale { get; set; } = NTAxisScale.Linear;

    /// <summary>
    ///    Gets or sets the padding at the minimum end of the axis.
    /// </summary>
    [Parameter]
    public decimal MinPadding { get; set; } = 0.05m;

    /// <summary>
    ///    Gets or sets the padding at the maximum end of the axis.
    /// </summary>
    [Parameter]
    public decimal MaxPadding { get; set; } = 0.05m;

    /// <summary>
    ///    Gets or sets the minimum value for the axis. If null, the minimum will be calculated from the data.
    /// </summary>
    [Parameter]
    public decimal? Min { get; set; }

    /// <summary>
    ///    Gets or sets the maximum value for the axis. If null, the maximum will be calculated from the data.
    /// </summary>
    [Parameter]
    public decimal? Max { get; set; }

    /// <summary>
    ///    Gets or sets the maximum number of ticks on the axis. Default is 10.
    /// </summary>
    [Parameter]
    public int MaxTicks { get; set; } = 10;

    /// <inheritdoc />
    public RenderOrdered RenderOrder => RenderOrdered.Axis;

    /// <inheritdoc />
    public abstract SKRect Render(NTRenderContext context, SKRect renderArea);

    /// <inheritdoc />
    public virtual void Invalidate() => ClearCache();

    internal (double Min, double Max)? CachedXRange { get; set; }
    internal (decimal Min, decimal Max)? CachedYRange { get; set; }

    internal void ClearCache() {
        CachedXRange = null;
        CachedYRange = null;
    }

    internal (double niceMin, double niceMax, double spacing) CalculateNiceScaling(double min, double max, int maxTicks = 10) {
        if (min == max) {
            max = min + 1;
        }

        var range = CalculateNiceNumber(max - min, false);
        var tickSpacing = CalculateNiceNumber(range / (maxTicks - 1), true);

        var niceMin = Math.Floor(min / tickSpacing) * tickSpacing;
        var niceMax = Math.Ceiling(max / tickSpacing) * tickSpacing;

        return (niceMin, niceMax, tickSpacing);
    }

    internal (decimal niceMin, decimal niceMax, decimal spacing) CalculateNiceScaling(decimal min, decimal max, int maxTicks = 10) {
        if (min == max) {
            max = min + 1;
        }

        var range = CalculateNiceNumber(max - min, false);
        var tickSpacing = CalculateNiceNumber(range / (maxTicks - 1), true);

        var niceMin = Math.Floor(min / tickSpacing) * tickSpacing;
        var niceMax = Math.Ceiling(max / tickSpacing) * tickSpacing;

        return (niceMin, niceMax, tickSpacing);
    }

    private double CalculateNiceNumber(double range, bool round) {
        var exponent = Math.Floor(Math.Log10(range));
        var fraction = range / Math.Pow(10, exponent);
        double niceFraction;

        if (round) {
            niceFraction = fraction < 1.5 ? 1 : fraction < 3 ? 2 : fraction < 7 ? 5 : 10;
        }
        else {
            niceFraction = fraction <= 1 ? 1 : fraction <= 2 ? 2 : fraction <= 5 ? 5 : 10;
        }

        return niceFraction * Math.Pow(10, exponent);
    }

    private decimal CalculateNiceNumber(decimal range, bool round) {
        var exponent = Math.Floor(Math.Log10((double)range));
        var fraction = (double)range / Math.Pow(10, exponent);
        double niceFraction;

        if (round) {
            niceFraction = fraction < 1.5 ? 1 : fraction < 3 ? 2 : fraction < 7 ? 5 : 10;
        }
        else {
            niceFraction = fraction <= 1 ? 1 : fraction <= 2 ? 2 : fraction <= 5 ? 5 : 10;
        }

        return (decimal)(niceFraction * Math.Pow(10, exponent));
    }

    public virtual SKRect Measure(NTRenderContext context, SKRect renderArea) => renderArea;
}


