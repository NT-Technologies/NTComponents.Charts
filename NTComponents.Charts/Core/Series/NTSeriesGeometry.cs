using System.Numerics;
using NTComponents.Charts.Core.Axes;
using SkiaSharp;

namespace NTComponents.Charts.Core.Series;

/// <summary>Small geometry helpers shared by the grid-like series (heat map, box plot).</summary>
internal static class NTSeriesGeometry {
    /// <summary>
    ///     Sorts <paramref name="values" /> in place and returns the smallest positive difference between neighbours, or one when
    ///     every value is equal (a single band).
    /// </summary>
    public static T MinGap<T>(List<T> values) where T : INumber<T> {
        values.Sort();
        var gap = T.Zero;
        for (var i = 1; i < values.Count; i++) {
            var diff = values[i] - values[i - 1];
            if (diff > T.Zero && (gap == T.Zero || diff < gap)) {
                gap = diff;
            }
        }

        return gap == T.Zero ? T.One : gap;
    }

    /// <summary>
    ///     Maps a Y value to a pixel on the primary or secondary Y axis; the single place the grid-like series pick their axis.
    /// </summary>
    public static float ScaleY<TData>(NTChart<TData> chart, bool secondary, decimal y, SKRect area) where TData : class => chart.ScaleY(y, (secondary ? chart.SecondaryYAxis : chart.YAxis) as NTAxisOptions<TData>, area);

    /// <summary>Scales the alpha channel of <paramref name="color" /> by <paramref name="factor" /> (clamped to 0..1).</summary>
    public static SKColor WithFactor(SKColor color, float factor) => color.WithAlpha((byte)Math.Clamp(color.Alpha * Math.Clamp(factor, 0f, 1f), 0f, 255f));

    /// <summary>
    ///     Returns <paramref name="preferred" /> when it reaches WCAG AA (4.5:1) on <paramref name="background" /> composited over <paramref name="surface" />,
    ///     otherwise black or white. Keeps text legible when the fill behind it is de-emphasized.
    /// </summary>
    internal static SKColor LegibleTextColor(SKColor preferred, SKColor background, SKColor surface) {
        if (background.Alpha < 255) {
            var t = background.Alpha / 255f;
            background = new SKColor((byte)(surface.Red + ((background.Red - surface.Red) * t)), (byte)(surface.Green + ((background.Green - surface.Green) * t)), (byte)(surface.Blue + ((background.Blue - surface.Blue) * t)));
        }

        return NTChartTokens.ContrastRatio(preferred.WithAlpha(255), background) >= 4.5f ? preferred : NTChartTokens.ContrastingTextColor(background);
    }
}
