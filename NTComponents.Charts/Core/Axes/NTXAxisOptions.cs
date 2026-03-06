using Microsoft.AspNetCore.Components;
using SkiaSharp;
using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using NTComponents.Charts;

namespace NTComponents.Charts.Core.Axes;

/// <summary>
///     Options for the X axis of a cartesian chart.
/// </summary>
public class NTXAxisOptions<TData, TAxisType> : NTAxisOptions<TData>, INTXAxis<TData> where TData : class {

    public static INTXAxis<TData> Default { get; } = new NTXAxisOptions<TData, TAxisType>() {
        ValueSelector = data => default!
    };

    [Parameter, EditorRequired]
    public Func<TData, TAxisType> ValueSelector { get; set; }

    /// <inheritdoc />
    public bool IsCategorical {
        get {
            // Horizontal bar charts flip axis roles: X becomes a value axis.
            if (Chart is NTChart<TData> ntChart &&
                ntChart.Series.OfType<NTBarSeries<TData>>().Any(s => s.IsEffectivelyVisible && s.Orientation == NTChartOrientation.Horizontal)) {
                return false;
            }

            var type = typeof(TAxisType);
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>)) {
                type = Nullable.GetUnderlyingType(type)!;
            }

            return !(type == typeof(DateTime) ||
                     type == typeof(DateTimeOffset) ||
                     type == typeof(DateOnly) ||
                     type == typeof(TimeOnly) ||
                     type.GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(INumberBase<>)));
        }
    }

    [Parameter]
    public bool EnableAutoDateGrouping { get; set; } = true;

    [Parameter]
    public int DateGroupingThreshold { get; set; }

    private readonly List<AxisTick> _cachedTicks = [];
    private AxisCacheKey? _cacheKey;
    private SKPaint _linePaint = default!;
    private float _rotation;
    private float _tickLabelHeight;
    private SKFont _textFont = default!;
    private SKPaint _textPaint = default!;
    private SKFont _titleFont = default!;
    private SKPaint _titlePaint = default!;
    private float? _titleHeight;
    private NTDateGroupingLevel _activeDateGroupingLevel = NTDateGroupingLevel.None;
    private readonly Dictionary<DateTickCacheKey, List<AxisTick>> _groupedDateTickCache = [];

    public override void Dispose() {
        _textPaint?.Dispose();
        _titlePaint?.Dispose();
        _linePaint?.Dispose();
        _textFont?.Dispose();
        _titleFont?.Dispose();
        Chart.UnregisterAxis(this);
        base.Dispose();
    }

    public override void Invalidate() {
        _cacheKey = null;
        _cachedTicks.Clear();
        _groupedDateTickCache.Clear();
        _rotation = 0f;
        _tickLabelHeight = 0f;
        _titleHeight = null;
        _activeDateGroupingLevel = NTDateGroupingLevel.None;
    }

    /// <inheritdoc />
    public override SKRect Measure(NTRenderContext context, SKRect renderArea) {
        if (!Visible) {
            return renderArea;
        }

        InitializePaints(context);
        _titleHeight = GetTitleHeight(context, _titleFont);

        BuildTicks(context, renderArea);

        var tickHeight = 5 * context.Density;
        var totalHeight = _titleHeight.Value + _tickLabelHeight + tickHeight + (10 * context.Density);
        return new SKRect(renderArea.Left, renderArea.Top, renderArea.Right, renderArea.Bottom - totalHeight);
    }

    /// <inheritdoc />
    public override SKRect Render(NTRenderContext context, SKRect renderArea) {
        if (!Visible) {
            return renderArea;
        }

        InitializePaints(context);
        BuildTicks(context, renderArea);

        var plotArea = context.PlotArea;
        var axisY = plotArea.Bottom;
        var tickHeight = 5 * context.Density;

        context.Canvas.DrawLine(plotArea.Left, axisY, plotArea.Right, axisY, _linePaint);

        foreach (var tick in _cachedTicks) {
            var x = Chart.ScaleX(tick.Value, plotArea);
            if (x < plotArea.Left - context.Density || x > plotArea.Right + context.Density) {
                continue;
            }

            context.Canvas.DrawLine(x, axisY, x, axisY + tickHeight, _linePaint);

            var textBaseline = axisY + tickHeight + _tickLabelHeight + (2 * context.Density);
            if (_rotation == 0f) {
                context.Canvas.DrawText(tick.Label, x, textBaseline, SKTextAlign.Center, _textFont, _textPaint);
            }
            else {
                context.Canvas.Save();
                context.Canvas.Translate(x, textBaseline);
                context.Canvas.RotateDegrees(_rotation);
                context.Canvas.DrawText(tick.Label, 0, 0, SKTextAlign.Center, _textFont, _textPaint);
                context.Canvas.Restore();
            }
        }

        if (!string.IsNullOrEmpty(Title)) {
            var titleY = renderArea.Bottom - (2 * context.Density);
            context.Canvas.DrawText(Title, plotArea.MidX, titleY, SKTextAlign.Center, _titleFont, _titlePaint);
        }

        return renderArea;
    }

    protected override void OnInitialized() {
        base.OnInitialized();
        Chart.RegisterAxis(this);
    }

    private void BuildTicks(NTRenderContext context, SKRect renderArea) {
        var plotArea = context.PlotArea;
        var (min, max) = Chart.GetXRange(this, true);
        var allXCount = IsCategorical ? Chart.GetAllXValues().Count : 0;
        var key = new AxisCacheKey(
            IsCategorical,
            allXCount,
            Math.Round(min, 6),
            Math.Round(max, 6),
            Math.Round(plotArea.Width, 2),
            Math.Round(context.Density, 3),
            AxisFontSize,
            LabelFormat,
            Title,
            TitleFontSize,
            EnableAutoDateGrouping,
            DateGroupingThreshold);

        if (_cacheKey.HasValue && _cacheKey.Value.Equals(key)) {
            return;
        }

        _cacheKey = key;
        _cachedTicks.Clear();
        _rotation = 0f;
        _tickLabelHeight = 0f;
        _activeDateGroupingLevel = NTDateGroupingLevel.None;

        if (IsCategorical) {
            BuildCategoricalTicks(context, plotArea, min, max);
        }
        else {
            BuildContinuousTicks(context, min, max, plotArea.Width);
        }
    }

    private void BuildCategoricalTicks(NTRenderContext context, SKRect plotArea, double min, double max) {
        var allX = Chart.GetAllXValues();
        if (allX.Count == 0) {
            return;
        }

        var start = Math.Max(0, (int)Math.Floor(Math.Min(min, max)));
        var end = Math.Min(allX.Count - 1, (int)Math.Ceiling(Math.Max(min, max)));
        if (end < start) {
            return;
        }

        var visibleCount = end - start + 1;
        var (maxLabelWidth, maxLabelHeight) = MeasureSampleLabels(context, start, end, idx => FormatLabel(allX[idx], false));

        var targetTicks = EstimateTargetTicks(plotArea.Width, maxLabelWidth, context.Density, preferDensity: true);
        var step = Math.Max(1, (int)Math.Floor(visibleCount / (double)targetTicks));

        for (var i = start; i <= end; i += step) {
            _cachedTicks.Add(new AxisTick(i, FormatLabel(allX[i], false)));
        }

        if (_cachedTicks.Count == 0 || _cachedTicks[^1].Value != end) {
            _cachedTicks.Add(new AxisTick(end, FormatLabel(allX[end], false)));
        }

        var labelSlotWidth = plotArea.Width / Math.Max(1, _cachedTicks.Count - 1);
        if (maxLabelWidth + (5 * context.Density) > labelSlotWidth) {
            _rotation = -45f;
            var rad = Math.Abs(_rotation) * Math.PI / 180.0;
            _tickLabelHeight = (float)((Math.Sin(rad) * maxLabelWidth) + (Math.Cos(rad) * maxLabelHeight));
        }
        else {
            _tickLabelHeight = maxLabelHeight;
        }
    }

    private void BuildContinuousTicks(NTRenderContext context, double min, double max, float plotWidth) {
        if (double.IsNaN(min) || double.IsNaN(max) || double.IsInfinity(min) || double.IsInfinity(max)) {
            return;
        }

        if (Chart.IsXAxisDateTime) {
            BuildDateTimeTicks(context, min, max, plotWidth);
            return;
        }

        if (Math.Abs(max - min) < double.Epsilon) {
            _cachedTicks.Add(new AxisTick(min, FormatLabel(ToLabelValue(min), false)));
            _textFont.MeasureText(_cachedTicks[0].Label, out var bounds);
            _tickLabelHeight = bounds.Height;
            ApplyLabelRotationIfNeeded(context, plotWidth);
            return;
        }

        var minLabel = FormatLabel(ToLabelValue(min), false);
        var maxLabel = FormatLabel(ToLabelValue(max), false);

        _textFont.MeasureText(minLabel, out var minBounds);
        _textFont.MeasureText(maxLabel, out var maxBounds);
        var estWidth = Math.Max(minBounds.Width, maxBounds.Width);

        var targetTicks = EstimateTargetTicks(plotWidth, estWidth, context.Density, preferDensity: Chart.IsXAxisDateTime);
        var spacing = CalculateNiceSpacing(min, max, targetTicks);

        if (spacing <= 0 || double.IsInfinity(spacing) || double.IsNaN(spacing)) {
            _cachedTicks.Add(new AxisTick(min, minLabel));
            _cachedTicks.Add(new AxisTick(max, maxLabel));
            _tickLabelHeight = Math.Max(minBounds.Height, maxBounds.Height);
            ApplyLabelRotationIfNeeded(context, plotWidth);
            return;
        }

        var firstTick = Math.Ceiling(min / spacing) * spacing;
        var epsilon = spacing / 10000.0;

        for (var tick = firstTick; tick <= max + epsilon; tick += spacing) {
            if (tick < min - epsilon) {
                continue;
            }

            _cachedTicks.Add(new AxisTick(tick, FormatLabel(ToLabelValue(tick), false)));
        }

        if (_cachedTicks.Count == 0) {
            _cachedTicks.Add(new AxisTick(min, minLabel));
            _cachedTicks.Add(new AxisTick(max, maxLabel));
        }

        _tickLabelHeight = Math.Max(minBounds.Height, maxBounds.Height);
        ApplyLabelRotationIfNeeded(context, plotWidth);
    }

    private void BuildDateTimeTicks(NTRenderContext context, double min, double max, float plotWidth) {
        if (Math.Abs(max - min) < double.Epsilon) {
            _cachedTicks.Add(new AxisTick(min, FormatLabel(ToLabelValue(min), false)));
            _textFont.MeasureText(_cachedTicks[0].Label, out var bounds);
            _tickLabelHeight = bounds.Height;
            _activeDateGroupingLevel = NTDateGroupingLevel.Day;
            return;
        }

        _activeDateGroupingLevel = ResolveDateGroupingLevel(min, max, plotWidth, context.Density);

        var startTicks = (long)Math.Round(Math.Min(min, max));
        var endTicks = (long)Math.Round(Math.Max(min, max));
        startTicks = Math.Clamp(startTicks, DateTime.MinValue.Ticks, DateTime.MaxValue.Ticks);
        endTicks = Math.Clamp(endTicks, DateTime.MinValue.Ticks, DateTime.MaxValue.Ticks);

        var groupedTicks = GetOrCreateGroupedDateTicks(startTicks, endTicks, _activeDateGroupingLevel);
        foreach (var tick in DownsampleTicksForReadability(groupedTicks, plotWidth, context.Density)) {
            _cachedTicks.Add(tick);
        }

        if (_cachedTicks.Count == 0) {
            var start = new DateTime(startTicks);
            var end = new DateTime(endTicks);
            _cachedTicks.Add(new AxisTick(startTicks, FormatLabel(start, false)));
            if (endTicks != startTicks) {
                _cachedTicks.Add(new AxisTick(endTicks, FormatLabel(end, false)));
            }
        }

        float maxHeight = 0f;
        var sampleStep = Math.Max(1, _cachedTicks.Count / 24);
        for (var i = 0; i < _cachedTicks.Count; i += sampleStep) {
            _textFont.MeasureText(_cachedTicks[i].Label, out var bounds);
            maxHeight = Math.Max(maxHeight, bounds.Height);
        }
        _tickLabelHeight = maxHeight <= 0f ? (_textFont.Size + (2f * context.Density)) : maxHeight;
        ApplyLabelRotationIfNeeded(context, plotWidth);
    }

    private void ApplyLabelRotationIfNeeded(NTRenderContext context, float plotWidth) {
        if (_cachedTicks.Count <= 1) {
            if (_tickLabelHeight <= 0f) {
                _tickLabelHeight = _textFont.Size + (2f * context.Density);
            }
            return;
        }

        const int maxSamples = 24;
        var step = Math.Max(1, _cachedTicks.Count / maxSamples);
        float maxLabelWidth = 0f;
        float maxLabelHeight = 0f;

        for (var i = 0; i < _cachedTicks.Count; i += step) {
            _textFont.MeasureText(_cachedTicks[i].Label, out var bounds);
            maxLabelWidth = Math.Max(maxLabelWidth, bounds.Width);
            maxLabelHeight = Math.Max(maxLabelHeight, bounds.Height);
        }

        if ((_cachedTicks.Count - 1) % step != 0) {
            _textFont.MeasureText(_cachedTicks[^1].Label, out var bounds);
            maxLabelWidth = Math.Max(maxLabelWidth, bounds.Width);
            maxLabelHeight = Math.Max(maxLabelHeight, bounds.Height);
        }

        var labelSlotWidth = plotWidth / Math.Max(1, _cachedTicks.Count - 1);
        if (maxLabelWidth + (5f * context.Density) > labelSlotWidth) {
            _rotation = -45f;
            var radians = Math.Abs(_rotation) * Math.PI / 180.0;
            _tickLabelHeight = (float)((Math.Sin(radians) * maxLabelWidth) + (Math.Cos(radians) * maxLabelHeight));
            return;
        }

        _rotation = 0f;
        _tickLabelHeight = Math.Max(_tickLabelHeight, maxLabelHeight);
    }

    private (float MaxWidth, float MaxHeight) MeasureSampleLabels(NTRenderContext context, int start, int end, Func<int, string> labelFactory) {
        const int maxSamples = 24;
        var count = end - start + 1;
        if (count <= 0) {
            return (0, 0);
        }

        var step = Math.Max(1, count / maxSamples);
        float maxWidth = 0;
        float maxHeight = 0;

        for (var i = start; i <= end; i += step) {
            var label = labelFactory(i);
            _textFont.MeasureText(label, out var bounds);
            maxWidth = Math.Max(maxWidth, bounds.Width);
            maxHeight = Math.Max(maxHeight, bounds.Height);
        }

        if ((end - start) % step != 0) {
            var label = labelFactory(end);
            _textFont.MeasureText(label, out var bounds);
            maxWidth = Math.Max(maxWidth, bounds.Width);
            maxHeight = Math.Max(maxHeight, bounds.Height);
        }

        return (maxWidth, maxHeight);
    }

    private static int EstimateTargetTicks(float plotWidth, float estimatedLabelWidth, float density, bool preferDensity = false) {
        var padding = preferDensity ? (4 * density) : (8 * density);
        var width = Math.Max(1f, estimatedLabelWidth + padding);
        var densityBoost = preferDensity ? 1.25f : 1.45f;
        var minTicks = preferDensity ? 8 : 4;
        return Math.Max(minTicks, (int)((plotWidth / width) * densityBoost));
    }

    private static double CalculateNiceSpacing(double min, double max, int targetTicks) {
        var range = Math.Abs(max - min);
        if (range <= 0 || targetTicks <= 1) {
            return 0;
        }

        var rough = range / (targetTicks - 1);
        var magnitude = Math.Pow(10, Math.Floor(Math.Log10(rough)));
        var residual = rough / magnitude;

        var nice = residual switch {
            <= 1 => 1,
            <= 2 => 2,
            <= 5 => 5,
            _ => 10
        };

        return nice * magnitude;
    }

    private object ToLabelValue(double scaledValue) {
        if (!Chart.IsXAxisDateTime) {
            return scaledValue;
        }

        var ticks = (long)Math.Round(scaledValue);
        if (ticks >= DateTime.MinValue.Ticks && ticks <= DateTime.MaxValue.Ticks) {
            return new DateTime(ticks);
        }

        return scaledValue;
    }

    private float GetTitleHeight(NTRenderContext context, SKFont font) {
        if (string.IsNullOrEmpty(Title)) {
            return 0f;
        }

        font.MeasureText(Title, out var bounds);
        return bounds.Height + (5 * context.Density);
    }

    public string FormatValue(object? value, bool forTooltip = false) => FormatLabel(value, forTooltip);

    public NTDateGroupingLevel ResolveDateGroupingLevel(double min, double max, float plotWidth, float density) {
        if (!Chart.IsXAxisDateTime || !EnableAutoDateGrouping) {
            return NTDateGroupingLevel.None;
        }

        var startTicks = (long)Math.Round(Math.Min(min, max));
        var endTicks = (long)Math.Round(Math.Max(min, max));
        startTicks = Math.Clamp(startTicks, DateTime.MinValue.Ticks, DateTime.MaxValue.Ticks);
        endTicks = Math.Clamp(endTicks, DateTime.MinValue.Ticks, DateTime.MaxValue.Ticks);

        var pointCapacity = Math.Max(8, (int)Math.Floor(plotWidth / Math.Max(2f, 4f * density)));
        var dayThreshold = DateGroupingThreshold > 0 ? Math.Max(DateGroupingThreshold, pointCapacity) : pointCapacity;
        var (visibleDays, _, visibleYears) = CountVisibleDateDensity(startTicks, endTicks);

        // Strict LOD order: Year -> Month -> Day(show all).
        if (visibleDays <= dayThreshold) {
            return NTDateGroupingLevel.Day;
        }

        if (visibleYears > 5) {
            return NTDateGroupingLevel.Year;
        }

        return NTDateGroupingLevel.Month;
    }

    private string FormatLabel(object? value, bool forTooltip) {
        if (value is null) {
            return string.Empty;
        }

        if (!string.IsNullOrEmpty(LabelFormat)) {
            try {
                return string.Format(LabelFormat, value);
            }
            catch {
                return value.ToString() ?? string.Empty;
            }
        }

        if (value is DateTime dt) {
            var level = _activeDateGroupingLevel == NTDateGroupingLevel.None
                ? ResolveDateGroupingLevelFromCurrentView()
                : _activeDateGroupingLevel;

            return level switch {
                NTDateGroupingLevel.Year => dt.ToString("yyyy"),
                NTDateGroupingLevel.Month => dt.ToString("MMM yyyy"),
                NTDateGroupingLevel.Day => dt.ToString("MM/dd/yyyy"),
                _ => forTooltip ? dt.ToString("MM/dd/yyyy") : dt.ToString("d")
            };
        }

        return value.ToString() ?? string.Empty;
    }

    private NTDateGroupingLevel ResolveDateGroupingLevelFromCurrentView() {
        if (!Chart.IsXAxisDateTime || !EnableAutoDateGrouping) {
            return NTDateGroupingLevel.None;
        }

        var (min, max) = Chart.GetXRange(this, true);
        var plotWidth = Chart is NTChart<TData> ntChart ? ntChart.LastPlotArea.Width : 0f;
        return ResolveDateGroupingLevel(min, max, Math.Max(1f, plotWidth), Math.Max(0.1f, Chart.Density));
    }

    private (int VisibleDays, int VisibleMonths, int VisibleYears) CountVisibleDateDensity(long minTicks, long maxTicks) {
        if (maxTicks < minTicks) {
            (minTicks, maxTicks) = (maxTicks, minTicks);
        }

        minTicks = Math.Clamp(minTicks, DateTime.MinValue.Ticks, DateTime.MaxValue.Ticks);
        maxTicks = Math.Clamp(maxTicks, DateTime.MinValue.Ticks, DateTime.MaxValue.Ticks);

        var start = new DateTime(minTicks).Date;
        var end = new DateTime(maxTicks).Date;
        if (end < start) {
            return (0, 0, 0);
        }

        var visibleDaysLong = (end - start).Days + 1L;
        var visibleMonthsLong = ((end.Year - start.Year) * 12L) + end.Month - start.Month + 1L;
        var visibleYearsLong = (end.Year - start.Year) + 1L;

        return (
            (int)Math.Clamp(visibleDaysLong, 0L, int.MaxValue),
            (int)Math.Clamp(visibleMonthsLong, 0L, int.MaxValue),
            (int)Math.Clamp(visibleYearsLong, 0L, int.MaxValue));
    }

    private IReadOnlyList<AxisTick> GetOrCreateGroupedDateTicks(long minTicks, long maxTicks, NTDateGroupingLevel level) {
        if (level == NTDateGroupingLevel.None) {
            return [];
        }

        if (maxTicks < minTicks) {
            (minTicks, maxTicks) = (maxTicks, minTicks);
        }
        minTicks = Math.Clamp(minTicks, DateTime.MinValue.Ticks, DateTime.MaxValue.Ticks);
        maxTicks = Math.Clamp(maxTicks, DateTime.MinValue.Ticks, DateTime.MaxValue.Ticks);

        var key = new DateTickCacheKey(level, minTicks, maxTicks);
        if (_groupedDateTickCache.TryGetValue(key, out var cached)) {
            return cached;
        }

        var ticksSorted = BuildGroupedDateTicks(minTicks, maxTicks, level);

        if (_groupedDateTickCache.Count > 32) {
            _groupedDateTickCache.Clear();
        }
        _groupedDateTickCache[key] = ticksSorted;
        return ticksSorted;
    }

    private List<AxisTick> BuildGroupedDateTicks(long minTicks, long maxTicks, NTDateGroupingLevel level) {
        var start = new DateTime(minTicks);
        var end = new DateTime(maxTicks);
        if (end < start) {
            return [];
        }

        var ticks = new List<AxisTick>();

        if (level == NTDateGroupingLevel.Year) {
            for (var year = start.Year; year <= end.Year; year++) {
                var dt = new DateTime(year, 7, 1);
                ticks.Add(new AxisTick(dt.Ticks, FormatLabel(dt, false)));
            }
            return ticks;
        }

        if (level == NTDateGroupingLevel.Month) {
            var cursor = new DateTime(start.Year, start.Month, 1);
            var endMonth = new DateTime(end.Year, end.Month, 1);
            while (cursor <= endMonth) {
                var dt = cursor.AddDays(14);
                ticks.Add(new AxisTick(dt.Ticks, FormatLabel(dt, false)));
                cursor = cursor.AddMonths(1);
            }
            return ticks;
        }

        var dayCursor = start.Date;
        var dayEnd = end.Date;
        while (dayCursor <= dayEnd) {
            ticks.Add(new AxisTick(dayCursor.Ticks, FormatLabel(dayCursor, false)));
            dayCursor = dayCursor.AddDays(1);
        }

        return ticks;
    }

    private IReadOnlyList<AxisTick> DownsampleTicksForReadability(IReadOnlyList<AxisTick> ticks, float plotWidth, float density) {
        if (ticks.Count <= 2 || plotWidth <= 0) {
            return ticks;
        }

        var sampleStep = Math.Max(1, ticks.Count / 24);
        float maxWidth = 0f;
        for (var i = 0; i < ticks.Count; i += sampleStep) {
            _textFont.MeasureText(ticks[i].Label, out var bounds);
            maxWidth = Math.Max(maxWidth, bounds.Width);
        }

        var minSpacing = Math.Max(24f * density, maxWidth + (10f * density));
        var maxReadableTicks = Math.Max(2, (int)Math.Floor(plotWidth / minSpacing));
        if (ticks.Count <= maxReadableTicks) {
            return ticks;
        }

        var step = Math.Max(1, (int)Math.Ceiling((ticks.Count - 1d) / (maxReadableTicks - 1d)));
        var reduced = new List<AxisTick>(maxReadableTicks + 1);
        for (var i = 0; i < ticks.Count; i += step) {
            reduced.Add(ticks[i]);
        }

        if (reduced.Count == 0 || reduced[^1].Value != ticks[^1].Value) {
            reduced.Add(ticks[^1]);
        }

        return reduced;
    }

    private static long GetDateBucketKey(DateTime dt, NTDateGroupingLevel level) {
        return level switch {
            NTDateGroupingLevel.Year => new DateTime(dt.Year, 7, 1).Ticks,
            NTDateGroupingLevel.Month => new DateTime(dt.Year, dt.Month, 1).AddDays(14).Ticks,
            _ => dt.Date.Ticks
        };
    }

    [MemberNotNull(nameof(_textPaint), nameof(_textFont), nameof(_titlePaint), nameof(_titleFont), nameof(_linePaint))]
    private void InitializePaints(NTRenderContext context) {
        _textPaint ??= new SKPaint {
            IsAntialias = true,
            Color = context.TextColor
        };
        _textPaint.Color = context.TextColor;
        _textFont ??= new SKFont(context.DefaultFont.Typeface, AxisFontSize * context.Density);
        _textFont.Size = AxisFontSize * context.Density;

        _titlePaint ??= new SKPaint {
            IsAntialias = true,
            Color = context.TextColor
        };
        _titlePaint.Color = context.TextColor;
        _titleFont ??= new SKFont(context.DefaultFont.Typeface, TitleFontSize * context.Density);
        _titleFont.Size = TitleFontSize * context.Density;

        _linePaint ??= new SKPaint {
            IsAntialias = true,
            Color = context.TextColor,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 1 * context.Density
        };
        _linePaint.Color = context.TextColor;
        _linePaint.StrokeWidth = context.Density;
    }

    private readonly record struct AxisTick(double Value, string Label);

    private readonly record struct AxisCacheKey(
        bool IsCategorical,
        int CategoryCount,
        double Min,
        double Max,
        double PlotWidth,
        double Density,
        float AxisFontSize,
        string? LabelFormat,
        string? Title,
        float TitleFontSize,
        bool EnableAutoDateGrouping,
        int DateGroupingThreshold);

    private readonly record struct DateTickCacheKey(
        NTDateGroupingLevel Level,
        long MinTicks,
        long MaxTicks);
}
