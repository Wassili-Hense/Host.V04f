using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using JSC = NiL.JS.Core;
using JSL = NiL.JS.BaseLibrary;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace X13.UI {
  internal class veDefault : TextBlock, IValueEditor {
    public veDefault(ValueControl owner, JSC.JSValue schema) {
      base.HorizontalAlignment = System.Windows.HorizontalAlignment.Left;
      base.Padding = new System.Windows.Thickness(10, 0, 10, 0);
      ValueChanged(owner.valueRaw);
    }

    public void ValueChanged(JSC.JSValue value) {
      string rez=null;
      if(value == null) {
        rez = "null";
      } else {
        if(value.ValueType == JSC.JSValueType.Object) {
          if(value.Value == null) {
            rez = "null";
          } else {
            var sc = value["$schema"];
            if((rez = sc.Value as string) == null) {
              rez = "Object";
            }
          }
        } else {
          rez = value.ToString();
        }
      }
      this.Text = rez;
    }

    public void SchemaChanged(JSC.JSValue schema) {
    }
  }
}
