using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Input;
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
      DEFS_Bool["schema"] = "Boolean";

      DEFS_Double = JSC.JSObject.CreateObject();
      DEFS_Double["schema"] = "Double";

      DEFS_String = JSC.JSObject.CreateObject();
      DEFS_String["schema"] = "String";

      DEFS_Date = JSC.JSObject.CreateObject();
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
      _parent = parent;
      _root = _parent == null;
      IsGroupHeader = _root;
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
        parent._owner.GetAsync("/etc/schema/" + (sn.Value as string)).ContinueWith(SchemaLoaded, TaskScheduler.FromCurrentSynchronizationContext());
      }
    }

    public override JSC.JSValue value { get { return _owner != null ? _owner.value : JSC.JSValue.NotExists; } set { if(_owner != null) { _owner.SetValue(value); } } }
    public ObservableCollection<InTopic> items {
      get {
        if(_owner != null && _items == null) {
          _populated = true;
          if(_owner.children != null) {
            InsertItems(_owner.children);
          }
        }
        return _items;
      }
    }
    public void FinishNameEdit(string name) {
      if(_owner == null) {
        if(!string.IsNullOrEmpty(name)) {
          //base.name = name;
          var td = _parent._owner.CreateAsync(name, _cStruct["schema"].Value as string, _cStruct["default"]);
          //td.ContinueWith(SetNameComplete);
        }
        _parent.items.Remove(this);
      } else {
        if(!string.IsNullOrEmpty(name)) {
          _owner.Move(_owner.parent, name);
        }
        IsEdited = false;
        PropertyChangedReise("IsEdited");
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
      foreach(var t in its.ToArray()) {
        await AddTopic(t);
      }
      if(pc_items) {
        PropertyChangedReise("items");
      }
    }
    private async Task AddTopic(DTopic t) {
      InTopic tmp;
      var tt = await t.GetAsync(null);
      if(tt != null) {
        if((tmp = _items.FirstOrDefault(z => z.name == tt.name)) != null) {
          _items.Remove(tmp);
          tmp.RefreshOwner(tt);
        } else {
          tmp = new InTopic(tt, this);
        }
        int i;
        for(i = 0; i < _items.Count; i++) {
          if(string.Compare(_items[i].name, tt.name) > 0) {
            break;
          }
        }
        _items.Insert(i, tmp);
      }
    }
    private void RefreshOwner(DTopic tt) {
      if(_owner != null) {
        _owner.changed -= _owner_PropertyChanged;
        if(_items != null) {
          _items.Clear();
          _items = null;
        }
      }
      _owner = tt;
      name = tt.name;
      if(_populated && _owner.children != null) {
        InsertItems(_owner.children);
      }
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
        _parent.items.Remove(this);
      }
    }
    private void SchemaLoaded(Task<DTopic> dt) {
      if(dt.IsCompleted && dt.Result != null) {
        base.UpdateSchema(dt.Result.value);
      }
    }
    private void _owner_PropertyChanged(DTopic.Art art, DTopic child) {
      if(!_root) {
        if(art == DTopic.Art.schema) {
          Log.Debug("{0} #{1}", _owner.path, art.ToString());
          this.UpdateSchema(_owner.schema);
        } else if(art == DTopic.Art.value) {
          Log.Debug("{0} #{1}", _owner.path, art.ToString());
          this.UpdateSchema(_owner.schema);
          this.editor.ValueChanged(_owner.value);
        }
      }
      if(_populated) {
        if(art == DTopic.Art.addChild) {
          Log.Debug("{0} #{1}", child.path, art.ToString());
          if(_items == null) {
            InsertItems(_owner.children);
          } else {
            var td = AddTopic(child);
          }
        } else if(art == DTopic.Art.RemoveChild) {
          Log.Debug("{0} #{1}", child.path, art.ToString());
          var it = _items.FirstOrDefault(z => z.name == child.name);
          if(it != null) {
            _items.Remove(it);
          }
        }
      }
    }

    #region ContextMenu
    public override List<Control> MenuItems(System.Windows.FrameworkElement src) {
      var l = new List<Control>();
      JSC.JSValue f, tmp1;
      MenuItem mi;
      MenuItem ma = new MenuItem() { Header = "Add" };
      if(_owner.CheckAcl(DTopic.ACL.Create) && _schema != null && (f = _schema["Children"]).ValueType == JSC.JSValueType.Object) {
        foreach(var kv in f.Where(z => z.Value != null && z.Value.ValueType == JSC.JSValueType.Object)) {
          // TODO: check resources
          if(_items.Any(z => (tmp1 = kv.Value["name"]).ValueType == JSC.JSValueType.String && z.name == tmp1.Value as string)) {
            continue;
          }
          mi = new MenuItem() { Header = kv.Key, Command = ApplicationCommands.New, CommandTarget = src, CommandParameter = kv.Value };
          if(kv.Value["schema"].ValueType == JSC.JSValueType.String) {
            mi.Icon = SchemaName2Icon(kv.Value["schema"].Value as string);
          }
          ma.Items.Add(mi);
        }
      } else {
        ma.Items.Add(new MenuItem() { Header = "Boolean", Command = ApplicationCommands.New, CommandTarget = src, CommandParameter = InTopic.DEFS_Bool, Icon = new Image() { Source = App.GetIcon("Boolean") } });
        ma.Items.Add(new MenuItem() { Header = "Double", Command = ApplicationCommands.New, CommandTarget = src, CommandParameter = InTopic.DEFS_Double, Icon = new Image() { Source = App.GetIcon("Double") } });
        ma.Items.Add(new MenuItem() { Header = "String", Command = ApplicationCommands.New, CommandTarget = src, CommandParameter = InTopic.DEFS_String, Icon = new Image() { Source = App.GetIcon("String") } });
        ma.Items.Add(new MenuItem() { Header = "Date", Command = ApplicationCommands.New, CommandTarget = src, CommandParameter = InTopic.DEFS_Date, Icon = new Image() { Source = App.GetIcon("Date") } });
      }
      if(ma.HasItems) {
        l.Add(ma);
        l.Add(new Separator());
      }
      if(!_root) {
        l.Add(new MenuItem() { Command = ApplicationCommands.Open, CommandTarget = src });
        l.Add(new Separator());
      }
      mi = new MenuItem() { Command = ApplicationCommands.Delete, CommandTarget = src, Icon = new Image() { Source = App.GetIcon("component/Images/Edit_Delete.png"), Width = 16, Height = 16 } };
      l.Add(mi);
      if(!_root && _owner.CheckAcl(DTopic.ACL.Delete) && _parent._owner.CheckAcl(DTopic.ACL.Create)) {
        l.Add(new MenuItem() { Command = InspectorForm.CmdRename, CommandTarget = src, Icon = new Image() { Source = App.GetIcon("component/Images/Edit_Rename.png"), Width = 16, Height = 16 } });
      }
      return l;
    }

    public override bool CanExecute(ICommand cmd, object p) {
      JSC.JSValue f;
      if(cmd == ApplicationCommands.Open || cmd == InspectorForm.CmdRename) {
        return true;
      } else if(cmd == ApplicationCommands.Delete) {
        return !_root && (_schema == null || (f = _schema["required"]).ValueType != JSC.JSValueType.Boolean || true != (bool)f) && _owner.CheckAcl(DTopic.ACL.Delete);
      } else if(cmd == ApplicationCommands.New) {
        return _owner.CheckAcl(DTopic.ACL.Create);
      }
      return false;
    }
    public override void CmdExecuted(ICommand cmd, object p) {
      if(cmd == ApplicationCommands.Open) {
        DWorkspace.This.Open(_owner.fullPath);
      } else if(cmd == ApplicationCommands.Delete) {
        _owner.Delete();
      } else if(cmd == InspectorForm.CmdRename) {
        base.IsEdited = true;
        PropertyChangedReise("IsEdited");
      } else if(cmd == ApplicationCommands.New) {
        if(!IsExpanded) {
          IsExpanded = true;
          base.PropertyChangedReise("IsExpanded");
        }
        bool pc_items = false;
        var decl = p as JSC.JSValue;
        if(decl != null) {
          var mName = decl["name"];
          if(mName.ValueType == JSC.JSValueType.String && !string.IsNullOrEmpty(mName.Value as string)) {
            _owner.CreateAsync(mName.Value as string, decl["schema"].Value as string, decl["default"]);
          } else {
            if(_items == null) {
              lock(this) {
                if(_items == null) {
                  _items = new ObservableCollection<InTopic>();
                  pc_items = true;
                }
              }
            }
            _items.Insert(0, new InTopic(decl, this));
          }
        }
        if(pc_items) {
          PropertyChangedReise("items");
        }
      }
    }

    private Image SchemaName2Icon(string sn) {
      Image img = new Image();
      this._owner.GetAsync("/etc/schema/" + sn).ContinueWith(IconFromSchemaLoaded, img, TaskScheduler.FromCurrentSynchronizationContext());
      return img;
    }
    private void IconFromSchemaLoaded(Task<DTopic> td, object o){
      var img = o as Image;
        if(img!=null && td.IsCompleted && td.Result != null && td.Result.value != null && td.Result.value["icon"].ValueType == JSC.JSValueType.String) {
          img.Source = App.GetIcon(td.Result.value["icon"].Value as string);
        }
    }
    #endregion ContextMenu

    #region IDisposable Member
    public void Dispose() {
      if(_owner != null) {
        _owner.changed -= _owner_PropertyChanged;
        _owner = null;
      }
    }
    #endregion IDisposable Member

    public override string ToString() {
      StringBuilder sb = new StringBuilder();
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
