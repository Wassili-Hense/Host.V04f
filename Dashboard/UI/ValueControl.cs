using JSL = NiL.JS.BaseLibrary;
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

namespace X13.UI {
  public class ValueControl : INotifyPropertyChanged {
    private InspectorForm _src;
    private ValueControl _parent;
    private string _name;
    private JSC.JSValue _value;
    private ObservableCollection<ValueControl> _fields;

    public ValueControl(InspectorForm src, ValueControl parent, string name, JSC.JSValue value) {
      _src = src;
      _parent = parent;
      _name = name;
      _fields = new ObservableCollection<ValueControl>();
      UpdateData(value);
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
        var keys=_value.Select(z=>z.Key).ToArray();
        for(i = _fields.Count - 1; i>=0 ; i--) {
          if(!keys.Contains(_fields[i]._name)) {
            _fields.RemoveAt(i);
          }
        }
      }
      PropertyChangedReise("value");
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

    public string view { get { return _value.ValueType.ToString(); } }
    public bool IsExpanded { get; set; }
    public string name { get { return _name ?? "value"; } }
    public ObservableCollection<ValueControl> fields { get { return _fields; } }

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
  public class ValueViewTS : DataTemplateSelector {
    public DataTemplate Default { get; set; }
    public DataTemplate Bool { get; set; }
    //public DataTemplate Integer { get; set; }
    public DataTemplate Double { get; set; }
    public DataTemplate String { get; set; }
    public DataTemplate Date { get; set; }

    public override DataTemplate SelectTemplate(object item, DependencyObject container) {
      var cp = container as ContentPresenter;
      if(cp != null) {
        var vc = cp.Content as ValueControl;
        if(cp != null) {
          switch(vc.view) {
          case "Boolean":
            return Bool;
          case "Double":
            return Double;
          case "String":
            return this.String;
          case "Date":
            return this.Date;
          }
        }
      }
      return Default;
    }
  }
}
