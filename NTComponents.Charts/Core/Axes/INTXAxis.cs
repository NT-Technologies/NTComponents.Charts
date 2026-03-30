namespace NTComponents.Charts.Core.Axes;

/// <summary>
///     Specifies the granularity used when grouping date/time values on a chart axis.
/// </summary>
public enum NTDateGroupingLevel {
    /// <summary>No date grouping is applied; individual data points are rendered as-is.</summary>
    None,
    /// <summary>Data points are grouped by year.</summary>
    Year,
    /// <summary>Data points are grouped by month.</summary>
    Month,
    /// <summary>Data points are grouped by day.</summary>
    Day
}

/// <summary>
///     Represents the X axis of a chart that displays data of type <typeparamref name="TData"/>.
/// </summary>
/// <typeparam name="TData">The type of data items used in the chart.</typeparam>
public interface INTXAxis<TData> : INTAxis<TData> where TData : class {
    /// <summary>Gets the default X axis configuration for the chart.</summary>
    static abstract INTXAxis<TData> Default { get; }

    /// <summary>Gets a value indicating whether this axis treats its values as discrete categories rather than continuous numbers or dates.</summary>
    bool IsCategorical { get; }

    /// <summary>Formats a raw axis value as a display string.</summary>
    /// <param name="value">The raw value to format.</param>
    /// <param name="forTooltip">When <see langword="true"/>, applies tooltip-specific formatting.</param>
    /// <returns>The formatted string representation of <paramref name="value"/>.</returns>
    string FormatValue(object? value, bool forTooltip = false);

    /// <summary>Determines the appropriate date grouping level for the visible range and available plot space.</summary>
    /// <param name="min">The minimum axis value (as a numeric tick).</param>
    /// <param name="max">The maximum axis value (as a numeric tick).</param>
    /// <param name="plotWidth">The width of the plot area in pixels.</param>
    /// <param name="density">The display density (DPI scale factor).</param>
    /// <returns>The <see cref="NTDateGroupingLevel"/> that best fits the visible range.</returns>
    NTDateGroupingLevel ResolveDateGroupingLevel(double min, double max, float plotWidth, float density);

    /// <summary>Gets a value indicating whether automatic date grouping is enabled for this axis.</summary>
    bool EnableAutoDateGrouping { get; }

    /// <summary>Gets the minimum number of data points required before date grouping is activated.</summary>
    int DateGroupingThreshold { get; }
}
