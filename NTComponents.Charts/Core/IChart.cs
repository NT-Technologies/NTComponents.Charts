using SkiaSharp;
using System.Collections.Generic;
using NTComponents.Charts.Core.Axes;
using NTComponents.Core;

namespace NTComponents.Charts.Core;

/// <summary>
///    Interface for a chart series.
/// </summary>
public interface ISeries : IRenderable {
    bool Visible { get; }
    TnTColor? TooltipBackgroundColor { get; }
    TnTColor? TooltipTextColor { get; }
    TooltipInfo GetTooltipInfo(object data);
}

/// <summary>
///     Common interface for all charts.
/// </summary>
public interface IChart {
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

    ChartMargin Margin { get; }

    void RegisterRenderable(IRenderable renderable);
    void UnregisterRenderable(IRenderable renderable);

    /// <summary>
    ///     Resolves a theme color.
    /// </summary>
    SKColor GetThemeColor(TnTColor color);

    TnTColor TextColor { get; }

    NTTitleOptions? TitleOptions { get; }


    public NTXAxisOptions XAxis { get; }

    void RegisterAxis(NTXAxisOptions axis);

    void UnregisterAxis(NTXAxisOptions axis);

    NTYAxisOptions YAxis { get; }
    NTYAxisOptions? SecondaryYAxis { get; }

    void RegisterAxis(NTYAxisOptions axis);

    void UnregisterAxis(NTYAxisOptions axis);

    void SetTooltip(NTTooltip tooltip);

    object? HoveredDataPoint { get; }
    int? HoveredPointIndex { get; }
    ISeries? HoveredSeries { get; }
    SKPoint? LastMousePosition { get; }

    TnTColor TooltipBackgroundColor { get; }
    TnTColor TooltipTextColor { get; }

    (double Min, double Max) GetXRange(NTAxisOptions? axis, bool padded);
    (decimal Min, decimal Max) GetYRange(NTAxisOptions? axis, bool padded);

    float ScaleX(double x, SKRect plotArea, NTAxisOptions? axis = null);
    float ScaleY(decimal y, NTAxisOptions? axis, SKRect plotArea);

    double ScaleXInverse(float coord, SKRect plotArea, NTAxisOptions? axis = null);
    decimal ScaleYInverse(float coord, NTAxisOptions? axis, SKRect plotArea);

    bool IsCategoricalX { get; }
    bool IsCategoricalY { get; }

    List<object> GetAllXValues();
    List<object> GetAllYValues();

    double GetScaledXValue(object? originalX);
    decimal GetScaledYValue(object? originalY);

    bool IsXAxisDateTime { get; }

    bool HasViewRange(NTAxisOptions axis);
}
