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
      UpdateType(_data.Connection.TypeManifest.value);
      UpdateData(_data.type);
      base._isExpanded = this.HasChildren;
      _data.changed += _data_PropertyChanged;
      _data.Connection.TypeManifest.changed+=Manifest_changed;
    }
    private InManifest(InManifest parent, string name, JSC.JSValue value, JSC.JSValue type) {
      this._parent = parent;
      this._data = _parent._data;
      base._collFunc = _parent._collFunc;
      this._path = string.IsNullOrEmpty(_parent._path)?name:(_parent._path + "." + name);
      base.name = name;
      base._items = new List<InBase>();
      base._isVisible = true;
      base._isExpanded = true;
      base.IsGroupHeader = false;
      levelPadding = _parent.levelPadding + 5;
      this._value = value;
      UpdateType(type);
      UpdateData(value);
      _isExpanded = this.HasChildren;
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
        editor = InspectorForm.GetEdititor(_editorName, this, _manifest);
        PropertyChangedReise("editor");
      } else {
        editor.ValueChanged(_value);
      }
    }
    private void _data_PropertyChanged(DTopic.Art art, DTopic child) {
      if(art == DTopic.Art.type) {
        _value = _data.type;
        UpdateType(_data.Connection.TypeManifest.value);
        UpdateData(_data.type);
      }
    }
    private void Manifest_changed(DTopic.Art art, DTopic src) {
      if(art == DTopic.Art.value) {
        UpdateType(_data.Connection.TypeManifest.value);
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
        _data.SetField(_path, value).ContinueWith(SetFieldResp, TaskScheduler.FromCurrentSynchronizationContext());
      }
    }
    public override List<Control> MenuItems(FrameworkElement src) {
      var l = new List<Control>();
      JSC.JSValue f;
      MenuItem mi;
      if(!base.IsReadonly && ( !_value.Defined || _value.ValueType==JSC.JSValueType.Object )) {
        MenuItem ma = new MenuItem() { Header = "Add" };
        if(_manifest != null && ( f = _manifest["Fields"] ).ValueType == JSC.JSValueType.Object) {
          foreach(var kv in f.Where(z => z.Value != null && z.Value.ValueType == JSC.JSValueType.Object)) {
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
        }
        if(ma.HasItems) {
          l.Add(ma);
        }
      }
      mi = new MenuItem() { Header = "Delete", Icon = new Image() { Source = App.GetIcon("component/Images/Edit_Delete.png"), Width = 16, Height = 16 } };
      mi.IsEnabled = _parent != null && !IsRequired;
      mi.Click += miDelete_Click;
      l.Add(mi);
      return l;
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
    private void miAdd_Click(object sender, RoutedEventArgs e) {
      var mi = sender as MenuItem;
      if(!IsReadonly && mi != null) {
        var name = mi.Header as string;
        var decl = mi.Tag as JSC.JSValue;
        if(name != null && decl != null) {
          var def=decl["default"];
          _data.SetField(IsGroupHeader?name:_path+"."+name, def.Defined?def:JSC.JSValue.Null);
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
      return _data.fullPath + "." + _path;
    }
  }
}
