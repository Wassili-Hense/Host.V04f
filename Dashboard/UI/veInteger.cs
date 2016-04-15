using JSC = NiL.JS.Core;
using JSL = NiL.JS.BaseLibrary;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace X13.UI {
  internal class veInteger: Xceed.Wpf.Toolkit.LongUpDown, IValueEditor {
    public static IValueEditor Create(ValueControl owner, JSC.JSValue schema) {
      return new veInteger(owner, schema);
    }

    private ValueControl _owner;
    private long _oldValue;

    public veInteger(ValueControl owner, JSC.JSValue schema) {
      _owner = owner;
      base.TabIndex = 5;
      base.HorizontalAlignment = System.Windows.HorizontalAlignment.Left;
      base.Padding = new System.Windows.Thickness(10, 0, 10, 0);
      base.BorderThickness = new System.Windows.Thickness(1, 0, 0 ,0);
      base.BorderBrush = System.Windows.Media.Brushes.Black;
      base.GotFocus += ve_GotFocus;
      base.LostFocus += ve_LostFocus;
      base.KeyUp += ve_KeyUp;
      base.Background = System.Windows.Media.Brushes.Azure;
      ValueChanged(_owner.valueRaw);
      SchemaChanged(schema);
    }
    public new void ValueChanged(JSC.JSValue value) {
      try {
        _oldValue = (long)value;
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
