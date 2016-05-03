using JSC = NiL.JS.Core;
using JSL = NiL.JS.BaseLibrary;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace X13.UI {
  class veDouble : Xceed.Wpf.Toolkit.DoubleUpDown, IValueEditor {
    public static IValueEditor Create(ValueControl owner, JSC.JSValue schema) {
      return new veDouble(owner, schema);
    }

    private ValueControl _owner;
    private double _oldValue;

    public veDouble(ValueControl owner, JSC.JSValue schema) {
      _owner = owner;
      base.TabIndex = 5;
      base.HorizontalAlignment = System.Windows.HorizontalAlignment.Left;
      base.Padding = new System.Windows.Thickness(10, 0, 10, 0);
      base.BorderThickness = new System.Windows.Thickness(0);
      base.Background = System.Windows.Media.Brushes.Azure;
      base.GotFocus += ve_GotFocus;
      base.LostFocus += ve_LostFocus;
      base.KeyUp += ve_KeyUp;
      ValueChanged(_owner.valueRaw);
      SchemaChanged(schema);
    }
    public new void ValueChanged(JSC.JSValue value) {
      try {
        _oldValue = (double)value;
        base.Value = _oldValue;
      }
      catch(Exception) {
        base.Value = null;
      }
    }

    public void SchemaChanged(JSC.JSValue schema) {
    }
    protected override void OnDecrement() {
      base.OnDecrement();
      Publish();
    }
    protected override void OnIncrement() {
      base.OnIncrement();
      Publish();
    }
    private void Publish() {
      if(base.Value.HasValue) {
        if(_oldValue != base.Value.Value) {
          _owner.valueRaw = new JSL.Number(base.Value.Value);
        }
      } else {
        _owner.valueRaw = JSC.JSValue.Null;
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
