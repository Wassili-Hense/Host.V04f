///<remarks>This file is part of the <see cref="https://github.com/X13home">X13.Home</see> project.<remarks>
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
  public partial class InspectorForm : UserControl, IBaseForm {
    private static SortedList<string, Func<InBase, JSC.JSValue, IValueEditor>> _editors;
    private static RoutedUICommand _cmdRename;
    static InspectorForm() {
      _editors = new SortedList<string, Func<InBase, JSC.JSValue, IValueEditor>>();
      _editors["Boolean"] = veSliderBool.Create;
      _editors["Integer"] = veInteger.Create;
      _editors["Double"] = veDouble.Create;
      _editors["String"] = veString.Create;
      _editors["Date"] = veDateTimePicker.Create;
      _cmdRename = new RoutedUICommand("Rename", "Rename", typeof(InspectorForm));
    }
    public static IValueEditor GetEdititor(string view, InBase owner, JSC.JSValue schema) {
      IValueEditor rez;
      Func<InBase, JSC.JSValue, IValueEditor> ct;
      if(_editors.TryGetValue(view, out ct) && ct != null) {
        rez = ct(owner, schema);
      } else {
        rez = new veDefault(owner, schema);
      }
      return rez;
    }
    public static RoutedUICommand CmdRename { get { return _cmdRename; } }

    private InBase[] valueVC;

    public InspectorForm(DTopic data) {
      valueVC = new InBase[2];
      this.data = data;
      valueVC[0] = new InValue(data);
      valueVC[1] = new InTopic(data, null);
      InitializeComponent();
      this.tvValue.ItemsSource = valueVC;
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
    private void Grid_ContextMenuOpening(object sender, ContextMenuEventArgs e) {
      var gr = sender as FrameworkElement;
      if(gr != null) {
        var d = gr.DataContext as InBase;
        if(d != null) {
          var mi = d.MenuItems(gr);
          if(mi != null && mi.Count() > 0) {
            gr.ContextMenu.ItemsSource = mi;
            return;
          }
        }
      }
      e.Handled = true;
    }
    private void Grid_ContextMenuClosing(object sender, ContextMenuEventArgs e) {
      var gr = sender as FrameworkElement;
      if(gr != null && gr.ContextMenu != null) {
        gr.ContextMenu.ItemsSource = null;
        gr.ContextMenu.Items.Clear();
      }
    }

    private void tbItemName_Loaded(object sender, RoutedEventArgs e) {
      (sender as TextBox).SelectAll();
      (sender as TextBox).Focus();
    }

    private void tbItemName_PreviewKeyDown(object sender, KeyEventArgs e) {
      TextBox tb;
      InTopic tv;
      if((tb = sender as TextBox) == null || (tv = tb.DataContext as InTopic) == null) {
        return;
      }
      if(e.Key == Key.Escape) {
        tv.FinishNameEdit(null);
        e.Handled = true;
      } else if(e.Key == Key.Enter) {
        tv.FinishNameEdit(tb.Text);
        e.Handled = true;
      }
    }

    private void tbItemName_LostFocus(object sender, RoutedEventArgs e) {
      TextBox tb;
      InTopic tv;
      if((tb = sender as TextBox) == null || (tv = tb.DataContext as InTopic) == null) {
        return;
      }
      tv.FinishNameEdit(tb.Text);
      e.Handled = true;
    }

    private void CmdDelete_CanExecute(object sender, CanExecuteRoutedEventArgs e) {
      var gr = sender as FrameworkElement;
      if(gr != null) {
        var it = gr.DataContext as InBase;
        if(it != null) {
          e.CanExecute = it.CanExecute(e.Command, e.Parameter);
          e.Handled = true;
        }
      }
    }

    private void CommandBinding_Executed(object sender, ExecutedRoutedEventArgs e) {
      var gr = sender as FrameworkElement;
      if(gr != null) {
        var it = gr.DataContext as InBase;
        if(it != null) {
          it.CmdExecuted(e.Command, e.Parameter);
          e.Handled = true;
        }
      }
    }

    #region IBaseForm Members
    public string view {
      get { return "Inspector"; }
    }
    public BitmapSource icon { get { return App.GetIcon(null); } }
    public bool altView {
      get { return data!=null && (data.schemaStr=="Logram"); }
    }
    #endregion IBaseForm Members
  }
}
