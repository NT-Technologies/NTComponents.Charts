using SkiaSharp;

namespace NTComponents.Charts.Tests.Rendering;

/// <summary>
///     Stable theme palettes returned by the stubbed <c>getThemeColors</c> JS call. <see cref="Create" /> is the NTComponents fallback
///     light scheme (<c>NTTheme.runtime.ts</c>) and <see cref="Dark" /> is the site's <c>dark.css</c>, so golden images and contrast
///     tests see real Material 3 role values.
/// </summary>
public static class GoldenThemeColors {
    private static readonly Dictionary<string, string> _light = new() {
        ["Primary"] = "#545A92", ["SurfaceTint"] = "#545A92", ["OnPrimary"] = "#FFFFFF", ["PrimaryContainer"] = "#E0E0FF",
        ["OnPrimaryContainer"] = "#3C4279", ["Secondary"] = "#5C5D72", ["OnSecondary"] = "#FFFFFF", ["SecondaryContainer"] = "#E1E0F9",
        ["OnSecondaryContainer"] = "#444559", ["Tertiary"] = "#78536B", ["OnTertiary"] = "#FFFFFF", ["TertiaryContainer"] = "#FFD7EF",
        ["OnTertiaryContainer"] = "#5E3C53", ["Error"] = "#BA1A1A", ["OnError"] = "#FFFFFF", ["ErrorContainer"] = "#FFDAD6",
        ["OnErrorContainer"] = "#93000A", ["Background"] = "#FBF8FF", ["OnBackground"] = "#1B1B21", ["Surface"] = "#FBF8FF",
        ["OnSurface"] = "#1B1B21", ["SurfaceVariant"] = "#E3E1EC", ["OnSurfaceVariant"] = "#46464F", ["Outline"] = "#777680",
        ["OutlineVariant"] = "#C7C5D0", ["Shadow"] = "#000000", ["Scrim"] = "#000000", ["InverseSurface"] = "#303036",
        ["InverseOnSurface"] = "#F2EFF7", ["InversePrimary"] = "#BDC2FF", ["PrimaryFixed"] = "#E0E0FF", ["OnPrimaryFixed"] = "#0F154B",
        ["PrimaryFixedDim"] = "#BDC2FF", ["OnPrimaryFixedVariant"] = "#3C4279", ["SecondaryFixed"] = "#E1E0F9", ["OnSecondaryFixed"] = "#181A2C",
        ["SecondaryFixedDim"] = "#C4C4DD", ["OnSecondaryFixedVariant"] = "#444559", ["TertiaryFixed"] = "#FFD7EF", ["OnTertiaryFixed"] = "#2E1126",
        ["TertiaryFixedDim"] = "#E7B9D5", ["OnTertiaryFixedVariant"] = "#5E3C53", ["SurfaceDim"] = "#DBD9E0", ["SurfaceBright"] = "#FBF8FF",
        ["SurfaceContainerLowest"] = "#FFFFFF", ["SurfaceContainerLow"] = "#F5F2FA", ["SurfaceContainer"] = "#EFEDF4", ["SurfaceContainerHigh"] = "#EAE7EF",
        ["SurfaceContainerHighest"] = "#E4E1E9", ["Info"] = "#435E91", ["OnInfo"] = "#FFFFFF", ["InfoContainer"] = "#D7E2FF",
        ["OnInfoContainer"] = "#2A4677", ["Success"] = "#316A42", ["OnSuccess"] = "#FFFFFF", ["SuccessContainer"] = "#B3F1BE",
        ["OnSuccessContainer"] = "#16512C", ["Warning"] = "#6F5D0D", ["OnWarning"] = "#FFFFFF", ["WarningContainer"] = "#FBE186",
        ["OnWarningContainer"] = "#554500", ["Assert"] = "#7C4E7E", ["OnAssert"] = "#FFFFFF", ["AssertContainer"] = "#FFD6FC",
        ["OnAssertContainer"] = "#623765"
    };

    private static readonly Dictionary<string, string> _dark = new() {
        ["Primary"] = "#FFB4A6", ["SurfaceTint"] = "#FFB4A6", ["OnPrimary"] = "#561E15", ["PrimaryContainer"] = "#733429",
        ["OnPrimaryContainer"] = "#FFDAD3", ["Secondary"] = "#B5C4FF", ["OnSecondary"] = "#1C2D61", ["SecondaryContainer"] = "#344479",
        ["OnSecondaryContainer"] = "#DCE1FF", ["Tertiary"] = "#81D3DF", ["OnTertiary"] = "#00363C", ["TertiaryContainer"] = "#004F57",
        ["OnTertiaryContainer"] = "#9DF0FC", ["Error"] = "#FFB4AB", ["OnError"] = "#690005", ["ErrorContainer"] = "#93000A",
        ["OnErrorContainer"] = "#FFDAD6", ["Background"] = "#1A1110", ["OnBackground"] = "#F1DFDB", ["Surface"] = "#1A1110",
        ["OnSurface"] = "#F1DFDB", ["SurfaceVariant"] = "#534340", ["OnSurfaceVariant"] = "#D8C2BD", ["Outline"] = "#A08C89",
        ["OutlineVariant"] = "#534340", ["Shadow"] = "#000000", ["Scrim"] = "#000000", ["InverseSurface"] = "#F1DFDB",
        ["InverseOnSurface"] = "#392E2C", ["InversePrimary"] = "#904B3E", ["PrimaryFixed"] = "#FFDAD3", ["OnPrimaryFixed"] = "#3A0A04",
        ["PrimaryFixedDim"] = "#FFB4A6", ["OnPrimaryFixedVariant"] = "#733429", ["SecondaryFixed"] = "#DCE1FF", ["OnSecondaryFixed"] = "#02174B",
        ["SecondaryFixedDim"] = "#B5C4FF", ["OnSecondaryFixedVariant"] = "#344479", ["TertiaryFixed"] = "#9DF0FC", ["OnTertiaryFixed"] = "#001F23",
        ["TertiaryFixedDim"] = "#81D3DF", ["OnTertiaryFixedVariant"] = "#004F57", ["SurfaceDim"] = "#1A1110", ["SurfaceBright"] = "#423735",
        ["SurfaceContainerLowest"] = "#140C0B", ["SurfaceContainerLow"] = "#231918", ["SurfaceContainer"] = "#271D1C", ["SurfaceContainerHigh"] = "#322826",
        ["SurfaceContainerHighest"] = "#3D3230", ["Success"] = "#A2D399", ["OnSuccess"] = "#0C390E", ["SuccessContainer"] = "#255023",
        ["OnSuccessContainer"] = "#BDF0B3", ["Info"] = "#A9C8FF", ["OnInfo"] = "#07305F", ["InfoContainer"] = "#264777",
        ["OnInfoContainer"] = "#D6E3FF", ["Warning"] = "#D8C770", ["OnWarning"] = "#383000", ["WarningContainer"] = "#514700",
        ["OnWarningContainer"] = "#F5E389", ["Assert"] = "#DAB9F9", ["OnAssert"] = "#3E2459", ["AssertContainer"] = "#553B71",
        ["OnAssertContainer"] = "#F0DBFF"
    };

    /// <summary>Returns the fallback light scheme as a hex color for every <see cref="NTColor"/> token; unlisted tokens get a deterministic hue.</summary>
    public static Dictionary<string, string?> Create() => Build(_light);

    /// <summary>Returns the site dark scheme as a hex color for every <see cref="NTColor"/> token; unlisted tokens get a deterministic hue.</summary>
    public static Dictionary<string, string?> Dark() => Build(_dark);

    private static Dictionary<string, string?> Build(Dictionary<string, string> scheme) {
        var colors = new Dictionary<string, string?>();
        var names = Enum.GetNames<NTColor>();
        for (var i = 0; i < names.Length; i++) {
            colors[names[i]] = scheme.TryGetValue(names[i], out var hex) ? hex : SKColor.FromHsl((i * 47) % 360, 45, 62).ToString();
        }
        return colors;
    }
}
