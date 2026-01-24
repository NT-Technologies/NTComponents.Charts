using SkiaSharp;

namespace NTComponents.Charts.Core.Axes;

/// <summary>
///     Base class for all chart axis options.
/// </summary>
public abstract class NTAxisOptions {

    /// <summary>
    ///    Gets or sets the title of the axis.
    /// </summary>
    public string? Title { get; set; }

    /// <summary>
    ///    Gets or sets the format string for axis labels.
    /// </summary>
    public string? LabelFormat { get; set; }

    /// <summary>
    ///    Gets or sets whether the axis is visible.
    /// </summary>
    public bool Visible { get; set; } = true;


    /// <summary>
    ///    Gets or sets the scale used by the axis.
    /// </summary>
    public NTAxisScale Scale { get; set; } = NTAxisScale.Linear;

    /// <summary>
    ///    Measures the axis and returns the remaining area.
    /// </summary>
    /// <param name="renderArea">The current available area.</param>
    /// <param name="chart">The chart the axis belongs to.</param>
    /// <returns>The remaining area after the axis has taken its space.</returns>
    internal abstract SKRect Measure<TData>(SKRect renderArea, NTChart<TData> chart) where TData : class;

    /// <summary>
    ///    Renders the axis on the canvas.
    /// </summary>
    /// <param name="canvas">The canvas to render on.</param>
    /// <param name="plotArea">The area of the chart content.</param>
    /// <param name="totalArea">The total area of the chart.</param>
    /// <param name="chart">The chart the axis belongs to.</param>
    internal abstract void Render<TData>(SKCanvas canvas, SKRect plotArea, SKRect totalArea, NTChart<TData> chart) where TData : class;
}
