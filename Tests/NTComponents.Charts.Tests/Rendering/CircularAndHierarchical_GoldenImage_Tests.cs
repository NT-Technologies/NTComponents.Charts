namespace NTComponents.Charts.Tests.Rendering;

public class CircularAndHierarchical_GoldenImage_Tests : GoldenChartTestBase {
    [Fact]
    public async Task Pie_FourSlicesWithLabels_MatchesGolden() {
        var chart = Chart(c => c.Valued<NTPieSeries<GoldenRow>>(GoldenData.Slices(), GoldenSelectors.Category, GoldenSelectors.A, ("ExplosionOnEntry", false)));

        await VerifyAsync("pie", chart);
    }

    [Fact]
    public async Task Donut_InnerRadiusRatio_MatchesGolden() {
        var chart = Chart(c => c.Valued<NTPieSeries<GoldenRow>>(GoldenData.Slices(), GoldenSelectors.Category, GoldenSelectors.A, ("InnerRadiusRatio", 0.55f), ("ExplosionOnEntry", false)));

        await VerifyAsync("donut", chart);
    }

    [Fact]
    public async Task Radar_ThreeSeries_MatchesGolden() {
        var spokes = GoldenData.Spokes();
        var chart = Chart(c => c
            .Valued<NTRadarSeries<GoldenRow>>(spokes, GoldenSelectors.Category, GoldenSelectors.A, ("MaxValue", 30m))
            .Valued<NTRadarSeries<GoldenRow>>(spokes, GoldenSelectors.Category, GoldenSelectors.B, ("MaxValue", 30m))
            .Valued<NTRadarSeries<GoldenRow>>(spokes, GoldenSelectors.Category, GoldenSelectors.C, ("MaxValue", 30m)));

        await VerifyAsync("radar", chart);
    }

    [Fact]
    public async Task TreeMap_GroupedWithGroupLabels_MatchesGolden() {
        var chart = Chart(c => c.Valued<NTTreeMapSeries<GoldenRow>>(GoldenData.Hierarchy(), GoldenSelectors.Category, GoldenSelectors.Weight,
            ("GroupSelector", (Func<GoldenRow, string?>)(r => r.Group)),
            ("GroupSelector", (Func<GoldenRow, string?>)(r => r.Group)),
            ("LeafLabelSelector", (Func<GoldenRow, string?>)(r => r.Leaf)),
            ("GroupLevelLabels", (IReadOnlyList<string>)["Category"]),
            ("ColorSelector", GoldenSelectors.TreeMapColor)));

        await VerifyAsync("treemap-grouped", chart);
    }

    [Fact]
    public async Task BubblePack_FrozenAfterFixedSteps_MatchesGolden() {
        var chart = Chart(c => c.Valued<NTBubblePackSeries<GoldenRow>>(GoldenData.Hierarchy(), GoldenSelectors.Category, GoldenSelectors.Weight,
            ("GroupSelector", (Func<GoldenRow, string?>)(r => r.Group)),
            ("LeafLabelSelector", (Func<GoldenRow, string?>)(r => r.Leaf)),
            ("ColorSelector", GoldenSelectors.BubbleColor),
            ("GravityStrength", 0f),
            ("VelocityDamping", 0f)));

        await VerifyAsync("bubblepack-frozen", chart, prepare: FreezeBubblePhysicsAsync);
    }
}
