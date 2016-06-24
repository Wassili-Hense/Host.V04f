///<remarks>This file is part of the <see cref="https://github.com/X13home">X13.Home</see> project.<remarks>
using JSL = NiL.JS.BaseLibrary;
using JSC = NiL.JS.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using X13.Data;
using System.Windows.Input;

namespace X13.UI {
  public class LogramView : Canvas {
    public const int CELL_SIZE = 8;
    public static readonly Typeface FT_FONT;
    public static readonly Pen PEN_NORMAL;

    static LogramView() {
      FT_FONT = new Typeface("Times New Roman");
      PEN_NORMAL = new Pen(Brushes.Black, 1);
    }

    private DrawingVisual _backgroundVisual;
    private DTopic _owner;
    private List<Visual> _visuals;

    private double _zoom = 1.0;
    private Point _startOffset;
    private TransformGroup _transformGroup;
    private TranslateTransform _translateTransform;
    private ScaleTransform _zoomTransform;
    private double _offsetLeft;
    private double _offsetTop;

    BorderHitType _mouseAction;
    private bool _borderDragInProgress;

    public LogramView() {
      var t = this.DataContext as DTopic;
      if(t != null) {
        OwnerChanged(t);
      }
      base.DataContextChanged += LogramView_DataContextChanged;

      _translateTransform = new TranslateTransform();
      _zoomTransform = new ScaleTransform() { ScaleX = _zoom, ScaleY = _zoom };
      _transformGroup = new TransformGroup();

      _transformGroup.Children.Add(_zoomTransform);
      _transformGroup.Children.Add(_translateTransform);
      RenderTransform = _transformGroup;

      _backgroundVisual = new DrawingVisual();
      _visuals = new List<Visual>();
      AddVisualChild(_backgroundVisual);
    }

    public void AddVisual(Visual item) {
      _visuals.Add(item);
      base.AddVisualChild(item);
      base.AddLogicalChild(item);
    }
    public void DeleteVisual(Visual item) {
      _visuals.Remove(item);
      base.RemoveVisualChild(item);
      base.RemoveLogicalChild(item);
    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e) {
      _startOffset = e.GetPosition(this);
      _mouseAction = SetHitType(_startOffset);
      if((_mouseAction & BorderHitType.Border) != BorderHitType.None) {
        _borderDragInProgress = true;
        e.Handled = true;
        CaptureMouse();
      } else {
        base.OnMouseLeftButtonDown(e);
      }
    }
    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e) {
      if(_borderDragInProgress) {
        _borderDragInProgress = false;
        e.Handled = true;
        ReleaseMouseCapture();
        var p = e.GetPosition(this);
        double offset_x = p.X - _startOffset.X;
        double offset_y = p.Y - _startOffset.Y;

        double left = _offsetLeft;
        double top = _offsetTop;
        double right = left + 2 * Math.Round((this.Width - 10) / (CELL_SIZE * 2));
        double bottom = top + 2 * Math.Round((this.Height - 10) / (CELL_SIZE * 2));

        JSC.JSObject o = CloneJSO(_owner.value.ToObject());

        if((_mouseAction & BorderHitType.L) == BorderHitType.L) {
          left += 2 * Math.Round(offset_x / (CELL_SIZE * 2));
          o["L"] = new JSL.Number(left);
          o["W"] = new JSL.Number(right - left);
        } else if((_mouseAction & BorderHitType.R) == BorderHitType.R) {
          right += 2 * Math.Round(offset_x / (CELL_SIZE * 2));
          o["W"] = new JSL.Number(right - left);
        }
        if((_mouseAction & BorderHitType.T) == BorderHitType.T) {
          top += 2 * Math.Round(offset_y / (CELL_SIZE * 2));
          o["T"] = new JSL.Number(top);
          o["H"] = new JSL.Number(bottom - top);
        } else if((_mouseAction & BorderHitType.B) == BorderHitType.B) {
          bottom += 2 * Math.Round(offset_y  / (CELL_SIZE * 2));
          o["H"] = new JSL.Number(bottom - top);
        }
        if((right > left + 2) && (bottom > top + 2)) {
          _owner.SetValue(o);
        }
      } else {
        base.OnMouseLeftButtonUp(e);
      }
    }
    protected override void OnMouseMove(MouseEventArgs e) {
      if(_borderDragInProgress) {
        var p = e.GetPosition(this);
        double offset_x = p.X - _startOffset.X;
        double offset_y = p.Y - _startOffset.Y;

        double left = 0;
        double top = 0;
        double right = Width;
        double bottom = Height;

        if((_mouseAction & BorderHitType.L) == BorderHitType.L) {
          left = CELL_SIZE * 2 * Math.Round((left + offset_x) / (CELL_SIZE * 2));
        } else if((_mouseAction & BorderHitType.R) == BorderHitType.R) {
          right = CELL_SIZE * 2 * Math.Round((right + offset_x-10) / (CELL_SIZE * 2))+10;
        }
        if((_mouseAction & BorderHitType.T) == BorderHitType.T) {
          top = CELL_SIZE * 2 * Math.Round((top + offset_y) / (CELL_SIZE * 2));
        } else if((_mouseAction & BorderHitType.B) == BorderHitType.B) {
          bottom = CELL_SIZE * 2 * Math.Round((bottom + offset_y-10) / (CELL_SIZE * 2))+10;
        }
        if((right > left + 10 + 2 * CELL_SIZE) && (bottom > top + 10 + 2 * CELL_SIZE)) {
          RenderBackground(left, top, right, bottom);
        }
      } else {
        _mouseAction = SetHitType(e.GetPosition(this));
        base.OnMouseMove(e);
      }
    }
    protected override void OnMouseWheel(MouseWheelEventArgs e) {
      if(e.Delta < 0 ? _zoom > 0.4 : _zoom < 2.5) {
        var p = e.GetPosition(this);
        _zoom += e.Delta / 3000.0;
        _translateTransform.X = p.X * (_zoomTransform.ScaleX - _zoom) + _translateTransform.X;
        _translateTransform.Y = p.Y * (_zoomTransform.ScaleY - _zoom) + _translateTransform.Y;
        _zoomTransform.ScaleY = _zoom;
        _zoomTransform.ScaleX = _zoom;
      }
      e.Handled = true;
    }

    [Flags]
    private enum BorderHitType {
      None = 0,
      L = 1,
      R = 2,
      T = 4,
      LT = 5,
      RT = 6,
      B = 8,
      LB = 9,
      RB = 10,
      Border = 15,
    };
    private BorderHitType SetHitType(Point p) {
      BorderHitType h = BorderHitType.None;
      if(p.X < 10) {
        h |= BorderHitType.L;
      } else if(p.X > this.Width - 10) {
        h |= BorderHitType.R;
      }
      if(p.Y < 10) {
        h |= BorderHitType.T;
      } else if(p.Y > this.Height - 10) {
        h |= BorderHitType.B;
      }
      if(_mouseAction != h) {
        switch(h) {
        case BorderHitType.None:
          this.Cursor = Cursors.Arrow;
          break;
        case BorderHitType.L:
        case BorderHitType.R:
          this.Cursor = Cursors.SizeWE;
          break;
        case BorderHitType.T:
        case BorderHitType.B:
          this.Cursor = Cursors.SizeNS;
          break;
        case BorderHitType.LT:
        case BorderHitType.RB:
          this.Cursor = Cursors.SizeNWSE;
          break;
        case BorderHitType.LB:
        case BorderHitType.RT:
          this.Cursor = Cursors.SizeNESW;
          break;
        }
      }

      return h;
    }

    protected override int VisualChildrenCount {
      get {
        return _visuals.Count+1;   // _backgroundVisual, _mSelectVisual
      }
    }
    protected override Visual GetVisualChild(int index) {
      if(index == 0) {
        return _backgroundVisual;
      }
      return _visuals[index - 1];
    }
    protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo) {
      base.OnRenderSizeChanged(sizeInfo);
      if(Width > 0 && Height > 0) {
        RenderBackground(0, 0, Width, Height);
      }
    }
    private void RenderBackground(double left, double top, double rigth, double bottom) {
      using(DrawingContext dc = _backgroundVisual.RenderOpen()) {
        Pen pen;
        pen = new Pen(Brushes.LightGray, 1);
        dc.DrawRectangle(Brushes.White, pen, new Rect(left + 5, top + 5, rigth - left - 10, bottom - top - 10));

        pen = new Pen(Brushes.LightGray, 0.5d);
        pen.DashStyle = new DashStyle(new double[] { 3, CELL_SIZE * 4 - 3 }, 1.5);
        for(double x = left + CELL_SIZE * 2 + 5; x < rigth - 10; x += CELL_SIZE * 2) {
          dc.DrawLine(pen, new Point(x, top + 5), new Point(x, bottom - 10));
        }
        for(double y = top + CELL_SIZE * 2 + 5; y < bottom - 10; y += CELL_SIZE * 2) {
          dc.DrawLine(pen, new Point(left + 5, y), new Point(rigth - 10, y));
        }
      }
    }
    private void LogramView_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e) {
      var t = this.DataContext as DTopic;
      if(t != null) {
        OwnerChanged(t);
      }
    }
    private void OwnerChanged(DTopic t) {
      if(t != _owner) {
        if(_owner != null) {
          _owner.changed -= _owner_changed;
        }
        _owner = t;
        if(_owner != null) {
          _owner.changed += _owner_changed;
        }
      }
      if(_owner == null) {
        return;
      }
      _offsetTop = _owner.GetField<double>("T");
      _offsetLeft = _owner.GetField<double>("L");
      this.Width = _owner.GetField<double>("W") * CELL_SIZE + 10;
      this.Height = _owner.GetField<double>("H") * CELL_SIZE + 10;
      AddVisual(new LiBrick(this, t));
    }
    private void _owner_changed(DTopic.Art art, DTopic src) {
      if(art == DTopic.Art.value && src == _owner) {
        OwnerChanged(_owner);
      }
    }

    private JSC.JSObject CloneJSO(JSC.JSObject obj) {
      var o = JSC.JSObject.CreateObject();
      foreach(var kv in obj.Where(z => obj.GetProperty(z.Key, JSC.PropertyScope.Own).Defined)) {
        o[kv.Key] = kv.Value;
      }
      return o;
    }
  }
}
