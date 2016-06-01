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
    }

    #region Properies
    public DTopic data { get; private set; }
    #endregion Properies

    private void BrickLoaded(Task<DTopic> td) {
      if(td.IsCompleted && td.Result != null) {
        td.Result.changed += TBrick_changed;
        if(td.Result.schemaStr == "Brick") {
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
      if(src == null || src.schemaStr != "Brick" || src.value.ValueType!=JSC.JSValueType.Object || art == DTopic.Art.schema) {
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
        info = (this.owner.value["info"].Value as string) ?? owner.name;
        image = App.GetIcon((this.owner.value["icon"].Value as string) ?? "Null");
      }
      public string info { get; private set; }
      public BitmapSource image { get; private set; }
    }
  }
}
