///<remarks>This file is part of the <see cref="https://github.com/X13home">X13.Home</see> project.<remarks>
using JSL = NiL.JS.BaseLibrary;
using JSC = NiL.JS.Core;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using X13.Data;

namespace X13.UI {
  /// <summary>Interaction logic for LogramForm.xaml</summary>
  public partial class LogramForm : UserControl, IBaseForm {

    private ObservableCollection<BrickInfo> _bricks;

    public LogramForm(DTopic data) {
      _bricks = new ObservableCollection<BrickInfo>();
      this.data = data;
      this.data.GetAsync("/etc/brick").ContinueWith(BrickLoaded, TaskScheduler.FromCurrentSynchronizationContext());
      InitializeComponent();
      this.icBricks.ItemsSource = _bricks;
      this.lvCanvas.DataContext = data;
    }

    #region Properies
    public DTopic data { get; private set; }
    #endregion Properies

    private void BrickLoaded(Task<DTopic> td) {
      if(td.IsCompleted && td.Result != null) {
        td.Result.changed += TBrick_changed;
        if(td.Result.typeStr == "Bclass") {
          this.TBrick_changed(DTopic.Art.addChild, td.Result);
        } else {
          foreach(var t in td.Result.children) {
            t.GetAsync(null).ContinueWith(BrickLoaded, TaskScheduler.FromCurrentSynchronizationContext());
          }
        }
      } else if(td.IsFaulted) {
        Log.Warning("{0}.GetBrick - {1}", data.fullPath, td.Exception);
      }
    }

    private void TBrick_changed(DTopic.Art art, DTopic src) {
      if(src == null || src.typeStr != "Bclass" || src.value.ValueType!=JSC.JSValueType.Object || art == DTopic.Art.type) {
        return;
      }
      for(int i = 0; i < _bricks.Count; i++) {
        if(string.Compare(src.path, _bricks[i].owner.path) > 0) {
          if(art == DTopic.Art.addChild || art == DTopic.Art.value) {
            _bricks.Insert(i, new BrickInfo(src));
          }
          return;
        } else if(_bricks[i].owner == src) {
          if(art == DTopic.Art.addChild || art == DTopic.Art.value) {
            _bricks[i] = new BrickInfo(src);
          } else if(art == DTopic.Art.RemoveChild) {
            _bricks.RemoveAt(i);
          }
          return;
        }
      }
      _bricks.Add(new BrickInfo(src));
    }

    #region IBaseForm Members
    public string view {
      get { return "Logram"; }
    }
    public BitmapSource icon { get { return App.GetIcon("Logram"); } }
    public bool altView {
      get { return true; }
    }
    #endregion IBaseForm Members

    private class BrickInfo {
      public readonly DTopic owner;

      public BrickInfo(DTopic owner) {
        this.owner = owner;
        info = this.owner.GetField<string>("info") ?? owner.name;
        image = App.GetIcon(this.owner.GetField<string>("icon") ?? "Null");
      }
      public string info { get; private set; }
      public BitmapSource image { get; private set; }
    }
    /*
    #region LVBorder
    private enum HitType {
      None,
      Body,
      UL,
      UR,
      LR,
      LL,
      L,
      R,
      T,
      B
    };

    // True if a drag is in progress.
    private bool DragInProgress = false;

    // The drag's last point.
    private Point LastPoint;

    // The part of the rectangle under the mouse.
    HitType MouseHitType = HitType.None;

    // Return a HitType value to indicate what is at the point.
    private HitType SetHitType(Point point) {
      double left = lvCanvas.Left;
      double top = lvCanvas.Top;
      double right = left + lvCanvas.Width;
      double bottom = top + lvCanvas.Height;
      if(point.X < left)
        return HitType.None;
      if(point.X > right)
        return HitType.None;
      if(point.Y < top)
        return HitType.None;
      if(point.Y > bottom)
        return HitType.None;

      const double GAP = 10;
      if(point.X - left < GAP) {
        // Left edge.
        if(point.Y - top < GAP)
          return HitType.UL;
        if(bottom - point.Y < GAP)
          return HitType.LL;
        return HitType.L;
      }
      if(right - point.X < GAP) {
        // Right edge.
        if(point.Y - top < GAP)
          return HitType.UR;
        if(bottom - point.Y < GAP)
          return HitType.LR;
        return HitType.R;
      }
      if(point.Y - top < GAP)
        return HitType.T;
      if(bottom - point.Y < GAP)
        return HitType.B;
      return HitType.Body;
    }

    // Set a mouse cursor appropriate for the current hit type.
    private void SetMouseCursor() {
      // See what cursor we should display.
      Cursor desired_cursor = Cursors.Arrow;
      switch(MouseHitType) {
      case HitType.None:
        desired_cursor = Cursors.Arrow;
        break;
      case HitType.Body:
        desired_cursor = Cursors.ScrollAll;
        break;
      case HitType.UL:
      case HitType.LR:
        desired_cursor = Cursors.SizeNWSE;
        break;
      case HitType.LL:
      case HitType.UR:
        desired_cursor = Cursors.SizeNESW;
        break;
      case HitType.T:
      case HitType.B:
        desired_cursor = Cursors.SizeNS;
        break;
      case HitType.L:
      case HitType.R:
        desired_cursor = Cursors.SizeWE;
        break;
      }

      // Display the desired cursor.
      if(Cursor != desired_cursor)
        Cursor = desired_cursor;
    }


    private void Grid_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) {
      MouseHitType = SetHitType(Mouse.GetPosition(grBody));
      SetMouseCursor();
      if(MouseHitType == HitType.None)
        return;

      LastPoint = Mouse.GetPosition(grBody);
      DragInProgress = true;
    }
    private void grBody_MouseLeftButtonUp(object sender, MouseButtonEventArgs e) {
      DragInProgress = false;
    }

    private void grBody_MouseMove(object sender, MouseEventArgs e) {
      if(!DragInProgress) {
        MouseHitType = SetHitType(Mouse.GetPosition(grBody));
        SetMouseCursor();
      } else {
        // See how much the mouse has moved.
        Point point = Mouse.GetPosition(grBody);
        double offset_x = point.X - LastPoint.X;
        double offset_y = point.Y - LastPoint.Y;

        // Get the rectangle's current position.
        double new_x = lvCanvas.Left;
        double new_y = lvCanvas.Top;
        double new_width = lvCanvas.Width;
        double new_height = lvCanvas.Height;

        // Update the rectangle.
        switch(MouseHitType) {
        case HitType.Body:
          new_x += offset_x;
          new_y += offset_y;
          break;
        case HitType.UL:
          new_x += offset_x;
          new_y += offset_y;
          new_width -= offset_x;
          new_height -= offset_y;
          break;
        case HitType.UR:
          new_y += offset_y;
          new_width += offset_x;
          new_height -= offset_y;
          break;
        case HitType.LR:
          new_width += offset_x;
          new_height += offset_y;
          break;
        case HitType.LL:
          new_x += offset_x;
          new_width -= offset_x;
          new_height += offset_y;
          break;
        case HitType.L:
          new_x += offset_x;
          new_width -= offset_x;
          break;
        case HitType.R:
          new_width += offset_x;
          break;
        case HitType.B:
          new_height += offset_y;
          break;
        case HitType.T:
          new_y += offset_y;
          new_height -= offset_y;
          break;
        }

        // Don't use negative width or height.
        if((new_width > 0) && (new_height > 0)) {
          // Update the rectangle.
          Canvas.SetLeft(rectangle1, new_x);
          Canvas.SetTop(rectangle1, new_y);
          rectangle1.Width = new_width;
          rectangle1.Height = new_height;

          // Save the mouse's new location.
          LastPoint = point;
        }
      }
    }
    #endregion LVBorder*/

  }
}
