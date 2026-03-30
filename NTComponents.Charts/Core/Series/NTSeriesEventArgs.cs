using Microsoft.AspNetCore.Components.Web;
using NTComponents.Charts.Core.Axes;
using SkiaSharp;

namespace NTComponents.Charts.Core.Series;

/// <summary>
///     Event data raised when the pointer enters a series data point or series area.
/// </summary>
/// <typeparam name="TData">The type of the data items bound to the series.</typeparam>
public sealed class NTSeriesHoverEnterEventArgs<TData> where TData : class {
    /// <summary>
    ///     Gets the series that raised the hover-enter event.
    /// </summary>
    public required NTBaseSeries<TData> Series { get; init; }
    /// <summary>
    ///     Gets the zero-based index of the hovered data point, or <see langword="null" /> if no specific point is targeted.
    /// </summary>
    public int? PointIndex { get; init; }
    /// <summary>
    ///     Gets the data item at the hovered point, or <see langword="null" /> if no specific point is targeted.
    /// </summary>
    public TData? DataPoint { get; init; }
    /// <summary>
    ///     Gets the pointer position in canvas coordinates at the time of the event, or <see langword="null" /> if unavailable.
    /// </summary>
    public SKPoint? PointerPosition { get; init; }
}

/// <summary>
///     Event data raised when the pointer leaves a series data point or series area.
/// </summary>
/// <typeparam name="TData">The type of the data items bound to the series.</typeparam>
public sealed class NTSeriesHoverLeaveEventArgs<TData> where TData : class {
    /// <summary>
    ///     Gets the series that raised the hover-leave event.
    /// </summary>
    public required NTBaseSeries<TData> Series { get; init; }
    /// <summary>
    ///     Gets the zero-based index of the data point that was last hovered, or <see langword="null" /> if no specific point was targeted.
    /// </summary>
    public int? PointIndex { get; init; }
    /// <summary>
    ///     Gets the pointer position in canvas coordinates at the time of the event, or <see langword="null" /> if unavailable.
    /// </summary>
    public SKPoint? PointerPosition { get; init; }
}

/// <summary>
///     Event data raised when a series visibility is toggled.
/// </summary>
/// <typeparam name="TData">The type of the data items bound to the series.</typeparam>
public sealed class NTSeriesVisibilityChangedEventArgs<TData> where TData : class {
    /// <summary>
    ///     Gets the series whose visibility changed.
    /// </summary>
    public required NTBaseSeries<TData> Series { get; init; }
    /// <summary>
    ///     Gets a value indicating whether the series is now visible.
    /// </summary>
    public required bool Visible { get; init; }
}

/// <summary>
///     Event data raised when a series or one of its data points is clicked.
/// </summary>
/// <typeparam name="TData">The type of the data items bound to the series.</typeparam>
public sealed class NTSeriesClickEventArgs<TData> where TData : class {
    /// <summary>
    ///     Gets the series that raised the click event.
    /// </summary>
    public required NTBaseSeries<TData> Series { get; init; }
    /// <summary>
    ///     Gets the zero-based index of the clicked data point, or <see langword="null" /> if no specific point was targeted.
    /// </summary>
    public int? PointIndex { get; init; }
    /// <summary>
    ///     Gets the data item at the clicked point, or <see langword="null" /> if no specific point was targeted.
    /// </summary>
    public TData? DataPoint { get; init; }
    /// <summary>
    ///     Gets the pointer position in canvas coordinates at the time of the click, or <see langword="null" /> if unavailable.
    /// </summary>
    public SKPoint? PointerPosition { get; init; }
    /// <summary>
    ///     Gets the underlying Blazor mouse event arguments for the click.
    /// </summary>
    public required MouseEventArgs MouseEvent { get; init; }
}

/// <summary>
///     Event data for grouped date point clicks (for example, year/month buckets on aggregated date axes).
/// </summary>
public sealed class NTSeriesDateGroupClickEventArgs<TData> where TData : class {
    /// <summary>
    ///     Gets the series that raised the click event.
    /// </summary>
    public required NTBaseSeries<TData> Series { get; init; }
    /// <summary>
    ///     Gets the zero-based index of the clicked data point, or <see langword="null" /> if no specific point was targeted.
    /// </summary>
    public int? PointIndex { get; init; }
    /// <summary>
    ///     Gets the data item at the clicked point, or <see langword="null" /> if no specific point was targeted.
    /// </summary>
    public TData? DataPoint { get; init; }
    /// <summary>
    ///     Gets the source data point date that was clicked.
    /// </summary>
    public required DateTime ClickedDate { get; init; }
    /// <summary>
    ///     Gets the normalized date for the clicked group (start of year or start of month).
    /// </summary>
    public required DateTime GroupDate { get; init; }
    /// <summary>
    ///     Gets the grouping level (year or month) that was active when the click occurred.
    /// </summary>
    public required NTDateGroupingLevel GroupingLevel { get; init; }
    /// <summary>
    ///     Gets the inclusive start of the visible date range at the time of the click.
    /// </summary>
    public required DateTime RangeStart { get; init; }
    /// <summary>
    ///     Gets the inclusive end of the visible date range at the time of the click.
    /// </summary>
    public required DateTime RangeEnd { get; init; }
    /// <summary>
    ///     Gets a value indicating whether a zoom operation was applied as a result of this click.
    /// </summary>
    public bool ZoomApplied { get; init; }
    /// <summary>
    ///     Gets the pointer position in canvas coordinates at the time of the click, or <see langword="null" /> if unavailable.
    /// </summary>
    public SKPoint? PointerPosition { get; init; }
    /// <summary>
    ///     Gets the underlying Blazor mouse event arguments for the click.
    /// </summary>
    public required MouseEventArgs MouseEvent { get; init; }
}

/// <summary>
///     Event data raised when a pan gesture begins on a series.
/// </summary>
/// <typeparam name="TData">The type of the data items bound to the series.</typeparam>
public sealed class NTSeriesPanStartEventArgs<TData> where TData : class {
    /// <summary>
    ///     Gets the series on which the pan gesture started.
    /// </summary>
    public required NTBaseSeries<TData> Series { get; init; }
    /// <summary>
    ///     Gets the pointer position in canvas coordinates at the start of the pan, or <see langword="null" /> if unavailable.
    /// </summary>
    public SKPoint? PointerPosition { get; init; }
    /// <summary>
    ///     Gets the underlying Blazor mouse event arguments for the pan-start interaction.
    /// </summary>
    public required MouseEventArgs MouseEvent { get; init; }
    /// <summary>
    ///     Gets the current visible X-axis range (min/max) at the start of the pan, or <see langword="null" /> if not applicable.
    /// </summary>
    public (double Min, double Max)? ViewXRange { get; init; }
    /// <summary>
    ///     Gets the current visible Y-axis range (min/max) at the start of the pan, or <see langword="null" /> if not applicable.
    /// </summary>
    public (decimal Min, decimal Max)? ViewYRange { get; init; }
}

/// <summary>
///     Event data raised continuously while a pan gesture is in progress on a series.
/// </summary>
/// <typeparam name="TData">The type of the data items bound to the series.</typeparam>
public sealed class NTSeriesPanEventArgs<TData> where TData : class {
    /// <summary>
    ///     Gets the series on which the pan gesture is in progress.
    /// </summary>
    public required NTBaseSeries<TData> Series { get; init; }
    /// <summary>
    ///     Gets the current pointer position in canvas coordinates, or <see langword="null" /> if unavailable.
    /// </summary>
    public SKPoint? PointerPosition { get; init; }
    /// <summary>
    ///     Gets the underlying Blazor mouse event arguments for the ongoing pan interaction.
    /// </summary>
    public required MouseEventArgs MouseEvent { get; init; }
    /// <summary>
    ///     Gets the updated visible X-axis range (min/max) after the latest pan delta, or <see langword="null" /> if not applicable.
    /// </summary>
    public (double Min, double Max)? ViewXRange { get; init; }
    /// <summary>
    ///     Gets the updated visible Y-axis range (min/max) after the latest pan delta, or <see langword="null" /> if not applicable.
    /// </summary>
    public (decimal Min, decimal Max)? ViewYRange { get; init; }
}

/// <summary>
///     Event data raised when a pan gesture ends on a series.
/// </summary>
/// <typeparam name="TData">The type of the data items bound to the series.</typeparam>
public sealed class NTSeriesPanEndEventArgs<TData> where TData : class {
    /// <summary>
    ///     Gets the series on which the pan gesture ended.
    /// </summary>
    public required NTBaseSeries<TData> Series { get; init; }
    /// <summary>
    ///     Gets the pointer position in canvas coordinates at the end of the pan, or <see langword="null" /> if unavailable.
    /// </summary>
    public SKPoint? PointerPosition { get; init; }
    /// <summary>
    ///     Gets the underlying Blazor mouse event arguments for the pan-end interaction.
    /// </summary>
    public required MouseEventArgs MouseEvent { get; init; }
    /// <summary>
    ///     Gets the final visible X-axis range (min/max) after the pan completes, or <see langword="null" /> if not applicable.
    /// </summary>
    public (double Min, double Max)? ViewXRange { get; init; }
    /// <summary>
    ///     Gets the final visible Y-axis range (min/max) after the pan completes, or <see langword="null" /> if not applicable.
    /// </summary>
    public (decimal Min, decimal Max)? ViewYRange { get; init; }
}

/// <summary>
///     Event data raised when a zoom operation is performed on a series (typically via the mouse wheel).
/// </summary>
/// <typeparam name="TData">The type of the data items bound to the series.</typeparam>
public sealed class NTSeriesZoomEventArgs<TData> where TData : class {
    /// <summary>
    ///     Gets the series on which the zoom was performed.
    /// </summary>
    public required NTBaseSeries<TData> Series { get; init; }
    /// <summary>
    ///     Gets the pointer position in canvas coordinates at the time of the zoom, or <see langword="null" /> if unavailable.
    /// </summary>
    public SKPoint? PointerPosition { get; init; }
    /// <summary>
    ///     Gets the underlying Blazor wheel event arguments for the zoom interaction.
    /// </summary>
    public required WheelEventArgs WheelEvent { get; init; }
    /// <summary>
    ///     Gets the updated visible X-axis range (min/max) after the zoom is applied, or <see langword="null" /> if not applicable.
    /// </summary>
    public (double Min, double Max)? ViewXRange { get; init; }
    /// <summary>
    ///     Gets the updated visible Y-axis range (min/max) after the zoom is applied, or <see langword="null" /> if not applicable.
    /// </summary>
    public (decimal Min, decimal Max)? ViewYRange { get; init; }
}

/// <summary>
///     Event data raised when the view is reset to its default zoom and pan state on a series.
/// </summary>
/// <typeparam name="TData">The type of the data items bound to the series.</typeparam>
public sealed class NTSeriesResetViewEventArgs<TData> where TData : class {
    /// <summary>
    ///     Gets the series whose view was reset.
    /// </summary>
    public required NTBaseSeries<TData> Series { get; init; }
    /// <summary>
    ///     Gets the X-axis range (min/max) after the reset, or <see langword="null" /> if not applicable.
    /// </summary>
    public (double Min, double Max)? ViewXRange { get; init; }
    /// <summary>
    ///     Gets the Y-axis range (min/max) after the reset, or <see langword="null" /> if not applicable.
    /// </summary>
    public (decimal Min, decimal Max)? ViewYRange { get; init; }
}
