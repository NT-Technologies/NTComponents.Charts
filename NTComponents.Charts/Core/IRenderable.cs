using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NTComponents.Charts.Core;

public interface IRenderable : IDisposable {
    /// <summary>
    ///   Gets the render order of this component.
    /// </summary>
    RenderOrdered RenderOrder { get; }

    /// <summary>
    ///   Renders the component within the given render area.
    /// </summary>
    /// <param name="context">The render context for the current frame.</param>
    /// <param name="renderArea">The available area for rendering.</param>
    /// <returns>The new renderable area</returns>
    /// <remarks>
    ///  The returned SKRect represents the area that remains after this component has rendered. This allows for proper layout of multiple renderable components within the same chart. Note: some renderables may not update the render area and simply return the input renderArea.
    /// Series will likely return the same renderArea, while axes and legends will typically reduce the available area by their required space. 
    /// </remarks>
    SKRect Render(NTRenderContext context, SKRect renderArea);

    /// <summary>
    /// Invalidates the renderable, causing it to clear any cached data and recalculate them when it makes sense to.
    /// </summary>
    void Invalidate();
}
