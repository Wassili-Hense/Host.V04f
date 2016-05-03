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
using X13.Data;
using System.Collections.ObjectModel;
using System.ComponentModel;

namespace X13.UI {
  public partial class InspectorForm : UserControl {
    private static SortedList<string, Func<InBase, JSC.JSValue, IValueEditor>> _editors;

    static InspectorForm(){
      _editors = new SortedList<string, Func<InBase, JSC.JSValue, IValueEditor>>();
      _editors["Boolean"] = veSliderBool.Create;
      _editors["Integer"] = veInteger.Create;
      _editors["Double"] = veDouble.Create;
      _editors["String"] = veString.Create;
      _editors["Date"] = veDateTimePicker.Create;
    }
    public static IValueEditor GetEdititor(string view, InBase owner, JSC.JSValue schema) {
      IValueEditor rez;
      Func<InBase, JSC.JSValue, IValueEditor> ct;
      if(_editors.TryGetValue(view, out ct) && ct!=null) {
        rez = ct(owner, schema);
      }else{
        rez = new veDefault(owner, schema);
      }
      return rez;
    }

    private InBase[] valueVC;

    public InspectorForm(DTopic data) {
      valueVC = new InBase[2];
      this.data = data;
      valueVC[0] = new InValue(data);
      valueVC[1] = new InTopic(data, true);
      InitializeComponent();
	  this.tvValue.ItemsSource=valueVC;
    }

    #region Properies
	public DTopic data { get; private set; }
    #endregion Properies

    #region Children

    private void Border_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e) {
      FrameworkElement p;
      DTopic t;
      if((p = sender as FrameworkElement) != null && (t = p.DataContext as DTopic) != null) {
        DWorkspace.This.Open(t.fullPath);
        e.Handled = true;
      }
    }

    #endregion Children
  }
}
