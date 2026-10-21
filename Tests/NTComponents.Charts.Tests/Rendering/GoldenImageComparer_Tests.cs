using SkiaSharp;

namespace NTComponents.Charts.Tests.Rendering;

public sealed class GoldenImageComparer_Tests : IDisposable {
    private readonly string _root = Path.Combine(Path.GetTempPath(), "ntcharts-comparer-" + Guid.NewGuid().ToString("N"));

    public void Dispose() {
        if (Directory.Exists(_root)) {
            Directory.Delete(_root, true);
        }
    }

    [Fact]
    public void MissingGolden_Fails_InsteadOfCreatingIt() {
        using var actual = Solid(SKColors.White);

        var act = () => GoldenImageComparer.AssertMatchesGolden("missing", actual, _root, update: false);

        act.Should().Throw<Exception>().WithMessage("*does not exist*");
        File.Exists(Path.Combine(_root, "missing.png")).Should().BeFalse();
    }

    [Fact]
    public void UpdateMode_WritesGoldenThatThenMatches() {
        using var actual = Solid(SKColors.SteelBlue);

        GoldenImageComparer.AssertMatchesGolden("written", actual, _root, update: true);

        File.Exists(Path.Combine(_root, "written.png")).Should().BeTrue();
        GoldenImageComparer.AssertMatchesGolden("written", actual, _root, update: false);
    }

    [Fact]
    public void SubToleranceNoiseOnEveryPixel_Passes() {
        using var expected = Solid(new SKColor(100, 100, 100));
        using var actual = Solid(new SKColor(100 + GoldenImageComparer.ChannelTolerance, 100, 100));

        GoldenImageComparer.Compare(expected, actual).Passes.Should().BeTrue();
    }

    [Fact]
    public void RecoloredRegionAboveBudget_FailsAndWritesActualAndDiffImages() {
        using var expected = Solid(SKColors.White);
        GoldenImageComparer.AssertMatchesGolden("shape", expected, _root, update: true);
        using var actual = Solid(SKColors.White);
        for (var x = 0; x < 20; x++) {
            for (var y = 0; y < 20; y++) {
                actual.SetPixel(x, y, SKColors.Red);
            }
        }
        var failures = Path.Combine(_root, "failures");

        var act = () => GoldenImageComparer.AssertMatchesGolden("shape", actual, _root, update: false, failureDirectory: failures);

        act.Should().Throw<Exception>().WithMessage("*400 of 10000*shape.actual.png*shape.diff.png*");
        File.Exists(Path.Combine(failures, "shape.actual.png")).Should().BeTrue();
        File.Exists(Path.Combine(failures, "shape.diff.png")).Should().BeTrue();
    }

    [Fact]
    public void FewDifferingPixelsWithinBudget_Passes() {
        using var expected = Solid(SKColors.White);
        using var actual = Solid(SKColors.White);
        for (var x = 0; x < 20; x++) {
            actual.SetPixel(x, 0, SKColors.Black);
        }

        var result = GoldenImageComparer.Compare(expected, actual);

        result.DifferingPixels.Should().Be(20);
        result.Passes.Should().BeTrue("20 of 10,000 pixels is 0.2%, exactly the budget");
    }

    [Fact]
    public void DifferentDimensions_Fail() {
        using var expected = Solid(SKColors.White);
        using var actual = new SKBitmap(50, 100);

        GoldenImageComparer.Compare(expected, actual).Passes.Should().BeFalse();
    }

    private static SKBitmap Solid(SKColor color) {
        var bitmap = new SKBitmap(100, 100);
        bitmap.Erase(color);
        return bitmap;
    }
}
