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

    [Parameter]
    public float TitleFontSize { get; set; } = 16;
    [Parameter]
    public float AxisFontSize { get; set; } = 12;

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
}