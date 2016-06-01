///<remarks>This file is part of the <see cref="https://github.com/X13home">X13.Home</see> project.<remarks>
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using JSL = NiL.JS.BaseLibrary;
using JSC = NiL.JS.Core;
using System.Collections.ObjectModel;
using X13.Data;
using System.Windows.Controls;
using System.Windows;
using System.Windows.Input;

namespace X13.UI {
  public class InValue : InBase, IDisposable {
    private DTopic _data;
    private InValue _parent;
    private JSC.JSValue _value;
    private ObservableCollection<InValue> _items;

    public InValue(DTopic data) {
      _data = data;
      _parent = null;
      name = "value";
      IsExpanded = true;
      _items = new ObservableCollection<InValue>();
      _value = _data.value;
      UpdateSchema(_data.schema);
      UpdateData(_data.value);
      _data.changed += _data_PropertyChanged;
    }

    private InValue(DTopic data, InValue parent, string name, JSC.JSValue value, JSC.JSValue schema) {
      _data = data;
      _parent = parent;
      base.name = name;
      _items = new ObservableCollection<InValue>();
      IsExpanded = false;
      _value = value;
      UpdateSchema(schema);
      UpdateData(value);
    }

    public override NiL.JS.Core.JSValue value {
      get {
        return _value;
      }
      set {
        if(_parent == null) {
          _data.SetValue(value);
        } else {
          _parent.ChangeValue(name, value);
        }
      }
    }
    public ObservableCollection<InValue> items { get { return _items; } }
    protected override void UpdateSchema(JSC.JSValue schema) {
      base.UpdateSchema(schema);
      if(_schema != null) {
        var pr = _schema["Properties"] as JSC.JSValue;
        if(pr != null) {
          InValue vc;
          foreach(var kv in pr) {
            vc = _items.FirstOrDefault(z => z.name == kv.Key);
            if(vc != null) {
              vc.UpdateSchema(kv.Value);
            }
          }
        }
      }
      bool gh = _parent == null && editor is veDefault;
      if(gh != IsGroupHeader) {
        IsGroupHeader = gh;
        PropertyChangedReise("IsGroupHeader");
      }
    }
    private void UpdateData(JSC.JSValue val) {
      _value = val;
      if(_value.ValueType == JSC.JSValueType.Object) {
        InValue vc;
        int i;
        foreach(var kv in _value.OrderBy(z => z.Key)) {
          vc = _items.FirstOrDefault(z => z.name == kv.Key);
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
              if(_schema == null || (pr = _schema["Properties"] as JSC.JSValue).ValueType != JSC.JSValueType.Object || (cs = pr[kv.Key]).ValueType != JSC.JSValueType.Object) {
                cs = null;
              }
            }
            _items.Insert(i + 1, new InValue(_data, this, kv.Key, kv.Value, cs));
          }
        }
        var keys = _value.Select(z => z.Key).ToArray();
        for(i = _items.Count - 1; i >= 0; i--) {
          if(!keys.Contains(_items[i].name)) {
            _items.RemoveAt(i);
          }
        }
      }
      if(editor == null) {
        editor = InspectorForm.GetEdititor(_view, this, _schema);
        PropertyChangedReise("editor");
      } else {
        editor.ValueChanged(_value);
      }
    }

    private void ChangeValue(string name, JSC.JSValue val) {
      if(_value.ValueType == JSC.JSValueType.Object) {
        var jo = JSC.JSObject.CreateObject();
        foreach(var kv in _value.OrderBy(z => z.Key)) {
          if(kv.Key == name) {
            if(val != null) {
              jo[kv.Key] = val;
            } else {
              jo.DeleteProperty(kv.Key);
            }
          } else {
            jo[kv.Key] = kv.Value;
          }
        }
        if(val != null && !jo.GetProperty(name, JSC.PropertyScope.Own).Defined) {
          jo[name] = val;
        }
        if(_parent == null) {
          _data.SetValue(jo);
        } else {
          _parent.ChangeValue(name, jo);
        }
      } else {
        throw new NotImplementedException();
      }
    }
    private void _data_PropertyChanged(DTopic.Art art, DTopic child) {
      if(art==DTopic.Art.schema) {
        UpdateSchema(_data.schema);
      } else if(art==DTopic.Art.value) {
        _value = _data.value;
        UpdateSchema(_data.schema);
        UpdateData(_data.value);
      }
    }

    #region ContextMenu
    public override List<Control> MenuItems(System.Windows.FrameworkElement src) {
      var l = new List<Control>();
      JSC.JSValue f;
      MenuItem mi;
      if(_schema != null && (f = _schema["Properties"]).ValueType == JSC.JSValueType.Object) {
        MenuItem ma = new MenuItem() { Header = "Add" };
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
        if(ma.HasItems) {
          l.Add(ma);
        }
      }
      mi = new MenuItem() { Command = ApplicationCommands.Delete, CommandTarget = src, Icon = new Image() { Source = App.GetIcon("component/Images/Edit_Delete.png"), Width = 16, Height = 16 } };
      l.Add(mi);
      return l;
    }

    private void miAdd_Click(object sender, RoutedEventArgs e) {
      var mi = sender as MenuItem;
      if(mi != null) {
        var name = mi.Header as string;
        var decl = mi.Tag as JSC.JSValue;
        if(name != null && decl != null) {
          this.ChangeValue(name, decl["default"]);
        }
      }
    }

    public override bool CanExecute(System.Windows.Input.ICommand cmd, object p) {
      JSC.JSValue f;
      if(cmd == ApplicationCommands.Delete) {
        return _parent != null && (_schema == null || (f = _schema["required"]).ValueType != JSC.JSValueType.Boolean || true != (bool)f);
      }
      return false;
    }
    public override void CmdExecuted(ICommand cmd, object p) {
      if(cmd == ApplicationCommands.Delete) {
        if(_parent != null) {
          _parent.ChangeValue(name, null);
        }
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
  }

}
