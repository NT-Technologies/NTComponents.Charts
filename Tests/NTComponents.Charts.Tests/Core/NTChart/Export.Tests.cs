using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using NTComponents.Charts.Core;
using SkiaSharp;

namespace NTComponents.Charts.Tests.ChartCore;

public class NTChart_Export_Tests : ChartTestBase {
    private sealed record Download(string? FileName, byte[] Bytes);

    private readonly List<Download> _downloads = [];

    private IRenderedComponent<NTChart<Pt>> ExportChart(NTTitleOptions? title = null) {
        JSInterop.SetupVoid("NTComponents.downloadFileFromStream", invocation => {
            var stream = ((DotNetStreamReference)invocation.Arguments[1]!).Stream;
            using var copy = new MemoryStream();
            stream.CopyTo(copy);
            _downloads.Add(new Download((string?)invocation.Arguments[0], copy.ToArray()));
            return true;
        }).SetVoidResult();
        var chart = Render<NTChart<Pt>>(p => p
            .Add(c => c.TitleOptions, title)
            .Add(c => c.ChildContent, Combine(XAxis<double>(x => x.X, Args(("Visible", false))), YAxis(Args(("Visible", false))), Line())));
        chart.WaitForState(() => chart.Instance.GetThemeColor(NTColor.Surface) != SKColors.Black);
        // The chart only learns which interactions are enabled after its series register, which re-renders it asynchronously;
        // render once more here so the toolbar and cursor are in their final state before any test inspects them.
        chart.Render(_ => { });
        return chart;
    }

    [Fact]
    public async Task BeforeTheFirstFrame_ExportDoesNothing() {
        var chart = ExportChart();

        await chart.InvokeAsync(() => chart.Instance.ExportAsPngAsync());

        _downloads.Should().BeEmpty();
    }

    [Fact]
    public async Task Export_DownloadsAPngOfTheLastPaintedSizeNamedChartByDefault() {
        var chart = ExportChart();
        using var surface = await Paint(chart, 400, 250);

        await chart.InvokeAsync(() => chart.Instance.ExportAsPngAsync());

        var download = _downloads.Should().ContainSingle().Subject;
        download.FileName.Should().Be("chart.png");
        using var image = SKBitmap.Decode(download.Bytes);
        image.Width.Should().Be(400);
        image.Height.Should().Be(250);
        image.GetPixel(2, 2).Should().Be(SKColors.White, "the export re-renders the full chart including its background");
    }

    [Fact]
    public async Task Export_FileNameFollowsTheTitleOrTheExplicitArgument() {
        var chart = ExportChart(new NTTitleOptions("Quarterly Revenue"));
        using var surface = await Paint(chart);

        await chart.InvokeAsync(() => chart.Instance.ExportAsPngAsync());
        await chart.InvokeAsync(() => chart.Instance.ExportAsPngAsync("custom.png"));

        _downloads.Select(d => d.FileName).Should().Equal("Quarterly Revenue.png", "custom.png");
    }

    [Fact]
    public async Task ExportButton_TriggersTheDownload() {
        var chart = ExportChart();
        using var surface = await Paint(chart);

        await Fire(chart, h => h.Click(), "button[aria-label='Export chart as PNG']");

        chart.WaitForAssertion(() => _downloads.Should().ContainSingle());
    }

    [Fact]
    public async Task Export_ContainsTheSeriesDrawnAtTheSameResolutionAsTheCanvas() {
        var chart = ExportChart();
        using var surface = await Paint(chart);
        using var live = Bitmap(surface);

        await chart.InvokeAsync(() => chart.Instance.ExportAsPngAsync());
        using var exported = SKBitmap.Decode(_downloads.Single().Bytes);

        CountColor(exported, SKColors.Red, 40).Should().BeGreaterThan(50);
        CountColor(exported, SKColors.Red, 40).Should().BeCloseTo(CountColor(live, SKColors.Red, 40), (uint)(CountColor(live, SKColors.Red, 40) / 5));
    }
}
