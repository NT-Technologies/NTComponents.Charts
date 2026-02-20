namespace NTComponents.Charts.Core.Axes;

public enum NTDateGroupingLevel {
    None,
    Year,
    Month,
    Day
}

public interface INTXAxis<TData> : INTAxis<TData> where TData : class {
    static abstract INTXAxis<TData> Default { get; }

    bool IsCategorical { get; }

    string FormatValue(object? value, bool forTooltip = false);

    NTDateGroupingLevel ResolveDateGroupingLevel(double min, double max, float plotWidth, float density);

    bool EnableAutoDateGrouping { get; }

    int DateGroupingThreshold { get; }
}
