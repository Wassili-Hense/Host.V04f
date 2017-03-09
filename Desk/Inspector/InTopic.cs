﻿///<remarks>This file is part of the <see cref="https://github.com/X13home">X13.Home</see> project.<remarks>
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
    private InTopic _parent;
    private DTopic _owner;
    private bool _populated;
    private string _createType;

    public InTopic(DTopic owner, InTopic parent, Action<InBase, bool> collFunc) {
      _owner = owner;
      _parent = parent;
      _collFunc = collFunc;
      IsGroupHeader = _parent == null;
      _owner.changed += _owner_PropertyChanged;
      if(IsGroupHeader) {
        _manifest = _owner.type;  // if(IsGroupHeader) don't use UpdateType(...)
        name = "children";
        icon = App.GetIcon("children");
        editor = null;
        levelPadding = 1;
        _populated = true;
        if(_owner.children != null) {
          InsertItems(_owner.children);
        }
      } else {
        name = _owner.name;
        base.UpdateType(_owner.type);
        levelPadding = _parent.levelPadding + 5;
      }
      base._isExpanded = IsGroupHeader && _owner.children != null && _owner.children.Any();
      base._isVisible = IsGroupHeader || (_parent._isVisible && _parent._isExpanded);
    }
    private InTopic(string type, InTopic parent) {
      _parent = parent;
      _collFunc = parent._collFunc;
      name = string.Empty;
      IsEdited = true;
      levelPadding = _parent == null ? 1 : _parent.levelPadding + 5;
      _createType = type;
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
        return (_owner != null && _owner.children != null && _owner.children.Any()) || (_items != null && _items.Any());
      }
    }
    public override JSC.JSValue value { get { return _owner != null ? _owner.value : JSC.JSValue.NotExists; } set { if(_owner != null) { _owner.SetValue(value); } } }
    public override void FinishNameEdit(string name) {
      if(_owner == null) {
        _parent._items.Remove(this);
        _parent._collFunc(this, false);
        if(!_parent._items.Any()) {
          _parent._items = null;
          _parent.IsExpanded = false;
        }
        if(!string.IsNullOrEmpty(name)) {
          _parent._owner.CreateAsync(name, _createType).ContinueWith(SetNameComplete, TaskScheduler.FromCurrentSynchronizationContext());
        }
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
        if(_parent._items != null) {
          _parent._items.Remove(this);
          _collFunc(this, false);
          if(!_parent._items.Any()) {
            _parent._items = null;
            _parent.IsExpanded = false;
          }
        }
      }
    }
    private void _owner_PropertyChanged(DTopic.Art art, DTopic child) {
      if(IsGroupHeader) {
        if(art == DTopic.Art.type) {
          _manifest = _owner.type;
        }
      } else {
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
          if(_items != null) {
            var it = _items.FirstOrDefault(z => z.name == child.name);
            if(it != null) {
              it.Deleted();
              _items.Remove(it);
              if(!_items.Any()) {
                _items = null;
                IsExpanded = false;
              }
            }
          }
        }
      }
    }

    #region ContextMenu
    public override List<Control> MenuItems(System.Windows.FrameworkElement src) {
      var l = new List<Control>();
      JSC.JSValue v1, v2;
      MenuItem mi;
      MenuItem ma = new MenuItem() { Header = "Add" };
      if(_manifest != null && (v1 = _manifest["Children"]).ValueType == JSC.JSValueType.Object) {
        foreach(var kv in v1.Where(z => z.Value != null && z.Value.ValueType == JSC.JSValueType.Object)) {
          if(_items.Any(z => (v2 = kv.Value["name"]).ValueType == JSC.JSValueType.String && z.name == v2.Value as string)) {
            continue;
          }
          // TODO: check resources
          mi = new MenuItem() { Header = kv.Key, Tag = kv.Value };
          if((v2 = kv.Value["icon"]).ValueType == JSC.JSValueType.String) {
            mi.Icon = new Image() { Source = App.GetIcon(v2.Value as string) };
          } else {
            mi.Icon = new Image() { Source = App.GetIcon(kv.Key) };
          }
          mi.Click += miAdd_Click;
          ma.Items.Add(mi);
        }
      } else {
        if(_owner.Connection.TypeManifest != null) {
          foreach(var t in _owner.Connection.TypeManifest.parent.children) {
            if(t.name == "Manifest" || (v1 = t.value).ValueType != JSC.JSValueType.Object || v1.Value == null) {
              continue;
            }
            mi = new MenuItem() { Header = t.name, Tag = v1 };
            if((v2 = v1["icon"]).ValueType == JSC.JSValueType.String) {
              mi.Icon = new Image() { Source = App.GetIcon(v2.Value as string) };
            } else {
              mi.Icon = new Image() { Source = App.GetIcon(t.name) };
            }
            mi.Click += miAdd_Click;
            ma.Items.Add(mi);

          }
        }
      }
      if(ma.HasItems) {
        l.Add(ma);
        l.Add(new Separator());
      }
      if(!IsGroupHeader) {
        mi = new MenuItem() { Header = "Open" };
        mi.Click += miOpen_Click;
        l.Add(mi);
        l.Add(new Separator());
      }
      mi = new MenuItem() { Header = "Delete", Icon = new Image() { Source = App.GetIcon("component/Images/Edit_Delete.png"), Width = 16, Height = 16 } };
      mi.IsEnabled = !IsGroupHeader && !IsRequired;
      mi.Click += miDelete_Click;
      l.Add(mi);
      if(!IsGroupHeader && !IsRequired) {
        mi = new MenuItem() { Header = "Rename", Icon = new Image() { Source = App.GetIcon("component/Images/Edit_Rename.png"), Width = 16, Height = 16 } };
        mi.Click += miRename_Click;
        l.Add(mi);
      }
      return l;
    }

    private void miAdd_Click(object sender, System.Windows.RoutedEventArgs e) {
      var mi = sender as MenuItem;
      if(mi == null) {
        return;
      }
      if(!IsExpanded) {
        IsExpanded = true;
        base.PropertyChangedReise("IsExpanded");
      }
      bool pc_items = false;
      var decl = mi.Tag as JSC.JSValue;
      if(decl != null) {
        if((bool)decl["willful"]) {
          if(_items == null) {
            lock(this) {
              if(_items == null) {
                _items = new List<InBase>();
                pc_items = true;
              }
            }
          }
          var ni = new InTopic(decl["type"].Value as string, this);
          _items.Insert(0, ni);
          _collFunc(ni, true);
        }
      } else {
        _owner.CreateAsync(mi.Header as string, decl["type"].Value as string);
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
      return o == null ? 1 : this.path.CompareTo(o.path);
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
