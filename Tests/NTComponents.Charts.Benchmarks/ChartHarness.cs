using System.Reflection;
using Bunit.JSInterop;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components.Web;
using NTComponents.Charts.Core;
using NTComponents.Charts.Core.Series;
using SkiaSharp;

namespace NTComponents.Charts.Benchmarks;

/// <summary>
///     Renders an <see cref="NTChart{TData}"/> headless through bUnit (Skia view stubbed) and exposes the protected paint and
///     mouse entry points as delegates created once, so no reflection happens inside a measured method.
/// </summary>
public sealed class ChartHarness : IDisposable {
    private const int Width = 1200;
    private const int Height = 600;
    private static readonly BindingFlags NonPublic = BindingFlags.Instance | BindingFlags.NonPublic;

    private readonly BunitContext _context = new();
    private readonly SKSurface _surface = SKSurface.Create(new SKImageInfo(Width, Height))!;
    private readonly Action<SKCanvas, SKImageInfo> _paint;
    private readonly Action<MouseEventArgs> _mouseMove;
    private readonly Action<MouseEventArgs> _mouseOut;
    private readonly Func<NTBaseSeries<BenchDatum>, bool> _requiresAnimationFrame;
    private readonly Func<SKRect> _plotArea;
    private readonly SKImageInfo[] _sizes = [new(Width, Height), new(Width - 200, Height - 100)];
    private readonly MouseEventArgs[] _moves;
    private int _sizeIndex;
    private int _moveIndex;

    public ChartHarness(RenderFragment seriesContent, bool legend = true, bool title = true) {
        _context.JSInterop.Mode = JSRuntimeMode.Loose;
        var module = _context.JSInterop.SetupModule("./_content/NTComponents.Charts/ntcomponents-charts.js");
        module.Setup<float>("getDevicePixelRatio").SetResult(1f);
        module.Setup<Dictionary<string, string?>>("getThemeColors", _ => true).SetResult([]);
        _context.ComponentFactories.AddStub(type => type.FullName is "SkiaSharp.Views.Blazor.SKGLView" or "SkiaSharp.Views.Blazor.SKCanvasView");

        // Paint and mouse handlers may call StateHasChanged, which requires the renderer's dispatcher. Adopting its
        // synchronization context on the benchmark thread runs those renders inline without per-call InvokeAsync overhead.
        // Every entry point re-adopts it because a benchmark class may own several harnesses (each with its own renderer).
        var dispatcher = _context.Renderer.Dispatcher;
        var syncContext = (SynchronizationContext)dispatcher.GetType().GetField("_context", NonPublic)!.GetValue(dispatcher)!;
        SynchronizationContext.SetSynchronizationContext(syncContext);

        Chart = _context.Render<NTChart<BenchDatum>>(parameters => {
            parameters.Add(p => p.ChildContent, (RenderFragment)(builder => {
                seriesContent(builder);
                if (legend) {
                    builder.OpenComponent<NTLegend<BenchDatum>>(10_000);
                    builder.CloseComponent();
                }
            }));
            if (title) {
                parameters.Add(p => p.TitleOptions, new NTTitleOptions("Benchmark chart"));
            }
        }).Instance;

        var paint = Chart.GetType().GetMethod("OnPaintSurface", NonPublic, [typeof(SKCanvas), typeof(SKImageInfo)])!.CreateDelegate<Action<SKCanvas, SKImageInfo>>(Chart);
        _paint = (canvas, info) => { SynchronizationContext.SetSynchronizationContext(syncContext); paint(canvas, info); };
        var mouseMove = Chart.GetType().GetMethod("OnMouseMove", NonPublic)!.CreateDelegate<Action<MouseEventArgs>>(Chart);
        _mouseMove = e => { SynchronizationContext.SetSynchronizationContext(syncContext); mouseMove(e); };
        var mouseOut = Chart.GetType().GetMethod("OnMouseOut", NonPublic)!.CreateDelegate<Action<MouseEventArgs>>(Chart);
        _mouseOut = e => { SynchronizationContext.SetSynchronizationContext(syncContext); mouseOut(e); };
        _requiresAnimationFrame = typeof(NTBaseSeries<BenchDatum>).GetProperty("RequiresAnimationFrame", NonPublic)!.GetMethod!.CreateDelegate<Func<NTBaseSeries<BenchDatum>, bool>>();

        _plotArea = typeof(NTChart<BenchDatum>).GetProperty("LastPlotArea", NonPublic)!.GetMethod!.CreateDelegate<Func<SKRect>>(Chart);
        Paint();
        var area = _plotArea();
        _moves = Enumerable.Range(0, 8).Select(i => new MouseEventArgs {
            OffsetX = area.Left + (area.Width * (i + 0.5) / 8),
            OffsetY = area.Top + (area.Height * (i % 2 == 0 ? 0.45 : 0.6))
        }).ToArray();
    }

    public NTChart<BenchDatum> Chart { get; }

    /// <summary>Gets the plot area measured by the last paint.</summary>
    public SKRect PlotArea => _plotArea();

    /// <summary>One full frame at the canonical size.</summary>
    public void Paint() => _paint(_surface.Canvas, _sizes[0]);

    /// <summary>One full frame, alternating between two surface sizes so axes, title and hit-test state recompute.</summary>
    public void PaintAlternatingSize() => _paint(_surface.Canvas, _sizes[_sizeIndex ^= 1]);

    /// <summary>Invalidates every series (as if data changed).</summary>
    public void InvalidateSeries() {
        foreach (var series in Chart.Series) {
            series.Invalidate();
        }
    }

    /// <summary>Invalidates the chart as a whole (all renderables and data caches).</summary>
    public void InvalidateChart() => Chart.Invalidate();

    /// <summary>Moves the mouse to the next of eight positions across the plot.</summary>
    public void MoveMouse() => _mouseMove(_moves[_moveIndex++ & 7]);

    /// <summary>Moves the mouse to a specific plot position (fractions of the plot area).</summary>
    public void MoveMouseTo(double fx, double fy) {
        var area = PlotArea;
        _mouseMove(new MouseEventArgs { OffsetX = area.Left + (area.Width * fx), OffsetY = area.Top + (area.Height * fy) });
    }

    public void MouseOut() => _mouseOut(new MouseEventArgs());

    /// <summary>Repaints until no series requests an animation frame (physics/animations settled). Returns whether it settled.</summary>
    public bool SettleAnimations(int maxFrames = 1500) {
        for (var i = 0; i < maxFrames; i++) {
            Paint();
            if (!Chart.Series.Any(_requiresAnimationFrame)) {
                return true;
            }
            Thread.Sleep(4);
        }
        return false;
    }

    public void Dispose() {
        _surface.Dispose();
        _context.Dispose();
    }

    /// <summary>Builds a fragment that adds one series component with the given attributes.</summary>
    public static RenderFragment Series(Type seriesType, IEnumerable<BenchDatum> data, params (string Name, object Value)[] attributes) => builder => {
        var seq = 0;
        builder.OpenComponent(seq++, seriesType);
        builder.AddAttribute(seq++, "Data", data);
        builder.AddAttribute(seq++, "AnimationEnabled", false);
        foreach (var (name, value) in attributes) {
            builder.AddAttribute(seq++, name, value);
        }
        builder.CloseComponent();
    };

    /// <summary>Concatenates fragments, giving each a distinct sequence range.</summary>
    public static RenderFragment Combine(params RenderFragment[] fragments) => builder => {
        for (var i = 0; i < fragments.Length; i++) {
            builder.OpenRegion(i);
            fragments[i](builder);
            builder.CloseRegion();
        }
    };
}
