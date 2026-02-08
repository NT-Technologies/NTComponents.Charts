using SkiaSharp;

namespace NTComponents.Charts.Core;

/// <summary>
///     Provides context for rendering a chart component.
/// </summary>
public class NTRenderContext {

    /// <summary>
    ///     Gets the canvas to render on.
    /// </summary>
    public required SKCanvas Canvas { get; init; }

    /// <summary>
    ///    Gets the default font.
    /// </summary>
    public required SKFont DefaultFont { get; init; }

    /// <summary>
    ///     Gets the device pixel ratio.
    /// </summary>
    public required float Density { get; init; }

    /// <summary>
    ///     Gets the image info.
    /// </summary>
    public required SKImageInfo Info { get; init; }

    /// <summary>
    ///     Gets or sets the current plot area. This is updated during the measurement pass.
    /// </summary>
    public required SKRect PlotArea { get; set; }

    /// <summary>
    ///    Gets the regular font.
    /// </summary>
    public required SKFont RegularFont { get; init; }

    /// <summary>
    ///    Gets the default text color.
    /// </summary>
    public required SKColor TextColor { get; init; }

    /// <summary>
    ///     Gets the total area available for the chart.
    /// </summary>
    public required SKRect TotalArea { get; init; }
}
