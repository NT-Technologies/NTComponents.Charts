using Microsoft.AspNetCore.Components;
using SkiaSharp;

namespace NTComponents.Charts.Core.Series;

/// <summary>
///     Base class for circular series (Pie, Donut).
/// </summary>
/// <typeparam name="TData">The type of the data.</typeparam>
public abstract class NTCircularSeries<TData> : NTBaseSeries<TData> where TData : class
{
   private List<TData>? _cachedData;
   private Func<TData, decimal>? _cachedValueSelector;
   private NTChart<TData>? _themeColorChart;
   private Func<NTColor, SKColor>? _themeColorSelector;
   private bool _preserveHiddenIndices;
   private bool _sliceCacheValid;
   /// <inheritdoc />
   public override ChartCoordinateSystem CoordinateSystem => ChartCoordinateSystem.Circular;

   /// <summary>
   ///     Gets or sets the function that extracts the numeric value from a data item.
   /// </summary>
   [Parameter, EditorRequired]
   public Func<TData, decimal> ValueSelector { get; set; } = default!;

    /// <inheritdoc />
    internal override TooltipInfo GetTooltipInfo(TData data)
    {
        var value = ValueSelector(data);
        var labelValue = string.Format(DataLabelFormat, value);
        var xValue = XValue?.Invoke(data);

        return new TooltipInfo
        {
            Header = xValue?.ToString(),
            Lines =
            [
                new TooltipLine
                {
                    Label = Title ?? "Series",
                    Value = labelValue,
                    Color = Chart.GetSeriesColor(this)
                }
            ]
        };
    }

   /// <summary>
   ///     Gets or sets the format for the data labels.
   /// </summary>
   [Parameter]
   public string DataLabelFormat { get; set; } = "{0:0.#}";

   /// <summary>
   ///     Gets or sets the color of the data labels.
   /// </summary>
   [Parameter]
   public NTColor? DataLabelColor { get; set; }

   /// <summary>
   ///    Gets or sets the thickness of the data labels.
   /// </summary>
   [Parameter]
   public float DataLabelSize { get; set; } = 12.0f;

   /// <summary>
   ///     Gets or sets whether to show data labels.
   /// </summary>
   [Parameter]
   public bool ShowDataLabels { get; set; } = true;

   /// <summary>
   ///     Gets or sets the inner radius ratio (0.0 to 1.0).
   ///     Set to > 0 for a donut chart.
   /// </summary>
   [Parameter]
   public float InnerRadiusRatio { get; set; } = 0f;

   /// <summary>
   ///     Gets the computed slice geometry information for the current data set.
   /// </summary>
   protected List<PieSliceInfo> SliceInfos { get; } = new();

   private HashSet<int> _hiddenIndices = new();

   /// <inheritdoc />
   protected override void OnDataChanged()
   {
      base.OnDataChanged();
      InvalidateGeometryCache();
      if (!_preserveHiddenIndices)
      {
         _hiddenIndices.Clear();
      }
   }

   /// <inheritdoc />
   public override void Invalidate()
   {
      _preserveHiddenIndices = true;
      try
      {
         base.Invalidate();
      }
      finally
      {
         _preserveHiddenIndices = false;
      }
   }

   /// <inheritdoc />
   protected override void OnParametersSet()
   {
      base.OnParametersSet();
      if (!ReferenceEquals(_cachedValueSelector, ValueSelector))
      {
         InvalidateGeometryCache();
         _cachedValueSelector = ValueSelector;
      }
   }

   private void InvalidateGeometryCache()
   {
      _cachedData = null;
      SliceInfos.Clear();
      _sliceCacheValid = false;
      OnCircularGeometryInvalidated();
   }

   /// <summary>Clears geometry derived by a concrete circular series.</summary>
   protected virtual void OnCircularGeometryInvalidated()
   {
   }

   /// <summary>Returns the current data snapshot used by circular-series geometry.</summary>
   protected IReadOnlyList<TData> GetCachedData()
   {
      _cachedData ??= Data?.ToList() ?? [];
      return _cachedData;
   }

   /// <summary>Returns the reusable theme-color delegate for point render callbacks.</summary>
   protected Func<NTColor, SKColor> GetThemeColorSelector()
   {
      if (!ReferenceEquals(_themeColorChart, Chart))
      {
         _themeColorChart = Chart;
         _themeColorSelector = Chart.GetThemeColor;
      }

      return _themeColorSelector!;
   }

   internal override void ToggleLegendItem(int? index)
   {
      if (index.HasValue)
      {
         if (_hiddenIndices.Contains(index.Value))
         _hiddenIndices.Remove(index.Value);
         else
            _hiddenIndices.Add(index.Value);
         ResetAnimation();
         InvalidateGeometryCache();
      }
      else
      {
         base.ToggleLegendItem(index);
      }
   }

   /// <inheritdoc />
   internal override IEnumerable<LegendItemInfo<TData>> GetLegendItems()
   {
      if (Data == null) yield break;

      var dataList = GetCachedData();
      for (int i = 0; i < dataList.Count; i++)
      {
         var item = dataList[i];
         var label = XValue?.Invoke(item)?.ToString() ?? $"Item {i + 1}";
         var color = Chart.Palette[i % Chart.Palette.Count].Background;

         yield return new LegendItemInfo<TData>
         {
            Label = label,
            Color = Chart.GetThemeColor(color),
            Series = this,
            Index = i,
            Key = label,
            IsVisible = Visible && !_hiddenIndices.Contains(i)
         };
      }
   }

   /// <summary>
   ///     Computes <see cref="SliceInfos"/> for the current data set within the given render area,
   ///     respecting hidden indices and animation progress.
   /// </summary>
   /// <param name="renderArea">The bounding rectangle available for rendering the chart.</param>
   protected void CalculateSlices(SKRect renderArea)
   {
      if (_sliceCacheValid) return;
      SliceInfos.Clear();

      var dataList = GetCachedData();
      if (dataList.Count == 0) {
         _sliceCacheValid = true;
         return;
      }
      var visibleData = dataList.Select((d, i) => new { Data = d, Index = i })
                              .Where(x => !_hiddenIndices.Contains(x.Index))
                              .ToList();

      var total = visibleData.Sum(x => Math.Max(0m, ValueSelector(x.Data)));
      if (total <= 0) {
         _sliceCacheValid = true;
         return;
      }

      float startAngle = -90f;

      foreach (var item in visibleData)
      {
         var value = Math.Max(0m, ValueSelector(item.Data));
         var sweepAngle = (float)(value / total) * 360f;

         SliceInfos.Add(new PieSliceInfo
         {
            Index = item.Index,
            StartAngle = startAngle,
            SweepAngle = sweepAngle,
            Value = (float)value,
            Data = item.Data
         });

         startAngle += sweepAngle;
      }

      _sliceCacheValid = true;
   }

   /// <summary>
   ///     Holds the computed geometry for a single pie or donut slice.
   /// </summary>
   protected struct PieSliceInfo
   {
      /// <summary>Gets or sets the zero-based index of this slice within the data collection.</summary>
      public int Index { get; set; }
      /// <summary>Gets or sets the angle (in degrees) at which this slice starts, measured clockwise from 12 o'clock.</summary>
      public float StartAngle { get; set; }
      /// <summary>Gets or sets the angular size (in degrees) of this slice.</summary>
      public float SweepAngle { get; set; }
      /// <summary>Gets or sets the raw data value represented by this slice.</summary>
      public float Value { get; set; }
      /// <summary>Gets or sets the data item associated with this slice.</summary>
      public TData Data { get; set; }
   }
}
