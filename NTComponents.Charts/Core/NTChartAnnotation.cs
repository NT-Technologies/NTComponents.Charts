using NTComponents.Core;
using SkiaSharp;

namespace NTComponents.Charts.Core;

/// <summary>
///     Specifies the visual style of a chart annotation.
/// </summary>
public enum NTChartAnnotationType {
    /// <summary>A vertical line at a fixed X value.</summary>
    XLine,

    /// <summary>A shaded band between two X values.</summary>
    XRange,

    /// <summary>A horizontal line at a fixed Y value.</summary>
    YLine,

    /// <summary>A shaded band between two Y values.</summary>
    YRange,

    /// <summary>A marker point at an (X, Y) coordinate.</summary>
    Point,

    /// <summary>A text label placed at an (X, Y) coordinate.</summary>
    Text,

    /// <summary>A fully custom annotation rendered by <see cref="NTChartAnnotation.CustomRenderer"/>.</summary>
    Custom
}

/// <summary>
///     Describes a chart annotation rendered over the plot area.
/// </summary>
public sealed class NTChartAnnotation {
    /// <summary>
    ///     Annotation style/type.
    /// </summary>
    public NTChartAnnotationType Type { get; set; } = NTChartAnnotationType.YLine;

    /// <summary>
    ///     Primary X-axis value (number, date, or category).
    /// </summary>
    public object? X { get; set; }

    /// <summary>
    ///     Secondary X-axis value for range annotations.
    /// </summary>
    public object? X2 { get; set; }

    /// <summary>
    ///     Primary Y-axis value.
    /// </summary>
    public object? Y { get; set; }

    /// <summary>
    ///     Secondary Y-axis value for range annotations.
    /// </summary>
    public object? Y2 { get; set; }

    /// <summary>
    ///     Optional label text rendered near the annotation.
    /// </summary>
    public string? Label { get; set; }

    /// <summary>
    ///     Uses the secondary Y-axis for Y-based annotations when available.
    /// </summary>
    public bool UseSecondaryYAxis { get; set; }

    /// <summary>
    ///     Main stroke color.
    /// </summary>
    public TnTColor StrokeColor { get; set; } = TnTColor.Primary;

    /// <summary>
    ///     Optional fill color. Defaults to a translucent version of <see cref="StrokeColor"/>.
    /// </summary>
    public TnTColor? FillColor { get; set; }

    /// <summary>
    ///     Optional text color. Defaults to chart text color.
    /// </summary>
    public TnTColor? TextColor { get; set; }

    /// <summary>Gets or sets the stroke line width in pixels. Defaults to <c>1.5</c>.</summary>
    public float StrokeWidth { get; set; } = 1.5f;

    /// <summary>Gets or sets the dash segment length. A value of <c>0</c> produces a solid line.</summary>
    public float DashLength { get; set; }

    /// <summary>Gets or sets the radius of the point marker in pixels. Defaults to <c>5</c>.</summary>
    public float MarkerSize { get; set; } = 5f;

    /// <summary>Gets or sets the font size used for text annotations in points. Defaults to <c>11</c>.</summary>
    public float FontSize { get; set; } = 11f;

    /// <summary>Gets or sets the horizontal offset applied to the annotation label in pixels.</summary>
    public float LabelOffsetX { get; set; }

    /// <summary>Gets or sets the vertical offset applied to the annotation label in pixels.</summary>
    public float LabelOffsetY { get; set; }

    /// <summary>Gets or sets the opacity of the annotation. Ranges from <c>0</c> (transparent) to <c>1</c> (opaque). Defaults to <c>1</c>.</summary>
    public float Opacity { get; set; } = 1f;

    /// <summary>Gets or sets a value indicating whether the annotation is clipped to the plot area boundaries. Defaults to <see langword="true"/>.</summary>
    public bool ClipToPlotArea { get; set; } = true;

    /// <summary>
    ///     Optional custom renderer. If <see cref="Type"/> is <see cref="NTChartAnnotationType.Custom"/>, this is used exclusively.
    ///     For other types, this runs after the built-in rendering.
    /// </summary>
    public Action<NTChartAnnotationRenderContext>? CustomRenderer { get; set; }
}

/// <summary>
///     Provides the rendering context passed to a custom annotation renderer.
/// </summary>
public sealed class NTChartAnnotationRenderContext {
    /// <summary>Gets the SkiaSharp canvas to draw on.</summary>
    public required SKCanvas Canvas { get; init; }

    /// <summary>Gets the bounding rectangle of the chart plot area in canvas coordinates.</summary>
    public required SKRect PlotArea { get; init; }

    /// <summary>Gets the display density (DPI scaling factor) of the canvas.</summary>
    public required float Density { get; init; }

    /// <summary>Gets the annotation being rendered.</summary>
    public required NTChartAnnotation Annotation { get; init; }

    /// <summary>Gets a function that converts a data value to its X canvas coordinate, or <see langword="null"/> if the value is out of range.</summary>
    public required Func<object?, float?> ScaleX { get; init; }

    /// <summary>Gets a function that converts a data value to its Y canvas coordinate. The <see langword="bool"/> parameter selects the secondary Y axis when <see langword="true"/>.</summary>
    public required Func<object?, bool, float?> ScaleY { get; init; }

    /// <summary>Gets a function that resolves a <see cref="TnTColor"/> token to its concrete <see cref="SKColor"/> using the current chart theme.</summary>
    public required Func<TnTColor, SKColor> ResolveThemeColor { get; init; }
}
