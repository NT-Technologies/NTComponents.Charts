using Microsoft.AspNetCore.Components;
using NTComponents.Charts.Core.Axes;
using NTComponents.Charts.Core.Series;
using NTComponents.Charts.Core;
using SkiaSharp;

namespace NTComponents.Charts;

/// <summary>
///     Represents a box plot series in a cartesian chart.
/// </summary>
/// <remarks>
///     The box width is a fraction of the smallest gap between distinct X positions (one category on a categorical axis). The Y range
///     covers the whiskers and any outliers. The hover and click area of an item is its box width from the upper to the lower whisker.
/// </remarks>
/// <typeparam name="TData">The type of the data.</typeparam>
public class NTBoxPlotSeries<TData> : NTCartesianSeries<TData> where TData : class {
   private readonly record struct BoxPlotGeometryKey(
      SKRect Area,
      double XMin,
      double XMax,
      decimal YMin,
      decimal YMax,
      float BoxWidthRatio,
      float WhiskerWidthRatio,
      float Density,
      bool Secondary,
      Func<TData, BoxPlotValues> BoXValue,
      Func<TData, object> XValue);

   /// <summary>Pixel geometry of one box. Y values are screen coordinates, so Max is the smallest and Min the largest.</summary>
   private struct BoxGeometry {
      public float X;
      public float HalfBox;
      public float HalfCap;
      public float Max;
      public float Q3;
      public float Median;
      public float Q1;
      public float Min;
      public int OutlierStart;
      public int OutlierCount;
   }

   /// <summary>
   ///    Gets or sets the selector that extracts box plot statistical values from a data item.
   /// </summary>
   [Parameter, EditorRequired]
   public Func<TData, BoxPlotValues> BoXValue { get; set; } = default!;

   /// <summary>
   ///    Gets or sets the width of the box as a fraction of the available space (0.0 to 1.0).
   /// </summary>
   [Parameter]
   public float BoxWidthRatio { get; set; } = 0.6f;

   /// <summary>
   ///    Gets or sets the width of the whiskers as a fraction of the box width (0.0 to 1.0).
   /// </summary>
   [Parameter]
   public float WhiskerWidthRatio { get; set; } = 0.5f;

   private SKPaint? _strokePaint;
   private SKPaint? _fillPaint;
   // Buffers are reused: the snapshot and stats are valid until the data or a selector changes (OnDataChanged), the box
   // geometry while _cachedKey is set.
   private readonly List<TData> _snapshot = [];
   private readonly List<double> _xs = [];
   private readonly List<BoxGeometry> _boxes = [];
   private readonly List<float> _outliers = [];
   private bool _statsValid;
   private double _xMin;
   private double _xMax;
   private double _xGap = 1;
   private BoxPlotGeometryKey? _cachedKey;
   private bool _drawn;
   private Func<TData, BoxPlotValues>? _lastBoxSelector;
   private Func<TData, object>? _lastXSelector;
   private Func<NTColor, SKColor>? _themeColor;

   /// <inheritdoc />
   protected override void OnDataChanged() {
      base.OnDataChanged();
      _statsValid = false;
      _cachedKey = null;
      _drawn = false;
   }

   /// <inheritdoc />
   protected override void OnParametersSet() {
      base.OnParametersSet();
      var changed = (_lastBoxSelector is not null && !ReferenceEquals(_lastBoxSelector, BoXValue)) || (_lastXSelector is not null && !ReferenceEquals(_lastXSelector, XValue));
      _lastBoxSelector = BoXValue;
      _lastXSelector = XValue;
      if (changed) {
         OnDataChanged();
      }
   }

   private bool EnsureStats() {
      if (_statsValid) {
         return _snapshot.Count > 0;
      }

      _snapshot.Clear();
      _xs.Clear();
      if (Data is not null) {
         _snapshot.AddRange(Data);
      }

      foreach (var item in _snapshot) {
         _xs.Add(Chart.GetScaledXValue(XValue(item)));
      }

      if (_snapshot.Count > 0) {
         _xGap = NTSeriesGeometry.MinGap(_xs);
         _xMin = _xs[0];
         _xMax = _xs[^1];
      }

      _statsValid = true;
      return _snapshot.Count > 0;
   }

   /// <summary>Gets the X range covering every box, widened by half a box slot so the outer boxes fit the plot.</summary>
   public override (double Min, double Max)? GetXRange() => EnsureStats() ? (_xMin - (_xGap / 2), _xMax + (_xGap / 2)) : null;

   /// <summary>
   ///    Renders the box plot series onto the provided canvas within the given render area.
   /// </summary>
   /// <param name="context">The rendering context containing the canvas and theme information.</param>
   /// <param name="renderArea">The bounding rectangle available for rendering.</param>
   /// <returns>The render area after rendering.</returns>
   public override SKRect Render(NTRenderContext context, SKRect renderArea) {
      _drawn = false;
      var alpha = Math.Clamp(HoverFactor * VisibilityFactor, 0f, 1f);
      if (alpha <= 0f || !EnsureStats()) {
         return renderArea;
      }

      var (xMin, xMax) = Chart.GetXRange(Chart.XAxis as NTAxisOptions<TData>, true);
      var (yMin, yMax) = Chart.GetYRange((UseSecondaryYAxis ? Chart.SecondaryYAxis : Chart.YAxis) as NTAxisOptions<TData>, true);
      var key = new BoxPlotGeometryKey(renderArea, xMin, xMax, yMin, yMax, BoxWidthRatio, WhiskerWidthRatio, context.Density, UseSecondaryYAxis, BoXValue, XValue);
      if (_cachedKey != key) {
         _cachedKey = null;
         BuildBoxes(renderArea);
         _cachedKey = key;
      }

      _fillPaint ??= new SKPaint { IsAntialias = true, Style = SKPaintStyle.Fill };
      _strokePaint ??= new SKPaint { IsAntialias = true, Style = SKPaintStyle.Stroke };
      var seriesColor = Chart.GetSeriesColor(this);
      var canvas = context.Canvas;
      var density = context.Density;
      // Boxes grow out of the median line: every Y interpolates from the median to its final position. Outliers fade in.
      var grow = EaseAnimation(GetAnimationProgress());
      for (var i = 0; i < _boxes.Count; i++) {
         var box = _boxes[i];
         if (float.IsNaN(box.X)) {
            continue;
         }

         box.Max = box.Median + ((box.Max - box.Median) * grow);
         box.Q3 = box.Median + ((box.Q3 - box.Median) * grow);
         box.Q1 = box.Median + ((box.Q1 - box.Median) * grow);
         box.Min = box.Median + ((box.Min - box.Median) * grow);

         var color = seriesColor;
         if (OnDataPointRender is { } onDataPointRender) {
            var args = new NTDataPointRenderArgs<TData> { Data = _snapshot[i], Index = i, Color = color, GetThemeColor = _themeColor ??= Chart.GetThemeColor };
            onDataPointRender(args);
            color = args.Color ?? color;
         }

         var isHovered = Chart.HoveredSeries == this && Chart.HoveredPointIndex == i;
         _fillPaint.Color = NTSeriesGeometry.WithFactor(color, alpha * (isHovered ? 0.6f : 0.3f));
         _strokePaint.Color = NTSeriesGeometry.WithFactor(color, alpha);
         _strokePaint.StrokeWidth = (isHovered ? 3f : 2f) * density;

         var (left, right) = (box.X - box.HalfBox, box.X + box.HalfBox);
         var body = new SKRect(left, box.Q3, right, box.Q1);
         canvas.DrawRect(body, _fillPaint);
         canvas.DrawRect(body, _strokePaint);
         canvas.DrawLine(left, box.Median, right, box.Median, _strokePaint);
         canvas.DrawLine(box.X, box.Q3, box.X, box.Max, _strokePaint);
         canvas.DrawLine(box.X, box.Q1, box.X, box.Min, _strokePaint);
         canvas.DrawLine(box.X - box.HalfCap, box.Max, box.X + box.HalfCap, box.Max, _strokePaint);
         canvas.DrawLine(box.X - box.HalfCap, box.Min, box.X + box.HalfCap, box.Min, _strokePaint);
         _strokePaint.Color = NTSeriesGeometry.WithFactor(color, alpha * grow);
         for (var o = box.OutlierStart; o < box.OutlierStart + box.OutlierCount; o++) {
            canvas.DrawCircle(box.X, _outliers[o], 3f * density, _strokePaint);
         }
      }

      _drawn = true;
      return renderArea;
   }

   private void BuildBoxes(SKRect area) {
      _boxes.Clear();
      _outliers.Clear();
      var slot = Math.Abs(Chart.ScaleX(_xMin + _xGap, area) - Chart.ScaleX(_xMin, area));
      var halfBox = slot * Math.Clamp(BoxWidthRatio, 0f, 1f) / 2;
      var halfCap = halfBox * Math.Clamp(WhiskerWidthRatio, 0f, 1f);
      foreach (var item in _snapshot) {
         if (BoXValue(item) is not { } values) {
            _boxes.Add(new BoxGeometry { X = float.NaN });
            continue;
         }

         var box = new BoxGeometry {
            X = Chart.ScaleX(Chart.GetScaledXValue(XValue(item)), area),
            HalfBox = halfBox,
            HalfCap = halfCap,
            Max = NTSeriesGeometry.ScaleY(Chart, UseSecondaryYAxis, values.Max, area),
            Q3 = NTSeriesGeometry.ScaleY(Chart, UseSecondaryYAxis, values.Q3, area),
            Median = NTSeriesGeometry.ScaleY(Chart, UseSecondaryYAxis, values.Median, area),
            Q1 = NTSeriesGeometry.ScaleY(Chart, UseSecondaryYAxis, values.Q1, area),
            Min = NTSeriesGeometry.ScaleY(Chart, UseSecondaryYAxis, values.Min, area),
            OutlierStart = _outliers.Count
         };
         if (values.Outliers is { } outliers) {
            foreach (var outlier in outliers) {
               _outliers.Add(NTSeriesGeometry.ScaleY(Chart, UseSecondaryYAxis, outlier, area));
            }
         }

         box.OutlierCount = _outliers.Count - box.OutlierStart;
         _boxes.Add(box);
      }
   }

   /// <inheritdoc />
   protected override void Dispose(bool disposing) {
      if (disposing) {
         _strokePaint?.Dispose();
         _fillPaint?.Dispose();
         _strokePaint = null;
         _fillPaint = null;
      }
      base.Dispose(disposing);
   }

   /// <inheritdoc />
   internal override TooltipInfo GetTooltipInfo(TData data) {
      var color = Chart.GetSeriesColor(this);
      var values = BoXValue(data);
      var lines = new List<TooltipLine>(6);
      if (values is not null) {
         lines.Add(new TooltipLine { Label = "Max", Value = string.Format(DataLabelFormat, values.Max), Color = color });
         lines.Add(new TooltipLine { Label = "Q3", Value = string.Format(DataLabelFormat, values.Q3), Color = color });
         lines.Add(new TooltipLine { Label = "Median", Value = string.Format(DataLabelFormat, values.Median), Color = color });
         lines.Add(new TooltipLine { Label = "Q1", Value = string.Format(DataLabelFormat, values.Q1), Color = color });
         lines.Add(new TooltipLine { Label = "Min", Value = string.Format(DataLabelFormat, values.Min), Color = color });
         if (values.Outliers is { Length: > 0 } outliers) {
            lines.Add(new TooltipLine { Label = "Outliers", Value = outliers.Length.ToString(), Color = color });
         }
      }

      var defaultInfo = new TooltipInfo { Header = Chart.XAxis.FormatValue(XValue(data), forTooltip: true), Lines = lines };
      return ResolveTooltipInfo(data, defaultInfo, Chart.HoveredPointIndex);
   }

   /// <summary>
   ///    Performs a hit test against the rendered boxes to identify which data item (if any) is at the given point.
   /// </summary>
   /// <param name="point">The point in screen coordinates to test.</param>
   /// <param name="renderArea">The bounding rectangle used during the last render pass.</param>
   /// <returns>The index and data item at the point, or <see langword="null"/> if no hit was found.</returns>
   public override (int Index, TData? Data)? HitTest(SKPoint point, SKRect renderArea) {
      if (!_drawn) {
         return null;
      }

      for (var i = _boxes.Count - 1; i >= 0; i--) {
         var box = _boxes[i];
         if (Math.Abs(point.X - box.X) <= box.HalfBox && point.Y >= box.Max && point.Y <= box.Min) {
            return (i, _snapshot[i]);
         }
      }

      return null;
   }
}
