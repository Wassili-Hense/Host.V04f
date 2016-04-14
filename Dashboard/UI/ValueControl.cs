﻿using JSL = NiL.JS.BaseLibrary;
using JSC = NiL.JS.Core;
using JSF = NiL.JS.Core.Functions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Controls;
using System.Windows;
using System.Windows.Media.Imaging;
using X13.Data;

namespace X13.UI {
  public class ValueControl : INotifyPropertyChanged {
    private InspectorForm _src;
    private ValueControl _parent;
    private string _name;
    private JSC.JSValue _value;
    private JSC.JSValue _schema;
    private ObservableCollection<ValueControl> _fields;
    private string _view;
    private BitmapSource _icon;

    public ValueControl(InspectorForm src, ValueControl parent, string name, JSC.JSValue value) {
      _src = src;
      _parent = parent;
      _name = name;
      _fields = new ObservableCollection<ValueControl>();
      UpdateData(value);
      if(parent == null) {
        if(src != null && src.data != null && src.data.schema != null && src.data.schema.data != null) {
          UpdateSchema(src.data.schema.data);
        } else {
          UpdateSchema(null);
        }
      } else {
        UpdateSchema(null);
      }
      IsExpanded = true;
    }
    public void UpdateData(JSC.JSValue val) {
      _value = val;
      if(_value.ValueType == JSC.JSValueType.Object) {
        ValueControl vc;
        int i;
        foreach(var kv in _value.OrderBy(z => z.Key)) {
          vc = _fields.FirstOrDefault(z => z._name == kv.Key);
          if(vc != null) {
            vc.UpdateData(kv.Value);
          } else {
            for(i = _fields.Count - 1; i >= 0; i--) {
              if(string.Compare(_fields[i]._name, kv.Key) < 0) {
                break;
              }
            }
            _fields.Insert(i + 1, new ValueControl(_src, this, kv.Key, kv.Value));
          }
        }
        var keys = _value.Select(z => z.Key).ToArray();
        for(i = _fields.Count - 1; i >= 0; i--) {
          if(!keys.Contains(_fields[i]._name)) {
            _fields.RemoveAt(i);
          }
        }
      }
      if(editor == null) {
        editor = InspectorForm.GetEdititor(this.view, this, _schema);
        PropertyChangedReise("editor");
      } else {
        editor.ValueChanged(_value);
      }
      //PropertyChangedReise("value");

    }
    public void UpdateSchema(JSC.JSValue val) {
      var oldView = this.view;
      this._schema = val;
      if(_schema != null && _schema.Value != null) {
        var vv = _schema["view"];
        if(vv.ValueType == JSC.JSValueType.String) {
          _view = vv.Value as string;
          PropertyChangedReise("view");
        }
        var iv = _schema["icon"];
        if(iv.ValueType == JSC.JSValueType.String) {
          _icon = DWorkspace.This.GetIcon(iv.Value as string);
        } else {
          _icon = null;
        }
        var pr = _schema["Properties"] as JSC.JSValue;
        if(pr != null) {
          ValueControl vc;
          foreach(var kv in pr) {
            vc = _fields.FirstOrDefault(z => z._name == kv.Key);
            if(vc != null) {
              vc.UpdateSchema(kv.Value);
            }
          }
        }
      } else {
        _icon = null;
        if(_view != null) {
          _view = null;
          PropertyChangedReise("view");
        }
      }
      if(_icon == null) {
        _icon = DWorkspace.This.GetIcon(view);
      }
      if(_icon == null) {
        _icon = DWorkspace.This.GetIcon(null);
      }
      PropertyChangedReise("icon");
      if(editor == null || oldView!=this.view) {
        editor = InspectorForm.GetEdititor(this.view, this, _schema);
        PropertyChangedReise("editor");
      }
    }

    public bool IsExpanded { get; set; }
    public string name { get { return _name ?? "value"; } }
    public string view {
      get {
        var v = _view ?? _value.ValueType.ToString();
        return v;
      }
    }
    public BitmapSource icon {
      get {
        return _icon;
      }
    }
    public IValueEditor editor { get; private set; }

    public JSC.JSValue valueRaw { 
      get { return _value; }
      set {
        if(_parent == null) {
          _src.DataChanged(value);
        } else {
          _parent.ChangeValue(_name, value);
        }
      }
    }
    public string valueStr { get { return (_value == null || _value.Value == null) ? "null" : (_value.Value.ToString()); } }
    public object value {
      get {
        if(_value.ValueType == JSC.JSValueType.Date) {
          return (_value.Value as JSL.Date).ToDateTime();
        }
        return _value.Value;
      }
      set {
        if(!object.Equals(value, _value)) {
          if(_parent == null) {
            _src.DataChanged(JSC.JSValue.Marshal(value));
          } else {
            _parent.ChangeValue(_name, JSC.JSValue.Marshal(value));
          }
        }
      }
    }
    public ObservableCollection<ValueControl> fields { get { return _fields; } }

    public override string ToString() {
      return _src.data.path + "." + name;
    }
    private void ChangeValue(string name, JSC.JSValue val) {
      if(_value.ValueType == JSC.JSValueType.Object) {
        var jo = JSC.JSObject.CreateObject();
        foreach(var kv in _value.OrderBy(z => z.Key)) {
          jo[kv.Key] = kv.Key == name ? val : kv.Value;
        }
        if(_parent == null) {
          _src.DataChanged(jo);
        } else {
          _parent.ChangeValue(_name, jo);
        }
      } else {
        throw new NotImplementedException();
      }
    }

    #region INotifyPropertyChanged Members
    public event PropertyChangedEventHandler PropertyChanged;
    private void PropertyChangedReise(string propertyName) {
      if(PropertyChanged != null) {
        PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
      }
    }
    #endregion INotifyPropertyChanged Members
  }
}
