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

namespace X13.UI {
  public partial class InspectorForm : UiBaseForm {
    private DTopic _data;

    public InspectorForm(string path) {
      valueVC = new ObservableCollection<ValueControl>();
      this.DataContext = this;
      InitializeComponent();
      DWorkspace.This.GetAsync(new Uri(path), false).ContinueWith((t) => this.Dispatcher.BeginInvoke(new Action<Task<DTopic>>(this.DataUpd), t));
    }
    public override string ToString() {
      return _data==null?"IN":("InspectorForm:" + _data.fullPath);
    }

    public override string viewArt {
      get { return "IN"; }
    }
    public object data { get { return _data; } }
    internal void SetData(object value) {
      _data.value = JSC.JSValue.Marshal(value);
      valueVC[0].UpdateData(_data.value);
    }

    public ObservableCollection<ValueControl> valueVC { get; private set; }
    private void DataUpd(Task<DTopic> t) {
      if(t.IsCompleted) {
        _data = t.Result;
        OnPropertyChanged("data");
        var v= new ValueControl(this, null, null, _data.value);
        if(valueVC.Count == 0) {
          valueVC.Add(v);
        } else {
          valueVC[0] = v;
        }
        OnPropertyChanged("valueVC");
      }
    }

    private void StackPanel_MouseUp(object sender, MouseButtonEventArgs e) {
      StackPanel p;
      DTopic t;
      if((p = sender as StackPanel) != null && (t = p.DataContext as DTopic) != null) {
        DWorkspace.This.Open(t.fullPath);
      }
    }

    private void ValueControl_GotFocus(object sender, RoutedEventArgs e) {
      DependencyObject cur;
      TreeViewItem parent;
      DependencyObject parentObject;

      for(cur = sender as DependencyObject; cur != null; cur = parentObject) {
        parentObject = VisualTreeHelper.GetParent(cur);
        if((parent = parentObject as TreeViewItem) != null) {
          parent.IsSelected = true;
          break;
        }
      }
    }

  }
}
