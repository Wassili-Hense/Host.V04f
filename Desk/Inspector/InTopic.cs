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
    private JSC.JSValue _createTag;

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
        levelPadding = _parent.levelPadding + 8;
      }
      base._isExpanded = IsGroupHeader && _owner.children != null && _owner.children.Any();
      base._isVisible = IsGroupHeader || (_parent._isVisible && _parent._isExpanded);
    }
    private InTopic(JSC.JSValue tag, InTopic parent) {
      _parent = parent;
      _collFunc = parent._collFunc;
      name = string.Empty;
      IsEdited = true;
      levelPadding = _parent == null ? 1 : _parent.levelPadding + 8;
      _createTag = tag;
    }

    public override bool IsExpanded {
      get {
        return _isExpanded && HasChildren;
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
    public override DTopic Root {
      get { return _owner.Connection.root; }
    }
    public override void FinishNameEdit(string name) {
      if(_owner == null) {
        _parent._items.Remove(this);
        _parent._collFunc(this, false);
        if(!string.IsNullOrEmpty(name)) {
          _parent._owner.CreateAsync(name, _createTag["default"], _createTag["manifest"]).ContinueWith(SetNameComplete, TaskScheduler.FromCurrentSynchronizationContext());
        } else if(!_parent._items.Any()) {
          _parent._items = null;
          PropertyChangedReise("items");
          PropertyChangedReise("HasChildren");
          _parent.IsExpanded = false;
        }
      } else {
        if(!string.IsNullOrEmpty(name)) {
          _owner.Move(_owner.parent, name);
        }
        IsEdited = false;
        PropertyChangedReise("IsEdited");
      }
    }

    private void SetNameComplete(Task<DTopic> td) {
      if(td.IsCompleted && td.Result != null) {
        //_owner = td.Result;
        //_owner.changed += _owner_PropertyChanged;
        //base.name = _owner.name;
        //base.UpdateType(_owner.type);
        //IsEdited = false;
        //PropertyChangedReise("IsEdited");
        //PropertyChangedReise("name");
      } else {
        if(td.IsFaulted) {
          Log.Warning("{0}/{1} - {2}", _parent._owner.fullPath, base.name, td.Exception.Message);
        }
        if(_parent._items != null) {
          _parent._items.Remove(this);
          _collFunc(this, false);
          if(!_parent._items.Any()) {
            _parent._items = null;
            PropertyChangedReise("items");
            PropertyChangedReise("HasChildren");
            _parent.IsExpanded = false;
          }
        }
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
        PropertyChangedReise("HasChildren");
        if(_items != null && _items.Any()) {
          _parent.IsExpanded = true;
        }
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
        }
        int i;
        for(i = 0; i < _items.Count; i++) {
          if(string.Compare(_items[i].name, tt.name) > 0) {
            break;
          }
        }
        _items.Insert(i, tmp);
        if(_items.Count == 1) {
          PropertyChangedReise("items");
          PropertyChangedReise("HasChildren");
        }
        if(_isVisible && _isExpanded) {
          _collFunc(tmp, true);
        }
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
    private void _owner_PropertyChanged(DTopic.Art art, DTopic child) {
      {
        var pr = this;
        while(pr._parent != null) {
          pr = pr._parent;
        }
        Log.Debug("$ " + pr._owner.path + "(" + art.ToString() + ", " + (child != null ? child.path : "null") + ")");
      }
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
                PropertyChangedReise("HasChildren");
                PropertyChangedReise("items");
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
      if(!IsGroupHeader) {
        mi = new MenuItem() { Header = "Open in new tab" };
        mi.Click += miOpen_Click;
        l.Add(mi);
        l.Add(new Separator());
      }
      MenuItem ma = new MenuItem() { Header = "Add" };
      KeyValuePair<string, JSC.JSValue>[] _acts;
      if(_manifest != null && (v1 = _manifest["Children"]).ValueType == JSC.JSValueType.Object) {
        _acts = v1.Where(z => z.Value != null && z.Value.ValueType == JSC.JSValueType.Object && z.Value["default"].Defined).ToArray();
      } else if(_owner.Connection.TypeManifest != null) {
        _acts = _owner.Connection.TypeManifest.parent.children.Where(z => z.value.ValueType == JSC.JSValueType.Object && z.value.Value != null && z.value["default"].Defined)
          .Select(z => new KeyValuePair<string, JSC.JSValue>(z.name, z.value)).ToArray();
      } else {
        _acts = null;
      }
      if(_acts != null && _acts.Length > 0) {
        List<RcUse> resource = new List<RcUse>();
        string rName;
        JSC.JSValue tmp1;
        KeyValuePair<string, JSC.JSValue> rca;
        string rcs;
        // fill used resources
        if(_owner.children != null) {
          foreach(var ch in _owner.children) {
            if((tmp1 = JsLib.GetField(ch.type, "MQTT-SN.tag")).ValueType != JSC.JSValueType.String || string.IsNullOrEmpty(rName = tmp1.Value as string)) {
              rName = ch.name;
            }
            rca = _acts.FirstOrDefault(z => z.Key == rName);
            if(rca.Value == null || (tmp1 = rca.Value["rc"]).ValueType != JSC.JSValueType.String || string.IsNullOrEmpty(rcs = tmp1.Value as string)) {
              continue;
            }
            foreach(string curRC in rcs.Split(',').Where(z => !string.IsNullOrWhiteSpace(z) && z.Length > 1)) {
              int pos;
              if(!int.TryParse(curRC.Substring(1), out pos)) {
                continue;
              }
              for(int i = pos - resource.Count; i >= 0; i--) {
                resource.Add(RcUse.None);
              }
              if(curRC[0] != (char)RcUse.None && (curRC[0] != (char)RcUse.Shared || resource[pos] != RcUse.None)) {
                resource[pos] = (RcUse)curRC[0];
              }
            }
          }
        }
        // Add menuitems
        foreach(var kv in _acts) {
          bool busy = false;
          if((tmp1 = kv.Value["rc"]).ValueType == JSC.JSValueType.String && !string.IsNullOrEmpty(rcs = tmp1.Value as string)) { // check used resources
            foreach(string curRC in rcs.Split(',').Where(z => !string.IsNullOrWhiteSpace(z) && z.Length > 1)) {
              int pos;
              if(!int.TryParse(curRC.Substring(1), out pos)) {
                continue;
              }
              if(pos < resource.Count && ((curRC[0] == (char)RcUse.Exclusive && resource[pos] != RcUse.None) || (curRC[0] == (char)RcUse.Shared && resource[pos] != RcUse.None && resource[pos] != RcUse.Shared))) {
                busy = true;
                break;
              }
            }
          }
          if(busy) {
            continue;
          }
          mi = new MenuItem() { Header = kv.Key.Replace("_", "__"), Tag = kv.Value };
          if((v2 = kv.Value["icon"]).ValueType == JSC.JSValueType.String) {
            mi.Icon = new Image() { Source = App.GetIcon(v2.Value as string), Height = 16, Width = 16 };
          } else {
            mi.Icon = new Image() { Source = App.GetIcon(kv.Key), Height = 16, Width = 16 };
          }
          if((v2 = kv.Value["info"]).ValueType == JSC.JSValueType.String) {
            mi.ToolTip = v2.Value;
          }
          mi.Click += miAdd_Click;
          if((v2 = kv.Value["menu"]).ValueType == JSC.JSValueType.String && kv.Value.Value != null) {
            AddSubMenu(ma, v2.Value as string, mi);
          } else {
            ma.Items.Add(mi);
          }
        }
      }
      if(ma.HasItems) {
        if(ma.Items.Count < 5) {
          l.AddRange(ma.Items.SourceCollection.OfType<System.Windows.Controls.Control>());
        } else {
          l.Add(ma);
        }
        l.Add(new Separator());
      }
      Uri uri;
      if(System.Windows.Clipboard.ContainsText(System.Windows.TextDataFormat.Text)
        && Uri.TryCreate(System.Windows.Clipboard.GetText(System.Windows.TextDataFormat.Text), UriKind.Absolute, out uri)
        && _owner.Connection.server == uri.DnsSafeHost) {
        mi = new MenuItem() { Header = "Paste", Icon = new Image() { Source = App.GetIcon("component/Images/Edit_Paste.png"), Width = 16, Height = 16 } };
        mi.Click += miPaste_Click;
        l.Add(mi);

      }
      mi = new MenuItem() { Header = "Cut", Icon = new Image() { Source = App.GetIcon("component/Images/Edit_Cut.png"), Width = 16, Height = 16 } };
      mi.IsEnabled = !IsGroupHeader && !IsRequired;
      mi.Click += miCut_Click;
      l.Add(mi);
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

    private void AddSubMenu(MenuItem ma, string prefix, MenuItem mi) {
      MenuItem mm = ma, mn;
      string[] lvls = prefix.Split(new char[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
      for(int j = 0; j < lvls.Length; j++) {
        mn = mm.Items.OfType<MenuItem>().FirstOrDefault(z => z.Header as string == lvls[j]);
        if(mn == null) {
          mn = new MenuItem();
          mn.Header = lvls[j];
          mm.Items.Add(mn);
        }
        mm = mn;
      }
      mm.Items.Add(mi);
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
      var tag = mi.Tag as JSC.JSValue;
      if(tag != null) {
        if((bool)tag["willful"]) {
          if(_items == null) {
            lock(this) {
              if(_items == null) {
                _items = new List<InBase>();
                pc_items = true;
              }
            }
          }
          var ni = new InTopic(tag, this);
          _items.Insert(0, ni);
          _collFunc(ni, true);
        } else {
          _owner.CreateAsync((mi.Header as string).Replace("__", "_"), tag["default"], tag["manifest"]);
        }
      }
      if(pc_items) {
        PropertyChangedReise("items");
      }
    }
    private void miOpen_Click(object sender, System.Windows.RoutedEventArgs e) {
      App.Workspace.Open(_owner.fullPath);
    }
    private void miCut_Click(object sender, System.Windows.RoutedEventArgs e) {
      System.Windows.Clipboard.SetText(_owner.fullPath, System.Windows.TextDataFormat.Text);
    }
    private void miPaste_Click(object sender, System.Windows.RoutedEventArgs e) {
      Uri uri;
      if(System.Windows.Clipboard.ContainsText(System.Windows.TextDataFormat.Text)
        && Uri.TryCreate(System.Windows.Clipboard.GetText(System.Windows.TextDataFormat.Text), UriKind.Absolute, out uri)
        && _owner.Connection.server==uri.DnsSafeHost) {
          System.Windows.Clipboard.Clear();
          App.Workspace.GetAsync(uri).ContinueWith(td => {
            if(td.IsCompleted && !td.IsFaulted && td.Result != null) {
              td.Result.Move(_owner, td.Result.name);
            }
          }, TaskScheduler.FromCurrentSynchronizationContext());
      }
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
    private enum RcUse : ushort {
      None = '0',
      Baned = 'B',
      Shared = 'S',
      Exclusive = 'X',
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
