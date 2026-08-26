using SkiaSharp;

namespace NTComponents.Charts.Core;

internal static class NTRobotoFonts {

    private const string ResourcePrefix = "NTComponents.Charts.Fonts.";

    internal static SKTypeface Bold { get; } = Load("Roboto-Bold.ttf");

    internal static SKTypeface Medium { get; } = Load("Roboto-Medium.ttf");

    internal static SKTypeface Regular { get; } = Load("Roboto-Regular.ttf");

    private static SKTypeface Load(string fileName) {
        using var stream = typeof(NTRobotoFonts).Assembly.GetManifestResourceStream($"{ResourcePrefix}{fileName}")
            ?? throw new InvalidOperationException($"Embedded Roboto font resource '{fileName}' was not found.");
        using var data = SKData.Create(stream);
        return SKTypeface.FromData(data)
            ?? throw new InvalidOperationException($"Embedded Roboto font resource '{fileName}' is invalid.");
    }
}
