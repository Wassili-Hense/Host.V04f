///<remarks>This file is part of the <see cref="https://github.com/X13home">X13.Home</see> project.<remarks>
using JSC = NiL.JS.Core;
using JSL = NiL.JS.BaseLibrary;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;

namespace X13.UI {
  class veDateTimePicker : Xceed.Wpf.Toolkit.DateTimePicker, IValueEditor {
    public static IValueEditor Create(InBase owner, JSC.JSValue type) {
      return new veDateTimePicker(owner, type);
    }

    private InBase _owner;
    private DateTime _oldValue;
    public veDateTimePicker(InBase owner, JSC.JSValue type) {
      _owner = owner;
      base.TabIndex = 5;
      base.HorizontalAlignment = System.Windows.HorizontalAlignment.Left;
      base.Padding = new System.Windows.Thickness(10, 0, 10, 0);
      base.BorderThickness = new System.Windows.Thickness(1,0,1,0);
      base.BorderBrush = Brushes.Black;
      base.Background = Brushes.Azure;
      base.GotFocus += ve_GotFocus;
      base.LostFocus += ve_LostFocus;
      base.KeyUp += ve_KeyUp;
      ValueChanged(_owner.value);
      TypeChanged(type);
    }
    public new void ValueChanged(JSC.JSValue value) {
      if(value.ValueType == JSC.JSValueType.Date) {
        _oldValue = (value.Value as JSL.Date).ToDateTime();
        base.Value = _oldValue;
      } else {
        base.Value = null;
      }
    }

    public void TypeChanged(JSC.JSValue type) {
    }

    private void Publish() {
      if(base.Value.HasValue) {
        if(_oldValue != base.Value.Value) {
          _owner.value = JSC.JSValue.Marshal(base.Value.Value);
        }
      } else {
        _owner.value = JSC.JSValue.Undefined;
      }
    }
    private void ve_KeyUp(object sender, System.Windows.Input.KeyEventArgs e) {
      if(e.Key == System.Windows.Input.Key.Enter) {
        e.Handled = true;
        Publish();
      } else if(e.Key == System.Windows.Input.Key.Escape) {
        base.Value = _oldValue;
        base.MoveFocus(new System.Windows.Input.TraversalRequest(System.Windows.Input.FocusNavigationDirection.Previous));
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
