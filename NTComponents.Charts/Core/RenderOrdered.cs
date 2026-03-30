using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NTComponents.Charts.Core;

/// <summary>
///     Defines the draw order of chart layers. Not intended for external use.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public enum RenderOrdered {
    /// <summary>The chart title layer.</summary>
    Title,

    /// <summary>The legend layer.</summary>
    Legend,

    /// <summary>The axis layer.</summary>
    Axis,

    /// <summary>The data series layer.</summary>
    Series,

    /// <summary>The annotation layer.</summary>
    Annotation,

    /// <summary>The tooltip layer.</summary>
    Tooltip
}
