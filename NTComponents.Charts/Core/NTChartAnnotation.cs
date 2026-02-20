using NTComponents.Core;
using SkiaSharp;

namespace NTComponents.Charts.Core;

public enum NTChartAnnotationType {
    XLine,
    XRange,
    YLine,
    YRange,
    Point,
    Text,
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

    public float StrokeWidth { get; set; } = 1.5f;
    public float DashLength { get; set; }
    public float MarkerSize { get; set; } = 5f;
    public float FontSize { get; set; } = 11f;
    public float LabelOffsetX { get; set; }
    public float LabelOffsetY { get; set; }
    public float Opacity { get; set; } = 1f;
    public bool ClipToPlotArea { get; set; } = true;

    /// <summary>
    ///     Optional custom renderer. If <see cref="Type"/> is <see cref="NTChartAnnotationType.Custom"/>, this is used exclusively.
    ///     For other types, this runs after the built-in rendering.
    /// </summary>
    public Action<NTChartAnnotationRenderContext>? CustomRenderer { get; set; }
}

public sealed class NTChartAnnotationRenderContext {
    public required SKCanvas Canvas { get; init; }
    public required SKRect PlotArea { get; init; }
    public required float Density { get; init; }
    public required NTChartAnnotation Annotation { get; init; }
    public required Func<object?, float?> ScaleX { get; init; }
    public required Func<object?, bool, float?> ScaleY { get; init; }
    public required Func<TnTColor, SKColor> ResolveThemeColor { get; init; }
}
