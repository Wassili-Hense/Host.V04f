///<remarks>This file is part of the <see cref="https://github.com/X13home">X13.Home</see> project.<remarks>
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
    public static IValueEditor Create(InBase owner, JSC.JSValue schema) {
      return new veString(owner, schema);
    }

    private InBase _owner;
    private string _oldValue;

    public veString(InBase owner, JSC.JSValue schema) {
      _owner = owner;
      base.HorizontalAlignment = System.Windows.HorizontalAlignment.Left;
      base.Padding = new System.Windows.Thickness(10, 0, 10, 0);
      base.BorderThickness = new System.Windows.Thickness(0);
      base.Background = System.Windows.Media.Brushes.Azure;
      base.GotFocus += ve_GotFocus;
      base.LostFocus += ve_LostFocus;
      base.KeyUp += ve_KeyUp;
      ValueChanged(_owner.value);
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
        _owner.value = new JSL.String(base.Text);
      }
    }

    private void ve_KeyUp(object sender, System.Windows.Input.KeyEventArgs e) {
      if(e.Key == System.Windows.Input.Key.Enter) {
        e.Handled = true;
        Publish();
      } else if(e.Key == System.Windows.Input.Key.Escape) {
        e.Handled = true;
        base.MoveFocus(new System.Windows.Input.TraversalRequest(System.Windows.Input.FocusNavigationDirection.Up));
        base.Text = _oldValue;
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
