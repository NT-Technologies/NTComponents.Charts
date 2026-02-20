using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components.Web;
using NTComponents.Charts.Core;
using NTComponents.Charts.Core.Series;
using SkiaSharp;

namespace NTComponents.Charts;

/// <summary>
///     Context used by <see cref="NTBubblePackSeries{TData}.ColorSelector"/> to choose a bubble color.
/// </summary>
public sealed class BubbleColorContext<TData> where TData : class {
    public required int Depth { get; init; }
    public required bool IsGroup { get; init; }
    public required string Key { get; init; }
    public required IReadOnlyList<string> Path { get; init; }
    public required decimal Value { get; init; }
    public TData? Data { get; init; }
}

/// <summary>
///     Represents a hierarchical bubble-pack series with drilldown and center-gravity physics.
/// </summary>
/// <typeparam name="TData">The type of the bound data.</typeparam>
public class NTBubblePackSeries<TData> : NTBaseSeries<TData>, ITreeMapDrillableSeries, INestedSeriesParent<TData> where TData : class {
    private const float MinZoomScale = 0.45f;
    private const float MaxZoomScale = 3.2f;

    [Parameter]
    public Func<TData, decimal> ValueSelector { get; set; } = _ => 0;

    /// <summary>
    ///     Optional group selector for this level. Nest additional <see cref="NTBubblePackSeries{TData}"/> components to define deeper levels.
    /// </summary>
    [Parameter]
    public Func<TData, string?>? GroupSelector { get; set; }

    /// <summary>
    ///     Optional explicit leaf label selector. Defaults to <see cref="NTBaseSeries{TData}.XValue"/> text.
    /// </summary>
    [Parameter]
    public Func<TData, string?>? LeafLabelSelector { get; set; }

    /// <summary>
    ///     Optional labels that describe each group level (e.g., "Customer", "Class Code").
    /// </summary>
    [Parameter]
    public IReadOnlyList<string>? GroupLevelLabels { get; set; }

    /// <summary>
    ///     Optional callback for bubble color selection.
    /// </summary>
    [Parameter]
    public Func<BubbleColorContext<TData>, TnTColor>? ColorSelector { get; set; }

    [Parameter]
    public string DataLabelFormat { get; set; } = "{0:N0}";

    [Parameter]
    public bool ShowLabels { get; set; } = true;

    [Parameter]
    public bool ShowValuesInLabels { get; set; } = false;

    [Parameter]
    public float MinLabelFontSize { get; set; } = 8f;

    [Parameter]
    public float MaxLabelFontSize { get; set; } = 26f;

    [Parameter]
    public float MinBubbleRadius { get; set; } = 14f;

    [Parameter]
    public float MaxBubbleRadius { get; set; } = 120f;

    [Parameter]
    public float BubbleSpacing { get; set; } = 2f;

    /// <summary>
    ///     Target fraction of chart area occupied by bubbles at rest.
    /// </summary>
    [Parameter]
    public float TargetFillRatio { get; set; } = 0.32f;

    /// <summary>
    ///     Inner area padding used for initial packing and relative sizing.
    /// </summary>
    [Parameter]
    public float CanvasPadding { get; set; } = 10f;

    /// <summary>
    ///     Adds inner padding between labels and bubble edges.
    /// </summary>
    [Parameter]
    public float LabelPadding { get; set; } = 12f;

    [Parameter]
    public bool EnableDrilldown { get; set; } = true;

    /// <summary>
    ///     When true, bubble centers are constrained inside the chart content area.
    /// </summary>
    [Parameter]
    public bool ConstrainToCanvas { get; set; } = false;

    [Parameter]
    public bool ShowNavigation { get; set; } = true;

    [Parameter]
    public bool ShowDrillIndicator { get; set; } = true;

    [Parameter]
    public string BackText { get; set; } = "Back";

    [Parameter]
    public float NavigationHeight { get; set; } = 30f;

    /// <summary>
    ///     Spring force used to pull bubbles back toward chart center.
    /// </summary>
    [Parameter]
    public float GravityStrength { get; set; } = 3.2f;

    /// <summary>
    ///     Collision push-out intensity when bubbles overlap.
    /// </summary>
    [Parameter]
    public float CollisionStrength { get; set; } = 0.9f;

    /// <summary>
    ///     Velocity damping factor applied per frame (~60Hz). Lower values settle faster.
    /// </summary>
    [Parameter]
    public float VelocityDamping { get; set; } = 0.94f;

    /// <summary>
    ///     Boundary restitution when bubbles hit chart edges while <see cref="ConstrainToCanvas"/> is enabled.
    /// </summary>
    [Parameter]
    public float EdgeBounce { get; set; } = 0.35f;

    /// <summary>
    ///     Scales throw velocity captured while dragging.
    /// </summary>
    [Parameter]
    public float ThrowStrength { get; set; } = 1f;

    [Parameter]
    public int PhysicsSubsteps { get; set; } = 2;

    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    private sealed class TreeNode {
        public required string Key { get; init; }
        public required string DisplayLabel { get; init; }
        public required int Depth { get; init; }
        public required int StableIndex { get; init; }
        public required NTBubblePackSeries<TData> StyleSeries { get; init; }
        public required TreeNode? Parent { get; init; }
        public required string[] PathLabels { get; init; }
        public decimal Value { get; set; }
        public int ItemCount { get; set; }
        public TData? SampleData { get; set; }
        public List<TreeNode> Children { get; } = [];
        public Dictionary<string, TreeNode>? ChildLookup { get; set; }
        public bool IsGroup => Children.Count > 0;
    }

    private sealed class BubbleState {
        public required int NodeId { get; init; }
        public SKPoint Position { get; set; }
        public SKPoint Velocity { get; set; }
        public float Radius { get; set; }
    }

    private sealed class RenderedBubble {
        public required TreeNode Node { get; init; }
        public required BubbleState State { get; init; }
        public required float TargetRadius { get; init; }
        public required bool IsInteractive { get; init; }
    }

    private readonly record struct DrillStep(string Key, string Label);

    public override ChartCoordinateSystem CoordinateSystem => ChartCoordinateSystem.TreeMap;

    bool ITreeMapDrillableSeries.IsInDrilldown => EnableDrilldown && _drillPath.Count > 0;

    internal override decimal GetTotalValue() => _root?.Value ?? (Data?.Sum(d => Math.Max(0m, ValueSelector(d))) ?? 0m);

    private TreeNode? _root;
    private readonly List<DrillStep> _drillPath = [];
    private readonly List<NTBubblePackSeries<TData>> _childSeries = [];
    private readonly List<RenderedBubble> _visibleBubbles = [];
    private readonly Dictionary<int, BubbleState> _bubbleStates = [];
    private int _hierarchyVersion;
    private int _nodeId;
    private int _lastConfigurationHash;

    private SKRect _lastSeriesArea = SKRect.Empty;
    private SKRect _lastContentArea = SKRect.Empty;
    private SKRect _backButtonRect = SKRect.Empty;
    private float _zoomScaleX = 1f;
    private float _zoomScaleY = 1f;

    private DateTime _lastPhysicsStepUtc = DateTime.MinValue;

    private int? _pointerDownNodeId;
    private int? _draggedNodeId;
    private SKPoint _pointerDownPoint = SKPoint.Empty;
    private SKPoint _currentDragPoint = SKPoint.Empty;
    private SKPoint _lastDragPoint = SKPoint.Empty;
    private SKPoint _dragReleaseVelocity = SKPoint.Empty;
    private DateTime _lastDragUpdateUtc = DateTime.MinValue;

    private SKPaint? _bubblePaint;
    private SKPaint? _strokePaint;
    private SKPaint? _labelPaint;
    private SKPaint? _navPaint;
    private SKPaint? _navTextPaint;
    private SKPaint? _navButtonPaint;
    private SKPaint? _navButtonTextPaint;
    private SKPaint? _drillIndicatorPaint;
    private SKPaint? _drillIndicatorTextPaint;
    private SKFont? _labelFont;
    private SKFont? _navFont;
    private SKFont? _drillIndicatorFont;

    void INestedSeriesParent<TData>.RegisterChildSeries(NTBaseSeries<TData> series) {
        if (series is not NTBubblePackSeries<TData> child || ReferenceEquals(child, this) || _childSeries.Contains(child)) {
            return;
        }

        _childSeries.Add(child);
        NotifyChildSeriesChanged();
    }

    void INestedSeriesParent<TData>.UnregisterChildSeries(NTBaseSeries<TData> series) {
        if (series is not NTBubblePackSeries<TData> child) {
            return;
        }

        if (_childSeries.Remove(child)) {
            NotifyChildSeriesChanged();
        }
    }

    void INestedSeriesParent<TData>.NotifyChildSeriesChanged() => NotifyChildSeriesChanged();

    private void EnsureZoomOnlyInteractions() {
        var interactions = Interactions & ~(ChartInteractions.XPan | ChartInteractions.YPan);
        if (interactions == ChartInteractions.None) {
            interactions = ChartInteractions.XZoom | ChartInteractions.YZoom;
        }

        if (interactions != Interactions) {
            Interactions = interactions;
        }
    }

    protected override void OnDataChanged() {
        base.OnDataChanged();
        _root = null;
        _nodeId = 0;
        _hierarchyVersion++;
        _drillPath.Clear();
        ResetSimulation();
        NotifyNestedParentSeriesChanged();
    }

    protected override void OnParametersSet() {
        EnsureZoomOnlyInteractions();
        base.OnParametersSet();

        var configurationHash = GetConfigurationHash();
        if (configurationHash != _lastConfigurationHash) {
            _lastConfigurationHash = configurationHash;
            _root = null;
            _nodeId = 0;
            _hierarchyVersion++;
            _drillPath.Clear();
            ResetSimulation();
            NotifyNestedParentSeriesChanged();
        }
    }

    protected override void BuildRenderTree(RenderTreeBuilder builder) {
        if (ChildContent is null) {
            return;
        }

        builder.OpenComponent<CascadingValue<INestedSeriesParent<TData>>>(0);
        builder.AddAttribute(1, "Value", (INestedSeriesParent<TData>)this);
        builder.AddAttribute(2, "IsFixed", true);
        builder.AddAttribute(3, "ChildContent", ChildContent);
        builder.CloseComponent();
    }

    public override SKRect Render(NTRenderContext context, SKRect renderArea) {
        if (renderArea.Width <= 0 || renderArea.Height <= 0 || Data is null) {
            return renderArea;
        }

        EnsureHierarchy(Data);
        if (_root is null || _root.Value <= 0m) {
            return renderArea;
        }

        var current = ResolveCurrentNode() ?? _root;
        if (current != _root && current.Parent is null) {
            _drillPath.Clear();
            current = _root;
            ResetSimulation();
        }

        var navHeight = ShowNavigation ? NavigationHeight * context.Density : 0f;
        var contentArea = navHeight > 0f
            ? new SKRect(renderArea.Left, renderArea.Top + navHeight, renderArea.Right, renderArea.Bottom)
            : renderArea;

        _lastSeriesArea = renderArea;
        _lastContentArea = contentArea;

        BuildVisibleBubbles(context, current, contentArea);
        InitializePaints(context);
        SimulatePhysics(contentArea);

        if (ShowNavigation) {
            RenderNavigation(context, renderArea, current, navHeight);
        }
        else {
            _backButtonRect = SKRect.Empty;
        }

        var visibility = VisibilityFactor;
        var hoveredNodeId = Chart.HoveredSeries == this ? Chart.HoveredPointIndex : null;
        var sorted = _visibleBubbles.OrderByDescending(b => b.State.Radius).ToList();
        var viewOrigin = new SKPoint(contentArea.MidX, contentArea.MidY);
        var radiusZoomScale = GetRadiusZoomScale();

        for (var i = 0; i < sorted.Count; i++) {
            var bubble = sorted[i];
            var node = bubble.Node;
            var state = bubble.State;
            var radius = state.Radius * radiusZoomScale;
            if (radius <= 0.5f) {
                continue;
            }

            var center = WorldToView(state.Position, viewOrigin);
            var styleSeries = node.StyleSeries;
            var color = ResolveNodeColor(node, i);
            var labelColorOverride = default(SKColor?);

            if (!node.IsGroup && node.SampleData is not null) {
                var args = new NTDataPointRenderArgs<TData> {
                    Data = node.SampleData,
                    Index = node.StableIndex,
                    Color = color,
                    GetThemeColor = Chart.GetThemeColor
                };
                styleSeries.OnDataPointRender?.Invoke(args);
                color = args.Color ?? color;
                labelColorOverride = args.DataLabelColor;
            }

            var isHovered = hoveredNodeId == node.StableIndex;
            var alphaFactor = 1f;
            if (hoveredNodeId.HasValue && !isHovered) {
                alphaFactor = 0.35f;
            }

            _bubblePaint!.Color = color.WithAlpha((byte)(color.Alpha * visibility * alphaFactor));
            context.Canvas.DrawCircle(center, radius, _bubblePaint);

            _strokePaint!.StrokeWidth = Math.Max(1f, context.Density) + (isHovered ? 1.2f * context.Density : 0f);
            _strokePaint.Color = Chart.GetThemeColor(TnTColor.OutlineVariant).WithAlpha((byte)(220f * visibility * alphaFactor));
            context.Canvas.DrawCircle(center, radius, _strokePaint);

            if (ShowDrillIndicator && EnableDrilldown && bubble.IsInteractive && node.IsGroup) {
                RenderDrillIndicator(context, center, radius, styleSeries);
            }

            if (styleSeries.ShowLabels) {
                RenderBubbleLabel(context, node, center, radius, color, labelColorOverride, styleSeries);
            }
        }

        return renderArea;
    }

    public override void HandleMouseDown(MouseEventArgs e) {
        var pointView = ToCanvasPoint(e);
        if (!_lastSeriesArea.Contains(pointView)) {
            return;
        }

        if (_backButtonRect.Contains(pointView) && _drillPath.Count > 0) {
            _drillPath.RemoveAt(_drillPath.Count - 1);
            ResetSimulation();
            return;
        }

        var viewOrigin = new SKPoint(_lastContentArea.MidX, _lastContentArea.MidY);
        var point = ViewToWorld(pointView, viewOrigin);
        var hit = FindBubbleAtPoint(point, interactiveOnly: true);
        if (hit is null) {
            _pointerDownNodeId = null;
            _draggedNodeId = null;
            return;
        }

        _pointerDownNodeId = hit.Node.StableIndex;
        _pointerDownPoint = point;
        _currentDragPoint = point;
        _lastDragPoint = point;
        _dragReleaseVelocity = SKPoint.Empty;
        _lastDragUpdateUtc = DateTime.UtcNow;
    }

    public override void HandleMouseMove(MouseEventArgs e) {
        if (_pointerDownNodeId is null) {
            return;
        }

        var pointView = ToCanvasPoint(e);
        var viewOrigin = new SKPoint(_lastContentArea.MidX, _lastContentArea.MidY);
        var point = ViewToWorld(pointView, viewOrigin);
        var nowUtc = DateTime.UtcNow;
        if (_draggedNodeId is null) {
            var distance = Distance(point, _pointerDownPoint);
            if (distance > (6f * Math.Max(1f, Chart.Density))) {
                _draggedNodeId = _pointerDownNodeId;
            }
        }

        if (_draggedNodeId is null) {
            return;
        }

        if (!_bubbleStates.TryGetValue(_draggedNodeId.Value, out var state)) {
            return;
        }

        var dt = (float)(nowUtc - _lastDragUpdateUtc).TotalSeconds;
        if (dt > 0.0001f) {
            _dragReleaseVelocity = new SKPoint(
                (point.X - _lastDragPoint.X) / dt,
                (point.Y - _lastDragPoint.Y) / dt);
        }

        _currentDragPoint = ConstrainToCanvas
            ? ClampPointToArea(point, state.Radius, _lastContentArea)
            : point;
        state.Position = _currentDragPoint;
        state.Velocity = _dragReleaseVelocity;
        _lastDragPoint = point;
        _lastDragUpdateUtc = nowUtc;
    }

    public override void HandleMouseUp(MouseEventArgs e) {
        if (_pointerDownNodeId is null) {
            return;
        }

        if (_draggedNodeId is int draggedNodeId && _bubbleStates.TryGetValue(draggedNodeId, out var draggedState)) {
            var throwVelocity = new SKPoint(_dragReleaseVelocity.X * ThrowStrength, _dragReleaseVelocity.Y * ThrowStrength);
            draggedState.Velocity = ClampVelocity(throwVelocity, maxSpeed: 2500f);
            _draggedNodeId = null;
            _pointerDownNodeId = null;
            return;
        }

        var pointView = ToCanvasPoint(e);
        var viewOrigin = new SKPoint(_lastContentArea.MidX, _lastContentArea.MidY);
        var point = ViewToWorld(pointView, viewOrigin);
        var hit = FindBubbleAtPoint(point, interactiveOnly: true);
        if (EnableDrilldown &&
            hit is not null &&
            hit.Node.StableIndex == _pointerDownNodeId &&
            hit.Node.IsGroup) {
            _drillPath.Add(new DrillStep(hit.Node.Key, hit.Node.DisplayLabel));
            ResetSimulation();
        }

        _pointerDownNodeId = null;
    }

    public override void HandleMouseWheel(WheelEventArgs e) {
        if ((!Interactions.HasFlag(ChartInteractions.XZoom) && !Interactions.HasFlag(ChartInteractions.YZoom)) ||
            _lastContentArea.Width <= 0f ||
            _lastContentArea.Height <= 0f) {
            return;
        }

        var viewPoint = ToCanvasPoint(e);
        if (!_lastContentArea.Contains(viewPoint)) {
            return;
        }

        var scaleFactor = e.DeltaY > 0 ? (1f / 1.1f) : 1.1f;
        if (Interactions.HasFlag(ChartInteractions.XZoom)) {
            _zoomScaleX = Math.Clamp(_zoomScaleX * scaleFactor, MinZoomScale, MaxZoomScale);
        }

        if (Interactions.HasFlag(ChartInteractions.YZoom)) {
            _zoomScaleY = Math.Clamp(_zoomScaleY * scaleFactor, MinZoomScale, MaxZoomScale);
        }

        NotifyZoom(new NTSeriesZoomEventArgs<TData> {
            Series = this,
            PointerPosition = viewPoint,
            WheelEvent = e
        });
    }

    public override void ResetView() {
        _zoomScaleX = 1f;
        _zoomScaleY = 1f;
        base.ResetView();
    }

    public override (int Index, TData? Data)? HitTest(SKPoint point, SKRect renderArea) {
        var viewOrigin = new SKPoint(_lastContentArea.MidX, _lastContentArea.MidY);
        var worldPoint = ViewToWorld(point, viewOrigin);
        var hit = FindBubbleAtPoint(worldPoint, interactiveOnly: true);
        if (hit is null) {
            return null;
        }

        return (hit.Node.StableIndex, hit.Node.SampleData);
    }

    internal override IEnumerable<LegendItemInfo<TData>> GetLegendItems() {
        yield return new LegendItemInfo<TData> {
            Label = Title ?? "Bubble Pack",
            Color = Chart.GetSeriesColor(this),
            Series = this,
            Index = null,
            IsVisible = Visible
        };
    }

    internal override TooltipInfo GetTooltipInfo(TData data) {
        if (Chart.HoveredSeries == this &&
            Chart.HoveredPointIndex is int hoveredPointIndex) {
            for (var i = _visibleBubbles.Count - 1; i >= 0; i--) {
                var node = _visibleBubbles[i].Node;
                if (node.StableIndex != hoveredPointIndex) {
                    continue;
                }

                return new TooltipInfo {
                    Header = node.DisplayLabel,
                    Lines = [
                        new TooltipLine {
                            Label = node.StyleSeries.Title ?? Title ?? "Series",
                            Value = FormatValue(node.Value, DataLabelFormat),
                            Color = ResolveNodeColor(node, i)
                        }
                    ]
                };
            }
        }

        var value = ValueSelector(data);
        var xValue = XValue?.Invoke(data) ?? LeafLabelSelector?.Invoke(data);
        return new TooltipInfo {
            Header = xValue?.ToString(),
            Lines = [
                new TooltipLine {
                    Label = Title ?? "Series",
                    Value = FormatValue(value, DataLabelFormat),
                    Color = Chart.GetSeriesColor(this)
                }
            ]
        };
    }

    protected override void Dispose(bool disposing) {
        if (disposing) {
            _bubblePaint?.Dispose();
            _strokePaint?.Dispose();
            _labelPaint?.Dispose();
            _navPaint?.Dispose();
            _navTextPaint?.Dispose();
            _navButtonPaint?.Dispose();
            _navButtonTextPaint?.Dispose();
            _drillIndicatorPaint?.Dispose();
            _drillIndicatorTextPaint?.Dispose();
            _labelFont?.Dispose();
            _navFont?.Dispose();
            _drillIndicatorFont?.Dispose();

            _bubblePaint = null;
            _strokePaint = null;
            _labelPaint = null;
            _navPaint = null;
            _navTextPaint = null;
            _navButtonPaint = null;
            _navButtonTextPaint = null;
            _drillIndicatorPaint = null;
            _drillIndicatorTextPaint = null;
            _labelFont = null;
            _navFont = null;
            _drillIndicatorFont = null;
        }

        base.Dispose(disposing);
    }

    private void EnsureHierarchy(IEnumerable<TData> dataItems) {
        if (_root is not null) {
            return;
        }

        var allItems = dataItems.ToList();
        var chain = BuildSeriesChain();
        var rootSeries = chain[0];

        _nodeId = 0;
        var root = new TreeNode {
            Key = "$root",
            DisplayLabel = "Root",
            Depth = -1,
            StableIndex = _nodeId++,
            StyleSeries = this,
            Parent = null,
            PathLabels = []
        };

        var positiveItems = new List<TData>(allItems.Count);
        foreach (var item in allItems) {
            var value = Math.Max(0m, rootSeries.ValueSelector(item));
            if (value <= 0m) {
                continue;
            }

            root.Value += value;
            root.ItemCount++;
            root.SampleData ??= item;
            positiveItems.Add(item);
        }

        if (positiveItems.Count > 0 && root.Value > 0m) {
            BuildHierarchyFromSeriesLevel(root, positiveItems, chain, 0);
        }

        _root = root;
        _hierarchyVersion++;
    }

    private void BuildHierarchyFromSeriesLevel(TreeNode parent, List<TData> items, IReadOnlyList<NTBubblePackSeries<TData>> chain, int chainIndex) {
        if (items.Count == 0 || chainIndex >= chain.Count) {
            return;
        }

        var styleSeries = chain[chainIndex];
        if (styleSeries.GroupSelector is not null) {
            foreach (var group in items.GroupBy(item => NormalizeGroupKey(styleSeries.GroupSelector(item)))) {
                var groupedItems = group.ToList();
                var groupValue = groupedItems.Sum(item => Math.Max(0m, styleSeries.ValueSelector(item)));
                if (groupValue <= 0m) {
                    continue;
                }

                parent.ChildLookup ??= new Dictionary<string, TreeNode>(StringComparer.Ordinal);
                var groupKey = group.Key;
                if (!parent.ChildLookup.TryGetValue(groupKey, out var child)) {
                    child = new TreeNode {
                        Key = groupKey,
                        DisplayLabel = groupKey,
                        Depth = parent.Depth + 1,
                        StableIndex = _nodeId++,
                        StyleSeries = styleSeries,
                        Parent = parent,
                        PathLabels = [.. parent.PathLabels, groupKey]
                    };
                    parent.ChildLookup[groupKey] = child;
                    parent.Children.Add(child);
                }

                child.Value = groupValue;
                child.ItemCount = groupedItems.Count;
                child.SampleData = groupedItems[0];

                if (chainIndex + 1 < chain.Count) {
                    BuildHierarchyFromSeriesLevel(child, groupedItems, chain, chainIndex + 1);
                }
            }

            return;
        }

        for (var i = 0; i < items.Count; i++) {
            var item = items[i];
            var itemValue = Math.Max(0m, styleSeries.ValueSelector(item));
            if (itemValue <= 0m) {
                continue;
            }

            var label = styleSeries.ResolveLeafLabel(item, i);
            var key = $"{label}#{_nodeId}";
            var child = new TreeNode {
                Key = key,
                DisplayLabel = label,
                Depth = parent.Depth + 1,
                StableIndex = _nodeId++,
                StyleSeries = styleSeries,
                Parent = parent,
                PathLabels = [.. parent.PathLabels, label],
                Value = itemValue,
                ItemCount = 1,
                SampleData = item
            };

            parent.ChildLookup ??= new Dictionary<string, TreeNode>(StringComparer.Ordinal);
            parent.ChildLookup[key] = child;
            parent.Children.Add(child);

            if (chainIndex + 1 < chain.Count) {
                BuildHierarchyFromSeriesLevel(child, [item], chain, chainIndex + 1);
            }
        }
    }

    private static string NormalizeGroupKey(string? rawGroup) {
        return string.IsNullOrWhiteSpace(rawGroup) ? "Ungrouped" : rawGroup.Trim();
    }

    private List<NTBubblePackSeries<TData>> BuildSeriesChain() {
        var chain = new List<NTBubblePackSeries<TData>>(4);
        var visited = new HashSet<NTBubblePackSeries<TData>>();

        NTBubblePackSeries<TData>? current = this;
        while (current is not null && visited.Add(current)) {
            chain.Add(current);
            current = current._childSeries.FirstOrDefault();
        }

        return chain;
    }

    private string ResolveLeafLabel(TData item, int index) {
        var label = LeafLabelSelector?.Invoke(item) ?? XValue?.Invoke(item)?.ToString();
        if (!string.IsNullOrWhiteSpace(label)) {
            return label.Trim();
        }

        return $"Item {index + 1}";
    }

    private TreeNode? ResolveCurrentNode() {
        if (_root is null) {
            return null;
        }

        var current = _root;
        for (var i = 0; i < _drillPath.Count; i++) {
            var step = _drillPath[i];
            if (current.ChildLookup is null || !current.ChildLookup.TryGetValue(step.Key, out var next)) {
                return _root;
            }

            current = next;
        }

        return current;
    }

    private void BuildVisibleBubbles(NTRenderContext context, TreeNode current, SKRect contentArea) {
        _visibleBubbles.Clear();
        if (contentArea.Width <= 0 || contentArea.Height <= 0 || current.Children.Count == 0) {
            _bubbleStates.Clear();
            return;
        }

        var nodes = current.Children
            .Where(c => c.Value > 0m)
            .OrderByDescending(c => c.Value)
            .ToList();
        if (nodes.Count == 0) {
            _bubbleStates.Clear();
            return;
        }

        var center = new SKPoint(contentArea.MidX, contentArea.MidY);
        var activeNodeIds = new HashSet<int>(nodes.Count);
        var targetRadii = ComputeRelativeTargetRadii(nodes, contentArea, context.Density);

        for (var i = 0; i < nodes.Count; i++) {
            var node = nodes[i];
            var targetRadius = targetRadii.TryGetValue(node.StableIndex, out var computedRadius)
                ? computedRadius
                : 1.5f * Math.Max(1f, context.Density);

            if (!_bubbleStates.TryGetValue(node.StableIndex, out var state)) {
                var initialPosition = FindInitialBubblePosition(
                    nodeId: node.StableIndex,
                    targetRadius: targetRadius,
                    center: center,
                    contentArea: contentArea,
                    density: context.Density);
                state = new BubbleState {
                    NodeId = node.StableIndex,
                    Position = initialPosition,
                    Velocity = SKPoint.Empty,
                    Radius = Math.Max(4f * context.Density, targetRadius * 0.65f)
                };
                _bubbleStates[node.StableIndex] = state;
            }

            state.Radius = Lerp(state.Radius, targetRadius, 0.24f);
            if (ConstrainToCanvas) {
                state.Position = ClampPointToArea(state.Position, state.Radius, contentArea);
            }
            _visibleBubbles.Add(new RenderedBubble {
                Node = node,
                State = state,
                TargetRadius = targetRadius,
                IsInteractive = true
            });
            activeNodeIds.Add(node.StableIndex);
        }

        var stale = _bubbleStates.Keys.Where(key => !activeNodeIds.Contains(key)).ToList();
        for (var i = 0; i < stale.Count; i++) {
            _bubbleStates.Remove(stale[i]);
        }
    }

    private Dictionary<int, float> ComputeRelativeTargetRadii(List<TreeNode> nodes, SKRect area, float density) {
        var result = new Dictionary<int, float>(nodes.Count);
        if (nodes.Count == 0) {
            return result;
        }

        var canvasPadding = Math.Max(0f, CanvasPadding * density);
        var effectiveWidth = Math.Max(1f, area.Width - (2f * canvasPadding));
        var effectiveHeight = Math.Max(1f, area.Height - (2f * canvasPadding));
        var areaSize = Math.Max(1f, effectiveWidth * effectiveHeight);
        var valueWeights = new float[nodes.Count];
        float valueSum = 0f;

        for (var i = 0; i < nodes.Count; i++) {
            var weight = Math.Max(0.0001f, (float)Math.Max(0m, nodes[i].Value));
            valueWeights[i] = weight;
            valueSum += weight;
        }

        if (valueSum <= 0f) {
            valueSum = nodes.Count;
            for (var i = 0; i < nodes.Count; i++) {
                valueWeights[i] = 1f;
            }
        }

        var targetFillRatio = Math.Clamp(TargetFillRatio, 0.05f, 0.70f);
        var scale = MathF.Sqrt((areaSize * targetFillRatio) / (MathF.PI * valueSum));
        var radii = new float[nodes.Count];
        for (var i = 0; i < nodes.Count; i++) {
            radii[i] = MathF.Sqrt(valueWeights[i]) * scale;
        }

        var maxRadius = MaxBubbleRadius > 0f ? MaxBubbleRadius * density : float.MaxValue;
        var maxRadiusByCanvas = Math.Max(
            3f * Math.Max(1f, density),
            (Math.Min(effectiveWidth, effectiveHeight) * 0.5f) - Math.Max(2f * density, BubbleSpacing * density));
        maxRadius = Math.Min(maxRadius, maxRadiusByCanvas);
        if (float.IsFinite(maxRadius) && maxRadius > 0f) {
            var currentMax = radii.Max();
            if (currentMax > maxRadius && currentMax > 0f) {
                var factor = maxRadius / currentMax;
                for (var i = 0; i < radii.Length; i++) {
                    radii[i] *= factor;
                }
            }
        }

        var minRadius = Math.Max(0f, MinBubbleRadius * density);
        if (minRadius > 0f && float.IsFinite(maxRadius) && maxRadius > 0f) {
            var currentMin = radii.Min();
            if (currentMin > 0f && currentMin < minRadius) {
                var upFactor = minRadius / currentMin;
                var currentMax = radii.Max();
                if ((currentMax * upFactor) <= maxRadius) {
                    for (var i = 0; i < radii.Length; i++) {
                        radii[i] *= upFactor;
                    }
                }
            }
        }

        var renderFloor = 1.5f * Math.Max(1f, density);
        for (var i = 0; i < nodes.Count; i++) {
            result[nodes[i].StableIndex] = Math.Max(renderFloor, radii[i]);
        }

        return result;
    }

    private void SimulatePhysics(SKRect contentArea) {
        if (_visibleBubbles.Count == 0 || contentArea.Width <= 0 || contentArea.Height <= 0) {
            return;
        }

        var now = DateTime.UtcNow;
        if (_lastPhysicsStepUtc == DateTime.MinValue) {
            _lastPhysicsStepUtc = now;
            return;
        }

        var deltaTime = (float)(now - _lastPhysicsStepUtc).TotalSeconds;
        _lastPhysicsStepUtc = now;
        var dt = Math.Clamp(deltaTime, 0.001f, 0.05f);
        var substeps = Math.Clamp(PhysicsSubsteps, 1, 8);
        var substepDt = dt / substeps;

        for (var step = 0; step < substeps; step++) {
            SimulatePhysicsStep(contentArea, substepDt);
        }
    }

    private void SimulatePhysicsStep(SKRect area, float dt) {
        var center = new SKPoint(area.MidX, area.MidY);
        var draggedNodeId = _draggedNodeId;
        var spacing = BubbleSpacing * Math.Max(1f, Chart.Density);
        var damping = Math.Clamp(VelocityDamping, 0f, 0.9999f);
        var dampingFactor = MathF.Pow(damping, dt * 60f);
        var gravity = Math.Max(0f, GravityStrength);
        var collisionPush = Math.Max(0.05f, CollisionStrength);

        if (draggedNodeId is int dragId && _bubbleStates.TryGetValue(dragId, out var draggedState)) {
            draggedState.Position = ConstrainToCanvas
                ? ClampPointToArea(_currentDragPoint, draggedState.Radius, area)
                : _currentDragPoint;
        }

        for (var i = 0; i < _visibleBubbles.Count; i++) {
            var bubble = _visibleBubbles[i];
            var state = bubble.State;
            if (draggedNodeId == state.NodeId) {
                continue;
            }

            var toCenter = new SKPoint(center.X - state.Position.X, center.Y - state.Position.Y);
            state.Velocity = new SKPoint(
                state.Velocity.X + (toCenter.X * gravity * dt),
                state.Velocity.Y + (toCenter.Y * gravity * dt));
            state.Velocity = new SKPoint(state.Velocity.X * dampingFactor, state.Velocity.Y * dampingFactor);
            state.Velocity = ClampVelocity(state.Velocity, maxSpeed: 1600f);
        }

        for (var i = 0; i < _visibleBubbles.Count; i++) {
            var a = _visibleBubbles[i].State;
            for (var j = i + 1; j < _visibleBubbles.Count; j++) {
                var b = _visibleBubbles[j].State;
                var dx = b.Position.X - a.Position.X;
                var dy = b.Position.Y - a.Position.Y;
                var distSq = (dx * dx) + (dy * dy);
                var minDist = a.Radius + b.Radius + spacing;
                var minDistSq = minDist * minDist;
                if (distSq >= minDistSq) {
                    continue;
                }

                var dist = MathF.Sqrt(Math.Max(0.0001f, distSq));
                var nx = dx / dist;
                var ny = dy / dist;
                var overlap = (minDist - dist) * collisionPush;

                var aDragged = draggedNodeId == a.NodeId;
                var bDragged = draggedNodeId == b.NodeId;

                if (!aDragged && !bDragged) {
                    var halfPush = overlap * 0.5f;
                    a.Position = new SKPoint(a.Position.X - (nx * halfPush), a.Position.Y - (ny * halfPush));
                    b.Position = new SKPoint(b.Position.X + (nx * halfPush), b.Position.Y + (ny * halfPush));
                }
                else if (aDragged && !bDragged) {
                    b.Position = new SKPoint(b.Position.X + (nx * overlap), b.Position.Y + (ny * overlap));
                }
                else if (!aDragged && bDragged) {
                    a.Position = new SKPoint(a.Position.X - (nx * overlap), a.Position.Y - (ny * overlap));
                }

                var separationVelocity = overlap * 8f;
                if (!aDragged) {
                    a.Velocity = ClampVelocity(
                        new SKPoint(a.Velocity.X - (nx * separationVelocity), a.Velocity.Y - (ny * separationVelocity)),
                        maxSpeed: 1600f);
                }

                if (!bDragged) {
                    b.Velocity = ClampVelocity(
                        new SKPoint(b.Velocity.X + (nx * separationVelocity), b.Velocity.Y + (ny * separationVelocity)),
                        maxSpeed: 1600f);
                }
            }
        }

        for (var i = 0; i < _visibleBubbles.Count; i++) {
            var state = _visibleBubbles[i].State;
            if (draggedNodeId == state.NodeId) {
                continue;
            }

            state.Position = new SKPoint(
                state.Position.X + (state.Velocity.X * dt),
                state.Position.Y + (state.Velocity.Y * dt));

            if (ConstrainToCanvas) {
                var bounce = Math.Clamp(EdgeBounce, 0f, 1f);
                var leftLimit = area.Left + state.Radius;
                var rightLimit = area.Right - state.Radius;
                var topLimit = area.Top + state.Radius;
                var bottomLimit = area.Bottom - state.Radius;

                if (state.Position.X < leftLimit) {
                    state.Position = new SKPoint(leftLimit, state.Position.Y);
                    if (state.Velocity.X < 0f) {
                        state.Velocity = new SKPoint(-state.Velocity.X * bounce, state.Velocity.Y);
                    }
                }
                else if (state.Position.X > rightLimit) {
                    state.Position = new SKPoint(rightLimit, state.Position.Y);
                    if (state.Velocity.X > 0f) {
                        state.Velocity = new SKPoint(-state.Velocity.X * bounce, state.Velocity.Y);
                    }
                }

                if (state.Position.Y < topLimit) {
                    state.Position = new SKPoint(state.Position.X, topLimit);
                    if (state.Velocity.Y < 0f) {
                        state.Velocity = new SKPoint(state.Velocity.X, -state.Velocity.Y * bounce);
                    }
                }
                else if (state.Position.Y > bottomLimit) {
                    state.Position = new SKPoint(state.Position.X, bottomLimit);
                    if (state.Velocity.Y > 0f) {
                        state.Velocity = new SKPoint(state.Velocity.X, -state.Velocity.Y * bounce);
                    }
                }
            }
        }
    }

    private void InitializePaints(NTRenderContext context) {
        _bubblePaint ??= new SKPaint {
            Style = SKPaintStyle.Fill,
            IsAntialias = true
        };

        _strokePaint ??= new SKPaint {
            Style = SKPaintStyle.Stroke,
            StrokeWidth = Math.Max(1f, context.Density),
            IsAntialias = true
        };

        _labelPaint ??= new SKPaint {
            IsAntialias = true
        };

        _navPaint ??= new SKPaint {
            Style = SKPaintStyle.Fill,
            IsAntialias = true
        };

        _navTextPaint ??= new SKPaint {
            IsAntialias = true
        };

        _navButtonPaint ??= new SKPaint {
            Style = SKPaintStyle.Fill,
            IsAntialias = true
        };

        _navButtonTextPaint ??= new SKPaint {
            IsAntialias = true
        };

        _drillIndicatorPaint ??= new SKPaint {
            Style = SKPaintStyle.Fill,
            IsAntialias = true
        };

        _drillIndicatorTextPaint ??= new SKPaint {
            IsAntialias = true
        };

        _labelFont ??= new SKFont {
            Embolden = true,
            Typeface = context.DefaultFont.Typeface
        };

        _navFont ??= new SKFont {
            Embolden = true,
            Typeface = context.RegularFont.Typeface
        };

        _drillIndicatorFont ??= new SKFont {
            Embolden = true,
            Typeface = context.RegularFont.Typeface
        };

        _navTextPaint.Color = Chart.GetThemeColor(TextColor ?? Chart.TextColor);
        _navButtonTextPaint.Color = Chart.GetThemeColor(TextColor ?? Chart.TextColor);
        _navFont.Size = 12f * context.Density;
        _drillIndicatorTextPaint.Color = Chart.GetThemeColor(TextColor ?? Chart.TextColor);
        _drillIndicatorFont.Size = 10f * context.Density;
    }

    private void RenderNavigation(NTRenderContext context, SKRect renderArea, TreeNode current, float navHeight) {
        if (navHeight <= 0f) {
            _backButtonRect = SKRect.Empty;
            return;
        }

        var navArea = new SKRect(renderArea.Left, renderArea.Top, renderArea.Right, renderArea.Top + navHeight);
        _navPaint!.Color = Chart.GetThemeColor(TnTColor.SurfaceContainerLow).WithAlpha((byte)(235f * VisibilityFactor));
        context.Canvas.DrawRect(navArea, _navPaint);

        _strokePaint!.StrokeWidth = Math.Max(1f, context.Density);
        _strokePaint.Color = Chart.GetThemeColor(TnTColor.OutlineVariant).WithAlpha((byte)(180f * VisibilityFactor));
        context.Canvas.DrawLine(navArea.Left, navArea.Bottom, navArea.Right, navArea.Bottom, _strokePaint);

        float textX = navArea.Left + (8f * context.Density);
        _backButtonRect = SKRect.Empty;

        if (EnableDrilldown && _drillPath.Count > 0) {
            var btnHeight = navHeight - (8f * context.Density);
            var btnWidth = Math.Max(56f * context.Density, (BackText.Length + 3) * 7f * context.Density);
            var btnTop = navArea.Top + (4f * context.Density);
            var btnLeft = navArea.Left + (6f * context.Density);
            _backButtonRect = new SKRect(btnLeft, btnTop, btnLeft + btnWidth, btnTop + btnHeight);

            _navButtonPaint!.Color = Chart.GetThemeColor(TnTColor.SurfaceVariant).WithAlpha((byte)(220f * VisibilityFactor));
            context.Canvas.DrawRoundRect(_backButtonRect, 4f * context.Density, 4f * context.Density, _navButtonPaint);

            var backBaseline = _backButtonRect.MidY + (_navFont!.Size * 0.35f);
            context.Canvas.DrawText($"< {BackText}", _backButtonRect.MidX, backBaseline, SKTextAlign.Center, _navFont, _navButtonTextPaint!);
            textX = _backButtonRect.Right + (10f * context.Density);
        }

        var levelName = GetLevelName(current.Depth + 1);
        var breadcrumb = _drillPath.Count == 0
            ? "All"
            : string.Join(" / ", _drillPath.Select(d => d.Label));
        var title = string.IsNullOrWhiteSpace(levelName) ? breadcrumb : $"{levelName}: {breadcrumb}";

        var baseline = navArea.Top + (navArea.Height * 0.66f);
        _navTextPaint!.Color = Chart.GetThemeColor(TextColor ?? Chart.TextColor).WithAlpha((byte)(255f * VisibilityFactor));

        context.Canvas.Save();
        context.Canvas.ClipRect(new SKRect(textX, navArea.Top, navArea.Right - (6f * context.Density), navArea.Bottom));
        context.Canvas.DrawText(title, textX, baseline, SKTextAlign.Left, _navFont!, _navTextPaint);
        context.Canvas.Restore();
    }

    private string? GetLevelName(int level) {
        if (GroupLevelLabels is null || level < 0 || level >= GroupLevelLabels.Count) {
            return null;
        }

        return GroupLevelLabels[level];
    }

    private void RenderDrillIndicator(NTRenderContext context, SKPoint center, float radius, NTBubblePackSeries<TData> styleSeries) {
        if (radius < (14f * context.Density)) {
            return;
        }

        var size = Math.Clamp(radius * 0.38f, 10f * context.Density, 16f * context.Density);
        var indicatorCenter = new SKPoint(center.X + (radius * 0.45f), center.Y - (radius * 0.45f));
        var indicatorRect = new SKRect(
            indicatorCenter.X - (size / 2f),
            indicatorCenter.Y - (size / 2f),
            indicatorCenter.X + (size / 2f),
            indicatorCenter.Y + (size / 2f));

        _drillIndicatorPaint!.Color = Chart.GetThemeColor(TnTColor.SurfaceContainerHighest).WithAlpha((byte)(225f * VisibilityFactor));
        context.Canvas.DrawRoundRect(indicatorRect, 3f * context.Density, 3f * context.Density, _drillIndicatorPaint);

        _drillIndicatorTextPaint!.Color = Chart.GetThemeColor(styleSeries.TextColor ?? TnTColor.OnSurface).WithAlpha((byte)(255f * VisibilityFactor));
        _drillIndicatorFont!.Size = Math.Clamp(size * 0.7f, 8f * context.Density, 12f * context.Density);
        var baseline = indicatorRect.MidY + (_drillIndicatorFont.Size * 0.33f);
        context.Canvas.DrawText(">", indicatorRect.MidX, baseline, SKTextAlign.Center, _drillIndicatorFont, _drillIndicatorTextPaint);
    }

    private void RenderBubbleLabel(
        NTRenderContext context,
        TreeNode node,
        SKPoint center,
        float radius,
        SKColor bubbleColor,
        SKColor? labelColorOverride,
        NTBubblePackSeries<TData> styleSeries) {
        var minSide = radius * 2f;
        if (minSide < (16f * context.Density)) {
            return;
        }

        var maxFont = styleSeries.MaxLabelFontSize * context.Density;
        var configuredMinFont = styleSeries.MinLabelFontSize * context.Density;
        var labelPadding = Math.Max(0f, styleSeries.LabelPadding * context.Density);
        var interiorDiameter = (radius * 2f) - (labelPadding * 2f);
        if (interiorDiameter <= (6f * context.Density)) {
            return;
        }

        var adaptiveMinFont = Math.Max(
            4f * context.Density,
            Math.Min(configuredMinFont, interiorDiameter * (styleSeries.ShowValuesInLabels ? 0.14f : 0.18f)));
        var preferredPrimary = interiorDiameter * (styleSeries.ShowValuesInLabels ? 0.20f : 0.26f);
        var primaryMaxByInterior = interiorDiameter * (styleSeries.ShowValuesInLabels ? 0.32f : 0.44f);
        var primaryFont = Math.Clamp(preferredPrimary, adaptiveMinFont, Math.Min(maxFont, primaryMaxByInterior));
        if (primaryFont < (4f * context.Density)) {
            return;
        }

        var textColor = labelColorOverride ?? GetContrastColor(bubbleColor);
        _labelPaint!.Color = textColor.WithAlpha((byte)(255f * VisibilityFactor));
        _labelFont!.Size = primaryFont;

        var label = node.DisplayLabel;
        if (string.IsNullOrWhiteSpace(label)) {
            return;
        }

        var maxWidth = interiorDiameter;
        if (maxWidth <= 0f) {
            return;
        }

        var estimatedWidth = EstimateTextWidth(label, _labelFont.Size);
        if (estimatedWidth > maxWidth) {
            _labelFont.Size = Math.Max(adaptiveMinFont, _labelFont.Size * (maxWidth / Math.Max(1f, estimatedWidth)));
        }

        var baseline = center.Y + (_labelFont.Size * (styleSeries.ShowValuesInLabels ? -0.10f : 0.30f));
        context.Canvas.DrawText(label, center.X, baseline, SKTextAlign.Center, _labelFont, _labelPaint);

        if (!styleSeries.ShowValuesInLabels || interiorDiameter < (28f * context.Density)) {
            return;
        }

        var valueText = FormatValue(node.Value, styleSeries.DataLabelFormat);
        var valueFontSize = Math.Max(4f * context.Density, Math.Min(_labelFont.Size * 0.52f, interiorDiameter * 0.22f));
        _labelFont.Size = valueFontSize;
        var valueWidth = EstimateTextWidth(valueText, valueFontSize);
        if (valueWidth > maxWidth) {
            _labelFont.Size = Math.Max(3.5f * context.Density, valueFontSize * (maxWidth / Math.Max(1f, valueWidth)));
        }

        var valueBaseline = center.Y + (_labelFont.Size * 1.05f);
        context.Canvas.DrawText(valueText, center.X, valueBaseline, SKTextAlign.Center, _labelFont, _labelPaint);
    }

    private RenderedBubble? FindBubbleAtPoint(SKPoint point, bool interactiveOnly) {
        for (var i = _visibleBubbles.Count - 1; i >= 0; i--) {
            var bubble = _visibleBubbles[i];
            if (interactiveOnly && !bubble.IsInteractive) {
                continue;
            }

            var dx = point.X - bubble.State.Position.X;
            var dy = point.Y - bubble.State.Position.Y;
            if ((dx * dx) + (dy * dy) <= bubble.State.Radius * bubble.State.Radius) {
                return bubble;
            }
        }

        return null;
    }

    private int GetConfigurationHash() {
        var hash = new HashCode();
        hash.Add(ValueSelector);
        hash.Add(GroupSelector);
        hash.Add(LeafLabelSelector);
        hash.Add(GroupLevelLabels);
        hash.Add(ColorSelector);
        hash.Add(DataLabelFormat);
        hash.Add(ShowLabels);
        hash.Add(ShowValuesInLabels);
        hash.Add(MinLabelFontSize);
        hash.Add(MaxLabelFontSize);
        hash.Add(MinBubbleRadius);
        hash.Add(MaxBubbleRadius);
        hash.Add(BubbleSpacing);
        hash.Add(TargetFillRatio);
        hash.Add(CanvasPadding);
        hash.Add(LabelPadding);
        hash.Add(EnableDrilldown);
        hash.Add(ConstrainToCanvas);
        hash.Add(ShowNavigation);
        hash.Add(ShowDrillIndicator);
        hash.Add(BackText);
        hash.Add(NavigationHeight);
        hash.Add(GravityStrength);
        hash.Add(CollisionStrength);
        hash.Add(VelocityDamping);
        hash.Add(EdgeBounce);
        hash.Add(ThrowStrength);
        hash.Add(PhysicsSubsteps);
        hash.Add(Interactions);
        hash.Add(Title);
        hash.Add(Color);
        hash.Add(TextColor);
        hash.Add(TooltipBackgroundColor);
        hash.Add(TooltipTextColor);
        hash.Add(Visible);
        hash.Add(AnimationEnabled);
        hash.Add(AnimationDuration);
        return hash.ToHashCode();
    }

    private void NotifyChildSeriesChanged() {
        _root = null;
        _nodeId = 0;
        _hierarchyVersion++;
        _drillPath.Clear();
        ResetSimulation();
        NotifyNestedParentSeriesChanged();
    }

    private void ResetSimulation() {
        _visibleBubbles.Clear();
        _bubbleStates.Clear();
        _draggedNodeId = null;
        _pointerDownNodeId = null;
        _dragReleaseVelocity = SKPoint.Empty;
        _lastPhysicsStepUtc = DateTime.MinValue;
        _backButtonRect = SKRect.Empty;
    }

    private SKColor ResolveNodeColor(TreeNode node, int fallbackIndex) {
        var styleSeries = node.StyleSeries;
        if (styleSeries.ColorSelector is not null) {
            var context = new BubbleColorContext<TData> {
                Depth = node.Depth,
                IsGroup = node.IsGroup,
                Key = node.DisplayLabel,
                Path = node.PathLabels,
                Value = node.Value,
                Data = node.SampleData
            };

            return Chart.GetThemeColor(styleSeries.ColorSelector(context));
        }

        if (styleSeries.Color.HasValue && styleSeries.Color.Value != TnTColor.None) {
            return Chart.GetThemeColor(styleSeries.Color.Value);
        }

        var paletteIndex = node.IsGroup
            ? Math.Abs(HashCode.Combine(node.Depth, node.Key))
            : Math.Abs(node.StableIndex + fallbackIndex);
        return Chart.GetPaletteColor(paletteIndex);
    }

    private static string FormatValue(decimal value, string? dataLabelFormat) {
        var format = string.IsNullOrWhiteSpace(dataLabelFormat) ? "{0:N0}" : dataLabelFormat;
        try {
            if (format.Contains("{0", StringComparison.Ordinal)) {
                return string.Format(format, value);
            }

            return value.ToString(format);
        }
        catch {
            return value.ToString("N0");
        }
    }

    private static SKColor GetContrastColor(SKColor bgColor) {
        var luminance = (0.2126f * bgColor.Red) + (0.7152f * bgColor.Green) + (0.0722f * bgColor.Blue);
        return luminance > 140f ? SKColors.Black : SKColors.White;
    }

    private static float EstimateTextWidth(string text, float size) {
        if (string.IsNullOrEmpty(text)) {
            return 0f;
        }

        return text.Length * (size * 0.57f);
    }

    private float GetRadiusZoomScale() {
        var zx = Math.Max(0.0001f, _zoomScaleX);
        var zy = Math.Max(0.0001f, _zoomScaleY);
        return MathF.Sqrt(zx * zy);
    }

    private SKPoint WorldToView(SKPoint worldPoint, SKPoint origin) {
        return new SKPoint(
            origin.X + ((worldPoint.X - origin.X) * _zoomScaleX),
            origin.Y + ((worldPoint.Y - origin.Y) * _zoomScaleY));
    }

    private SKPoint ViewToWorld(SKPoint viewPoint, SKPoint origin) {
        var safeX = Math.Max(0.0001f, _zoomScaleX);
        var safeY = Math.Max(0.0001f, _zoomScaleY);
        return new SKPoint(
            origin.X + ((viewPoint.X - origin.X) / safeX),
            origin.Y + ((viewPoint.Y - origin.Y) / safeY));
    }

    private SKPoint ToCanvasPoint(MouseEventArgs e) {
        var density = Math.Max(1f, Chart.Density);
        return new SKPoint((float)e.OffsetX * density, (float)e.OffsetY * density);
    }

    private static SKPoint ClampPointToArea(SKPoint point, float radius, SKRect area) {
        return new SKPoint(
            Math.Clamp(point.X, area.Left + radius, area.Right - radius),
            Math.Clamp(point.Y, area.Top + radius, area.Bottom - radius));
    }

    private static SKPoint ClampVelocity(SKPoint velocity, float maxSpeed) {
        var speedSq = (velocity.X * velocity.X) + (velocity.Y * velocity.Y);
        if (speedSq <= maxSpeed * maxSpeed) {
            return velocity;
        }

        var speed = MathF.Sqrt(speedSq);
        if (speed <= 0.0001f) {
            return SKPoint.Empty;
        }

        var factor = maxSpeed / speed;
        return new SKPoint(velocity.X * factor, velocity.Y * factor);
    }

    private SKPoint FindInitialBubblePosition(int nodeId, float targetRadius, SKPoint center, SKRect contentArea, float density) {
        if (_visibleBubbles.Count == 0) {
            return center;
        }

        var spacing = Math.Max(0f, BubbleSpacing * density);
        var seedOffset = (Math.Abs(HashCode.Combine(nodeId, 1301)) % 628) / 100f;
        var radialStep = Math.Max(3f * density, targetRadius * 0.34f);
        const float goldenAngle = 2.3999632f;

        for (var attempt = 0; attempt < 320; attempt++) {
            var angle = seedOffset + (attempt * goldenAngle);
            var distance = MathF.Sqrt(attempt) * radialStep;
            var candidate = new SKPoint(
                center.X + (MathF.Cos(angle) * distance),
                center.Y + (MathF.Sin(angle) * distance));

            if (ConstrainToCanvas) {
                candidate = ClampPointToArea(candidate, targetRadius, contentArea);
            }

            var overlaps = false;
            for (var i = 0; i < _visibleBubbles.Count; i++) {
                var other = _visibleBubbles[i].State;
                var dx = candidate.X - other.Position.X;
                var dy = candidate.Y - other.Position.Y;
                var minDistance = targetRadius + other.Radius + spacing;
                if (((dx * dx) + (dy * dy)) < (minDistance * minDistance)) {
                    overlaps = true;
                    break;
                }
            }

            if (!overlaps) {
                return candidate;
            }
        }

        return ConstrainToCanvas ? ClampPointToArea(center, targetRadius, contentArea) : center;
    }

    private static float Distance(SKPoint a, SKPoint b) {
        var dx = a.X - b.X;
        var dy = a.Y - b.Y;
        return MathF.Sqrt((dx * dx) + (dy * dy));
    }

    private static float Lerp(float from, float to, float t) {
        var clamped = Math.Clamp(t, 0f, 1f);
        return from + ((to - from) * clamped);
    }
}
