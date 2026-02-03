using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NTComponents.Charts.Core;

public interface IRenderable : IDisposable {
    RenderOrdered RenderOrder { get; }

    SKRect Render(NTRenderContext context, SKRect renderArea);
    void Invalidate();
}
