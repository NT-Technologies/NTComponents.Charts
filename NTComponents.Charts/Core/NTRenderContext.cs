using SkiaSharp;

namespace NTComponents.Charts.Core;

/// <summary>
///     Provides context for rendering a chart component.
/// </summary>
public class NTRenderContext {

    public NTRenderContext(
        SKCanvas canvas,
        SKImageInfo info,
        SKRect totalArea,
        float density,
        SKFont defaultFont,
        SKFont regularFont,
        SKColor textColor) {
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
