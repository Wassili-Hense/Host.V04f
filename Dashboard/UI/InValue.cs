using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using JSL = NiL.JS.BaseLibrary;
using JSC = NiL.JS.Core;
using System.Collections.ObjectModel;
using X13.Data;

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
    }
    public void UpdateData(JSC.JSValue val) {
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
    private void _data_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e) {

    }

    #region IDisposable Member
    public void Dispose() {
      if(_parent == null) {
        _data.PropertyChanged -= _data_PropertyChanged;
      }
    }
    #endregion IDisposable Member
  }

}
