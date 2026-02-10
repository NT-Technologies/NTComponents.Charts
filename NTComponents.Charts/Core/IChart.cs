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
    bool Visible { get; }
    TnTColor? TooltipBackgroundColor { get; }
    TnTColor? TooltipTextColor { get; }
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

    ChartMargin Margin { get; }

    void RegisterRenderable(IRenderable renderable);
    void UnregisterRenderable(IRenderable renderable);

    /// <summary>
    ///     Resolves a theme color.
    /// </summary>
    SKColor GetThemeColor(TnTColor color);

    TnTColor TextColor { get; }

    NTTitleOptions? TitleOptions { get; }


    public INTXAxis<TData> XAxis { get; }

    void RegisterAxis(INTXAxis<TData> axis);

    void UnregisterAxis(INTXAxis<TData> axis);

    INTYAxis<TData> YAxis { get; }
    INTYAxis<TData>? SecondaryYAxis { get; }

    void RegisterAxis(INTYAxis<TData> axis);

    void UnregisterAxis(INTYAxis<TData> axis);

    TData? HoveredDataPoint { get; }
    int? HoveredPointIndex { get; }
    NTBaseSeries<TData>? HoveredSeries { get; }
    SKPoint? LastMousePosition { get; }

    TnTColor TooltipBackgroundColor { get; }
    TnTColor TooltipTextColor { get; }

    (double Min, double Max) GetXRange(NTAxisOptions<TData>? axis, bool padded);
    (decimal Min, decimal Max) GetYRange(NTAxisOptions<TData>? axis, bool padded);

    float ScaleX(double x, SKRect plotArea);
    float ScaleY(decimal y,  SKRect plotArea);

    double ScaleXInverse(float coord, SKRect plotArea);
    decimal ScaleYInverse(float coord, SKRect plotArea);

    List<object> GetAllXValues();
    List<object> GetAllYValues();

    double GetScaledXValue(object? originalX);
    decimal GetScaledYValue(object? originalY);

    bool IsXAxisDateTime { get; }

    bool HasViewRange(NTAxisOptions<TData> axis);
}
