///<remarks>This file is part of the <see cref="https://github.com/X13home">X13.Home</see> project.<remarks>
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using X13.Data;
using JSC = NiL.JS.Core;
using JSL = NiL.JS.BaseLibrary;

namespace X13.UI {
  public abstract class InBase : NPC_UI, IComparable<InBase> {
    protected bool _isVisible;
    protected JSC.JSValue _type;
    protected string _view;
    protected bool _isExpanded;
    protected List<InBase> _items;
    protected Action<InBase, bool> _collFunc;


    public double levelPadding { get; protected set; }
    public virtual bool IsExpanded {
      get {
        return _isExpanded;
      }
      set {
        if(value != _isExpanded) {
          _isExpanded = value;
          PropertyChangedReise();
          if(_items != null) {
            foreach(var i in _items) {
              i.IsVisible= this._isVisible && this._isExpanded;
            }
          }
        }
      }
    }

    public abstract bool HasChildren { get; }
    public bool IsVisible {
      get { return _isVisible; }
      set {
        if(value != _isVisible) {
          _isVisible = value;
          if(_items != null) {
            foreach(var i in _items) {
              i.IsVisible = this._isVisible && this._isExpanded;
            }
          }
          _collFunc(this, _isVisible);
        }
      }
    }
    public bool IsGroupHeader { get; protected set; }
    public bool IsEdited { get; protected set; }
    public string name { get; set; }
    public BitmapSource icon { get; protected set; }
    public IValueEditor editor { get; protected set; }

    public abstract JSC.JSValue value { get; set; }
    public abstract List<Control> MenuItems(FrameworkElement src);
    public void GotFocus(object sender, RoutedEventArgs e) {
      DependencyObject cur;
      ListViewItem parent;
      DependencyObject parentObject;

      for(cur = sender as DependencyObject; cur != null; cur = parentObject) {
        parentObject = VisualTreeHelper.GetParent(cur);
        if((parent = parentObject as ListViewItem) != null) {
          parent.IsSelected = true;
          break;
        }
      }
    }

    protected virtual void UpdateType(JSC.JSValue type) {
      this._type = type;

      //string nv = null;
      //BitmapSource ni = null;

      //if(_type != null && _type.Value != null) {
      //  var vv = _type["view"];
      //  if(vv.ValueType == JSC.JSValueType.String) {
      //    nv = vv.Value as string;
      //  }
      //  var iv = _type["icon"];
      //  if(iv.ValueType == JSC.JSValueType.String) {
      //    ni = App.GetIcon(iv.Value as string);
      //  }
      //}
      //if(nv == null) {
      //  nv = value.ValueType.ToString();
      //}
      //if(ni == null) {
      //  ni = App.GetIcon(nv);
      //}
      //if(ni == null) {
      //  ni = App.GetIcon(null);
      //}
      //if(ni != icon) {
      //  icon = ni;
      //  PropertyChangedReise("icon");
      //}
      //if(nv != _view) {
      //  _view = nv;
      //  editor = InspectorForm.GetEdititor(_view, this, _type);
      //  PropertyChangedReise("editor");
      //}
      //this.editor.TypeChanged(_type);
    }
    public void Deleted() {
      if(_isVisible) {
        if(_isExpanded) {
          foreach(var ch in _items.ToArray()) {
            ch.Deleted();
          }
        }
        _collFunc(this, false);
      }
    }
    public abstract int CompareTo(InBase other);
  }
}
