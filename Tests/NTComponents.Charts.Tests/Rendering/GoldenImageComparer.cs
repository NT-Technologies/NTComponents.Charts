using System.Runtime.CompilerServices;
using SkiaSharp;

namespace NTComponents.Charts.Tests.Rendering;

/// <summary>
///     Compares rendered rasters against golden PNGs. A pixel counts as different when any channel differs by more than
///     <see cref="ChannelTolerance"/>; the image fails when more than <see cref="MaxDifferingPixelFraction"/> of its pixels differ.
///     Set <c>NTCHARTS_UPDATE_GOLDEN=1</c> to overwrite goldens instead of comparing.
/// </summary>
public static class GoldenImageComparer {
    /// <summary>Environment variable that switches the suite into golden-update mode.</summary>
    public const string UpdateVariable = "NTCHARTS_UPDATE_GOLDEN";

    /// <summary>
    ///     Per-channel tolerance. Anti-aliasing and sub-pixel rounding shift edge pixels by a few levels to a few tens of levels;
    ///     a recolored or moved shape shifts channels by well over 24 across whole regions.
    /// </summary>
    public const int ChannelTolerance = 24;

    /// <summary>
    ///     Maximum fraction of differing pixels (0.2% = 512 of 256,000 at 640x400). Enough headroom for glyph rasterization
    ///     drift across operating systems, small enough that a moved bar, line, marker or recolored series fails.
    /// </summary>
    public const double MaxDifferingPixelFraction = 0.002;

    private static readonly string? _sourceDirectory = GetSourceDirectory();

    /// <summary>Directory holding the golden PNG files.</summary>
    public static string GoldenDirectory { get; } = ResolveGoldenDirectory();

    /// <summary>Where actual and diff images are written when a comparison fails.</summary>
    public static string FailureDirectory { get; } = Path.Combine(AppContext.BaseDirectory, "golden-failures");

    /// <summary>True when the update variable is set to 1.</summary>
    public static bool UpdateRequested => Environment.GetEnvironmentVariable(UpdateVariable) == "1";

    /// <summary>Compares two rasters using the suite tolerance, optionally painting a diff visualization.</summary>
    public static GoldenComparison Compare(SKBitmap expected, SKBitmap actual, SKBitmap? diff = null) {
        if (expected.Width != actual.Width || expected.Height != actual.Height) {
            return new GoldenComparison(false, expected.Width * expected.Height, expected.Width * expected.Height, 255);
        }

        var expectedPixels = expected.Pixels;
        var actualPixels = actual.Pixels;
        var differing = 0;
        var maxDelta = 0;
        for (var i = 0; i < expectedPixels.Length; i++) {
            var e = expectedPixels[i];
            var a = actualPixels[i];
            var delta = Math.Max(Math.Max(Math.Abs(e.Red - a.Red), Math.Abs(e.Green - a.Green)), Math.Max(Math.Abs(e.Blue - a.Blue), Math.Abs(e.Alpha - a.Alpha)));
            maxDelta = Math.Max(maxDelta, delta);
            var isDifferent = delta > ChannelTolerance;
            if (isDifferent) {
                differing++;
            }
            diff?.SetPixel(i % actual.Width, i / actual.Width, isDifferent ? SKColors.Red : new SKColor((byte)(128 + (a.Red / 2)), (byte)(128 + (a.Green / 2)), (byte)(128 + (a.Blue / 2))));
        }
        return new GoldenComparison(true, differing, expectedPixels.Length, maxDelta);
    }

    /// <summary>Fails the current test unless <paramref name="actual"/> matches the named golden (or writes it in update mode).</summary>
    public static void AssertMatchesGolden(string name, SKBitmap actual, string? goldenDirectory = null, bool? update = null, string? failureDirectory = null) {
        var directory = goldenDirectory ?? GoldenDirectory;
        var path = Path.Combine(directory, name + ".png");
        if (update ?? UpdateRequested) {
            Directory.CreateDirectory(directory);
            Save(actual, path);
            return;
        }

        if (!File.Exists(path)) {
            Xunit.Assert.Fail($"Golden image '{path}' does not exist. Review the rendering and generate it with {UpdateVariable}=1.");
        }

        using var expected = SKBitmap.Decode(path);
        using var diff = new SKBitmap(actual.Width, actual.Height);
        var result = expected is null ? new GoldenComparison(false, 1, 1, 255) : Compare(expected, actual, diff);
        if (result.Passes) {
            return;
        }

        var failures = failureDirectory ?? FailureDirectory;
        Directory.CreateDirectory(failures);
        var actualPath = Path.Combine(failures, name + ".actual.png");
        var diffPath = Path.Combine(failures, name + ".diff.png");
        Save(actual, actualPath);
        Save(diff, diffPath);
        Xunit.Assert.Fail(result.SameSize
            ? $"Rendering '{name}' differs from golden: {result.DifferingPixels} of {result.TotalPixels} pixels ({result.DifferingFraction:P3}) differ by more than {ChannelTolerance} (allowed {MaxDifferingPixelFraction:P3}), max channel delta {result.MaxChannelDelta}. Golden: {path}. Actual: {actualPath}. Diff: {diffPath}."
            : $"Rendering '{name}' has size {actual.Width}x{actual.Height} but golden is {expected?.Width}x{expected?.Height}. Golden: {path}. Actual: {actualPath}.");
    }

    private static void Save(SKBitmap bitmap, string path) {
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        using var file = File.Create(path);
        data.SaveTo(file);
    }

    private static string? GetSourceDirectory([CallerFilePath] string path = "") => string.IsNullOrEmpty(path) ? null : Path.GetDirectoryName(path);

    private static string ResolveGoldenDirectory() {
        // CallerFilePath is rewritten by deterministic/CI source path mapping, so fall back to walking up from the binaries.
        if (_sourceDirectory is not null && Directory.Exists(_sourceDirectory)) {
            return Path.Combine(_sourceDirectory, "Golden");
        }
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent) {
            var candidate = Path.Combine(directory.FullName, "Tests", "NTComponents.Charts.Tests", "Rendering", "Golden");
            if (Directory.Exists(candidate)) {
                return candidate;
            }
        }
        return Path.Combine(AppContext.BaseDirectory, "Golden");
    }
}
