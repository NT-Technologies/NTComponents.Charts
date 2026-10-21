using Microsoft.AspNetCore.Components;
using NTComponents.Charts.Core.Axes;
using NTComponents.Charts.Core.Series;
using NTComponents.Charts.Core;
using SkiaSharp;

namespace NTComponents.Charts;

/// <summary>
///     Represents a heatmap series in a cartesian chart. Every data item is one cell at (X, Y), colored by its weight.
/// </summary>
/// <remarks>
///     The cell size comes from the smallest gap between distinct X positions and between distinct Y values, so a categorical X
///     axis gives one column per category and the distinct Y values form the rows. The X and Y ranges are widened by half a cell so
///     the outer cells fit the plot.
/// </remarks>
/// <typeparam name="TData">The type of the data.</typeparam>
public class NTHeatMapSeries<TData> : NTCartesianSeries<TData> where TData : class {
   private readonly record struct HeatMapGeometryKey(
      SKRect Area,
      double XMin,
      double XMax,
      decimal YMin,
      decimal YMax,
      float CellPadding,
      float Density,
      bool Secondary,
      Func<TData, decimal> WeightSelector,
      Func<TData, decimal> YValueSelector,
      Func<TData, object> XValue,
      SKColor MinColor,
      SKColor MaxColor);

   /// <summary>
   ///    Gets or sets the selector that extracts the numeric weight value from a data item, used to determine cell intensity.
   /// </summary>
   [Parameter, EditorRequired]
   public Func<TData, decimal> WeightSelector { get; set; } = default!;

   /// <summary>
   ///    Gets or sets the theme color applied to cells with the lowest weight value.
   /// </summary>
   [Parameter]
   public NTColor MinColor { get; set; } = NTColor.SurfaceContainerHighest;

   /// <summary>
   ///    Gets or sets the theme color applied to cells with the highest weight value. When every cell has the same weight, all cells use this color.
   /// </summary>
   [Parameter]
   public NTColor MaxColor { get; set; } = NTColor.Primary;

   /// <summary>
   ///    Gets or sets the gap between neighboring cells in density-independent pixels. Defaults to 2.
   /// </summary>
   [Parameter]
   public float CellPadding { get; set; } = 2f;

   private SKPaint? _cellPaint;
   // Buffers are reused: the snapshot and stats are valid until the data or a selector changes (OnDataChanged), the cell
   // geometry while _cachedKey is set.
   private readonly List<TData> _snapshot = [];
   private readonly List<double> _xs = [];
   private readonly List<decimal> _ys = [];
   private readonly List<SKRect> _rects = [];
   private readonly List<SKColor> _colors = [];
   private bool _statsValid;
   private double _xMin;
   private double _xMax;
   private double _xGap = 1;
   private decimal _yMin;
   private decimal _yMax;
   private decimal _yGap = 1;
   private decimal _weightMin;
   private decimal _weightMax;
   private HeatMapGeometryKey? _cachedKey;
   private bool _drawn;
   private Func<TData, decimal>? _lastWeightSelector;
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
      var changed = (_lastWeightSelector is not null && !ReferenceEquals(_lastWeightSelector, WeightSelector)) || (_lastXSelector is not null && !ReferenceEquals(_lastXSelector, XValue));
      _lastWeightSelector = WeightSelector;
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
      _ys.Clear();
      if (Data is not null) {
         _snapshot.AddRange(Data);
      }

      _weightMin = decimal.MaxValue;
      _weightMax = decimal.MinValue;
      foreach (var item in _snapshot) {
         _xs.Add(Chart.GetScaledXValue(XValue(item)));
         _ys.Add(YValueSelector(item));
         var weight = WeightSelector(item);
         _weightMin = Math.Min(_weightMin, weight);
         _weightMax = Math.Max(_weightMax, weight);
      }

      if (_snapshot.Count > 0) {
         _xGap = NTSeriesGeometry.MinGap(_xs);
         _yGap = NTSeriesGeometry.MinGap(_ys);
         (_xMin, _xMax) = (_xs[0], _xs[^1]);
         (_yMin, _yMax) = (_ys[0], _ys[^1]);
      }

      _statsValid = true;
      return _snapshot.Count > 0;
   }

   /// <inheritdoc />
   public override (double Min, double Max)? GetXRange() => EnsureStats() ? (_xMin - (_xGap / 2), _xMax + (_xGap / 2)) : null;

   /// <summary>Gets the Y range covering every row with half a row of padding. The window arguments are ignored: a heat map is always drawn whole.</summary>
   public override (decimal Min, decimal Max)? GetYRange(double? xMin = null, double? xMax = null) => EnsureStats() ? (_yMin - (_yGap / 2), _yMax + (_yGap / 2)) : null;

   /// <summary>
   ///    Renders the heatmap series onto the provided canvas within the given render area.
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
      var minColor = Chart.GetThemeColor(MinColor);
      var maxColor = Chart.GetThemeColor(MaxColor);
      var key = new HeatMapGeometryKey(renderArea, xMin, xMax, yMin, yMax, CellPadding, context.Density, UseSecondaryYAxis, WeightSelector, YValueSelector, XValue, minColor, maxColor);
      if (_cachedKey != key) {
         _cachedKey = null;
         BuildCells(renderArea, minColor, maxColor);
         _cachedKey = key;
      }

      // Cells scale in around their centre with the eased progress; the cache keeps the final rects and the scaled rect is a stack value.
      var grow = EaseAnimation(GetAnimationProgress());
      _cellPaint ??= new SKPaint { IsAntialias = true };
      _cellPaint.Style = SKPaintStyle.Fill;
      for (var i = 0; i < _rects.Count; i++) {
         var color = _colors[i];
         if (OnDataPointRender is { } onDataPointRender) {
            var args = new NTDataPointRenderArgs<TData> { Data = _snapshot[i], Index = i, Color = color, GetThemeColor = _themeColor ??= Chart.GetThemeColor };
            onDataPointRender(args);
            color = args.Color ?? color;
         }

         _cellPaint.Color = NTSeriesGeometry.WithFactor(color, alpha);
         var cell = Grow(_rects[i], grow);
         var radius = GetCellRadius(cell, context.Density);
         if (radius >= 1f) {
            context.Canvas.DrawRoundRect(cell, radius, radius, _cellPaint);
         }
         else {
            context.Canvas.DrawRect(cell, _cellPaint);
         }
      }

      if (Chart.HoveredSeries == this && Chart.HoveredPointIndex is { } hovered && hovered >= 0 && hovered < _rects.Count) {
         var cell = Grow(_rects[hovered], grow);
         var radius = GetCellRadius(cell, context.Density);
         // Material 3 state layer: on-color overlay at hover (or pressed) opacity, under the outline.
         _cellPaint.Color = NTSeriesGeometry.WithFactor(NTChartTokens.ContrastingTextColor(_colors[hovered]), StateLayerOpacity * alpha);
         context.Canvas.DrawRoundRect(cell, radius, radius, _cellPaint);
         _cellPaint.Style = SKPaintStyle.Stroke;
         _cellPaint.StrokeWidth = 2 * context.Density;
         _cellPaint.Color = NTSeriesGeometry.WithFactor(Chart.GetThemeColor(NTColor.OnSurface), alpha);
         context.Canvas.DrawRoundRect(cell, radius, radius, _cellPaint);
      }

      _drawn = true;
      return renderArea;
   }

   private static SKRect Grow(SKRect cell, float factor) => factor >= 1f ? cell : SKRect.Create(cell.MidX - (cell.Width * factor / 2), cell.MidY - (cell.Height * factor / 2), cell.Width * factor, cell.Height * factor);

   /// <summary>Extra-small corner, clamped so small cells stay recognizably square. A sub-pixel radius is drawn as a plain rect (much cheaper on dense grids).</summary>
   private static float GetCellRadius(SKRect cell, float density) => Math.Min(NTChartTokens.Radius(NTCornerRadius.ExtraSmall) * density, Math.Min(cell.Width, cell.Height) / 4f);

   private void BuildCells(SKRect area, SKColor minColor, SKColor maxColor) {
      _rects.Clear();
      _colors.Clear();
      var width = Math.Abs(Chart.ScaleX(_xMin + _xGap, area) - Chart.ScaleX(_xMin, area));
      var height = Math.Abs(NTSeriesGeometry.ScaleY(Chart, UseSecondaryYAxis, _yMin, area) - NTSeriesGeometry.ScaleY(Chart, UseSecondaryYAxis, _yMin + _yGap, area));
      var gap = Math.Max(CellPadding, 0f) * Chart.Density;
      var halfWidth = Math.Max(width - gap, 0f) / 2;
      var halfHeight = Math.Max(height - gap, 0f) / 2;
      var weightRange = _weightMax - _weightMin;
      foreach (var item in _snapshot) {
         var x = Chart.ScaleX(Chart.GetScaledXValue(XValue(item)), area);
         var y = NTSeriesGeometry.ScaleY(Chart, UseSecondaryYAxis, YValueSelector(item), area);
         _rects.Add(new SKRect(x - halfWidth, y - halfHeight, x + halfWidth, y + halfHeight));
         var t = weightRange == 0 ? 1f : (float)((WeightSelector(item) - _weightMin) / weightRange);
         _colors.Add(InterpolateColor(minColor, maxColor, t));
      }
   }

   /// <inheritdoc />
   protected override void Dispose(bool disposing) {
      if (disposing) {
         _cellPaint?.Dispose();
         _cellPaint = null;
      }
      base.Dispose(disposing);
   }

   private static SKColor InterpolateColor(SKColor c1, SKColor c2, float t) {
      byte Lerp(byte a, byte b) => (byte)Math.Round(a + ((b - a) * t));
      return new SKColor(Lerp(c1.Red, c2.Red), Lerp(c1.Green, c2.Green), Lerp(c1.Blue, c2.Blue), Lerp(c1.Alpha, c2.Alpha));
   }

   /// <inheritdoc />
   internal override TooltipInfo GetTooltipInfo(TData data) {
      var color = Chart.GetSeriesColor(this);
      var defaultInfo = new TooltipInfo {
         Header = Chart.XAxis.FormatValue(XValue(data), forTooltip: true),
         Lines = [
            new TooltipLine { Label = Title ?? "Series", Value = string.Format(DataLabelFormat, WeightSelector(data)), Color = color },
            new TooltipLine { Label = "Y", Value = string.Format(DataLabelFormat, YValueSelector(data)), Color = color }
         ]
      };

      return ResolveTooltipInfo(data, defaultInfo, Chart.HoveredPointIndex);
   }

   /// <summary>
   ///    Performs a hit test against the rendered heatmap cells to identify which data item (if any) is at the given point.
   /// </summary>
   /// <param name="point">The point in screen coordinates to test.</param>
   /// <param name="renderArea">The bounding rectangle used during the last render pass.</param>
   /// <returns>The index and data item at the point, or <see langword="null"/> if no hit was found.</returns>
   public override (int Index, TData? Data)? HitTest(SKPoint point, SKRect renderArea) {
      if (!_drawn) {
         return null;
      }

      for (var i = _rects.Count - 1; i >= 0; i--) {
         if (_rects[i].Contains(point)) {
            return (i, _snapshot[i]);
         }
      }

      return null;
   }
}
