using Microsoft.AspNetCore.Components;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Linq;

namespace NTComponents.Charts.Core.Axes;

/// <summary>
///     Options for the X axis of a cartesian chart.
/// </summary>
public class NTXAxisOptions<TData> : NTAxisOptions<TData>, INTXAxis<TData> where TData : class {


    private SKPaint? _textPaint;
    private SKPaint? _titlePaint;
    private SKPaint? _linePaint;
    private SKFont? _textFont;
    private SKFont? _titleFont;

    public static INTXAxis<TData> Default { get; } = new NTXAxisOptions<TData>() {
        ValueSelector = data => data
    };

    [Parameter, EditorRequired]
    public Func<TData, object> ValueSelector { get; set; }

    public override void Dispose() {
        _textPaint?.Dispose();
        _titlePaint?.Dispose();
        _linePaint?.Dispose();
        _textFont?.Dispose();
        _titleFont?.Dispose();
        Chart.UnregisterAxis(this);
        base.Dispose();
    }

    protected override void OnInitialized() {
        base.OnInitialized();
        Chart.RegisterAxis(this);
    }

    /// <inheritdoc />
    public override SKRect Measure(NTRenderContext context, SKRect renderArea) {
        return renderArea;
        //if (!Visible) return renderArea;

        //float labelHeight = 16;
        //float titleHeight = string.IsNullOrEmpty(Title) ? 0 : 20;
        //var totalAxisHeight = (labelHeight + titleHeight + 4) * context.Density;

        //// Add a 10px margin on the right to prevent data points from being cut off,
        //// but only if there isn't a secondary Y-axis providing its own space.
        //float rightMargin = (Chart.SecondaryYAxis != null ? 0 : 10) * context.Density;

        //// Determine nice range once during measurement based on available space
        //if (!Chart.IsCategoricalX && Scale == NTAxisScale.Linear && !Chart.HasViewRange(this)) {
        //   var (min, max) = Chart.GetXRange(this, false);
        //   var maxTicks = Math.Min(MaxTicks, Math.Max(2, (int)(renderArea.Width / (100 * context.Density))));
        //   var (niceMin, niceMax, _) = CalculateNiceScaling(min, max, maxTicks);
        //   CachedXRange = (niceMin, niceMax);
        //}

        //return new SKRect(renderArea.Left, renderArea.Top, renderArea.Right - rightMargin, renderArea.Bottom - totalAxisHeight);
    }

    /// <inheritdoc />
    public override SKRect Render(NTRenderContext context, SKRect renderArea) {
        if(!Visible) {
            return renderArea;
        }
        return renderArea;
        //if (!Visible) return renderArea;

        //var newArea = Measure(context, renderArea);

        //float labelHeight = 16;
        //float titleHeight = string.IsNullOrEmpty(Title) ? 0 : 20;
        //var totalAxisHeight = (labelHeight + titleHeight + 4) * context.Density;
        //float rightMargin = (Chart.SecondaryYAxis != null ? 0 : 10) * context.Density;

        //// Now draw the axis
        //var plotArea = context.PlotArea; 
        //// In the new architecture, axes are drawn AFTER the final plotArea is determined?
        //// ARCHITECTURE.md says: "Each component performs its drawing and returns the remaining SKRect.
        //// For example, a LeftAxis might draw itself and return an area with the left margin removed,
        //// which the next component then uses as its bounds."

        //// So the AXIS should draw itself relative to the renderArea provided to it.
        //// If it's a bottom axis, it draws at the bottom of renderArea and returns renderArea minus its height.

        //var canvas = context.Canvas;
        //var (xMinReal, xMaxReal) = Chart.GetXRange(this, false);

        //_textPaint ??= new SKPaint {
        //   IsAntialias = true
        //};
        //_textPaint.Color = context.TextColor;

        //_textFont ??= new SKFont {
        //   Embolden = true,
        //   Typeface = context.DefaultFont.Typeface
        //};
        //_textFont.Size = 12 * context.Density;

        //_titlePaint ??= new SKPaint {
        //   IsAntialias = true
        //};
        //_titlePaint.Color = context.TextColor;

        //_titleFont ??= new SKFont {
        //   Embolden = true,
        //   Typeface = context.DefaultFont.Typeface
        //};
        //_titleFont.Size = 16 * context.Density;

        //_linePaint ??= new SKPaint {
        //   StrokeWidth = context.Density,
        //   Style = SKPaintStyle.Stroke,
        //   IsAntialias = true
        //};
        //_linePaint.Color = Chart.GetThemeColor(TnTColor.Outline);

        //// We use the full width of the renderArea for the axis line
        //var yLine = renderArea.Bottom - totalAxisHeight;
        //canvas.DrawLine(renderArea.Left, yLine, renderArea.Right - rightMargin, yLine, _linePaint);

        //// We need to use the PLOT AREA for scaling.
        //// But who determines the plot area?
        //// In the new architecture, the plot area is what's left after all axes have rendered.
        //// This means the axes need to render based on the FINAL plot area, OR they render markers based on the available width.

        //// Actually, if we follow the PASSES description in ARCHITECTURE.md:
        //// Pass 3: The Render Pass:
        //// "The chart iterates through the sorted list, calling Render on each.
        //// Each component performs its drawing and returns the remaining SKRect."

        //// This works for simple partitioning, but drawing labels needs to know the final bounds.
        //// Maybe we have a Measurement pass first?
        //// Actually, the user says: "All axis sizes, calculations, and drawing of actual chart logic should belong to IRenderable.cs implementors."

        //// If we use the provided renderArea.Right - rightMargin, we are assuming the width.
        //var axisWidth = (renderArea.Right - rightMargin) - renderArea.Left;
        //var axisLeft = renderArea.Left;

        //// Mocking PlotArea for the scale call if it's not set yet.
        //var tempPlotArea = new SKRect(axisLeft, renderArea.Top, axisLeft + axisWidth, yLine);

        //var isCategorical = Chart.IsCategoricalX;

        //if (isCategorical) {
        //   var allValues = Chart.GetAllXValues();
        //   if (allValues.Any()) {
        //      for (var i = 0; i < allValues.Count; i++) {
        //         var val = allValues[i];
        //         var scaledVal = Chart.GetScaledXValue(val);
        //         var screenCoord = Chart.ScaleX(scaledVal, tempPlotArea, this);

        //         if (screenCoord < tempPlotArea.Left - (1 * context.Density) || screenCoord > tempPlotArea.Right + (1 * context.Density)) continue;

        //         string label;
        //         if (!string.IsNullOrEmpty(LabelFormat)) {
        //            if (LabelFormat.Contains("{0")) {
        //               label = string.Format(LabelFormat, val);
        //            }
        //            else if (val is IFormattable formattable) {
        //               label = formattable.ToString(LabelFormat, null);
        //            }
        //            else {
        //               label = val?.ToString() ?? "";
        //            }
        //         }
        //         else {
        //            label = val is IFormattable f ? f.ToString("0.#", null) : val?.ToString() ?? "";
        //         }

        //         SKTextAlign textAlign = SKTextAlign.Center;
        //         canvas.DrawText(label, screenCoord, yLine + (12 * context.Density), textAlign, _textFont, _textPaint);
        //      }
        //   }
        //}
        //else if (Scale == NTAxisScale.Logarithmic) {
        //   var (min, max) = Chart.GetXRange(this, true);
        //   min = Math.Max(0.000001, min);
        //   max = Math.Max(min * 1.1, max);

        //   var startLog = (int)Math.Floor(Math.Log10(min));
        //   var endLog = (int)Math.Ceiling(Math.Log10(max));

        //   for (int log = startLog; log <= endLog; log++) {
        //      double val = Math.Pow(10, log);
        //      if (val < min || val > max) continue;

        //      var screenCoord = Chart.ScaleX(val, tempPlotArea, this);
        //      if (screenCoord < tempPlotArea.Left - (1 * context.Density) || screenCoord > tempPlotArea.Right + (1 * context.Density)) continue;

        //      canvas.DrawText(val.ToString("G"), screenCoord, yLine + (12 * context.Density), SKTextAlign.Center, _textFont, _textPaint);
        //   }
        //}
        //else {
        //   var maxTicks = Math.Min(MaxTicks, Math.Max(2, (int)(axisWidth / (100 * context.Density))));
        //   var (niceMin, niceMax, spacing) = CalculateNiceScaling(xMinReal, xMaxReal, maxTicks);
        //   int totalLabels = (int)Math.Round((niceMax - niceMin) / spacing) + 1;
        //   for (int i = 0; i < totalLabels; i++) {
        //      double val = niceMin + i * spacing;
        //      var screenCoord = Chart.ScaleX(val, tempPlotArea, this);

        //      if (screenCoord < tempPlotArea.Left - (1 * context.Density) || screenCoord > tempPlotArea.Right + (1 * context.Density)) continue;

        //      SKTextAlign textAlign = SKTextAlign.Center;
        //      if (i == 0) {
        //         textAlign = SKTextAlign.Left;
        //      }
        //      else if (i == totalLabels - 1) {
        //         textAlign = SKTextAlign.Right;
        //      }

        //      string label;
        //      if (!string.IsNullOrEmpty(LabelFormat)) {
        //         if (LabelFormat.Contains("{0")) {
        //            label = string.Format(LabelFormat, Chart.IsXAxisDateTime ? new DateTime((long)val) : val);
        //         }
        //         else if (Chart.IsXAxisDateTime) {
        //            label = new DateTime((long)val).ToString(LabelFormat);
        //         }
        //         else {
        //            label = val.ToString(LabelFormat);
        //         }
        //      }
        //      else if (Chart.IsXAxisDateTime) {
        //         label = new DateTime((long)val).ToString("d");
        //      }
        //      else {
        //         label = val.ToString("0.##");
        //      }

        //      canvas.DrawText(label, screenCoord, yLine + (12 * context.Density), textAlign, _textFont, _textPaint);

        //   }
        //}

        //if (!string.IsNullOrEmpty(Title)) {
        //   canvas.DrawText(Title, axisLeft + (axisWidth / 2), yLine + (30 * context.Density), SKTextAlign.Center, _titleFont, _titlePaint);
        //}

        //return newArea;
    }
}
