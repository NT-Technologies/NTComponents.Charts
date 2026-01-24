using SkiaSharp;
using NTComponents.Charts.Core.Series;

namespace NTComponents.Charts.Core;

internal class LegendItemInfo<TData> where TData : class
{
   public string Label { get; set; } = string.Empty;
   public SKColor Color { get; set; }
   public NTBaseSeries<TData>? Series { get; set; }
   public int? Index { get; set; }
   public bool IsVisible { get; set; } = true;
}
