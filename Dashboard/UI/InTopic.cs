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
      if(_populated && _owner.children!=null) {
        InsertItems(_owner.children);
      }
    }
    public void FinishNameEdit(string name) {
      if(!string.IsNullOrEmpty(name)) {
        if(_owner == null) {
          base.name = name;
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
          _owner.Move(_owner.parent, name);
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
        _parent.items.Remove(this);
      }
    }

    private void _owner_PropertyChanged(DTopic.Art art, DTopic child) {
      if(!_root) {
        if(art==DTopic.Art.schema) {
          Log.Debug("{0} #{1}", _owner.path, art.ToString());
          this.UpdateSchema(_owner.schema);
        } else if(art==DTopic.Art.value) {
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
            var td=AddTopic(child);
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
        l.Add(new MenuItem() { Command = ApplicationCommands.Open, CommandTarget = src});
        l.Add(new Separator());
      }
      //System.Windows.Clipboard. 
      l.Add(new MenuItem() { Command = ApplicationCommands.Cut, CommandTarget = src, Icon = new Image() { Source = App.GetIcon("component/Images/Edit_Cut.png"), Width = 16, Height = 16 } });
      l.Add(new MenuItem() { Command = ApplicationCommands.Copy, CommandTarget = src, Icon = new Image() { Source = App.GetIcon("component/Images/Edit_Copy.png"), Width = 16, Height = 16 } });
      if(_owner.CheckAcl(DTopic.ACL.Create) && System.Windows.Clipboard.ContainsText()) {
        Uri u;
        if(Uri.TryCreate(System.Windows.Clipboard.GetText(), UriKind.Absolute, out u) && u.Scheme != null 
          && u.Scheme.StartsWith("x13") && _owner.fullPath.StartsWith(u.GetLeftPart(UriPartial.Authority)) && !_owner.path.StartsWith(u.AbsolutePath) ) {
          l.Add(new MenuItem() {Command=ApplicationCommands.Paste, CommandTarget=src, Icon = new Image() { Source = App.GetIcon("component/Images/Edit_Paste.png"), Width = 16, Height = 16 } });
        }
      }
      mi = new MenuItem() {Command = ApplicationCommands.Delete, CommandTarget = src, Icon = new Image() { Source = App.GetIcon("component/Images/Edit_Delete.png"), Width = 16, Height = 16 } };
      l.Add(mi);
      if(!_root && _owner.CheckAcl(DTopic.ACL.Delete) && _parent._owner.CheckAcl(DTopic.ACL.Create)) {
        l.Add(new MenuItem() { Command = InspectorForm.CmdRename, CommandTarget = src, Icon = new Image() { Source = App.GetIcon("component/Images/Edit_Rename.png"), Width = 16, Height = 16 } });
      }
      return l;
    }

    public override bool CanExecute(ICommand cmd, object p) {
      JSC.JSValue f;
      if(cmd == ApplicationCommands.Open || cmd == ApplicationCommands.Paste || cmd == ApplicationCommands.Copy || cmd==InspectorForm.CmdRename) {
        return true;
      } else if(cmd == ApplicationCommands.Delete || cmd == ApplicationCommands.Cut) {
        return !_root && (_schema == null || (f = _schema["required"]).ValueType != JSC.JSValueType.Boolean || true != (bool)f) && _owner.CheckAcl(DTopic.ACL.Delete);
      }
      return false;
    }
    public override void CmdExecuted(ICommand cmd, object p) {
      if(cmd == ApplicationCommands.Open) {
        DWorkspace.This.Open(_owner.fullPath);
      }else if(cmd == ApplicationCommands.Delete) {
        _owner.Delete();
      } else if(cmd == ApplicationCommands.Cut) {
      } else if(cmd == ApplicationCommands.Copy) {
      } else if(cmd == ApplicationCommands.Paste) {
      } else if(cmd == InspectorForm.CmdRename) {
        base.IsEdited = true;
        PropertyChangedReise("IsEdited");
      }
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
