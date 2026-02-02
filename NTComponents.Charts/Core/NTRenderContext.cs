using SkiaSharp;

namespace NTComponents.Charts.Core;

/// <summary>
///     Provides context for rendering a chart component.
/// </summary>
public class NTRenderContext {
    /// <summary>
    /// Initializes a new instance of the NTRenderContext class with the specified rendering surface, image information,
    /// drawing area, density, fonts, and text color.
    /// </summary>
    /// <remarks>The initial plot area is set to the total area provided during initialization.</remarks>
    /// <param name="canvas">The SKCanvas on which graphics will be rendered.</param>
    /// <param name="info">The SKImageInfo that specifies the format and dimensions of the rendering surface.</param>
    /// <param name="totalArea">The SKRect that defines the total area available for rendering operations.</param>
    /// <param name="density">The density factor, typically in dots per inch (DPI), used to scale rendered output appropriately.</param>
    /// <param name="defaultFont">The SKFont to use as the default font for text rendering.</param>
    /// <param name="regularFont">The SKFont to use for rendering regular text.</param>
    /// <param name="textColor">The SKColor that specifies the color to use for rendered text.</param>
    public NTRenderContext(SKCanvas canvas, SKImageInfo info, SKRect totalArea, float density, SKFont defaultFont, SKFont regularFont, SKColor textColor) {
        Canvas = canvas;
        Info = info;
        TotalArea = totalArea;
        Density = density;
        DefaultFont = defaultFont;
        RegularFont = regularFont;
        TextColor = textColor;
        PlotArea = totalArea; // Initial plot area is the total area
    }

    /// <summary>
    ///     Gets the canvas to render on.
    /// </summary>
    public SKCanvas Canvas { get; }

    /// <summary>
    ///    Gets the default font.
    /// </summary>
    public SKFont DefaultFont { get; }

    /// <summary>
    ///     Gets the device pixel ratio.
    /// </summary>
    public float Density { get; }

    /// <summary>
    ///     Gets the image info.
    /// </summary>
    public SKImageInfo Info { get; }

    /// <summary>
    ///     Gets or sets the current plot area. This is updated during the measurement pass.
    /// </summary>
    public SKRect PlotArea { get; set; }

    /// <summary>
    ///    Gets the regular font.
    /// </summary>
    public SKFont RegularFont { get; }

    /// <summary>
    ///    Gets the default text color.
    /// </summary>
    public SKColor TextColor { get; }

    /// <summary>
    ///     Gets the total area available for the chart.
    /// </summary>
    public SKRect TotalArea { get; }
}
