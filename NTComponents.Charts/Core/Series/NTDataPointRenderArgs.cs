using SkiaSharp;

namespace NTComponents.Charts.Core.Series;

/// <summary>
///     Arguments for the OnDataPointRender callback.
/// </summary>
/// <typeparam name="TData">The type of the data.</typeparam>
public class NTDataPointRenderArgs<TData>
{
    /// <summary>
    ///     Gets the data item being rendered.
    /// </summary>
    public required TData Data { get; init; }

    /// <summary>
    ///     Gets the index of the data item.
    /// </summary>
    public required int Index { get; init; }

    /// <summary>
    ///     Gets or sets the color of the data point.
    /// </summary>
    public SKColor? Color { get; set; }

    /// <summary>
    ///     Gets or sets the stroke color of the data point.
    /// </summary>
    public SKColor? StrokeColor { get; set; }

    /// <summary>
    ///     Gets or sets the stroke width of the data point or line segment.
    /// </summary>
    public float? StrokeWidth { get; set; }

    /// <summary>
    ///     Gets or sets the size of the data point marker.
    /// </summary>
    public float? PointSize { get; set; }

    /// <summary>
    ///     Gets or sets the shape of the data point marker.
    /// </summary>
    public PointShape? PointShape { get; set; }

    /// <summary>
    ///     Gets or sets the line style for the segment ending at this point.
    /// </summary>
    public LineStyle? LineStyle { get; set; }

    /// <summary>
    ///     Gets or sets the font size for the data label.
    /// </summary>
    public float? DataLabelSize { get; set; }

    /// <summary>
    ///     Gets or sets the color for the data label.
    /// </summary>
    public SKColor? DataLabelColor { get; set; }

    /// <summary>
    ///     Gets a function that converts a <see cref="TnTColor"/> to an <see cref="SKColor"/> based on the current chart theme.
    /// </summary>
    public required Func<TnTColor, SKColor> GetThemeColor { get; init; }
}
