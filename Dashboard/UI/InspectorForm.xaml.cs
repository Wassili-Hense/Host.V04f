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

    private ObservableCollection<InBase> _valueVC;

    public InspectorForm(DTopic data) {
      _valueVC = new ObservableCollection<InBase>();
      this.data = data;
      //valueVC[0] = new InValue(data);
      _valueVC.Add(new InTopic(data, null, CollectionChange));
      InitializeComponent();

      var v = new System.Windows.Data.CollectionViewSource();
      v.Source = _valueVC;
      v.Filter += v_Filter;
      v.LiveFilteringProperties.Add("IsVisible");
      v.IsLiveFilteringRequested = true;
      ((ListCollectionView)v.View).CustomSort = new InBase.Comparer();
      Binding binding = new Binding();
      binding.Source = v;
      BindingOperations.SetBinding(lvValue, ListView.ItemsSourceProperty, binding);
    }

    private void v_Filter(object sender, FilterEventArgs e) {
      var v = e.Item as InBase;
      e.Accepted = v != null && v.IsVisible;
    }
    private void CollectionChange(InBase item, bool add) {
      if(add) {
        _valueVC.Add(item);
      } else {
        _valueVC.Remove(item);
      }
    }

    public DTopic data { get; private set; }

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

    private void TextBlock_KeyUp(object sender, KeyEventArgs e) {
        if(e.Key != Key.Left && e.Key!=Key.Right) {
        return;
      }
      var gr = sender as FrameworkElement;
      if(gr != null) {
        var it = gr.DataContext as InBase;
        if(it != null) {
          if(e.Key == Key.Right && it.HasChildren && !it.IsExpanded) {
            it.IsExpanded = true;
            e.Handled = true;
          } else if(e.Key == Key.Left && it.IsExpanded) {
            it.IsExpanded = false;
            e.Handled = true;
          }
        }
      }

    }
  }
  internal class GridColumnSpringConverter : IMultiValueConverter {
    public object Convert(object[] values, System.Type targetType, object parameter, System.Globalization.CultureInfo culture) {
      double v=values.OfType<double>().Aggregate((x, y) => x -= y) - 26;
      return v > 0 ? v : 100;
    }
    public object[] ConvertBack(object value, System.Type[] targetTypes, object parameter, System.Globalization.CultureInfo culture) {
      throw new System.NotImplementedException();
    }
  }
}
