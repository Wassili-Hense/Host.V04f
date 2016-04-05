using JSL = NiL.JS.BaseLibrary;
using JSC = NiL.JS.Core;
using JSF = NiL.JS.Core.Functions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Collections.ObjectModel;
using System.ComponentModel;

namespace X13.UI {
  public partial class ValueControl : UserControl, INotifyPropertyChanged {
    private InspectorForm _src;
    private ValueControl _parent;
    private string _name;
    private JSC.JSValue _value;
    private ObservableCollection<ValueControl> _fields;

    public ValueControl(InspectorForm src, ValueControl parent, string name, JSC.JSValue value) {
      _src = src;
      _parent = parent;
      _name = name;
      _value = value;
      if(_value.ValueType == JSC.JSValueType.Object) {
        _fields = new ObservableCollection<ValueControl>();
        foreach(var kv in _value.OrderBy(z => z.Key)) {
          _fields.Add(new ValueControl(_src, this, kv.Key, kv.Value));
        }
      } else {
        _fields = null;
      }
      valueStr = (_value.Value == null) ? "null" : (_value.Value.ToString());

      InitializeComponent();
      this.DataContext = this;
      //PropertyChangedReise("valueStr");
    }
    public void UpdateData(JSC.JSValue val) {
      _value = val;
      PropertyChangedReise("value");
    }

    public string valueStr { get; private set; }
    public object value {
      get { return _value.Value; }
      set {
        if(_parent == null) {
          _src.SetData(value);
        } else {
          _parent._value[_name] = JSC.JSValue.Marshal(value);
        }
      }
    }
    public string view { get { return _value.ValueType.ToString(); } }

    public string name { get { return _name ?? "value"; } }
    public ObservableCollection<ValueControl> fields { get { return _fields; } }

    public event PropertyChangedEventHandler PropertyChanged;

    private void PropertyChangedReise(string propertyName) {
      if(PropertyChanged != null) {
        PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
      }
    }


  }

  public class ValueViewTS : DataTemplateSelector {
    public DataTemplate Default { get; set; }
    public DataTemplate Bool { get; set; }

    public override DataTemplate SelectTemplate(object item, DependencyObject container) {
      var cp = container as ContentPresenter;
      if(cp != null) {
        var vc = cp.Content as ValueControl;
        if(cp != null) {
          switch(vc.view) {
          case "Boolean":
            return Bool;
          }
        }
      }
      return Default;
    }
  }
}
