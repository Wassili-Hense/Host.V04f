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
      _owner.changed += _owner_PropertyChanged;
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
            InsertItems(_owner.children);
          }
        }
        return _items;
      }
    }

    private async void InsertItems(ReadOnlyCollection<DTopic> its) {
      bool pc_items = false;
      if(_items == null) {
        lock(this) {
          if(_items == null) {
            _items = new ObservableCollection<InTopic>();
            pc_items = true;
          }
        }
      }
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
          _items.Add(tmp);
        }
      }
      if(pc_items) {
        PropertyChangedReise("items");
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
          td.ContinueWith(SetNameComplete);
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

    private void SetNameComplete(Task<DTopic> td) {
      if(td.IsCompleted && td.Result != null) {
        _owner = td.Result;
        _owner.changed += _owner_PropertyChanged;
        base.name = _owner.name;
        base.UpdateSchema(_owner.schema);
        IsEdited = false;
        PropertyChangedReise("IsEdited");
        PropertyChangedReise("name");
      } else {
        if(td.IsFaulted) {
          Log.Warning("{0}/{1} - {2}", _parent._owner.fullPath, base.name, td.Exception.Message);
        }
        FinishNameEdit(null);
      }
    }

    private void _owner_PropertyChanged(DTopic.Art art, int idx) {
      if(!_root) {
        if(art==DTopic.Art.schema) {
          this.UpdateSchema(_owner.schema);
        } else if(art==DTopic.Art.value) {
          this.UpdateSchema(_owner.schema);
          this.editor.ValueChanged(_owner.value);
        }
      }
      if(_populated) {
        if(art == DTopic.Art.addChild) {
          if(_items == null) {
            InsertItems(_owner.children);
          } else {
            var t=_owner.children[idx];
            var e = _items.FirstOrDefault(z => z.name == t.name);
            if(e != null) {
              _items.Remove(e);
              e.RefreshOwner(t);
            } else {
              e = new InTopic(t, this);
            }
            _items.Insert(idx, e);
          }
        } else if(art == DTopic.Art.RemoveChild) {
          _items.RemoveAt(idx);
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

      l.Add(new MenuItem() { Header = "Cut", Icon = new Image() { Source = App.GetIcon("component/Images/Edit_Cut.png"), Width = 16, Height = 16 }, IsEnabled = false });
      l.Add(new MenuItem() { Header = "Copy", Icon = new Image() { Source = App.GetIcon("component/Images/Edit_Copy.png"), Width = 16, Height = 16 }, IsEnabled = false });
      l.Add(new MenuItem() { Header = "Paste", Icon = new Image() { Source = App.GetIcon("component/Images/Edit_Paste.png"), Width = 16, Height = 16 }, IsEnabled = false });
      mi = new MenuItem() { Header = "Delete", Icon = new Image() { Source = App.GetIcon("component/Images/Edit_Delete.png"), Width = 16, Height = 16 } };
      if(_schema == null || (f = _schema["required"]).ValueType != JSC.JSValueType.Boolean || true != (bool)f) {
        mi.Click += miDelete_Click;
      } else {
        mi.IsEnabled = false;
      }
      l.Add(mi);
      l.Add(new MenuItem() { Header = "Rename", Icon = new Image() { Source = App.GetIcon("component/Images/Edit_Rename.png"), Width = 16, Height = 16 }, IsEnabled = false });
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
        base.PropertyChangedReise("IsExpanded");
      }
      bool pc_items = false;
      var mi = sender as MenuItem;
      if(mi != null) {
        var name = mi.Header as string;
        var decl = mi.Tag as JSC.JSValue;
        if(name != null && decl != null) {
          var mask = decl["mask"];
          if(_items == null) {
            lock(this) {
              if(_items == null) {
                _items = new ObservableCollection<InTopic>();
                pc_items = true;
              }
            }
          }
          if(mask.ValueType == JSC.JSValueType.Boolean && (bool)mask) {
            _items.Insert(0, new InTopic(decl, this));
          }
        }
        if(pc_items) {
          PropertyChangedReise("items");
        }
      }
    }
    private void miDelete_Click(object sender, System.Windows.RoutedEventArgs e) {
      _owner.Delete();
    }
    #endregion ContextMenu

    #region IDisposable Member
    public void Dispose() {
      _owner.changed -= _owner_PropertyChanged;
    }
    #endregion IDisposable Member

    public override string ToString() {
      StringBuilder sb= new StringBuilder();
      if(_owner == null) {
        if(_parent != null && _parent._owner != null) {
          sb.Append(_parent._owner.path);
        } else {
          sb.Append("...");
        }
        sb.AppendFormat("/{0}", name);
      } else {
        sb.Append(_owner.path);
      }
      return sb.ToString();
    }

  }
}
