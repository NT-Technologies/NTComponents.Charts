using SkiaSharp;

namespace NTComponents.Charts.Core;

/// <summary>
///     Fits label text into a pixel width by wrapping at spaces and ending the last line with an ellipsis.
/// </summary>
public static class NTLabelText {
    /// <summary>The character appended to text that was shortened to fit.</summary>
    public const string Ellipsis = "…";

    /// <summary>Returns <paramref name="text"/> unchanged when it fits, otherwise its longest prefix that fits with a trailing ellipsis.</summary>
    public static string Ellipsize(SKFont font, string text, float maxWidth) {
        if (font.MeasureText(text) <= maxWidth) {
            return text;
        }

        var fit = FitLength(font, text, maxWidth - font.MeasureText(Ellipsis));
        return text[..fit].TrimEnd() + Ellipsis;
    }

    /// <summary>Breaks <paramref name="text"/> into at most <paramref name="maxLines"/> lines no wider than <paramref name="maxWidth"/>.</summary>
    public static string[] Wrap(SKFont font, string text, float maxWidth, int maxLines) {
        var lines = new List<string>(maxLines);
        var rest = text.Trim();
        while (lines.Count < maxLines - 1 && font.MeasureText(rest) > maxWidth) {
            var fit = Math.Max(1, FitLength(font, rest, maxWidth));
            var space = rest.LastIndexOf(' ', Math.Min(fit, rest.Length - 1));
            var cut = space > 0 ? space : fit;
            lines.Add(rest[..cut].TrimEnd());
            rest = rest[cut..].TrimStart();
        }

        lines.Add(Ellipsize(font, rest, maxWidth));
        return [.. lines];
    }

    // The number of leading UTF-16 characters that fit, never splitting a surrogate pair.
    private static int FitLength(SKFont font, string text, float maxWidth) {
        var fit = maxWidth <= 0 ? 0 : Math.Min(text.Length, font.BreakText(text, maxWidth));
        return fit > 0 && char.IsHighSurrogate(text[fit - 1]) ? fit - 1 : fit;
    }
}
