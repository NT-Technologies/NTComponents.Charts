using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NTComponents.Charts.Core.Axes;

/// <summary>
///     Represents a chart axis that can render data of type <typeparamref name="TData"/>.
/// </summary>
/// <typeparam name="TData">The type of data items used in the chart.</typeparam>
public interface INTAxis<TData> : IRenderable where TData : class {

}
