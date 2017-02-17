///<remarks>This file is part of the <see cref="https://github.com/X13home">X13.Home</see> project.<remarks>
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
  /// Interaction logic for veAttribute.xaml
  /// </summary>
  public partial class veAttribute : UserControl, IValueEditor {
    public static IValueEditor Create(InBase owner, JSC.JSValue type) {
      return new veAttribute(owner, type);
    }

    private InBase _owner;

    public veAttribute(InBase owner, JSC.JSValue type) {
      _owner = owner;
      InitializeComponent();
      ValueChanged(_owner.value);
    }

    public void ValueChanged(NiL.JS.Core.JSValue value) {
      if(value == null || !value.IsNumber) {
        tbSaved.IsChecked = false;
        tbReadonly.IsChecked = false;
        tbRequired.IsChecked = false;
      } else {
        int a = (int)value;
        tbSaved.IsChecked = (a & 4) != 0;
        tbReadonly.IsChecked = (a & 2) != 0;
        tbRequired.IsChecked = (a & 1) != 0;
      }
    }
    public void TypeChanged(NiL.JS.Core.JSValue type) {
      tbSaved.IsEnabled = !_owner.IsReadonly;
      tbReadonly.IsEnabled = !_owner.IsReadonly;
      tbRequired.IsEnabled = !_owner.IsReadonly;
    }
    private void tbChanged(object sender, RoutedEventArgs e) {
      if(!_owner.IsReadonly) {
        _owner.value = new JSL.Number((tbSaved.IsChecked == true ? 4 : 0) + (tbRequired.IsChecked == true ? 1 : 0) + (tbReadonly.IsChecked == true ? 2 : 0));
      }
    }

    private void UserControl_GotFocus_1(object sender, RoutedEventArgs e) {
      _owner.GotFocus(sender, e);
    }
  }
}
