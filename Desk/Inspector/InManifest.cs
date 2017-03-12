///<remarks>This file is part of the <see cref="https://github.com/X13home">X13.Home</see> project.<remarks>
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using X13.Data;
using JSC = NiL.JS.Core;
using JSL = NiL.JS.BaseLibrary;

namespace X13.UI {
  internal class InManifest : InBase, IDisposable {
    private DTopic _data;
    private DTopic _tManifest;
    private InManifest _parent;
    private JSC.JSValue _value;
    private string _path;

    public InManifest(DTopic data, Action<InBase, bool> collFunc) {
      this._data = data;
      this._parent = null;
      base._collFunc = collFunc;
      this.name = "Manifest";
      this._path = string.Empty;
      base._isVisible = true;
      base._isExpanded = true;
      base.IsGroupHeader = true;
      base.levelPadding = 1;
      base._items = new List<InBase>();
      this._value = _data.type;
      UpdateType(_tManifest!=null?_tManifest.value:null);
      UpdateData(_data.type);
      base._isExpanded = this.HasChildren;
      _data.changed += _data_PropertyChanged;
      _data.GetAsync("/$YS/TYPES/Core/Manifest").ContinueWith(ManifestLoaded, TaskScheduler.FromCurrentSynchronizationContext());
    }

    private void ManifestLoaded(Task<DTopic> td) {
      if(td.IsCompleted && !td.IsFaulted && td.Result!=null) {
        _tManifest = td.Result;
        _tManifest.changed += Manifest_changed;
        UpdateType(_tManifest.value);
      }
    }
    private InManifest(InManifest parent, string name, JSC.JSValue value, JSC.JSValue type) {
      this._parent = parent;
      this._data = _parent._data;
      base._collFunc = _parent._collFunc;
      base.name = name;
      this._path = string.IsNullOrEmpty(_parent._path) ? name : (_parent._path + "." + name);
      base._isVisible = _parent._isExpanded;
      base._items = new List<InBase>();
      base.IsGroupHeader = false;
      levelPadding = _parent.levelPadding + 8;
      this._value = value;
      UpdateType(type);
      UpdateData(value);
    }
    private InManifest(JSC.JSValue manifest, InManifest parent) {
      this._parent = parent;
      base._manifest = manifest;
      base._collFunc = parent._collFunc;
      base.name = string.Empty;
      this._path = _parent._path + ".";
      base.levelPadding = _parent == null ? 1 : _parent.levelPadding + 8;
      base._items = new List<InBase>();
      base.IsEdited = true;
    }

    private void UpdateData(JSC.JSValue val) {
      _value = val;
      if(_value.ValueType == JSC.JSValueType.Object) {
        InManifest vc;
        int i;
        foreach(var kv in _value.OrderBy(z => z.Key)) {
          vc = _items.OfType<InManifest>().FirstOrDefault(z => z.name == kv.Key);
          if(vc != null) {
            vc.UpdateData(kv.Value);
          } else {
            for(i = _items.Count - 1; i >= 0; i--) {
              if(string.Compare(_items[i].name, kv.Key) < 0) {
                break;
              }
            }
            JSC.JSValue cs;
            {
              JSC.JSValue pr;
              if(_manifest == null || _manifest.ValueType != JSC.JSValueType.Object || _manifest.Value == null || (pr = _manifest["Fields"] as JSC.JSValue).ValueType != JSC.JSValueType.Object || pr.Value == null || (cs = pr[kv.Key]).ValueType != JSC.JSValueType.Object || cs.Value == null) {
                cs = null;
              }
            }
            var ni = new InManifest(this, kv.Key, kv.Value, cs);
            _items.Insert(i + 1, ni);
            if(_isVisible && _isExpanded) {
              _collFunc(ni, true);
            }
          }
        }
        var keys = _value.Select(z => z.Key).ToArray();
        for(i = _items.Count - 1; i >= 0; i--) {
          if(!keys.Contains(_items[i].name)) {
            if(_isVisible && _isExpanded) {
              _items[i].Deleted();
            }
            _items.RemoveAt(i);
          }
        }
      }
      if(editor == null) {
        editor = InspectorForm.GetEditor(_editorName, this, _manifest);
        PropertyChangedReise("editor");
      } else {
        editor.ValueChanged(_value);
      }
    }
    private void _data_PropertyChanged(DTopic.Art art, DTopic child) {
      if(art == DTopic.Art.type) {
        _value = _data.type;
        UpdateType(_tManifest != null ? _tManifest.value : null);
        UpdateData(_data.type);
      }
    }
    private void Manifest_changed(DTopic.Art art, DTopic src) {
      if(art == DTopic.Art.value) {
        UpdateType(_tManifest != null ? _tManifest.value : null);
      }
    }
    private void SetFieldResp(Task<JSC.JSValue> r) {
      if(r.IsCompleted) {
        if(r.IsFaulted) {
          UpdateData(value);
          Log.Warning("{0}.{1} - {2}", _data.fullPath, _path, r.Exception.InnerException);
        }
      }
    }

    #region InBase Members
    public override void FinishNameEdit(string name) {
      if(_data == null) {
        if(!string.IsNullOrEmpty(name)) {
          var def = _manifest["default"];
          _parent._data.SetField(_parent.IsGroupHeader ? name : (_parent._path + "." + name), def.Defined ? def : JSC.JSValue.Null);
        }
        _parent._items.Remove(this);
        _collFunc(this, false);
      } else {
        IsEdited = false;
        PropertyChangedReise("IsEdited");
        throw new NotImplementedException("InValue.Move");
      }
    }
    protected override void UpdateType(JSC.JSValue type) {
      base.UpdateType(type);
      if(_manifest != null && _manifest.ValueType == JSC.JSValueType.Object && _manifest.Value != null) {
        var pr = _manifest["Fields"] as JSC.JSValue;
        if(pr != null) {
          InManifest vc;
          foreach(var kv in pr) {
            vc = _items.OfType<InManifest>().FirstOrDefault(z => z.name == kv.Key);
            if(vc != null) {
              vc.UpdateType(kv.Value);
            }
          }
        }
      }
    }
    public override bool HasChildren { get { return _items.Any(); } }
    public override JSC.JSValue value {
      get {
        return _value;
      }
      set {
        JSL.Date js_d;
        if(value != null && value.ValueType == JSC.JSValueType.Date && (js_d = value.Value as JSL.Date) != null && Math.Abs((js_d.ToDateTime() - new DateTime(1001, 1, 1, 12, 0, 0)).TotalDays) < 1) {
          value = JSC.JSObject.Marshal(DateTime.UtcNow);
        }
        _data.SetField(_path, value).ContinueWith(SetFieldResp, TaskScheduler.FromCurrentSynchronizationContext());
      }
    }
    public override int CompareTo(InBase other) {
      var o = other as InManifest;
      if(o == null) {
        return (other is InValue)?1:-1;
      }
      return this._path.CompareTo(o._path);
    }
    #endregion InBase Members

    #region ContextMenu
    public override List<Control> MenuItems(FrameworkElement src) {
      var l = new List<Control>();
      JSC.JSValue v1, v2;
      MenuItem mi;
      if(!base.IsReadonly && _value.ValueType == JSC.JSValueType.Object) {
        MenuItem ma = new MenuItem() { Header = "Add" };
        if(_manifest != null && (v1 = _manifest["Fields"]).ValueType == JSC.JSValueType.Object) {
          foreach(var kv in v1.Where(z => z.Value != null && z.Value.ValueType == JSC.JSValueType.Object && z.Value["default"].Defined)) {
            if(_items.Any(z => z.name == kv.Key)) {
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
          if(_data.Connection.TypeManifest != null) {
            foreach(var t in _data.Connection.TypeManifest.parent.children) {
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
      }
      mi = new MenuItem() { Header = "Delete", Icon = new Image() { Source = App.GetIcon("component/Images/Edit_Delete.png"), Width = 16, Height = 16 } };
      mi.IsEnabled = _parent != null && !IsRequired;
      mi.Click += miDelete_Click;
      l.Add(mi);
      return l;
    }

    private void miAdd_Click(object sender, RoutedEventArgs e) {
      var mi = sender as MenuItem;
      JSC.JSValue decl;
      if(!IsReadonly && mi != null && (decl = mi.Tag as JSC.JSValue) != null) {
        if(!IsExpanded) {
          IsExpanded = true;
          base.PropertyChangedReise("IsExpanded");
        }
        bool pc_items = false;
        if((bool)decl["willful"]) {
          if(_items == null) {
            lock(this) {
              if(_items == null) {
                _items = new List<InBase>();
                pc_items = true;
              }
            }
          }
          var ni = new InManifest(decl, this);
          _items.Insert(0, ni);
          _collFunc(ni, true);
        } else {
          if(decl != null) {
            string fName = mi.Header as string;
            _data.SetField(IsGroupHeader ? fName : _path + "." + fName, decl["default"]);
          }
        }
        if(pc_items) {
          PropertyChangedReise("items");
        }
      }
    }
    private void miDelete_Click(object sender, RoutedEventArgs e) {
      if(!IsRequired && _parent != null) {
        _data.SetField(_path, null);
      }
    }
    #endregion ContextMenu

    #region IDisposable Member
    public void Dispose() {
      if(_parent == null) {
        _data.changed -= _data_PropertyChanged;
      }
    }
    #endregion IDisposable Member

    public override string ToString() {
      return (_data!=null?_data.fullPath:"<new>") + "." + _path;
    }
  }
}
