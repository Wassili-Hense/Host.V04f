using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using X13.Data;
using JSC = NiL.JS.Core;
using JSL = NiL.JS.BaseLibrary;

namespace X13.UI {
  public class InTopic : InBase, IDisposable {
    #region default children
    private static JSC.JSObject DEFS_String;
    static InTopic() {
      DEFS_String = JSC.JSObject.CreateObject();
      DEFS_String["mask"] = true;
      DEFS_String["schema"] = "String";
      DEFS_String["default"] = "";
    }
    #endregion default children

    private DTopic _owner;
    private bool _root;
    private ObservableCollection<InTopic> _items;
    private bool _populated;

    public InTopic(DTopic owner, bool root) {
      _owner = owner;
      _root = root;
      _owner.PropertyChanged += _owner_PropertyChanged;
      if(_root) {
        name = "children";
        icon = App.GetIcon("children");
        editor = null;
      } else {
        name = _owner.name;
        base.UpdateSchema(owner.schema);
      }
      base.IsExpanded = _root;
    }
    public override JSC.JSValue value { get { return _owner.value; } set { _owner.SetValue(value); } }
    public ObservableCollection<InTopic> items {
      get {
        if(_items == null) {
          _populated = true;
          if(_owner.children != null) {
            _owner.children.CollectionChanged += children_CollectionChanged;
            _items = new ObservableCollection<InTopic>();
            InsertItems(0, _owner.children.ToArray());
          }
        }
        return _items;
      }
    }

    private void children_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e) {
      if(!_populated) {
        return;
      }
      if(e.Action == NotifyCollectionChangedAction.Remove) {
        foreach(var t in e.OldItems.Cast<DTopic>()) {
          var n = _items.FirstOrDefault(z => z._owner == t);
          if(n != null) {
            _items.Remove(n);
          }
        }
        return;
      } else if(e.Action == NotifyCollectionChangedAction.Add) {
        InsertItems(e.NewStartingIndex, e.NewItems.Cast<DTopic>().ToArray());
        return;
      }
      throw new NotImplementedException();
    }
    private async void InsertItems(int idx, DTopic[] its) {
      foreach(var t in its) {
        var tt = await t.GetAsync(null, false);
        if(tt != null) {
          _items.Insert(idx++, new InTopic(tt, false));
        }
      }
    }

    private void _owner_PropertyChanged(object sender, PropertyChangedEventArgs e) {
      if(!_root) {
        if(e.PropertyName == "schema") {
          this.UpdateSchema(_owner.schema);
        } else if(e.PropertyName == "value") {
          this.UpdateSchema(_owner.schema);
          this.editor.ValueChanged(_owner.value);
        }
      }
      if(e.PropertyName == "children" && _populated) {
        if(_owner.children != null) {
          if(_items == null) {
            _owner.children.CollectionChanged += children_CollectionChanged;
            _items = new ObservableCollection<InTopic>();
            InsertItems(0, _owner.children.ToArray());
            PropertyChangedReise("items");
          }
        } else if(_items != null) {
          _items = null;
          PropertyChangedReise("items");
        }
      }
    }

    #region ContextMenu
    public override List<MenuItem> MenuItems {
      get {
        var l = new List<MenuItem>();
        JSC.JSValue f, tmp1;
        MenuItem mi;
        MenuItem ma = new MenuItem() { Header = "Add" };
        if(_schema != null && (f = _schema["Children"]).ValueType == JSC.JSValueType.Object) {
          foreach(var kv in f.Where(z => z.Value != null && z.Value.ValueType == JSC.JSValueType.Object)) {
            if(_items.Any(z => z.name == kv.Key && ((tmp1 = kv.Value["mask"]).ValueType != JSC.JSValueType.Boolean || (bool)tmp1 != true))) {
              continue;
            }
            mi = new MenuItem();
            mi.Header = kv.Key;
            if(kv.Value["icon"].ValueType == JSC.JSValueType.String) {
              mi.Icon = App.GetIcon(kv.Value["icon"].Value as string);
            }
            mi.Tag = kv.Value;
            mi.Click += miAdd_Click;
            ma.Items.Add(mi);
          }
        } else {
          mi = new MenuItem() { Header = "String", Icon = new Image() { Source = App.GetIcon("String") }, Tag = InTopic.DEFS_String };
          mi.Click += miAdd_Click;
          ma.Items.Add(mi);
        }
        if(ma.HasItems) {
          l.Add(ma);
        }
        mi = new MenuItem() { Header = "Delete", Icon = new Image() { Source = App.GetIcon("component/Images/delete.png") } };
        if(_schema == null || (f = _schema["required"]).ValueType != JSC.JSValueType.Boolean || true != (bool)f) {
          mi.Click += miDelete_Click;
        } else {
          mi.IsEnabled = false;
        }
        l.Add(mi);
        return l;
      }
    }

    private void miAdd_Click(object sender, System.Windows.RoutedEventArgs e) {
      if(!IsExpanded) {
        IsExpanded = true;
        base.PropertyChangedFunc("IsExpanded");
      }
    }
    private void miDelete_Click(object sender, System.Windows.RoutedEventArgs e) {
      _owner.Delete();
    }
    #endregion ContextMenu

    #region IDisposable Member
    public void Dispose() {
      _owner.PropertyChanged -= _owner_PropertyChanged;
    }
    #endregion IDisposable Member
  }
}
