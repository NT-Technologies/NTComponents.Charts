namespace NTComponents.Charts.Tests.Rendering;

/// <summary>Result of comparing two rasters pixel by pixel.</summary>
/// <param name="SameSize">Whether both images have identical dimensions.</param>
/// <param name="DifferingPixels">Pixels where any channel differs by more than the channel tolerance.</param>
/// <param name="TotalPixels">Pixels in the expected image.</param>
/// <param name="MaxChannelDelta">Largest single-channel difference observed.</param>
public sealed record GoldenComparison(bool SameSize, int DifferingPixels, int TotalPixels, int MaxChannelDelta) {
    /// <summary>Fraction of pixels that differ beyond the channel tolerance.</summary>
    public double DifferingFraction => TotalPixels == 0 ? 0 : (double)DifferingPixels / TotalPixels;

    /// <summary>Whether the images are considered visually equivalent.</summary>
    public bool Passes => SameSize && DifferingFraction <= GoldenImageComparer.MaxDifferingPixelFraction;
}
