using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace NTComponents.Charts.Tests.Rendering;

/// <summary>Small helper that adds chart children (series, axes, legend...) to a render tree.</summary>
public sealed class GoldenChartBuilder(RenderTreeBuilder builder) {
    private int _sequence;

    /// <summary>Adds any chart child component with explicit attributes.</summary>
    public GoldenChartBuilder Add<TComponent>(params (string Name, object? Value)[] attributes) where TComponent : IComponent {
        builder.OpenComponent<TComponent>(_sequence++);
        foreach (var (name, value) in attributes) {
            builder.AddAttribute(_sequence++, name, value);
        }
        builder.CloseComponent();
        return this;
    }

    /// <summary>Adds a cartesian series with animation disabled and the given X/Y selectors.</summary>
    public GoldenChartBuilder Cartesian<TSeries>(IEnumerable<GoldenRow> data, Func<GoldenRow, object> x, Func<GoldenRow, decimal> y, params (string Name, object? Value)[] extra) where TSeries : IComponent
        => Add<TSeries>([("Data", data), ("XValue", x), ("YValueSelector", y), ("AnimationEnabled", false), .. extra]);

    /// <summary>Adds a value-based series (pie, radar, tree map, bubble pack) with animation disabled.</summary>
    public GoldenChartBuilder Valued<TSeries>(IEnumerable<GoldenRow> data, Func<GoldenRow, object> x, Func<GoldenRow, decimal> value, params (string Name, object? Value)[] extra) where TSeries : IComponent
        => Add<TSeries>([("Data", data), ("XValue", x), ("ValueSelector", value), ("AnimationEnabled", false), .. extra]);
}
