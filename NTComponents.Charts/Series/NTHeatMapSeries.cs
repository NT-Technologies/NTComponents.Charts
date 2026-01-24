using Microsoft.AspNetCore.Components;
using NTComponents.Charts.Core.Series;
using NTComponents.Charts.Core;
using SkiaSharp;

namespace NTComponents.Charts;

/// <summary>
///     Represents a heatmap series in a cartesian chart.
/// </summary>
/// <typeparam name="TData">The type of the data.</typeparam>
public class NTHeatMapSeries<TData> : NTCartesianSeries<TData> where TData : class
{
   [Parameter, EditorRequired]
   public Func<TData, decimal> WeightSelector { get; set; } = default!;

   [Parameter]
   public TnTColor MinColor { get; set; } = TnTColor.SurfaceContainerLowest;

   [Parameter]
   public TnTColor MaxColor { get; set; } = TnTColor.Primary;

   /// <summary>
   ///    Gets or sets the padding between cells (0.0 to 1.0).
   /// </summary>
   [Parameter]
   public float CellPadding { get; set; } = 0.05f;

   public override void Render(SKCanvas canvas, SKRect renderArea)
   {
      if (Data == null || !Data.Any()) return;

      var dataList = Data.ToList();
      var allX = Chart.GetAllXValues();
      var allY = Chart.GetAllYValues();

      if (!allX.Any() || !allY.Any()) return;

      float cellWidth = renderArea.Width / allX.Count;
      float cellHeight = renderArea.Height / allY.Count;

      decimal minWeight = dataList.Min(WeightSelector);
      decimal maxWeight = dataList.Max(WeightSelector);
      decimal weightRange = maxWeight - minWeight;

      var skMinColor = Chart.GetThemeColor(MinColor);
      var skMaxColor = Chart.GetThemeColor(MaxColor);

      var visibilityFactor = VisibilityFactor;
      var hoverFactor = HoverFactor;

      for (int i = 0; i < dataList.Count; i++)
      {
         var item = dataList[i];
         var xVal = Chart.GetScaledXValue(XValue.Invoke(item));
         var yVal = Chart.GetScaledYValue(YValueSelector(item));
         var weight = WeightSelector(item);


         float t = weightRange > 0 ? (float)((weight - minWeight) / weightRange) : 1.0f;
         var baseColor = InterpolateColor(skMinColor, skMaxColor, t);
         
         var args = new NTDataPointRenderArgs<TData>
         {
            Data = item,
            Index = i,
            Color = baseColor,
            GetThemeColor = Chart.GetThemeColor
         };
         OnDataPointRender?.Invoke(args);

         var color = args.Color ?? baseColor;
         var isPointHovered = Chart.HoveredSeries == this && Chart.HoveredPointIndex == i;
         
         float currentHoverFactor = isPointHovered ? 1f : hoverFactor;
         color = color.WithAlpha((byte)(color.Alpha * visibilityFactor * currentHoverFactor));

         float screenXCoord = Chart.ScaleX(xVal, renderArea);
         float screenYCoord = Chart.ScaleY(yVal, YAxis, renderArea);

         float x = screenXCoord;
         float y = screenYCoord;

         var cellRect = new SKRect(x - cellWidth / 2, y - cellHeight / 2, x + cellWidth / 2, y + cellHeight / 2);
         cellRect.Inflate(-cellWidth * CellPadding / 2, -cellHeight * CellPadding / 2);

         using var paint = new SKPaint
         {
            Color = color,
            Style = SKPaintStyle.Fill,
            IsAntialias = true
         };

         canvas.DrawRect(cellRect, paint);

         if (ShowDataLabels)
         {
            var labelColor = args.DataLabelColor;
            var labelSize = args.DataLabelSize ?? DataLabelSize;
            RenderDataLabel(canvas, x, y + 5, weight, renderArea, labelColor, labelSize);
         }
      }
   }

   private SKColor InterpolateColor(SKColor c1, SKColor c2, float t)
   {
      byte r = (byte)(c1.Red + (c2.Red - c1.Red) * t);
      byte g = (byte)(c1.Green + (c2.Green - c1.Green) * t);
      byte b = (byte)(c1.Blue + (c2.Blue - c1.Blue) * t);
      byte a = (byte)(c1.Alpha + (c2.Alpha - c1.Alpha) * t);
      return new SKColor(r, g, b, a);
   }

   public override (int Index, TData? Data)? HitTest(SKPoint point, SKRect renderArea)
   {
      if (Data == null || !Data.Any()) return null;
      var dataList = Data.ToList();
      var allX = Chart.GetAllXValues();
      var allY = Chart.GetAllYValues();
      if (!allX.Any() || !allY.Any()) return null;

      float cellWidth = renderArea.Width / allX.Count;
      float cellHeight = renderArea.Height / allY.Count;

      for (int i = 0; i < dataList.Count; i++)
      {
         var item = dataList[i];
         var xVal = Chart.GetScaledXValue(XValue.Invoke(item));
         var yVal = Chart.GetScaledYValue(YValueSelector(item));

         float screenXCoord = Chart.ScaleX(xVal, renderArea);
         float screenYCoord = Chart.ScaleY(yVal, YAxis, renderArea);


         float x = screenXCoord;
         float y = screenYCoord;

         var cellRect = new SKRect(x - cellWidth / 2, y - cellHeight / 2, x + cellWidth / 2, y + cellHeight / 2);
         if (cellRect.Contains(point)) return (i, item);
      }

      return null;
   }
}
