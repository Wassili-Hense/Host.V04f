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
    private static JSC.JSObject DEFS_Bool;
    private static JSC.JSObject DEFS_Double;
    private static JSC.JSObject DEFS_String;
    private static JSC.JSObject DEFS_Date;
    static InTopic() {
      DEFS_Bool = JSC.JSObject.CreateObject();
      DEFS_Bool["mask"] = true;
      DEFS_Bool["schema"] = "Boolean";

      DEFS_Double = JSC.JSObject.CreateObject();
      DEFS_Double["mask"] = true;
      DEFS_Double["schema"] = "Double";

      DEFS_String = JSC.JSObject.CreateObject();
      DEFS_String["mask"] = true;
      DEFS_String["schema"] = "String";

      DEFS_Date = JSC.JSObject.CreateObject();
      DEFS_Date["mask"] = true;
      DEFS_Date["schema"] = "Date";
    }
    #endregion default children

    private InTopic _parent;
    private DTopic _owner;
    private bool _root;
    private ObservableCollection<InTopic> _items;
    private bool _populated;
    private JSC.JSValue _cStruct;

    public InTopic(DTopic owner, InTopic parent) {
      _owner = owner;
      _parent=parent;
      _root = _parent==null;
      _owner.PropertyChanged += _owner_PropertyChanged;
      if(_root) {
        name = "children";
        icon = App.GetIcon("children");
        editor = null;
      } else {
        name = _owner.name;
        base.UpdateSchema(_owner.schema);
      }
      base.IsExpanded = _root;
    }
    private InTopic(JSC.JSValue cStruct, InTopic parent) {
      _parent = parent;
      _cStruct = cStruct;
      name = string.Empty;
      IsEdited = true;
      JSC.JSValue sn;
      if(_cStruct != null && (sn = _cStruct["schema"]).ValueType == JSC.JSValueType.String) {
        parent._owner.GetAsync("/etc/schema/" + (sn.Value as string)).ContinueWith(dt => {
          if(dt.IsCompleted && dt.Result != null) {
            DWorkspace.ui.BeginInvoke(new Action(()=> base.UpdateSchema(dt.Result.value)));
          }
        });
      }
    }

    public override JSC.JSValue value { get { return _owner != null ? _owner.value : JSC.JSValue.NotExists; } set { if(_owner != null) { _owner.SetValue(value); } } }
    public ObservableCollection<InTopic> items {
      get {
        if(_owner!=null && _items == null) {
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
          var n = _items.FirstOrDefault(z => z.name == t.name);
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
      InTopic tmp;
      foreach(var t in its) {
        var tt = await t.GetAsync(null);
        if(tt != null) {
          if((tmp=_items.FirstOrDefault(z=>z.name==tt.name))!=null) {
            _items.Remove(tmp);
            tmp.RefreshOwner(tt);
          } else {
            tmp = new InTopic(tt, this);
          }
          _items.Insert(idx++, tmp);
        }
      }
    }

    private void RefreshOwner(DTopic tt) {
      _owner = tt;
    }
    public void FinishNameEdit(string name) {
      if(!string.IsNullOrEmpty(name)) {
        base.name = name;
        if(_owner == null) {
          JSC.JSValue def;
          string sName;
          if(_schema!=null) {
            sName=_cStruct["schema"].Value as string;
          } else {
            sName=null;
          }
          if(!(def=_cStruct["default"]).Exists && _schema!=null){
            def=_schema["default"];
          }
          var td = _parent._owner.CreateAsync(name, sName, def);
          td.Wait();
          if(td.IsCompleted && td.Result != null) {
            _owner = td.Result;
            _owner.PropertyChanged += _owner_PropertyChanged;
            name = _owner.name;
            base.UpdateSchema(_owner.schema);
          } else {
            if(td.IsFaulted) {
              Log.Warning("{0}/{1} - {2}", _parent._owner.fullPath, name, td.Exception.Message);
            }
            FinishNameEdit(null);
            return;
          }
        } else {
        }
      } else {
        if(_owner == null) {
            _parent.items.Remove(this);
          return;
        } else {
          base.name = _owner.name;
        }
      }
      IsEdited = false;
      PropertyChangedReise("IsEdited");
      PropertyChangedReise("name");
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
    public override List<Control> MenuItems() {
      var l = new List<Control>();
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
          if(kv.Value["schema"].ValueType == JSC.JSValueType.String) {
            mi.Icon = SchemaName2Icon(kv.Value["schema"].Value as string);
          }
          mi.Tag = kv.Value;
          mi.Click += miAdd_Click;
          ma.Items.Add(mi);
        }
      } else {
        mi = new MenuItem() { Header = "Boolean", Tag = InTopic.DEFS_Bool, Icon = new Image() { Source = App.GetIcon("Boolean") } };
        mi.Click += miAdd_Click;
        ma.Items.Add(mi);
        mi = new MenuItem() { Header = "Double", Tag = InTopic.DEFS_Double, Icon = new Image() { Source = App.GetIcon("Double") } };
        mi.Click += miAdd_Click;
        ma.Items.Add(mi);
        mi = new MenuItem() { Header = "String", Tag = InTopic.DEFS_String, Icon = new Image() { Source = App.GetIcon("String") } };
        mi.Click += miAdd_Click;
        ma.Items.Add(mi);
        mi = new MenuItem() { Header = "Date", Tag = InTopic.DEFS_Date, Icon = new Image() { Source = App.GetIcon("Date") } };
        mi.Click += miAdd_Click;
        ma.Items.Add(mi);
      }
      if(ma.HasItems) {
        l.Add(ma);
        l.Add(new Separator());
      }
      if(!_root) {
        mi = new MenuItem() { Header = "Open in new Tab" };
        mi.Click += miOpen_Click;
        l.Add(mi);
        l.Add(new Separator());
      }

      l.Add(new MenuItem() { Header = "Cut", IsEnabled = false });
      l.Add(new MenuItem() { Header = "Copy", IsEnabled = false });
      l.Add(new MenuItem() { Header = "Paste", IsEnabled = false });
      mi = new MenuItem() { Header = "Delete", Icon = new Image() { Source = App.GetIcon("component/Images/delete.png") } };
      if(_schema == null || (f = _schema["required"]).ValueType != JSC.JSValueType.Boolean || true != (bool)f) {
        mi.Click += miDelete_Click;
      } else {
        mi.IsEnabled = false;
      }
      l.Add(mi);
      l.Add(new MenuItem() { Header = "Rename", IsEnabled = false });
      return l;
    }


    private Image SchemaName2Icon(string sn) {
      Image img = new Image();
      this._owner.GetAsync("/etc/schema/" + sn).ContinueWith(td => {
        if(td.IsCompleted && td.Result != null && td.Result.value != null && td.Result.value["icon"].ValueType == JSC.JSValueType.String) {
          DWorkspace.ui.BeginInvoke(new Action(() => img.Source = App.GetIcon(td.Result.value["icon"].Value as string)));
        }
      });
      return img;
    }
    private void miOpen_Click(object sender, System.Windows.RoutedEventArgs e) {
      DWorkspace.This.Open(_owner.fullPath);
    }
    private void miAdd_Click(object sender, System.Windows.RoutedEventArgs e) {
      if(!IsExpanded) {
        IsExpanded = true;
        base.PropertyChangedFunc("IsExpanded");
      }
      var mi = sender as MenuItem;
      if(mi != null) {
        var name = mi.Header as string;
        var decl = mi.Tag as JSC.JSValue;
        if(name != null && decl != null) {
          var mask = decl["mask"];
          if(_items == null) {
            _items = new ObservableCollection<InTopic>();
            PropertyChangedReise("items");
          }
          if(mask.ValueType == JSC.JSValueType.Boolean && (bool)mask) {
            _items.Insert(0, new InTopic(decl, this));
          }
        }
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
