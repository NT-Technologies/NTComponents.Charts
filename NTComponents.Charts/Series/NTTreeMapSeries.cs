using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using NTComponents.Charts.Core;
using NTComponents.Charts.Core.Series;
using SkiaSharp;

namespace NTComponents.Charts;

/// <summary>
///     Context used by <see cref="NTTreeMapSeries{TData}.ColorSelector"/> to choose a treemap node color.
/// </summary>
public sealed class TreeMapColorContext<TData> where TData : class {
    public required int Depth { get; init; }
    public required bool IsGroup { get; init; }
    public required string Key { get; init; }
    public required IReadOnlyList<string> Path { get; init; }
    public required decimal Value { get; init; }
    public TData? Data { get; init; }
}

/// <summary>
///     Represents a treemap series that visualizes grouped or hierarchical data as nested rectangles.
/// </summary>
/// <typeparam name="TData">The type of the data.</typeparam>
public class NTTreeMapSeries<TData> : NTBaseSeries<TData>, ITreeMapDrillableSeries where TData : class {
    [Parameter]
    public Func<TData, decimal> ValueSelector { get; set; } = _ => 0;

    /// <summary>
    ///     Optional single-level group selector. When set, drill-down renders items within each group.
    /// </summary>
    [Parameter]
    public Func<TData, string?>? GroupSelector { get; set; }

    /// <summary>
    ///     Optional multi-level grouping selector. Returning multiple entries enables deep drill-down.
    /// </summary>
    [Parameter]
    public Func<TData, IEnumerable<string>>? GroupPathSelector { get; set; }

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
    ///     Optional callback for node color selection. Must return a <see cref="TnTColor"/>.
    /// </summary>
    [Parameter]
    public Func<TreeMapColorContext<TData>, TnTColor>? ColorSelector { get; set; }

    [Parameter]
    public float ItemPadding { get; set; } = 2f;

    [Parameter]
    public string DataLabelFormat { get; set; } = "{0:N0}";

    [Parameter]
    public bool ShowLabels { get; set; } = true;

    [Parameter]
    public bool ShowValuesInLabels { get; set; } = true;

    [Parameter]
    public float MinLabelFontSize { get; set; } = 9f;

    [Parameter]
    public float MaxLabelFontSize { get; set; } = 28f;

    [Parameter]
    public float MinLabelWidth { get; set; } = 36f;

    [Parameter]
    public float MinLabelHeight { get; set; } = 20f;

    [Parameter]
    public bool EnableDrilldown { get; set; } = true;

    [Parameter]
    public bool ShowNavigation { get; set; } = true;

    [Parameter]
    public string BackText { get; set; } = "Back";

    [Parameter]
    public float NavigationHeight { get; set; } = 30f;

    /// <inheritdoc />
    internal override TooltipInfo GetTooltipInfo(TData data) {
        if (Chart.HoveredSeries == this &&
            Chart.HoveredPointIndex is int hoveredPointIndex) {
            for (var i = _visibleNodes.Count - 1; i >= 0; i--) {
                var node = _visibleNodes[i].Node;
                if (node.StableIndex != hoveredPointIndex) {
                    continue;
                }

                return new TooltipInfo {
                    Header = node.DisplayLabel,
                    Lines = [
                        new TooltipLine {
                            Label = Title ?? "Series",
                            Value = FormatValue(node.Value),
                            Color = Chart.GetSeriesColor(this)
                        }
                    ]
                };
            }
        }

        var value = ValueSelector(data);
        var labelValue = FormatValue(value);
        var xValue = XValue?.Invoke(data) ?? LeafLabelSelector?.Invoke(data);

        return new TooltipInfo {
            Header = xValue?.ToString(),
            Lines = [
                new TooltipLine {
                    Label = Title ?? "Series",
                    Value = labelValue,
                    Color = Chart.GetSeriesColor(this)
                }
            ]
        };
    }

    public override ChartCoordinateSystem CoordinateSystem => ChartCoordinateSystem.TreeMap;

    bool ITreeMapDrillableSeries.IsInDrilldown => EnableDrilldown && _drillPath.Count > 0;

    internal override decimal GetTotalValue() => _root?.Value ?? (Data?.Sum(d => Math.Max(0m, ValueSelector(d))) ?? 0m);

    private sealed class TreeNode {
        public required string Key { get; init; }
        public required string DisplayLabel { get; init; }
        public required int Depth { get; init; }
        public required int StableIndex { get; init; }
        public required TreeNode? Parent { get; init; }
        public required string[] PathLabels { get; init; }
        public decimal Value { get; set; }
        public int ItemCount { get; set; }
        public TData? SampleData { get; set; }
        public List<TreeNode> Children { get; } = [];
        public Dictionary<string, TreeNode>? ChildLookup { get; set; }
        public bool IsGroup => Children.Count > 0;
    }

    private readonly record struct DrillStep(string Key, string Label);

    private readonly record struct RenderedNode(TreeNode Node, SKRect Rect, bool HasVisibleChildren, bool IsInteractive);

    private readonly record struct PathSegment(string Key, string Label);

    private TreeNode? _root;
    private readonly List<DrillStep> _drillPath = [];
    private readonly List<RenderedNode> _visibleNodes = [];
    private string? _lastLayoutKey;
    private int _hierarchyVersion;
    private int _nodeId;

    private SKRect _lastSeriesArea = SKRect.Empty;
    private SKRect _backButtonRect = SKRect.Empty;

    private Func<TData, string?>? _lastGroupSelectorRef;
    private Func<TData, IEnumerable<string>>? _lastGroupPathSelectorRef;
    private Func<TData, string?>? _lastLeafLabelSelectorRef;
    private Func<TreeMapColorContext<TData>, TnTColor>? _lastColorSelectorRef;

    private SKPaint? _itemPaint;
    private SKPaint? _borderPaint;
    private SKPaint? _labelPaint;
    private SKPaint? _navPaint;
    private SKPaint? _navTextPaint;
    private SKPaint? _navButtonPaint;
    private SKPaint? _navButtonTextPaint;
    private SKFont? _labelFont;
    private SKFont? _navFont;

    private int? _hoverAnimFromIndex;
    private int? _hoverAnimToIndex;
    private DateTime _hoverAnimStartUtc = DateTime.MinValue;

    protected override void OnDataChanged() {
        base.OnDataChanged();
        _root = null;
        _nodeId = 0;
        _hierarchyVersion++;
        _drillPath.Clear();
        InvalidateLayout();
    }

    protected override void OnParametersSet() {
        base.OnParametersSet();

        if (!ReferenceEquals(_lastGroupSelectorRef, GroupSelector) ||
            !ReferenceEquals(_lastGroupPathSelectorRef, GroupPathSelector) ||
            !ReferenceEquals(_lastLeafLabelSelectorRef, LeafLabelSelector) ||
            !ReferenceEquals(_lastColorSelectorRef, ColorSelector)) {
            _lastGroupSelectorRef = GroupSelector;
            _lastGroupPathSelectorRef = GroupPathSelector;
            _lastLeafLabelSelectorRef = LeafLabelSelector;
            _lastColorSelectorRef = ColorSelector;

            _root = null;
            _nodeId = 0;
            _hierarchyVersion++;
            _drillPath.Clear();
            InvalidateLayout();
        }
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
        }

        var navHeight = ShowNavigation ? NavigationHeight * context.Density : 0f;
        var contentArea = navHeight > 0
            ? new SKRect(renderArea.Left, renderArea.Top + navHeight, renderArea.Right, renderArea.Bottom)
            : renderArea;

        _lastSeriesArea = renderArea;
        BuildVisibleLayout(context, current, contentArea);

        InitializePaints(context);

        if (ShowNavigation) {
            RenderNavigation(context, renderArea, current, navHeight);
        }
        else {
            _backButtonRect = SKRect.Empty;
        }

        var progress = GetAnimationProgress();
        const float parentPhasePortion = 0.58f;
        var parentPhaseProgress = Math.Clamp(progress / parentPhasePortion, 0f, 1f);
        var childPhaseProgress = progress <= parentPhasePortion
            ? 0f
            : Math.Clamp((progress - parentPhasePortion) / (1f - parentPhasePortion), 0f, 1f);
        var parentEasedProgress = BackEase(parentPhaseProgress);
        var childEasedProgress = BackEase(childPhaseProgress);
        var visibilityFactor = VisibilityFactor;

        UpdateHoverAnimationState();
        var hoverProgress = GetHoverAnimationProgress();
        var baseBorderWidth = Math.Max(1f, context.Density);

        for (var i = 0; i < _visibleNodes.Count; i++) {
            var rendered = _visibleNodes[i];
            var node = rendered.Node;
            var rect = rendered.Rect;

            if (rect.Width <= 0 || rect.Height <= 0) {
                continue;
            }

            var nodePhaseProgress = rendered.IsInteractive ? parentPhaseProgress : childPhaseProgress;
            if (!rendered.IsInteractive && nodePhaseProgress <= 0f) {
                continue;
            }

            var nodeEasedProgress = rendered.IsInteractive ? parentEasedProgress : childEasedProgress;
            if (nodePhaseProgress < 1f) {
                var centerX = rect.MidX;
                var centerY = rect.MidY;
                var w = rect.Width * nodeEasedProgress;
                var h = rect.Height * nodeEasedProgress;
                rect = new SKRect(centerX - (w / 2f), centerY - (h / 2f), centerX + (w / 2f), centerY + (h / 2f));
            }

            var baseColor = ResolveNodeColor(node, i);
            var labelColorOverride = default(SKColor?);

            if (!node.IsGroup && node.SampleData is not null) {
                var args = new NTDataPointRenderArgs<TData> {
                    Data = node.SampleData,
                    Index = node.StableIndex,
                    Color = baseColor,
                    GetThemeColor = Chart.GetThemeColor
                };
                OnDataPointRender?.Invoke(args);
                baseColor = args.Color ?? baseColor;
                labelColorOverride = args.DataLabelColor;
            }

            var hoverIntensity = GetNodeHoverIntensity(node.StableIndex, hoverProgress);
            if (hoverIntensity > 0f) {
                var scale = 1f + (0.04f * hoverIntensity);
                rect = ScaleRect(rect, scale);
                baseColor = BlendTowards(baseColor, SKColors.White, 0.15f * hoverIntensity);
            }

            var nodeAlphaFactor = rendered.IsInteractive ? 1f : nodePhaseProgress;
            var color = baseColor.WithAlpha((byte)(baseColor.Alpha * visibilityFactor * nodeAlphaFactor));
            _itemPaint!.Color = color;
            context.Canvas.DrawRect(rect, _itemPaint);

            _borderPaint!.StrokeWidth = baseBorderWidth + (hoverIntensity * 1.4f * context.Density);
            _borderPaint!.Color = Chart.GetThemeColor(TnTColor.OutlineVariant).WithAlpha((byte)((210f + (35f * hoverIntensity)) * visibilityFactor));
            context.Canvas.DrawRect(rect, _borderPaint);

            if (ShowLabels) {
                RenderLabel(context, rect, node, color, labelColorOverride, rendered.HasVisibleChildren);
            }
        }

        return renderArea;
    }

    public override void HandleMouseDown(MouseEventArgs e) {
        if (!EnableDrilldown || _visibleNodes.Count == 0) {
            return;
        }

        var point = new SKPoint((float)e.OffsetX * Chart.Density, (float)e.OffsetY * Chart.Density);
        if (!_lastSeriesArea.Contains(point)) {
            return;
        }

        if (_backButtonRect.Contains(point) && _drillPath.Count > 0) {
            _drillPath.RemoveAt(_drillPath.Count - 1);
            InvalidateLayout();
            return;
        }

        for (var i = _visibleNodes.Count - 1; i >= 0; i--) {
            var rendered = _visibleNodes[i];
            if (!rendered.IsInteractive) {
                continue;
            }

            if (!rendered.Rect.Contains(point)) {
                continue;
            }

            if (rendered.Node.IsGroup) {
                _drillPath.Add(new DrillStep(rendered.Node.Key, rendered.Node.DisplayLabel));
                InvalidateLayout();
            }
            return;
        }
    }

    protected override void Dispose(bool disposing) {
        if (disposing) {
            _itemPaint?.Dispose();
            _borderPaint?.Dispose();
            _labelPaint?.Dispose();
            _navPaint?.Dispose();
            _navTextPaint?.Dispose();
            _navButtonPaint?.Dispose();
            _navButtonTextPaint?.Dispose();
            _labelFont?.Dispose();
            _navFont?.Dispose();

            _itemPaint = null;
            _borderPaint = null;
            _labelPaint = null;
            _navPaint = null;
            _navTextPaint = null;
            _navButtonPaint = null;
            _navButtonTextPaint = null;
            _labelFont = null;
            _navFont = null;
        }

        base.Dispose(disposing);
    }

    public override (int Index, TData? Data)? HitTest(SKPoint point, SKRect renderArea) {
        if (_visibleNodes.Count == 0) {
            return null;
        }

        for (var i = _visibleNodes.Count - 1; i >= 0; i--) {
            var rendered = _visibleNodes[i];
            if (!rendered.IsInteractive) {
                continue;
            }

            if (!rendered.Rect.Contains(point)) {
                continue;
            }

            return rendered.Node.IsGroup
                ? (rendered.Node.StableIndex, rendered.Node.SampleData)
                : (rendered.Node.StableIndex, rendered.Node.SampleData);
        }

        return null;
    }

    internal override IEnumerable<LegendItemInfo<TData>> GetLegendItems() {
        yield return new LegendItemInfo<TData> {
            Label = Title ?? "TreeMap",
            Color = Chart.GetSeriesColor(this),
            Series = this,
            Index = null,
            IsVisible = Visible
        };
    }

    private void EnsureHierarchy(IEnumerable<TData> dataItems) {
        if (_root is not null) {
            return;
        }

        _nodeId = 0;
        var root = new TreeNode {
            Key = "$root",
            DisplayLabel = "Root",
            Depth = -1,
            StableIndex = _nodeId++,
            Parent = null,
            PathLabels = []
        };

        var index = 0;
        foreach (var item in dataItems) {
            var value = Math.Max(0m, ValueSelector(item));
            if (value <= 0m) {
                index++;
                continue;
            }

            root.Value += value;
            root.ItemCount++;
            root.SampleData ??= item;

            var pathSegments = ResolvePath(item, index);
            var current = root;

            for (var p = 0; p < pathSegments.Count; p++) {
                var segment = pathSegments[p];
                current.ChildLookup ??= new Dictionary<string, TreeNode>(StringComparer.Ordinal);

                if (!current.ChildLookup.TryGetValue(segment.Key, out var child)) {
                    child = new TreeNode {
                        Key = segment.Key,
                        DisplayLabel = segment.Label,
                        Depth = current.Depth + 1,
                        StableIndex = _nodeId++,
                        Parent = current,
                        PathLabels = [.. current.PathLabels, segment.Label]
                    };

                    current.ChildLookup[segment.Key] = child;
                    current.Children.Add(child);
                }

                child.Value += value;
                child.ItemCount++;
                child.SampleData ??= item;
                current = child;
            }

            index++;
        }

        _root = root;
        _hierarchyVersion++;
        InvalidateLayout();
    }

    private List<PathSegment> ResolvePath(TData item, int index) {
        var segments = new List<PathSegment>(4);

        if (GroupPathSelector is not null) {
            foreach (var rawSegment in GroupPathSelector(item) ?? []) {
                if (string.IsNullOrWhiteSpace(rawSegment)) {
                    continue;
                }

                var segment = rawSegment.Trim();
                segments.Add(new PathSegment(segment, segment));
            }

            if (segments.Count == 0) {
                var leafLabel = ResolveLeafLabel(item, index);
                segments.Add(new PathSegment($"{leafLabel}#{index}", leafLabel));
            }

            return segments;
        }

        if (GroupSelector is not null) {
            var group = GroupSelector(item);
            var normalizedGroup = string.IsNullOrWhiteSpace(group) ? "Ungrouped" : group.Trim();
            segments.Add(new PathSegment(normalizedGroup, normalizedGroup));

            var leafLabel = ResolveLeafLabel(item, index);
            segments.Add(new PathSegment($"{leafLabel}#{index}", leafLabel));
            return segments;
        }

        var label = ResolveLeafLabel(item, index);
        segments.Add(new PathSegment($"{label}#{index}", label));
        return segments;
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

    private void BuildVisibleLayout(NTRenderContext context, TreeNode current, SKRect contentArea) {
        if (contentArea.Width <= 0 || contentArea.Height <= 0) {
            _visibleNodes.Clear();
            _lastLayoutKey = null;
            return;
        }

        var key = BuildLayoutKey(context, current, contentArea);
        if (string.Equals(_lastLayoutKey, key, StringComparison.Ordinal)) {
            return;
        }

        _lastLayoutKey = key;
        _visibleNodes.Clear();

        if (current.Children.Count == 0) {
            return;
        }

        var nodes = current.Children
            .Where(c => c.Value > 0m)
            .OrderByDescending(c => c.Value)
            .ToList();

        if (nodes.Count == 0) {
            return;
        }

        PartitionNodes(nodes, 0, nodes.Count, contentArea, horizontal: contentArea.Width >= contentArea.Height, context.Density, nestedDepth: 0);
    }

    private string BuildLayoutKey(NTRenderContext context, TreeNode current, SKRect contentArea) {
        return $"{_hierarchyVersion}|{current.StableIndex}|{Math.Round(contentArea.Left, 1)}|{Math.Round(contentArea.Top, 1)}|{Math.Round(contentArea.Right, 1)}|{Math.Round(contentArea.Bottom, 1)}|{Math.Round(context.Density, 3)}|{Math.Round(ItemPadding, 2)}|{_drillPath.Count}|{string.Join('>', _drillPath.Select(d => d.Key))}";
    }

    private void PartitionNodes(List<TreeNode> nodes, int start, int length, SKRect area, bool horizontal, float density, int nestedDepth) {
        if (length <= 0) {
            return;
        }

        if (length == 1) {
            var node = nodes[start];
            var rect = ApplyPadding(area, ItemPadding * density);
            if (rect.Width > 0 && rect.Height > 0) {
                var hasVisibleChildren = false;
                var childArea = SKRect.Empty;
                if (nestedDepth == 0 && node.Children.Count > 0 && TryGetChildContentArea(rect, density, out childArea)) {
                    hasVisibleChildren = node.Children.Any(c => c.Value > 0m);
                }

                _visibleNodes.Add(new RenderedNode(node, rect, hasVisibleChildren, IsInteractive: nestedDepth == 0));

                if (hasVisibleChildren) {
                    var childNodes = node.Children
                        .Where(c => c.Value > 0m)
                        .OrderByDescending(c => c.Value)
                        .ToList();
                    if (childNodes.Count > 0) {
                        PartitionNodes(childNodes, 0, childNodes.Count, childArea, horizontal: childArea.Width >= childArea.Height, density, nestedDepth: nestedDepth + 1);
                    }
                }
            }
            return;
        }

        decimal total = 0m;
        for (var i = 0; i < length; i++) {
            total += nodes[start + i].Value;
        }

        if (total <= 0m) {
            return;
        }

        var half = total / 2m;
        decimal running = 0m;
        var splitOffset = 1;

        for (var i = 0; i < length; i++) {
            running += nodes[start + i].Value;
            if (running >= half) {
                splitOffset = i + 1;
                break;
            }
        }

        splitOffset = Math.Clamp(splitOffset, 1, length - 1);

        var leftLength = splitOffset;
        var rightLength = length - splitOffset;

        decimal leftValue = 0m;
        for (var i = 0; i < leftLength; i++) {
            leftValue += nodes[start + i].Value;
        }

        if (horizontal) {
            var leftWidth = (float)(area.Width * (double)(leftValue / total));
            var leftArea = new SKRect(area.Left, area.Top, area.Left + leftWidth, area.Bottom);
            var rightArea = new SKRect(area.Left + leftWidth, area.Top, area.Right, area.Bottom);

            PartitionNodes(nodes, start, leftLength, leftArea, horizontal: leftArea.Width >= leftArea.Height, density, nestedDepth);
            PartitionNodes(nodes, start + splitOffset, rightLength, rightArea, horizontal: rightArea.Width >= rightArea.Height, density, nestedDepth);
        }
        else {
            var topHeight = (float)(area.Height * (double)(leftValue / total));
            var topArea = new SKRect(area.Left, area.Top, area.Right, area.Top + topHeight);
            var bottomArea = new SKRect(area.Left, area.Top + topHeight, area.Right, area.Bottom);

            PartitionNodes(nodes, start, leftLength, topArea, horizontal: topArea.Width >= topArea.Height, density, nestedDepth);
            PartitionNodes(nodes, start + splitOffset, rightLength, bottomArea, horizontal: bottomArea.Width >= bottomArea.Height, density, nestedDepth);
        }
    }

    private static SKRect ApplyPadding(SKRect rect, float padding) {
        if (padding <= 0f) {
            return rect;
        }

        if (rect.Width <= (padding * 2f) || rect.Height <= (padding * 2f)) {
            return rect;
        }

        rect.Inflate(-padding, -padding);
        return rect;
    }

    private static SKRect ScaleRect(SKRect rect, float scale) {
        if (Math.Abs(scale - 1f) < 0.0001f) {
            return rect;
        }

        var halfW = (rect.Width * scale) / 2f;
        var halfH = (rect.Height * scale) / 2f;
        return new SKRect(rect.MidX - halfW, rect.MidY - halfH, rect.MidX + halfW, rect.MidY + halfH);
    }

    private static SKColor BlendTowards(SKColor from, SKColor to, float amount) {
        var t = Math.Clamp(amount, 0f, 1f);
        return new SKColor(
            (byte)(from.Red + ((to.Red - from.Red) * t)),
            (byte)(from.Green + ((to.Green - from.Green) * t)),
            (byte)(from.Blue + ((to.Blue - from.Blue) * t)),
            from.Alpha);
    }

    private void UpdateHoverAnimationState() {
        var nextHoveredIndex = Chart.HoveredSeries == this ? Chart.HoveredPointIndex : null;
        if (nextHoveredIndex == _hoverAnimToIndex) {
            return;
        }

        _hoverAnimFromIndex = _hoverAnimToIndex;
        _hoverAnimToIndex = nextHoveredIndex;
        _hoverAnimStartUtc = DateTime.UtcNow;
    }

    private float GetHoverAnimationProgress() {
        if (_hoverAnimFromIndex == _hoverAnimToIndex) {
            return 1f;
        }

        var durationMs = Math.Max(1.0, Chart.HoverAnimationDuration.TotalMilliseconds);
        var elapsedMs = (DateTime.UtcNow - _hoverAnimStartUtc).TotalMilliseconds;
        var t = (float)Math.Clamp(elapsedMs / durationMs, 0.0, 1.0);
        return t * t * (3f - (2f * t));
    }

    private float GetNodeHoverIntensity(int nodeIndex, float progress) {
        if (_hoverAnimFromIndex == _hoverAnimToIndex) {
            return _hoverAnimToIndex == nodeIndex ? 1f : 0f;
        }

        if (_hoverAnimToIndex == nodeIndex) {
            return progress;
        }

        if (_hoverAnimFromIndex == nodeIndex) {
            return 1f - progress;
        }

        return 0f;
    }

    private static bool TryGetChildContentArea(SKRect rect, float density, out SKRect childArea) {
        var headerHeight = Math.Clamp(rect.Height * 0.16f, 12f * density, 28f * density);
        var inset = Math.Max(1f, 1.5f * density);
        childArea = new SKRect(rect.Left + inset, rect.Top + headerHeight, rect.Right - inset, rect.Bottom - inset);

        if (childArea.Width <= (8f * density) || childArea.Height <= (8f * density)) {
            childArea = SKRect.Empty;
            return false;
        }

        return true;
    }

    private SKColor ResolveNodeColor(TreeNode node, int fallbackIndex) {
        if (ColorSelector is not null) {
            var colorContext = new TreeMapColorContext<TData> {
                Depth = node.Depth,
                IsGroup = node.IsGroup,
                Key = node.DisplayLabel,
                Path = node.PathLabels,
                Value = node.Value,
                Data = node.SampleData
            };

            return Chart.GetThemeColor(ColorSelector(colorContext));
        }

        var paletteIndex = node.IsGroup
            ? Math.Abs(HashCode.Combine(node.Depth, node.Key))
            : Math.Abs(node.StableIndex + fallbackIndex);

        return Chart.GetPaletteColor(paletteIndex);
    }

    private void InitializePaints(NTRenderContext context) {
        _itemPaint ??= new SKPaint {
            Style = SKPaintStyle.Fill,
            IsAntialias = true
        };

        _borderPaint ??= new SKPaint {
            Style = SKPaintStyle.Stroke,
            StrokeWidth = Math.Max(1f, 1f * context.Density),
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
            IsAntialias = true,
            Color = Chart.GetThemeColor(TextColor ?? Chart.TextColor)
        };

        _navButtonPaint ??= new SKPaint {
            Style = SKPaintStyle.Fill,
            IsAntialias = true
        };

        _navButtonTextPaint ??= new SKPaint {
            IsAntialias = true,
            Color = Chart.GetThemeColor(TextColor ?? Chart.TextColor)
        };

        _labelFont ??= new SKFont {
            Embolden = true,
            Typeface = context.DefaultFont.Typeface
        };

        _navFont ??= new SKFont {
            Embolden = true,
            Typeface = context.RegularFont.Typeface,
            Size = 12f * context.Density
        };

        _navTextPaint.Color = Chart.GetThemeColor(TextColor ?? Chart.TextColor);
        _navButtonTextPaint.Color = Chart.GetThemeColor(TextColor ?? Chart.TextColor);
        _navFont.Size = 12f * context.Density;
    }

    private void RenderNavigation(NTRenderContext context, SKRect renderArea, TreeNode current, float navHeight) {
        if (navHeight <= 0f) {
            _backButtonRect = SKRect.Empty;
            return;
        }

        var navArea = new SKRect(renderArea.Left, renderArea.Top, renderArea.Right, renderArea.Top + navHeight);
        _navPaint!.Color = Chart.GetThemeColor(TnTColor.SurfaceContainerLow).WithAlpha((byte)(230 * VisibilityFactor));
        context.Canvas.DrawRect(navArea, _navPaint);

        _borderPaint!.Color = Chart.GetThemeColor(TnTColor.OutlineVariant).WithAlpha((byte)(180 * VisibilityFactor));
        context.Canvas.DrawLine(navArea.Left, navArea.Bottom, navArea.Right, navArea.Bottom, _borderPaint);

        float textX = navArea.Left + (8f * context.Density);
        _backButtonRect = SKRect.Empty;

        if (EnableDrilldown && _drillPath.Count > 0) {
            var btnHeight = navHeight - (8f * context.Density);
            var btnWidth = Math.Max(56f * context.Density, (BackText.Length + 3) * 7f * context.Density);
            var btnTop = navArea.Top + (4f * context.Density);
            var btnLeft = navArea.Left + (6f * context.Density);
            _backButtonRect = new SKRect(btnLeft, btnTop, btnLeft + btnWidth, btnTop + btnHeight);

            _navButtonPaint!.Color = Chart.GetThemeColor(TnTColor.SurfaceVariant).WithAlpha((byte)(220 * VisibilityFactor));
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
        _navTextPaint!.Color = Chart.GetThemeColor(TextColor ?? Chart.TextColor).WithAlpha((byte)(255 * VisibilityFactor));

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

    private void RenderLabel(NTRenderContext context, SKRect rect, TreeNode node, SKColor bgColor, SKColor? overrideLabelColor, bool hasVisibleChildren) {
        var minWidth = MinLabelWidth * context.Density;
        var minHeight = MinLabelHeight * context.Density;
        if (rect.Width < minWidth || rect.Height < minHeight) {
            return;
        }

        var maxFont = MaxLabelFontSize * context.Density;
        var configuredMinFont = MinLabelFontSize * context.Density;
        var minFont = Math.Min(configuredMinFont, Math.Max(4f * context.Density, configuredMinFont * 0.60f));
        var dynamicFont = Math.Clamp(Math.Min(rect.Width, rect.Height) * 0.24f, minFont, maxFont);
        if (!hasVisibleChildren && dynamicFont <= minFont * 0.95f) {
            return;
        }

        var textColor = overrideLabelColor ?? GetContrastColor(bgColor);
        _labelPaint!.Color = textColor.WithAlpha((byte)(255 * VisibilityFactor));

        var label = node.DisplayLabel;
        if (string.IsNullOrWhiteSpace(label)) {
            return;
        }

        context.Canvas.Save();
        context.Canvas.ClipRect(rect);

        if (hasVisibleChildren && TryGetChildContentArea(rect, context.Density, out var childArea)) {
            var headerRect = new SKRect(rect.Left, rect.Top, rect.Right, childArea.Top);
            var headerFont = FitTextToBounds(
                label,
                preferredSize: headerRect.Height * 0.58f,
                minSize: minFont,
                maxSize: maxFont,
                maxWidth: headerRect.Width - (10f * context.Density),
                maxHeight: headerRect.Height * 0.82f);
            if (headerRect.Width >= (minWidth * 0.8f) &&
                headerRect.Height >= (minHeight * 0.6f) &&
                headerFont > 0f) {
                _labelFont!.Size = headerFont;
                var textX = headerRect.Left + (5f * context.Density);
                var baseline = headerRect.Top + (headerRect.Height * 0.70f);
                context.Canvas.ClipRect(headerRect);
                context.Canvas.DrawText(label, textX, baseline, SKTextAlign.Left, _labelFont, _labelPaint);
            }

            context.Canvas.Restore();
            return;
        }

        var horizontalInset = 8f * context.Density;
        var primaryFont = FitTextToBounds(
            label,
            preferredSize: dynamicFont,
            minSize: minFont,
            maxSize: maxFont,
            maxWidth: rect.Width - (horizontalInset * 2f),
            maxHeight: ShowValuesInLabels ? rect.Height * 0.46f : rect.Height * 0.78f);
        if (primaryFont <= 0f) {
            context.Canvas.Restore();
            return;
        }

        _labelFont!.Size = primaryFont;
        var centerX = rect.MidX;
        var primaryBaseline = rect.MidY + (primaryFont * 0.30f);
        context.Canvas.DrawText(label, centerX, primaryBaseline, SKTextAlign.Center, _labelFont, _labelPaint);

        if (ShowValuesInLabels && rect.Height > (primaryFont * 1.9f) && rect.Width > (primaryFont * 2.2f)) {
            var valueText = FormatValue(node.Value);
            var secondarySize = FitTextToBounds(
                valueText,
                preferredSize: primaryFont * 0.58f,
                minSize: Math.Max(3f * context.Density, minFont * 0.80f),
                maxSize: primaryFont,
                maxWidth: rect.Width - (horizontalInset * 2f),
                maxHeight: rect.Height * 0.34f);
            if (secondarySize <= 0f) {
                context.Canvas.Restore();
                return;
            }

            _labelFont.Size = secondarySize;
            var secondaryBaseline = primaryBaseline + (secondarySize * 1.15f);
            context.Canvas.DrawText(valueText, centerX, secondaryBaseline, SKTextAlign.Center, _labelFont, _labelPaint);
        }

        context.Canvas.Restore();
    }

    private float FitTextToBounds(string text, float preferredSize, float minSize, float maxSize, float maxWidth, float maxHeight) {
        if (string.IsNullOrWhiteSpace(text) || maxWidth <= 1f || maxHeight <= 1f) {
            return 0f;
        }

        var size = Math.Clamp(preferredSize, minSize, maxSize);
        _labelFont!.Size = size;
        var measuredWidth = Math.Max(1f, _labelFont.MeasureText(text));
        if (measuredWidth > maxWidth) {
            size *= maxWidth / measuredWidth;
        }

        if (size > maxHeight) {
            size = maxHeight;
        }

        if (size < minSize) {
            return 0f;
        }

        _labelFont.Size = size;
        measuredWidth = _labelFont.MeasureText(text);
        if (measuredWidth > maxWidth) {
            size *= maxWidth / Math.Max(1f, measuredWidth);
        }

        return size >= minSize ? size : 0f;
    }

    private string FormatValue(decimal value) {
        try {
            if (DataLabelFormat.Contains("{0", StringComparison.Ordinal)) {
                return string.Format(DataLabelFormat, value);
            }

            return value.ToString(DataLabelFormat);
        }
        catch {
            return value.ToString("N0");
        }
    }

    private static SKColor GetContrastColor(SKColor bgColor) {
        var luminance = (0.2126f * bgColor.Red) + (0.7152f * bgColor.Green) + (0.0722f * bgColor.Blue);
        return luminance > 140f ? SKColors.Black : SKColors.White;
    }

    private void InvalidateLayout() {
        _lastLayoutKey = null;
        _visibleNodes.Clear();
        _backButtonRect = SKRect.Empty;
    }
}
