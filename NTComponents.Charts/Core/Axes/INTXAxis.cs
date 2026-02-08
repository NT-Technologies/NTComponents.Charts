using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NTComponents.Charts.Core.Axes;

public interface INTXAxis<TData> : INTAxis<TData> where TData : class {
    static abstract INTXAxis<TData> Default { get; }
}