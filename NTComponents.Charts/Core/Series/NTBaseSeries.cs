using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using SkiaSharp;

namespace NTComponents.Charts.Core.Series;

public abstract class NTBaseSeries<TData> : ComponentBase, ISeries where TData : class {

    /// <summary>
    ///     Gets or sets the duration of the animation.
    /// </summary>
    [Parameter]
    public TimeSpan AnimationDuration { get; set; } = TimeSpan.FromMilliseconds(500);

    /// <summary>
    ///     Gets or sets whether animation is enabled for this series.
    /// </summary>
    [Parameter]
    public bool AnimationEnabled { get; set; } = true;

    /// <summary>
    ///     Gets or sets the color of the series. If null or <see cref="TnTColor.None" />, a color will be chosen from the chart's palette.
    /// </summary>
    [Parameter]
    public TnTColor? Color { get; set; }

    /// <summary>
    ///     Gets the coordinate system of the series.
    /// </summary>
    public abstract ChartCoordinateSystem CoordinateSystem { get; }

    [Parameter]
    public IEnumerable<TData> Data { get; set; } = [];

    /// <summary>
    ///     Gets or sets the selector for the independent variable of the series.
    /// </summary>
    [Parameter, EditorRequired]
    public Func<TData, object> XValue { get; set; } = default!;


    /// <summary>
    ///     Gets the current hover factor (0.15 to 1.0) for animation.
    /// </summary>
    public float HoverFactor {
        get {
            // Target is 1.0 if this series is hovered OR if nothing is hovered. Target is 0.15 if another series is hovered.
            float target = (Chart.HoveredSeries == null || Chart.HoveredSeries == this) ? 1f : 0.15f;

            if (!AnimationEnabled) {
                return target;
            }

            if (Math.Abs(_targetHoverFactor - target) > 0.001f) {
                _startHoverFactor = _currentHoverFactor;
                _targetHoverFactor = target;
                _hoverAnimationStartTime = DateTime.Now;
            }

            if (_hoverAnimationStartTime == null) {
                return _currentHoverFactor = target;
            }

            var elapsed = DateTime.Now - _hoverAnimationStartTime.Value;
            var duration = Chart.HoverAnimationDuration;
            var progress = (float)(elapsed.TotalMilliseconds / duration.TotalMilliseconds);
            progress = Math.Clamp(progress, 0, 1);

            _currentHoverFactor = _startHoverFactor + ((_targetHoverFactor - _startHoverFactor) * progress);

            if (progress >= 1) {
                _hoverAnimationStartTime = null;
            }

            return _currentHoverFactor;
        }
    }

    /// <summary>
    ///     Returns true if the series is visible or currently animating visibility.
    /// </summary>
    public bool IsEffectivelyVisible => Visible || (VisibilityFactor > 0.001f);

    /// <summary>
    ///     Gets or sets a callback that allows for custom rendering of individual data points.
    /// </summary>
    [Parameter]
    public Action<NTDataPointRenderArgs<TData>>? OnDataPointRender { get; set; }

    [Parameter]
    public EventCallback<NTSeriesHoverEnterEventArgs<TData>> OnHoverEnter { get; set; }

    [Parameter]
    public EventCallback<NTSeriesHoverLeaveEventArgs<TData>> OnHoverLeave { get; set; }

    [Parameter]
    public EventCallback<NTSeriesVisibilityChangedEventArgs<TData>> OnVisibilityChanged { get; set; }

    [Parameter]
    public EventCallback<NTSeriesClickEventArgs<TData>> OnClick { get; set; }

    [Parameter]
    public EventCallback<NTSeriesPanStartEventArgs<TData>> OnPanStart { get; set; }

    [Parameter]
    public EventCallback<NTSeriesPanEventArgs<TData>> OnPan { get; set; }

    [Parameter]
    public EventCallback<NTSeriesPanEndEventArgs<TData>> OnPanEnd { get; set; }

    [Parameter]
    public EventCallback<NTSeriesZoomEventArgs<TData>> OnZoom { get; set; }

    [Parameter]
    public EventCallback<NTSeriesResetViewEventArgs<TData>> OnResetView { get; set; }

    /// <summary>
    ///     Gets or sets the text color of the series. If null or <see cref="TnTColor.None" />, a color will be chosen from the chart's palette.
    /// </summary>
    [Parameter]
    public TnTColor? TextColor { get; set; }

    /// <summary>
    ///     Gets or sets the title of the series.
    /// </summary>
    [Parameter]
    public string? Title { get; set; }

    /// <summary>
    ///     Gets or sets the background color of the tooltip for this series.
    /// </summary>
    [Parameter]
    public TnTColor? TooltipBackgroundColor { get; set; }

    /// <summary>
    ///     Gets or sets the text color of the tooltip for this series.
    /// </summary>
    [Parameter]
    public TnTColor? TooltipTextColor { get; set; }

    /// <summary>
    ///     Gets or sets an optional callback that can replace the default tooltip payload for this series.
    /// </summary>
    [Parameter]
    public Func<NTSeriesTooltipContext<TData>, TooltipInfo>? TooltipInfoSelector { get; set; }

    /// <summary>
    ///    Gets or sets the interaction modes enabled for this series.
    /// </summary>
    [Parameter]
    public ChartInteractions Interactions { get; set; } = ChartInteractions.None;

    /// <summary>
    ///     Gets the current visibility factor (0.0 to 1.0) for animation.
    /// </summary>
    public float VisibilityFactor {
        get {
            if (!AnimationEnabled) {
                return Visible ? 1f : 0f;
            }

            if (_visibilityAnimationStartTime == null) {
                return Visible ? 1f : 0f;
            }

            var elapsed = DateTime.Now - _visibilityAnimationStartTime.Value;
            var progress = (float)(elapsed.TotalMilliseconds / AnimationDuration.TotalMilliseconds);
            progress = Math.Clamp(progress, 0, 1);

            // Use simple linear for visibility factor transition
            _currentVisibility = _startVisibility + (((Visible ? 1f : 0f) - _startVisibility) * progress);

            if (progress >= 1) {
                _visibilityAnimationStartTime = null;
            }

            return _currentVisibility;
        }
    }

    /// <summary>
    ///     Gets or sets whether the series is visible.
    /// </summary>
    [Parameter]
    public bool Visible { get; set; } = true;

    /// <summary>
    ///     Gets the start time of the animation.
    /// </summary>
    protected DateTime AnimationStartTime { get; set; } = DateTime.Now;

    [CascadingParameter]
    protected NTChart<TData> Chart { get; set; } = default!;

    [CascadingParameter]
    private INestedSeriesParent<TData>? NestedSeriesParent { get; set; }

    /// <summary>
    ///     Gets or sets the previous data.
    /// </summary>
    protected IEnumerable<TData>? PreviousData { get; set; }

    private float _currentHoverFactor = 1f;
    private float _currentVisibility = 1f;
    private DateTime? _hoverAnimationStartTime;
    private bool _lastVisible = true;
    private float _startHoverFactor = 1f;

    private float _startVisibility = 1f;

    private float _targetHoverFactor = 1f;

    private DateTime? _visibilityAnimationStartTime;
    internal bool SuppressInteractionCallbacks { get; set; }

    public void Dispose() {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing) {
        if (disposing) {
            NestedSeriesParent?.UnregisterChildSeries(this);
            Chart?.UnregisterSeries(this);
            Chart?.UnregisterRenderable(this);
        }
    }

    /// <summary>
    ///     Performs a hit test on the series.
    /// </summary>
    /// <param name="point">     The mouse point.</param>
    /// <param name="renderArea">The plot area.</param>
    /// <returns>The index and data of the hit point, or null if no hit.</returns>
    public abstract (int Index, TData? Data)? HitTest(SKPoint point, SKRect renderArea);

    /// <inheritdoc />
    public virtual RenderOrdered RenderOrder => RenderOrdered.Series;

    /// <inheritdoc />
    public abstract SKRect Render(NTRenderContext context, SKRect renderArea);

    /// <inheritdoc />
    public virtual void Invalidate() => ResetAnimation();

    /// <summary>
    ///     Resets the animation to start from the beginning.
    /// </summary>
    public void ResetAnimation() => AnimationStartTime = DateTime.Now;

    /// <summary>
    ///     Returns the legend items for this series.
    /// </summary>
    /// <returns>The legend items.</returns>
    internal virtual IEnumerable<LegendItemInfo<TData>> GetLegendItems() {
        yield return new LegendItemInfo<TData> {
            Label = Title ?? $"Series {Chart.GetSeriesIndex(this) + 1}",
            Color = Chart.GetSeriesColor(this),
            Series = this,
            Key = Title ?? $"Series {Chart.GetSeriesIndex(this) + 1}",
            IsVisible = Visible
        };
    }

    /// <summary>
    ///     Gets the tooltip info for a specific data point.
    /// </summary>
    /// <param name="data">The data point.</param>
    /// <returns>The tooltip info.</returns>
    internal virtual TooltipInfo GetTooltipInfo(TData data) {
        var defaultInfo = new TooltipInfo {
            Header = null,
            Lines = [new TooltipLine { Label = Title ?? "Series", Value = string.Empty, Color = Chart.GetSeriesColor(this) }]
        };

        return ResolveTooltipInfo(data, defaultInfo);
    }

    TooltipInfo ISeries.GetTooltipInfo(object data) => GetTooltipInfo((TData)data);

    /// <summary>
    ///     Gets the total value of the series (used for partitioning areas in TreeMap/Circular charts).
    /// </summary>
    /// <returns>The total value of the series.</returns>
    internal virtual decimal GetTotalValue() => 0;

    /// <summary>
    ///     Gets the data bounds for the X axis.
    /// </summary>
    public virtual (double Min, double Max)? GetXRange() => null;

    /// <summary>
    ///     Gets the data bounds for the Y axis.
    /// </summary>
    /// <param name="xMin">The minimum X value to consider.</param>
    /// <param name="xMax">The maximum X value to consider.</param>
    public virtual (decimal Min, decimal Max)? GetYRange(double? xMin = null, double? xMax = null) => null;

    /// <summary>
    ///     Registers any categorical X values the series has.
    /// </summary>
    internal virtual void RegisterXValues(HashSet<object> values) { }

    /// <summary>
    ///     Registers any categorical Y values the series has.
    /// </summary>
    internal virtual void RegisterYValues(HashSet<object> values) { }

    /// <summary>
    ///     Toggles the visibility of a legend item.
    /// </summary>
    /// <param name="index">The index of the item, if applicable.</param>
    internal virtual void ToggleLegendItem(int? index) {
        if (index == null) {
            Visible = !Visible;
        }
    }

    internal virtual void ToggleLegendItem(LegendItemInfo<TData> item) => ToggleLegendItem(item.Index);

    internal virtual bool IsLegendItemHovered(LegendItemInfo<TData> item) {
        if (!item.InteractsWithChart) {
            return false;
        }

        return item.Index.HasValue
            ? (Chart.HoveredSeries == item.Series && Chart.HoveredPointIndex == item.Index.Value)
            : (Chart.HoveredSeries == item.Series);
    }

    internal virtual float GetLegendItemAlphaFactor(LegendItemInfo<TData> item) => HoverFactor;

    public virtual void HandleMouseDown(MouseEventArgs e) { }
    public virtual void HandleMouseMove(MouseEventArgs e) { }
    public virtual void HandleMouseUp(MouseEventArgs e) { }
    public virtual void HandleMouseWheel(WheelEventArgs e) { }
    public virtual void ResetView() {
        NotifyResetView(new NTSeriesResetViewEventArgs<TData> {
            Series = this
        });
    }

    public virtual (double Min, double Max)? GetViewXRange() => null;
    public virtual (decimal Min, decimal Max)? GetViewYRange() => null;

    /// <summary>
    ///     Returns true if the series is currently being panned.
    /// </summary>
    public virtual bool IsPanning => false;

    /// <summary>
    ///     Applies an overshoot effect to the progress.
    /// </summary>
    /// <param name="t">The progress value between 0 and 1.</param>
    /// <returns>The eased progress value.</returns>
    protected float BackEase(float t) {
        const float c1 = 1.70158f;
        const float c3 = c1 + 1;
        return 1 + (c3 * MathF.Pow(t - 1, 3)) + (c1 * MathF.Pow(t - 1, 2));
    }

    protected float GetAnimationProgress() {
        if (!AnimationEnabled) {
            return 1.0f;
        }

        var elapsed = DateTime.Now - AnimationStartTime;
        var progress = (float)(elapsed.TotalMilliseconds / AnimationDuration.TotalMilliseconds);
        return Math.Clamp(progress, 0, 1);
    }

    /// <summary>
    ///     Called when the data reference changes.
    /// </summary>
    protected virtual void OnDataChanged() {
        ResetAnimation();
        Chart?.InvalidateDataCaches();
    }

    protected void NotifyNestedParentSeriesChanged() => NestedSeriesParent?.NotifyChildSeriesChanged();

    internal void NotifyHoverEnter(NTSeriesHoverEnterEventArgs<TData> args) => NotifyCallback(OnHoverEnter, args);
    internal void NotifyHoverLeave(NTSeriesHoverLeaveEventArgs<TData> args) => NotifyCallback(OnHoverLeave, args);
    internal void NotifyVisibilityChanged(NTSeriesVisibilityChangedEventArgs<TData> args) => NotifyCallback(OnVisibilityChanged, args);
    internal void NotifyClick(NTSeriesClickEventArgs<TData> args) => NotifyCallback(OnClick, args);

    /// <summary>
    ///     Applies the optional tooltip selector to a default tooltip payload.
    /// </summary>
    /// <param name="data">The hovered data item.</param>
    /// <param name="defaultInfo">The default tooltip payload.</param>
    /// <param name="pointIndex">The hovered point index when available.</param>
    /// <param name="segmentLabel">The hovered segment label when available.</param>
    /// <param name="segmentValue">The hovered segment value when available.</param>
    /// <param name="segmentColor">The hovered segment color when available.</param>
    /// <returns>The final tooltip payload.</returns>
    protected TooltipInfo ResolveTooltipInfo(
        TData data,
        TooltipInfo defaultInfo,
        int? pointIndex = null,
        string? segmentLabel = null,
        decimal? segmentValue = null,
        SKColor? segmentColor = null) {
        if (TooltipInfoSelector is null) {
            return defaultInfo;
        }

        var customInfo = TooltipInfoSelector(new NTSeriesTooltipContext<TData> {
            Series = this,
            Data = data,
            PointIndex = pointIndex,
            DefaultInfo = defaultInfo,
            SegmentLabel = segmentLabel,
            SegmentValue = segmentValue,
            SegmentColor = segmentColor
        });

        return customInfo ?? defaultInfo;
    }
    internal void NotifyPanStart(NTSeriesPanStartEventArgs<TData> args) {
        if (!SuppressInteractionCallbacks) {
            NotifyCallback(OnPanStart, args);
        }
    }

    internal void NotifyPan(NTSeriesPanEventArgs<TData> args) {
        if (!SuppressInteractionCallbacks) {
            NotifyCallback(OnPan, args);
        }
    }

    internal void NotifyPanEnd(NTSeriesPanEndEventArgs<TData> args) {
        if (!SuppressInteractionCallbacks) {
            NotifyCallback(OnPanEnd, args);
        }
    }

    internal void NotifyZoom(NTSeriesZoomEventArgs<TData> args) {
        if (!SuppressInteractionCallbacks) {
            NotifyCallback(OnZoom, args);
        }
    }

    internal void NotifyResetView(NTSeriesResetViewEventArgs<TData> args) {
        if (!SuppressInteractionCallbacks) {
            NotifyCallback(OnResetView, args);
        }
    }

    private void NotifyCallback<TArgs>(EventCallback<TArgs> callback, TArgs args) where TArgs : class {
        if (!callback.HasDelegate) {
            return;
        }

        _ = InvokeAsync(() => callback.InvokeAsync(args));
    }

    protected override void OnInitialized() {
        base.OnInitialized();
        if (NestedSeriesParent is not null) {
            NestedSeriesParent.RegisterChildSeries(this);
            return;
        }

        if (Chart is null) {
            throw new ArgumentNullException(nameof(Chart), $"Series must be used within a {nameof(NTChart<TData>)}.");
        }
        Chart.RegisterSeries(this);
        Chart.RegisterRenderable(this);
    }

    protected override void OnParametersSet() {
        base.OnParametersSet();

        if (Visible != _lastVisible) {
            HandleVisibilityChanged();
            _lastVisible = Visible;
        }

        if (!ReferenceEquals(PreviousData, Data)) {
            OnDataChanged();
            PreviousData = Data;
        }
    }

    private void HandleVisibilityChanged() {
        _startVisibility = VisibilityFactor;
        _visibilityAnimationStartTime = DateTime.Now;

        // We also want to reset the primary data animation if we are appearing
        if (Visible) {
            ResetAnimation();
        }

        Chart?.InvalidateDataCaches();
        NotifyVisibilityChanged(new NTSeriesVisibilityChangedEventArgs<TData> {
            Series = this,
            Visible = Visible
        });
    }
}
