using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NTComponents.Charts.Core;

[EditorBrowsable(EditorBrowsableState.Never)]
public enum RenderOrdered {
    Title,
    Legend,
    Axis,
    Series,
    Tooltip
}
