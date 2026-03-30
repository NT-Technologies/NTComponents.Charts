using NTComponents.Charts.Core.Axes;
using NTComponents.Charts.Core.Series;
using NTComponents.Core;
using SkiaSharp;
using System.Collections.Generic;

namespace NTComponents.Charts.Core;

/// <summary>
///    Interface for a chart series.
/// </summary>
public interface ISeries : IRenderable {
    /// <summary>
    ///     Gets a value indicating whether this series is visible on the chart.
    /// </summary>
    bool Visible { get; }
    /// <summary>
    ///     Gets the optional background color for tooltips associated with this series.
    ///     When <see langword="null"/>, the chart's default tooltip background color is used.
    /// </summary>
    TnTColor? TooltipBackgroundColor { get; }
    /// <summary>
    ///     Gets the optional text color for tooltips associated with this series.
    ///     When <see langword="null"/>, the chart's default tooltip text color is used.
    /// </summary>
    TnTColor? TooltipTextColor { get; }
    /// <summary>
    ///     Gets the tooltip information to display for the specified data point.
    /// </summary>
    /// <param name="data">The data point for which to build tooltip content.</param>
    /// <returns>A <see cref="TooltipInfo"/> containing the header and value lines to render.</returns>
    TooltipInfo GetTooltipInfo(object data);
}

/// <summary>
///     Common interface for all charts.
/// </summary>
public interface IChart<TData> where TData : class {
    /// <summary>
    ///     Gets the density of the screen.
    /// </summary>
    float Density { get; }

    /// <summary>
    ///     Gets the default font.
    /// </summary>
    SKFont DefaultFont { get; }

    /// <summary>
    ///     Gets the regular font.
    /// </summary>
    SKFont RegularFont { get; }

    /// <summary>
    ///     Gets the margin applied around the chart plot area.
    /// </summary>
    ChartMargin Margin { get; }

    /// <summary>
    ///     Registers a renderable component with the chart so it participates in the render pipeline.
    /// </summary>
    /// <param name="renderable">The renderable component to register.</param>
    void RegisterRenderable(IRenderable renderable);
    /// <summary>
    ///     Unregisters a renderable component from the chart render pipeline.
    /// </summary>
    /// <param name="renderable">The renderable component to unregister.</param>
    void UnregisterRenderable(IRenderable renderable);

    /// <summary>
    ///     Resolves a theme color.
    /// </summary>
    SKColor GetThemeColor(TnTColor color);

    /// <summary>
    ///     Gets the default text color used throughout the chart.
    /// </summary>
    TnTColor TextColor { get; }

    /// <summary>
    ///     Gets the optional title configuration for the chart. When <see langword="null"/>, no title is rendered.
    /// </summary>
    NTTitleOptions? TitleOptions { get; }


    /// <summary>
    ///     Gets the primary X axis of the chart.
    /// </summary>
    public INTXAxis<TData> XAxis { get; }

    /// <summary>
    ///     Registers the X axis with the chart.
    /// </summary>
    /// <param name="axis">The X axis to register.</param>
    void RegisterAxis(INTXAxis<TData> axis);

    /// <summary>
    ///     Unregisters the X axis from the chart.
    /// </summary>
    /// <param name="axis">The X axis to unregister.</param>
    void UnregisterAxis(INTXAxis<TData> axis);

    /// <summary>
    ///     Gets the primary Y axis of the chart.
    /// </summary>
    INTYAxis<TData> YAxis { get; }
    /// <summary>
    ///     Gets the optional secondary Y axis. When <see langword="null"/>, only the primary Y axis is used.
    /// </summary>
    INTYAxis<TData>? SecondaryYAxis { get; }

    /// <summary>
    ///     Registers a Y axis with the chart.
    /// </summary>
    /// <param name="axis">The Y axis to register.</param>
    void RegisterAxis(INTYAxis<TData> axis);

    /// <summary>
    ///     Unregisters a Y axis from the chart.
    /// </summary>
    /// <param name="axis">The Y axis to unregister.</param>
    void UnregisterAxis(INTYAxis<TData> axis);

    /// <summary>
    ///     Gets the data point currently under the mouse cursor, or <see langword="null"/> if none is hovered.
    /// </summary>
    TData? HoveredDataPoint { get; }
    /// <summary>
    ///     Gets the index of the currently hovered data point within its series, or <see langword="null"/> if none is hovered.
    /// </summary>
    int? HoveredPointIndex { get; }
    /// <summary>
    ///     Gets the series that contains the currently hovered data point, or <see langword="null"/> if none is hovered.
    /// </summary>
    NTBaseSeries<TData>? HoveredSeries { get; }
    /// <summary>
    ///     Gets the last recorded mouse position on the chart canvas, or <see langword="null"/> if the mouse has not moved over the chart.
    /// </summary>
    SKPoint? LastMousePosition { get; }

    /// <summary>
    ///     Gets the default background color applied to tooltips when no series-specific color is provided.
    /// </summary>
    TnTColor TooltipBackgroundColor { get; }
    /// <summary>
    ///     Gets the default text color applied to tooltips when no series-specific color is provided.
    /// </summary>
    TnTColor TooltipTextColor { get; }

    /// <summary>
    ///     Gets the minimum and maximum X values across all series data.
    /// </summary>
    /// <param name="axis">Optional axis options that may constrain the range.</param>
    /// <param name="padded">When <see langword="true"/>, applies padding to the range for visual breathing room.</param>
    /// <returns>A tuple containing the minimum and maximum X values.</returns>
    (double Min, double Max) GetXRange(NTAxisOptions<TData>? axis, bool padded);
    /// <summary>
    ///     Gets the minimum and maximum Y values across all series data.
    /// </summary>
    /// <param name="axis">Optional axis options that may constrain the range.</param>
    /// <param name="padded">When <see langword="true"/>, applies padding to the range for visual breathing room.</param>
    /// <returns>A tuple containing the minimum and maximum Y values.</returns>
    (decimal Min, decimal Max) GetYRange(NTAxisOptions<TData>? axis, bool padded);

    /// <summary>
    ///     Scales an X data value to its corresponding canvas pixel coordinate.
    /// </summary>
    /// <param name="x">The data value to scale.</param>
    /// <param name="plotArea">The rectangle defining the drawable plot area.</param>
    /// <returns>The canvas X coordinate in pixels.</returns>
    float ScaleX(double x, SKRect plotArea);
    /// <summary>
    ///     Scales a Y data value to its corresponding canvas pixel coordinate.
    /// </summary>
    /// <param name="y">The data value to scale.</param>
    /// <param name="plotArea">The rectangle defining the drawable plot area.</param>
    /// <returns>The canvas Y coordinate in pixels.</returns>
    float ScaleY(decimal y,  SKRect plotArea);

    /// <summary>
    ///     Converts a canvas X pixel coordinate back to the corresponding data value.
    /// </summary>
    /// <param name="coord">The canvas X coordinate in pixels.</param>
    /// <param name="plotArea">The rectangle defining the drawable plot area.</param>
    /// <returns>The data value corresponding to the given coordinate.</returns>
    double ScaleXInverse(float coord, SKRect plotArea);
    /// <summary>
    ///     Converts a canvas Y pixel coordinate back to the corresponding data value.
    /// </summary>
    /// <param name="coord">The canvas Y coordinate in pixels.</param>
    /// <param name="plotArea">The rectangle defining the drawable plot area.</param>
    /// <returns>The data value corresponding to the given coordinate.</returns>
    decimal ScaleYInverse(float coord, SKRect plotArea);

    /// <summary>
    ///     Gets all unique X values across all series, used for categorical axis rendering.
    /// </summary>
    /// <returns>A list of all distinct X values in their original object form.</returns>
    List<object> GetAllXValues();
    /// <summary>
    ///     Gets all unique Y values across all series, used for categorical axis rendering.
    /// </summary>
    /// <returns>A list of all distinct Y values in their original object form.</returns>
    List<object> GetAllYValues();

    /// <summary>
    ///     Converts an original X data value into its numeric double representation for scaling.
    /// </summary>
    /// <param name="originalX">The original X value, which may be a <see cref="System.DateTime"/>, numeric type, or category label.</param>
    /// <returns>The numeric double representation of the X value.</returns>
    double GetScaledXValue(object? originalX);
    /// <summary>
    ///     Converts an original Y data value into its numeric decimal representation for scaling.
    /// </summary>
    /// <param name="originalY">The original Y value, which may be a numeric type.</param>
    /// <returns>The numeric decimal representation of the Y value.</returns>
    decimal GetScaledYValue(object? originalY);

    /// <summary>
    ///     Gets a value indicating whether the X axis data consists of <see cref="System.DateTime"/> values.
    /// </summary>
    bool IsXAxisDateTime { get; }

    /// <summary>
    ///     Determines whether the specified axis has a configured view range (zoom window).
    /// </summary>
    /// <param name="axis">The axis options to check.</param>
    /// <returns><see langword="true"/> if a view range is configured; otherwise, <see langword="false"/>.</returns>
    bool HasViewRange(NTAxisOptions<TData> axis);
}
