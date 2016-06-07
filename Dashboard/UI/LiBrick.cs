///<remarks>This file is part of the <see cref="https://github.com/X13home">X13.Home</see> project.<remarks>
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using X13.Data;

namespace X13.UI {
  internal class LiBrick : LiBase {

    public LiBrick(LogramView view, DTopic data) : base(view, data) {
      this.Offset = new Vector(50, 50);
      Render(3);
    }
    /// <summary>feel DrawingVisual</summary>
    /// <param name="chLevel">0 - locale, 1 - local & child, 2 - drag, 3- set position</param>
    public override void Render(int chLevel) {
      FormattedText head = new FormattedText(data.name, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, LogramView.FT_FONT , LogramView.CELL_SIZE * 1.2, Brushes.Black);
      double width = Math.Round(head.WidthIncludingTrailingWhitespace * 2 / LogramView.CELL_SIZE - 0.5)*2 * LogramView.CELL_SIZE;
      double height = 8 * LogramView.CELL_SIZE;
      double wo = width / 2;

      base.VisualBitmapScalingMode=BitmapScalingMode.HighQuality;
      using(DrawingContext dc = this.RenderOpen()) {
        Pen border = LogramView.PEN_NORMAL;
        dc.DrawRectangle(Brushes.White, null, new Rect(-1, 2, width + 4, height + LogramView.CELL_SIZE*2 - 2));
        dc.DrawRectangle(Brushes.AliceBlue, border, new Rect(3, LogramView.CELL_SIZE*2 - 0.5, wo > 0 ? width - 6 : width - 2, height + 1));
        dc.DrawText(head, new Point((width - head.WidthIncludingTrailingWhitespace) / 2, 1));
      }
    }
  }
}
