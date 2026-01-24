using Microsoft.AspNetCore.Components;
using SkiaSharp;

namespace NTComponents.Charts.Core.Series;

/// <summary>
///     Base class for circular series (Pie, Donut).
/// </summary>
/// <typeparam name="TData">The type of the data.</typeparam>
public abstract class NTCircularSeries<TData> : NTBaseSeries<TData> where TData : class
{
   /// <inheritdoc />
   public override ChartCoordinateSystem CoordinateSystem => ChartCoordinateSystem.Circular;

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
   public TnTColor? DataLabelColor { get; set; }

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

   protected List<PieSliceInfo> SliceInfos { get; } = new();

   private HashSet<int> _hiddenIndices = new();

   protected override void OnDataChanged()
   {
      base.OnDataChanged();
      SliceInfos.Clear();
      _hiddenIndices.Clear();
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

      var dataList = Data.ToList();
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
            IsVisible = Visible && !_hiddenIndices.Contains(i)
         };
      }
   }

   protected void CalculateSlices(SKRect renderArea)
   {
      SliceInfos.Clear();
      if (Data == null || !Data.Any()) return;

      var dataList = Data.ToList();
      var visibleData = dataList.Select((d, i) => new { Data = d, Index = i })
                              .Where(x => !_hiddenIndices.Contains(x.Index))
                              .ToList();

      var total = visibleData.Sum(x => Math.Max(0m, ValueSelector(x.Data)));
      if (total <= 0) return;

      var progress = GetAnimationProgress();
      var easedProgress = progress; // Linear or eased?

      float startAngle = -90f; // 12 o'clock
      float radius = Math.Min(renderArea.Width, renderArea.Height) / 2f;

      foreach (var item in visibleData)
      {
         var value = ValueSelector(item.Data);
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
   }

   protected struct PieSliceInfo
   {
      public int Index { get; set; }
      public float StartAngle { get; set; }
      public float SweepAngle { get; set; }
      public float Value { get; set; }
      public TData Data { get; set; }
   }
}
