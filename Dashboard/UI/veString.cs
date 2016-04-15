using JSC = NiL.JS.Core;
using JSL = NiL.JS.BaseLibrary;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;

namespace X13.UI {
  class veString : TextBox, IValueEditor {
    public static IValueEditor Create(ValueControl owner, JSC.JSValue schema) {
      return new veString(owner, schema);
    }

    private ValueControl _owner;
    private string _oldValue;

    public veString(ValueControl owner, JSC.JSValue schema) {
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
    public void ValueChanged(JSC.JSValue value) {
      if(value.ValueType == JSC.JSValueType.String) {
        _oldValue = value.Value as string;
      } else {
        _oldValue = value.ToString();
      }
      base.Text = _oldValue;
    }

    public void SchemaChanged(JSC.JSValue schema) {
    }

    private void Publish() {
        if(_oldValue != base.Text) {
          _owner.valueRaw = new JSL.String(base.Text);
        }
    }
    private void ve_KeyUp(object sender, System.Windows.Input.KeyEventArgs e) {
      if(e.Key == System.Windows.Input.Key.Enter) {
        e.Handled = true;
        Publish();
      } else if(e.Key == System.Windows.Input.Key.Escape) {
        base.Text = _oldValue;
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
