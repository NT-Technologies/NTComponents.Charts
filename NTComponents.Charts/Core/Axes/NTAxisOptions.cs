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
    ///     Gets or sets the font size used for axis tick labels.
    /// </summary>
    [Parameter]
    public float AxisFontSize { get; set; } = 12;

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
    ///     Gets or sets the font size used for the axis title.
    /// </summary>
    [Parameter]
    public float TitleFontSize { get; set; } = 14;

    /// <summary>
    ///     Gets or sets whether the axis is visible.
    /// </summary>
    [Parameter]
    public bool Visible { get; set; } = true;

    /// <summary>
    ///     Gets or sets the widest a rotated X tick label or a Y tick label may be, in device-independent pixels. Longer
    ///     labels end in an ellipsis and show their full text in a tooltip on hover. X labels that do not fit side by side
    ///     wrap onto two lines before they rotate. <see langword="null"/> never shortens labels.
    /// </summary>
    [Parameter]
    public float? MaxLabelWidth { get; set; } = 120f;

    /// <summary>
    ///     Gets the full text of the shortened tick label drawn at <paramref name="point"/>, or <see langword="null"/> when
    ///     no shortened label is there.
    /// </summary>
    /// <param name="point">A canvas point in device pixels.</param>
    public string? GetTruncatedLabelAt(SKPoint point) {
        if (!Visible) {
            return null;
        }

        foreach (var label in TruncatedLabels) {
            // Undo the label's rotation about its anchor so the test runs against its unrotated bounds.
            var radians = -label.RotationDegrees * (MathF.PI / 180f);
            var dx = point.X - label.Anchor.X;
            var dy = point.Y - label.Anchor.Y;
            var local = new SKPoint((dx * MathF.Cos(radians)) - (dy * MathF.Sin(radians)), (dx * MathF.Sin(radians)) + (dy * MathF.Cos(radians)));
            if (label.Bounds.Contains(local)) {
                return label.Text;
            }
        }

        return null;
    }

    /// <summary>
    ///     The shortened labels drawn by the last render: the anchor the label was drawn at, its rotation in degrees, its
    ///     unrotated bounds relative to the anchor, and its full text.
    /// </summary>
    protected List<(SKPoint Anchor, float RotationDegrees, SKRect Bounds, string Text)> TruncatedLabels { get; } = [];

    /// <summary>
    ///     Gets or sets the parent chart that owns this axis.
    /// </summary>
    [CascadingParameter]
    protected IChart<TData> Chart { get; set; } = default!;

    internal void AttachChart(IChart<TData> chart) {
        ArgumentNullException.ThrowIfNull(chart, nameof(chart));
        Chart = chart;
    }

    /// <inheritdoc />
    public virtual void Dispose() {
        Chart?.UnregisterRenderable(this);
        GC.SuppressFinalize(this);
    }

    /// <inheritdoc />
    public virtual void Invalidate() { }

    /// <summary>
    ///     Measures the required space for the axis within the given render area.
    /// </summary>
    /// <param name="context">   The current frame's render context.</param>
    /// <param name="renderArea">The availble render area.</param>
    /// <returns>The space that would be left over in the <paramref name="renderArea"/> after rendering the axis.</returns>
    public virtual SKRect Measure(NTRenderContext context, SKRect renderArea) => renderArea;

    /// <summary>
    ///     Renders the axis onto the canvas within the given render area.
    /// </summary>
    /// <param name="context">The current frame's render context.</param>
    /// <param name="renderArea">The available render area.</param>
    /// <returns>The remaining render area after the axis has been drawn.</returns>
    public abstract SKRect Render(NTRenderContext context, SKRect renderArea);

    /// <inheritdoc />
    protected override void OnInitialized() {
        base.OnInitialized();
        if (Chart is null) {
            throw new InvalidOperationException($"Axis {GetType().Name} must be used within a {nameof(NTChart<TData>)}.");
        }
        Chart.RegisterRenderable(this);
    }
}
