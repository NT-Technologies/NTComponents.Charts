using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using NTComponents.Charts.Core.Axes;
using NTComponents.Charts.Core.Series;
using SkiaSharp;
using SkiaSharp.Views.Blazor;

namespace NTComponents.Charts.Core;

public partial class NTChart<TData> {
    private RenderFragment RenderCanvas => builder => {
        builder.OpenComponent(0, EnableHardwareAcceleration ? typeof(SKGLView) : typeof(SKCanvasView));
        builder.AddAttribute(1, "EnableRenderLoop", _renderLoopEnabled);
        builder.AddAttribute(2, "IgnorePixelScaling", false);
        if (EnableHardwareAcceleration) {
            builder.AddAttribute(3, "OnPaintSurface", new Action<SKPaintGLSurfaceEventArgs>(OnPaintSurface));
        }
        else {
            builder.AddAttribute(4, "OnPaintSurface", new Action<SKPaintSurfaceEventArgs>(OnPaintSurface));
        }
        builder.AddAttribute(5, "style", "width: 100%; height: 100%; pointer-events: none;");
        builder.AddComponentReferenceCapture(6, component => _skiaView = (IComponent)component);
        builder.CloseComponent();
    };

    internal void RequestDraw(bool geometryChanged = false) {
        if (DisposalStarted) {
            return;
        }
        if (geometryChanged) {
            _hitTestDirty = true;
            _axesPictureDirty = true;
            _titlePictureDirty = true;
            Legend?.Invalidate();
        }
        _drawRequested = true;
        if (_viewReady && _isVisible && !_drawQueued) {
            FlushDrawRequest();
        }
    }

    private async void FlushDrawRequest() {
        _drawQueued = true;
        try {
            await InvokeAsync(async () => {
                // Batch parameter/interaction changes and leave a synchronous Skia paint callback first.
                await Task.Yield();
                _drawQueued = false;
                if (DisposalStarted || !_isVisible || !_drawRequested || !OperatingSystem.IsBrowser()) {
                    return;
                }
                _drawRequested = false;
                switch (_skiaView) {
                    case SKGLView glView:
                        glView.Invalidate();
                        break;
                    case SKCanvasView canvasView:
                        canvasView.Invalidate();
                        break;
                }
            });
        }
        catch (Exception error) when (!DisposalStarted) {
            await DispatchExceptionAsync(error);
        }
        catch (Exception) when (DisposalStarted) {
            // A queued draw can finish after the renderer has released the view.
        }
    }

    private void SetRenderLoop(bool enabled) {
        if (_renderLoopEnabled != enabled && !DisposalStarted) {
            _renderLoopEnabled = enabled;
            if (_viewReady) {
                StateHasChanged();
            }
        }
    }

    private void InvalidateAppearance() {
        foreach (var renderables in _renderablesByOrder.Values) {
            foreach (var renderable in renderables) {
                if (renderable is not NTBaseSeries<TData>) {
                    renderable.Invalidate();
                }
            }
        }
        _axesPictureDirty = true;
        _titlePictureDirty = true;
    }

    /// <summary>Receives the latest mouse position coalesced to a browser frame.</summary>
    /// <param name="e">The native mouse event.</param>
    [JSInvokable]
    public Task OnNativeMouseMove(MouseEventArgs e) => InvokeAsync(() => {
        if (!DisposalStarted && _isVisible) {
            OnMouseMove(e);
        }
    });

    /// <summary>Updates draw scheduling and pixel density after viewport changes.</summary>
    /// <param name="visible">Whether the chart is visible in the viewport and document.</param>
    /// <param name="density">The current device pixel ratio.</param>
    [JSInvokable]
    public Task OnNativeViewportChanged(bool visible, float density) => InvokeAsync(() => {
        if (DisposalStarted) {
            return;
        }
        _isVisible = visible;
        if (float.IsFinite(density) && density > 0 && Density != density) {
            Density = density;
            InvalidateAppearance();
        }
        if (!visible) {
            SetRenderLoop(false);
        }
        RequestDraw(geometryChanged: true);
    });

    private NTRenderContext RecordingContext(NTRenderContext context, SKCanvas canvas, SKRect plotArea) => new() {
        Canvas = canvas,
        DefaultFont = context.DefaultFont,
        RegularFont = context.RegularFont,
        Density = context.Density,
        Info = context.Info,
        TextColor = context.TextColor,
        TotalArea = context.TotalArea,
        PlotArea = plotArea
    };

    private SKRect RenderTitle(NTRenderContext context, SKRect area) {
        if (_title is null) {
            return area;
        }
        if (_titlePicture is null || _titlePictureDirty || _titlePictureArea != area) {
            using var recorder = new SKPictureRecorder();
            var canvas = recorder.BeginRecording(context.TotalArea);
            _titleRemainingArea = _title.Render(RecordingContext(context, canvas, context.PlotArea), area);
            var picture = recorder.EndRecording();
            _titlePicture?.Dispose();
            _titlePicture = picture;
            _titlePictureArea = area;
            _titlePictureDirty = false;
        }
        context.Canvas.DrawPicture(_titlePicture);
        return _titleRemainingArea;
    }

    private SKRect RenderAxes(NTRenderContext context, SKRect area) {
        var key = (area, GetXRange(XAxis as NTAxisOptions<TData>, true), GetYRange(YAxis as NTAxisOptions<TData>, true),
            SecondaryYAxis is null ? ((decimal Min, decimal Max)?)null : GetYRange(SecondaryYAxis as NTAxisOptions<TData>, true));
        if (_axesPicture is null || _axesPictureDirty || _axesPictureKey != key) {
            var plotArea = area;
            if (YAxis is NTAxisOptions<TData> primaryYAxis) {
                plotArea = primaryYAxis.Measure(context, plotArea);
            }
            if (SecondaryYAxis is NTAxisOptions<TData> secondaryYAxis) {
                plotArea = secondaryYAxis.Measure(context, plotArea);
            }
            if (XAxis is NTAxisOptions<TData> xAxis) {
                plotArea = xAxis.Measure(context, plotArea);
            }
            using var recorder = new SKPictureRecorder();
            var canvas = recorder.BeginRecording(context.TotalArea);
            var recording = RecordingContext(context, canvas, plotArea);
            XAxis.Render(recording, new SKRect(plotArea.Left, plotArea.Top, plotArea.Right, area.Bottom));
            YAxis.Render(recording, new SKRect(area.Left, plotArea.Top, plotArea.Right, plotArea.Bottom));
            SecondaryYAxis?.Render(recording, new SKRect(plotArea.Left, plotArea.Top, area.Right, plotArea.Bottom));
            var picture = recorder.EndRecording();
            _axesPicture?.Dispose();
            _axesPicture = picture;
            _axesPictureKey = key;
            _cachedPlotArea = plotArea;
            _axesPictureDirty = false;
        }
        context.Canvas.DrawPicture(_axesPicture);
        context.PlotArea = _cachedPlotArea;
        LastPlotArea = _cachedPlotArea;
        return _cachedPlotArea;
    }
}
