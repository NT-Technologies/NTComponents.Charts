namespace NTComponents.Charts.Core.Series;

internal interface INestedSeriesParent<TData> where TData : class {
    void RegisterChildSeries(NTBaseSeries<TData> series);
    void UnregisterChildSeries(NTBaseSeries<TData> series);
    void NotifyChildSeriesChanged();
}
