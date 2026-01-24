using Microsoft.AspNetCore.Components;
using NTComponents.Charts.Core;
using NTComponents.Charts.Core.Series;
using SkiaSharp;

namespace NTComponents.Charts;

/// <summary>
///     Represents a treemap series that visualizes data as nested rectangles.
/// </summary>
/// <typeparam name="TData">The type of the data.</typeparam>
public class NTTreeMapSeries<TData> : NTBaseSeries<TData> where TData : class
{
   [Parameter]
   public Func<TData, decimal> ValueSelector { get; set; } = _ => 0;

   [Parameter]
   public float ItemPadding { get; set; } = 2f;

   [Parameter]
   public string DataLabelFormat { get; set; } = "{0:N0}";

   [Parameter]
   public bool ShowLabels { get; set; } = true;

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

   public override ChartCoordinateSystem CoordinateSystem => ChartCoordinateSystem.TreeMap;


   internal override decimal GetTotalValue() => Data?.Sum(ValueSelector) ?? 0;

   private List<TreeMapRect> _lastRects = [];

   private record TreeMapRect(TData Data, SKRect Rect, SKColor Color, int Index);

   public override void Render(SKCanvas canvas, SKRect renderArea)
   {
      if (Data == null || !Data.Any()) return;

      var dataList = Data.ToList();
      var totalValue = dataList.Sum(ValueSelector);
      if (totalValue <= 0) return;

      var progress = GetAnimationProgress();
      var easedProgress = BackEase(progress);
      var visibilityFactor = VisibilityFactor;

      // Simple Squarified Treemap Layout (simplified version)
      _lastRects = LayoutTreeMap(dataList, renderArea);

      foreach (var item in _lastRects)
      {
         var rect = item.Rect;

         // Apply animation: scale from center of rect
         if (progress < 1)
         {
            float centerX = rect.MidX;
            float centerY = rect.MidY;
            float w = rect.Width * easedProgress;
            float h = rect.Height * easedProgress;
            rect = new SKRect(centerX - w / 2, centerY - h / 2, centerX + w / 2, centerY + h / 2);
         }

         var baseColor = item.Color;
         var args = new NTDataPointRenderArgs<TData>
         {
            Data = item.Data,
            Index = item.Index,
            Color = baseColor,
            GetThemeColor = Chart.GetThemeColor
         };
         OnDataPointRender?.Invoke(args);

         var isHovered = Chart.HoveredSeries == this && Chart.HoveredPointIndex == item.Index;
         var hoverFactor = (isHovered) ? 1.0f : HoverFactor;
         var pointColor = args.Color ?? baseColor;
         var color = pointColor.WithAlpha((byte)(pointColor.Alpha * visibilityFactor * hoverFactor));

         using var paint = new SKPaint
         {
            Color = color,
            Style = SKPaintStyle.Fill,
            IsAntialias = true
         };

         canvas.DrawRect(rect, paint);

         if (ShowLabels && rect.Width > 40 && rect.Height > 20)
         {
            RenderLabel(canvas, rect, item.Data, color, args);
         }
      }
   }

   private void RenderLabel(SKCanvas canvas, SKRect rect, TData data, SKColor bgColor, NTDataPointRenderArgs<TData>? args)
   {
      var label = XValue?.Invoke(data)?.ToString() ?? string.Empty;
      var value = ValueSelector(data);
      var valueText = string.Format(DataLabelFormat, value);

      // Default to series text color from palette
      var textColor = args?.DataLabelColor ?? Chart.GetSeriesTextColor(this);
      var fontSize = args?.DataLabelSize ?? 12;

      using var paint = new SKPaint
      {
         Color = textColor.WithAlpha((byte)(textColor.Alpha * VisibilityFactor)),
         IsAntialias = true
      };

      using var font = new SKFont
      {
         Size = fontSize,
         Embolden = true,
         Typeface = Chart.DefaultTypeface
      };

      // Clip text to rect
      canvas.Save();
      canvas.ClipRect(rect);

      float x = rect.Left + 5;
      float y = rect.Top + 15;

      canvas.DrawText(label, x, y, SKTextAlign.Left, font, paint);

      font.Size = 10;
      canvas.DrawText(valueText, x, y + 15, SKTextAlign.Left, font, paint);

      canvas.Restore();
   }

   private List<TreeMapRect> LayoutTreeMap(List<TData> data, SKRect area)
   {
      var result = new List<TreeMapRect>();
      var sortedData = data.Select((d, i) => new { Data = d, Value = ValueSelector(d), OriginalIndex = i })
                          .OrderByDescending(x => x.Value)
                          .ToList();

      var totalValue = sortedData.Sum(x => x.Value);

      // Use a simple Slice-and-Dice for now as it's more stable for animations
      // but we can refine it if needed.
      SliceAndDice(sortedData.Select(x => (object)x).ToList(), area, totalValue, result, true);

      return result;
   }

   private void SliceAndDice(List<object> items, SKRect area, decimal totalValue, List<TreeMapRect> result, bool horizontal)
   {
      if (!items.Any()) return;
      if (items.Count == 1)
      {
         dynamic item = items[0];
         var rect = area;
         if (rect.Width > ItemPadding * 2 && rect.Height > ItemPadding * 2)
         {
            rect.Inflate(-ItemPadding, -ItemPadding);
         }
         result.Add(new TreeMapRect(item.Data, rect, Chart.GetSeriesColor(this).WithAlpha((byte)(255 * (0.4 + 0.6 * (double)(item.Value / (totalValue == 0 ? 1 : totalValue))))), item.OriginalIndex));
         return;
      }

      // Split items into two groups
      int mid = items.Count / 2;
      var leftItems = items.Take(mid).ToList();
      var rightItems = items.Skip(mid).ToList();

      decimal leftValue = leftItems.Sum(x => (decimal)((dynamic)x).Value);
      decimal rightValue = rightItems.Sum(x => (decimal)((dynamic)x).Value);
      decimal total = leftValue + rightValue;

      if (total <= 0) return;

      if (horizontal)
      {
         float leftWidth = (float)(area.Width * (float)(leftValue / total));
         var leftArea = new SKRect(area.Left, area.Top, area.Left + leftWidth, area.Bottom);
         var rightArea = new SKRect(area.Left + leftWidth, area.Top, area.Right, area.Bottom);
         SliceAndDice(leftItems, leftArea, leftValue, result, !horizontal);
         SliceAndDice(rightItems, rightArea, rightValue, result, !horizontal);
      }
      else
      {
         float topHeight = (float)(area.Height * (float)(leftValue / total));
         var topArea = new SKRect(area.Left, area.Top, area.Right, area.Top + topHeight);
         var bottomArea = new SKRect(area.Left, area.Top + topHeight, area.Right, area.Bottom);
         SliceAndDice(leftItems, topArea, leftValue, result, !horizontal);
         SliceAndDice(rightItems, bottomArea, rightValue, result, !horizontal);
      }
   }

   public override (int Index, TData? Data)? HitTest(SKPoint point, SKRect renderArea)
   {
      foreach (var item in _lastRects)
      {
         if (item.Rect.Contains(point))
         {
            return (item.Index, item.Data);
         }
      }
      return null;
   }

   internal override IEnumerable<LegendItemInfo<TData>> GetLegendItems()
   {
      yield return new LegendItemInfo<TData>
      {
         Label = Title ?? "Series",
         Color = Chart.GetSeriesColor(this),
         Series = this,
         Index = null,
         IsVisible = Visible
      };
   }
}
