///<remarks>This file is part of the <see cref="https://github.com/X13home">X13.Home</see> project.<remarks>
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
  internal class InTopic : InBase, IDisposable {
    #region default children
    //private static JSC.JSObject DEFS_Bool;
    //private static JSC.JSObject DEFS_Double;
    //private static JSC.JSObject DEFS_String;
    //private static JSC.JSObject DEFS_Date;
    //static InTopic() {
    //  DEFS_Bool = JSC.JSObject.CreateObject();
    //  DEFS_Bool["type"] = "Boolean";

    //  DEFS_Double = JSC.JSObject.CreateObject();
    //  DEFS_Double["type"] = "Double";

    //  DEFS_String = JSC.JSObject.CreateObject();
    //  DEFS_String["type"] = "String";

    //  DEFS_Date = JSC.JSObject.CreateObject();
    //  DEFS_Date["type"] = "Date";
    //}
    #endregion default children

    private InTopic _parent;
    private DTopic _owner;
    private bool _root;
    private bool _populated;
    private JSC.JSValue _cStruct;

    public InTopic(DTopic owner, InTopic parent, Action<InBase, bool> collFunc) {
      _owner = owner;
      _parent = parent;
      _collFunc = collFunc;
      _root = _parent == null;
      IsGroupHeader = _root;
      _owner.changed += _owner_PropertyChanged;
      if(_root) {
        name = "children";
        icon = App.GetIcon("children");
        editor = null;
        levelPadding = 5;
        _populated = true;
        if(_owner.children != null) {
          InsertItems(_owner.children);
        }
      } else {
        name = _owner.name;
        base.UpdateType(_owner.type);
        levelPadding = _parent.levelPadding + 7;
      }
      base._isExpanded = _root && _owner.children!=null && _owner.children.Any();
      base._isVisible = _root || (_parent._isVisible && _parent._isExpanded);
    }
    private InTopic(JSC.JSValue cStruct, InTopic parent, Action<InBase, bool> collFunc) {
      _parent = parent;
      _cStruct = cStruct;
      name = string.Empty;
      IsEdited = true;
      levelPadding = _parent == null ? 5 : _parent.levelPadding + 7;

      JSC.JSValue sn;
      if(_cStruct != null && (sn = _cStruct["type"]).ValueType == JSC.JSValueType.String) {
        parent._owner.GetAsync("/etc/type/" + (sn.Value as string)).ContinueWith(TypeLoaded, TaskScheduler.FromCurrentSynchronizationContext());
      }
    }

    public override bool IsExpanded {
      get {
        return _isExpanded;
      }
      set {
        base.IsExpanded = value;
        if(_isExpanded && _owner != null && _items == null) {
          _populated = true;
          if(_owner.children != null) {
            InsertItems(_owner.children);
          }
        }
      }
    }
    public override bool HasChildren {
      get {
        return _owner != null && _owner.children != null;
      }
    }
    public override JSC.JSValue value { get { return _owner != null ? _owner.value : JSC.JSValue.NotExists; } set { if(_owner != null) { _owner.SetValue(value); } } }
    public void FinishNameEdit(string name) {
      if(_owner == null) {
        if(!string.IsNullOrEmpty(name)) {
          //base.name = name;
          var td = _parent._owner.CreateAsync(name, _cStruct["type"].Value as string, _cStruct["default"]);
          //td.ContinueWith(SetNameComplete);
        }
        _parent._items.Remove(this);
        _parent._collFunc(this, false);
      } else {
        if(!string.IsNullOrEmpty(name)) {
          _owner.Move(_owner.parent, name);
        }
        IsEdited = false;
        PropertyChangedReise("IsEdited");
      }
    }

    private void InsertItems(ReadOnlyCollection<DTopic> its) {
      bool pc_items = false;
      if(_items == null) {
        lock(this) {
          if(_items == null) {
            _items = new List<InBase>();
            pc_items = true;
          }
        }
      }
      foreach(var t in its.ToArray()) {
        var td = AddTopic(t);
      }
      if(pc_items) {
        PropertyChangedReise("items");
      }
    }
    private async Task AddTopic(DTopic t) {
      InTopic tmp;
      var tt = await t.GetAsync(null);
      if(tt != null) {
        if((tmp = _items.OfType<InTopic>().FirstOrDefault(z => z.name == tt.name)) != null) {
          _items.Remove(tmp);
          _collFunc(tmp, false);
          tmp.RefreshOwner(tt);
        } else {
          tmp = new InTopic(tt, this, _collFunc);
          if(_isVisible && _isExpanded) {
            _collFunc(tmp, true);
          }
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
        base.UpdateType(_owner.type);
        IsEdited = false;
        PropertyChangedReise("IsEdited");
        PropertyChangedReise("name");
      } else {
        if(td.IsFaulted) {
          Log.Warning("{0}/{1} - {2}", _parent._owner.fullPath, base.name, td.Exception.Message);
        }
        _parent._items.Remove(this);
        _collFunc(this, false);
      }
    }
    private void TypeLoaded(Task<DTopic> dt) {
      if(dt.IsCompleted && dt.Result != null) {
        base.UpdateType(dt.Result.value);
      }
    }
    private void _owner_PropertyChanged(DTopic.Art art, DTopic child) {
      if(!_root) {
        if(art == DTopic.Art.type) {
          this.UpdateType(_owner.type);
        } else if(art == DTopic.Art.value) {
          this.UpdateType(_owner.type);
          this.editor.ValueChanged(_owner.value);
        }
      }
      if(_populated) {
        if(art == DTopic.Art.addChild) {
          if(_items == null) {
            InsertItems(_owner.children);
          } else {
            var td = AddTopic(child);
          }
        } else if(art == DTopic.Art.RemoveChild) {
          var it = _items.FirstOrDefault(z => z.name == child.name);
          if(it != null) {
            it.Deleted();
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
      if(_type != null && (f = _type["Children"]).ValueType == JSC.JSValueType.Object) {
        foreach(var kv in f.Where(z => z.Value != null && z.Value.ValueType == JSC.JSValueType.Object)) {
          // TODO: check resources
          if(_items.Any(z => (tmp1 = kv.Value["name"]).ValueType == JSC.JSValueType.String && z.name == tmp1.Value as string)) {
            continue;
          }
          mi = new MenuItem() { Header = kv.Key, Tag = kv.Value };
          mi.Click += miAdd_Click;
          if(kv.Value["$type"].ValueType == JSC.JSValueType.String) {
            mi.Icon = TypeName2Icon(kv.Value["$type"].Value as string);
          }
          ma.Items.Add(mi);
        }
      //} else {
      //  mi = new MenuItem() { Header = "Boolean", Tag = InTopic.DEFS_Bool, Icon = new Image() { Source = App.GetIcon("Boolean") } };
      //  mi.Click += miAdd_Click;
      //  ma.Items.Add(mi);
      //  mi = new MenuItem() { Header = "Double",  Tag = InTopic.DEFS_Double, Icon = new Image() { Source = App.GetIcon("Double") } };
      //  mi.Click += miAdd_Click;
      //  ma.Items.Add(mi);
      //  mi = new MenuItem() { Header = "String", Tag = InTopic.DEFS_String, Icon = new Image() { Source = App.GetIcon("String") } };
      //  mi.Click += miAdd_Click;
      //  ma.Items.Add(mi);
      //  mi = new MenuItem() { Header = "Date", Tag = InTopic.DEFS_Date, Icon = new Image() { Source = App.GetIcon("Date") } };
      //  mi.Click += miAdd_Click;
      //  ma.Items.Add(mi);
      }
      if(ma.HasItems) {
        l.Add(ma);
        l.Add(new Separator());
      }
      if(!_root) {
        mi = new MenuItem() { Header = "Open" };
        mi.Click += miOpen_Click;
        l.Add(mi);
        l.Add(new Separator());
      }
      mi = new MenuItem() { Header="Delete", Icon = new Image() { Source = App.GetIcon("component/Images/Edit_Delete.png"), Width = 16, Height = 16 } };
      mi.IsEnabled = !_root && (_type == null || (f = _type["required"]).ValueType != JSC.JSValueType.Boolean || true != (bool)f);
      mi.Click += miDelete_Click;
      l.Add(mi);
      if(!_root) {
        mi = new MenuItem() { Header="Rename", Icon = new Image() { Source = App.GetIcon("component/Images/Edit_Rename.png"), Width = 16, Height = 16 } };
        mi.Click += miRename_Click;
        l.Add(mi);
      }
      return l;
    }

    private void miAdd_Click(object sender, System.Windows.RoutedEventArgs e) {
      if(!IsExpanded) {
        IsExpanded = true;
        base.PropertyChangedReise("IsExpanded");
      }
      bool pc_items = false;
      var decl = (sender as MenuItem).Tag as JSC.JSValue;
      if(decl != null) {
        var mName = decl["name"];
        if(mName.ValueType == JSC.JSValueType.String && !string.IsNullOrEmpty(mName.Value as string)) {
          _owner.CreateAsync(mName.Value as string, decl["type"].Value as string, decl["default"]);
        } else {
          if(_items == null) {
            lock(this) {
              if(_items == null) {
                _items = new List<InBase>();
                pc_items = true;
              }
            }
          }
          var ni = new InTopic(decl, this, _collFunc);
          _items.Insert(0, ni);
          _collFunc(ni, true);
        }
      }
      if(pc_items) {
        PropertyChangedReise("items");
      }
    }
    private void miOpen_Click(object sender, System.Windows.RoutedEventArgs e) {
      App.Workspace.Open(_owner.fullPath);
    }
    private void miDelete_Click(object sender, System.Windows.RoutedEventArgs e) {
      _owner.Delete();
    }
    private void miRename_Click(object sender, System.Windows.RoutedEventArgs e) {
      base.IsEdited = true;
      PropertyChangedReise("IsEdited");
    }

    private Image TypeName2Icon(string sn) {
      Image img = new Image();
      this._owner.GetAsync("/etc/type/" + sn).ContinueWith(IconFromTypeLoaded, img, TaskScheduler.FromCurrentSynchronizationContext());
      return img;
    }
    private void IconFromTypeLoaded(Task<DTopic> td, object o) {
      var img = o as Image;
      if(img != null && td.IsCompleted && td.Result != null) {
        //img.Source = App.GetIcon(td.Result.GetField<string>("icon"));
      }
    }
    #endregion ContextMenu

    #region IComparable<InBase> Members
    public override int CompareTo(InBase other) {
      var o = other as InTopic;
      return o == null?1:this.path.CompareTo(o.path);
    }
    private string path {
      get {
        if(_owner != null) {
          return _owner.path;
        } else if(_parent != null && _parent._owner != null) {
          return _parent._owner.path;
        }
        return "/";
      }
    }
    #endregion IComparable<InBase> Members

    #region IDisposable Member
    public void Dispose() {
      _collFunc(this, false);
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
