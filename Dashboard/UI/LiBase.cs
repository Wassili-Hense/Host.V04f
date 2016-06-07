///<remarks>This file is part of the <see cref="https://github.com/X13home">X13.Home</see> project.<remarks>
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;
using X13.Data;

namespace X13.UI {
  internal abstract class LiBase : DrawingVisual {
    protected LogramView _view;

    protected LiBase(LogramView view, DTopic data) {
      this._view = view;
      this.data = data;
    }

    public DTopic data { get; protected set; }

    /// <summary>feel DrawingVisual</summary>
    /// <param name="chLevel">0 - locale, 1 - local & child, 2 - drag, 3- set position</param>
    public abstract void Render(int chLevel);
    public override string ToString() {
      return data != null ? data.name : "??";
    }
  }
}
