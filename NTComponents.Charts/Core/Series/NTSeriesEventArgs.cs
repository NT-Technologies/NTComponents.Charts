using Microsoft.AspNetCore.Components.Web;
using NTComponents.Charts.Core.Axes;
using SkiaSharp;

namespace NTComponents.Charts.Core.Series;

public sealed class NTSeriesHoverEnterEventArgs<TData> where TData : class {
    public required NTBaseSeries<TData> Series { get; init; }
    public int? PointIndex { get; init; }
    public TData? DataPoint { get; init; }
    public SKPoint? PointerPosition { get; init; }
}

public sealed class NTSeriesHoverLeaveEventArgs<TData> where TData : class {
    public required NTBaseSeries<TData> Series { get; init; }
    public int? PointIndex { get; init; }
    public SKPoint? PointerPosition { get; init; }
}

public sealed class NTSeriesClickEventArgs<TData> where TData : class {
    public required NTBaseSeries<TData> Series { get; init; }
    public int? PointIndex { get; init; }
    public TData? DataPoint { get; init; }
    public SKPoint? PointerPosition { get; init; }
    public required MouseEventArgs MouseEvent { get; init; }
}

/// <summary>
///     Event data for grouped date point clicks (for example, year/month buckets on aggregated date axes).
/// </summary>
public sealed class NTSeriesDateGroupClickEventArgs<TData> where TData : class {
    public required NTBaseSeries<TData> Series { get; init; }
    public int? PointIndex { get; init; }
    public TData? DataPoint { get; init; }
    /// <summary>
    ///     Gets the source data point date that was clicked.
    /// </summary>
    public required DateTime ClickedDate { get; init; }
    /// <summary>
    ///     Gets the normalized date for the clicked group (start of year or start of month).
    /// </summary>
    public required DateTime GroupDate { get; init; }
    public required NTDateGroupingLevel GroupingLevel { get; init; }
    public required DateTime RangeStart { get; init; }
    public required DateTime RangeEnd { get; init; }
    public bool ZoomApplied { get; init; }
    public SKPoint? PointerPosition { get; init; }
    public required MouseEventArgs MouseEvent { get; init; }
}

public sealed class NTSeriesPanStartEventArgs<TData> where TData : class {
    public required NTBaseSeries<TData> Series { get; init; }
    public SKPoint? PointerPosition { get; init; }
    public required MouseEventArgs MouseEvent { get; init; }
    public (double Min, double Max)? ViewXRange { get; init; }
    public (decimal Min, decimal Max)? ViewYRange { get; init; }
}

public sealed class NTSeriesPanEventArgs<TData> where TData : class {
    public required NTBaseSeries<TData> Series { get; init; }
    public SKPoint? PointerPosition { get; init; }
    public required MouseEventArgs MouseEvent { get; init; }
    public (double Min, double Max)? ViewXRange { get; init; }
    public (decimal Min, decimal Max)? ViewYRange { get; init; }
}

public sealed class NTSeriesPanEndEventArgs<TData> where TData : class {
    public required NTBaseSeries<TData> Series { get; init; }
    public SKPoint? PointerPosition { get; init; }
    public required MouseEventArgs MouseEvent { get; init; }
    public (double Min, double Max)? ViewXRange { get; init; }
    public (decimal Min, decimal Max)? ViewYRange { get; init; }
}

public sealed class NTSeriesZoomEventArgs<TData> where TData : class {
    public required NTBaseSeries<TData> Series { get; init; }
    public SKPoint? PointerPosition { get; init; }
    public required WheelEventArgs WheelEvent { get; init; }
    public (double Min, double Max)? ViewXRange { get; init; }
    public (decimal Min, decimal Max)? ViewYRange { get; init; }
}

public sealed class NTSeriesResetViewEventArgs<TData> where TData : class {
    public required NTBaseSeries<TData> Series { get; init; }
    public (double Min, double Max)? ViewXRange { get; init; }
    public (decimal Min, decimal Max)? ViewYRange { get; init; }
}
