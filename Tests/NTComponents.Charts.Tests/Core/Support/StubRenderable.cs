using NTComponents.Charts.Core;
using SkiaSharp;

namespace NTComponents.Charts.Tests.ChartCore;

/// <summary>Renderable that records its calls and optionally consumes layout space from the area it is given.</summary>
public sealed class StubRenderable(RenderOrdered order, string name, List<string>? log = null, float consumeTop = 0f) : IRenderable {
    public RenderOrdered RenderOrder { get; } = order;
    public int RenderCount { get; private set; }
    public int InvalidateCount { get; private set; }
    public SKRect? LastArea { get; private set; }
    public NTRenderContext? LastContext { get; private set; }

    public void Invalidate() => InvalidateCount++;

    public SKRect Render(NTRenderContext context, SKRect renderArea) {
        RenderCount++;
        LastArea = renderArea;
        LastContext = context;
        log?.Add(name);
        return new SKRect(renderArea.Left, renderArea.Top + consumeTop, renderArea.Right, renderArea.Bottom);
    }

    public void Dispose() { }
}
