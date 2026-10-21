using SkiaSharp;

namespace NTComponents.Charts.Core;

// Mirrors NTComponents/Styles/_Variables/_typescale.scss ($nt-typescale-baseline, $nt-typescale-emphasized), _corner-radius.scss,
// _elevation.scss (_nt-elevation-shadows level 2), _motion.scss, Tooltip/NTTooltip.razor.scss (rich) and md.sys.state (material-web v34.0.21).
// This is the only place chart Material 3 values live.
internal static class NTChartTokens {

    // ---- State layers (md.sys.state) ----
    internal const float HoverOpacity = 0.08f;
    internal const float FocusOpacity = 0.10f;
    internal const float PressedOpacity = 0.10f;

    /// <summary>md.sys.state.disabled; opacity of non-hovered siblings and hidden legend items.</summary>
    internal const float DeEmphasisOpacity = 0.38f;

    // ---- Motion ($nt-motion-duration-short3, standard easing) ----
    internal const NTMotionDuration HoverDuration = NTMotionDuration.Ms150;

    // ---- Layout (4dp grid) ----
    internal const float ChartMargin = 8f;

    /// <summary>Title band: title-medium line height (24) plus 8 spacing.</summary>
    internal const float TitleBand = 32f;

    // ---- Rich tooltip (Tooltip/NTTooltip.razor.scss: $nt-tooltip-rich-*) ----
    internal const NTCornerRadius TooltipCorner = NTCornerRadius.Medium;
    internal const float TooltipPaddingBlock = 12f;
    internal const float TooltipPaddingInline = 16f;
    internal const float TooltipContentGap = 4f;
    internal const float TooltipMaxWidth = 320f;
    internal const float TooltipCursorOffset = 16f;
    internal const float TooltipKeySize = 8f;
    internal const float TooltipKeyGap = 8f;

    // ---- Legend (filter chip analogue) ----
    internal const NTCornerRadius LegendCorner = NTCornerRadius.Small;
    internal const float LegendRowHeight = 24f;
    internal const float LegendItemSpacing = 16f;
    internal const float LegendSwatchGap = 8f;
    internal const float LegendPadding = 8f;

    // ---- Data labels ----
    internal const NTCornerRadius DataLabelCorner = NTCornerRadius.ExtraSmall;
    internal const float DataLabelPaddingInline = 8f;
    internal const float DataLabelPaddingBlock = 2f;

    // ---- Axes ----
    internal const float AxisTickLength = 4f;

    // ---- Elevation level 2 (_elevation.scss _nt-elevation-shadows level 2): key 0 1px 2px 0 @30%, ambient 0 2px 6px 2px @15% ----
    internal const float KeyShadowY = 1f;
    internal const float KeyShadowBlur = 2f;
    internal const float KeyShadowAlpha = 0.30f;
    internal const float AmbientShadowY = 2f;
    internal const float AmbientShadowBlur = 6f;
    internal const float AmbientShadowSpread = 2f;
    internal const float AmbientShadowAlpha = 0.15f;

    // Roboto hhea metrics (ascender 1900, descender -500, unitsPerEm 2048). Constants avoid per-frame SKFont.Metrics calls and the WASM metric crash noted in NTPieSeries.
    private const float RobotoAscent = 1900f / 2048f;
    private const float RobotoDescent = 500f / 2048f;

    /// <summary>Corner radius in dp. <see cref="NTCornerRadius.Full" /> is <see cref="float.MaxValue" />; clamp it to the shape size.</summary>
    internal static float Radius(NTCornerRadius radius) => radius switch {
        NTCornerRadius.None => 0f,
        NTCornerRadius.ExtraSmall => 4f,
        NTCornerRadius.Small => 8f,
        NTCornerRadius.Medium => 12f,
        NTCornerRadius.Full => float.MaxValue,
        _ => throw new ArgumentOutOfRangeException(nameof(radius), radius, null)
    };

    /// <summary>Typeface of a role: 500 for title-small/medium, labels and emphasized body; 400 otherwise.</summary>
    internal static SKTypeface Typeface(NTTypography role, bool emphasized = false) => role switch {
        NTTypography.TitleSmall or NTTypography.TitleMedium or NTTypography.LabelSmall or NTTypography.LabelMedium or NTTypography.LabelLarge => NTRobotoFonts.Medium,
        NTTypography.TitleLarge or NTTypography.BodyLarge or NTTypography.BodyMedium or NTTypography.BodySmall => emphasized ? NTRobotoFonts.Medium : NTRobotoFonts.Regular,
        _ => throw new ArgumentOutOfRangeException(nameof(role), role, null)
    };

    /// <summary>Font size of a role in dp.</summary>
    internal static float Size(NTTypography role) => role switch {
        NTTypography.LabelSmall => 11f,
        NTTypography.LabelMedium or NTTypography.BodySmall => 12f,
        NTTypography.LabelLarge or NTTypography.BodyMedium or NTTypography.TitleSmall => 14f,
        NTTypography.BodyLarge or NTTypography.TitleMedium => 16f,
        NTTypography.TitleLarge => 22f,
        _ => throw new ArgumentOutOfRangeException(nameof(role), role, null)
    };

    /// <summary>Line height of a role in dp.</summary>
    internal static float LineHeight(NTTypography role) => role switch {
        NTTypography.LabelSmall or NTTypography.LabelMedium or NTTypography.BodySmall => 16f,
        NTTypography.LabelLarge or NTTypography.BodyMedium or NTTypography.TitleSmall => 20f,
        NTTypography.BodyLarge or NTTypography.TitleMedium => 24f,
        NTTypography.TitleLarge => 28f,
        _ => throw new ArgumentOutOfRangeException(nameof(role), role, null)
    };

    /// <summary>Baseline y that centers Roboto text of <paramref name="fontSize" /> inside a line starting at <paramref name="lineTop" />. All values in pixels.</summary>
    internal static float TextBaseline(float lineTop, float lineHeight, float fontSize) => lineTop + ((lineHeight - (fontSize * (RobotoAscent + RobotoDescent))) / 2f) + (fontSize * RobotoAscent);

    /// <summary>Tick, legend and radial label color: OnSurfaceVariant while the chart <c>TextColor</c> is left at its OnSurface default, otherwise the chart <c>TextColor</c>.</summary>
    internal static SKColor SecondaryTextColor<TData>(IChart<TData> chart) where TData : class => chart.GetThemeColor(chart.TextColor == NTColor.OnSurface ? NTColor.OnSurfaceVariant : chart.TextColor);

    /// <summary>WCAG 2.2 contrast ratio between two opaque colors.</summary>
    internal static float ContrastRatio(SKColor first, SKColor second) {
        var a = RelativeLuminance(first);
        var b = RelativeLuminance(second);
        return (Math.Max(a, b) + 0.05f) / (Math.Min(a, b) + 0.05f);
    }

    /// <summary>Returns black or white, whichever has the higher WCAG contrast against <paramref name="background" />.</summary>
    internal static SKColor ContrastingTextColor(SKColor background) => ContrastRatio(background, SKColors.Black) >= ContrastRatio(background, SKColors.White) ? SKColors.Black : SKColors.White;

    private static float RelativeLuminance(SKColor color) => (0.2126f * Linear(color.Red)) + (0.7152f * Linear(color.Green)) + (0.0722f * Linear(color.Blue));

    private static float Linear(byte channel) {
        var value = channel / 255f;
        return value <= 0.03928f ? value / 12.92f : MathF.Pow((value + 0.055f) / 1.055f, 2.4f);
    }
}
