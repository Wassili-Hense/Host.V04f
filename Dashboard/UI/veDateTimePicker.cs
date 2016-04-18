using JSC = NiL.JS.Core;
using JSL = NiL.JS.BaseLibrary;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace X13.UI {
  class veDateTimePicker : Xceed.Wpf.Toolkit.DateTimePicker, IValueEditor {
    public static IValueEditor Create(ValueControl owner, JSC.JSValue schema) {
      return new veDateTimePicker(owner, schema);
    }

    private ValueControl _owner;
    private DateTime _oldValue;
    public veDateTimePicker(ValueControl owner, JSC.JSValue schema) {
      _owner = owner;
      base.TabIndex = 5;
      base.HorizontalAlignment = System.Windows.HorizontalAlignment.Left;
      base.Padding = new System.Windows.Thickness(10, 0, 10, 0);
      base.BorderThickness = new System.Windows.Thickness(1, 0, 0, 0);
      base.BorderBrush = System.Windows.Media.Brushes.Black;
      base.Background = System.Windows.Media.Brushes.Azure;
      base.GotFocus += ve_GotFocus;
      base.LostFocus += ve_LostFocus;
      base.KeyUp += ve_KeyUp;
      ValueChanged(_owner.valueRaw);
      SchemaChanged(schema);
    }
    public new void ValueChanged(JSC.JSValue value) {
      if(value.ValueType == JSC.JSValueType.Date) {
        _oldValue = (value.Value as JSL.Date).ToDateTime();
        base.Value = _oldValue;
      } else {
        base.Value = null;
      }
    }

    public void SchemaChanged(JSC.JSValue schema) {
    }

    private void Publish() {
      if(base.Value.HasValue) {
        if(_oldValue != base.Value.Value) {
          _owner.valueRaw = JSC.JSValue.Marshal(base.Value.Value);
        }
      } else {
        _owner.valueRaw = JSC.JSValue.Undefined;
      }
    }
    private void ve_KeyUp(object sender, System.Windows.Input.KeyEventArgs e) {
      if(e.Key == System.Windows.Input.Key.Enter) {
        e.Handled = true;
        Publish();
      } else if(e.Key == System.Windows.Input.Key.Escape) {
        base.Value = _oldValue;
        base.MoveFocus(new System.Windows.Input.TraversalRequest(System.Windows.Input.FocusNavigationDirection.Up));
      } else if(e.Key == System.Windows.Input.Key.PageDown) {
        e.Handled = true;
        if(!base.MoveFocus(new System.Windows.Input.TraversalRequest(System.Windows.Input.FocusNavigationDirection.Down))) {
          base.MoveFocus(new System.Windows.Input.TraversalRequest(System.Windows.Input.FocusNavigationDirection.Next));
        }
      } else if(e.Key == System.Windows.Input.Key.PageUp) {
        e.Handled = true;
        if(!base.MoveFocus(new System.Windows.Input.TraversalRequest(System.Windows.Input.FocusNavigationDirection.Up))) {
          base.MoveFocus(new System.Windows.Input.TraversalRequest(System.Windows.Input.FocusNavigationDirection.Previous));
        }
      }
    }
    private void ve_GotFocus(object sender, System.Windows.RoutedEventArgs e) {
      _owner.GotFocus(sender, e);
    }
    private void ve_LostFocus(object sender, System.Windows.RoutedEventArgs e) {
      Publish();
    }
  }
}
