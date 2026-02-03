using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NTComponents.Charts.Core;

public interface IRenderable : IDisposable {

    public SKRect Render(NTRenderContext context, SKRect renderArea);
    public void Invalidate();
}
