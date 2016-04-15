using JSC = NiL.JS.Core;
using JSL = NiL.JS.BaseLibrary;
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

namespace X13.UI {
  /// <summary>
  /// Interaction logic for veSliderBool.xaml
  /// </summary>
  public partial class veSliderBool : UserControl, IValueEditor {
    public static IValueEditor Create(ValueControl owner, JSC.JSValue schema) {
      return new veSliderBool(owner, schema);
    }

    private ValueControl _owner;
    public veSliderBool(ValueControl owner, JSC.JSValue schema) {
      _owner = owner;
      InitializeComponent();
      ValueChanged(_owner.valueRaw);
      cbBool.Checked+=cbBool_Checked;
      cbBool.Unchecked+=cbBool_Unchecked;
    }

    public void ValueChanged(NiL.JS.Core.JSValue value) {
      this.cbBool.IsChecked= value.ValueType == JSC.JSValueType.Boolean && (bool)value;
    }

    public void SchemaChanged(NiL.JS.Core.JSValue schema) {
    }

    private void cbBool_Checked(object sender, RoutedEventArgs e) {
      _owner.valueRaw = new JSL.Boolean(true);
    }

    private void cbBool_Unchecked(object sender, RoutedEventArgs e) {
      _owner.valueRaw = new JSL.Boolean(false);
    }

    private void UserControl_GotFocus(object sender, RoutedEventArgs e) {
      _owner.GotFocus(sender, e);
    }
  }
}
