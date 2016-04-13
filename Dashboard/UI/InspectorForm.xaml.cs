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
  public partial class InspectorForm : UserControl, INotifyPropertyChanged {
    public InspectorForm(DTopic data) {
      valueVC = new ObservableCollection<ValueControl>();
      this.data = data;
      if(this.data!=null){
        this.data.PropertyChanged += data_PropertyChanged;
      }
      var v = new ValueControl(this, null, null, data.value);
      if(valueVC.Count == 0) {
        valueVC.Add(v);
      } else {
        valueVC[0] = v;
      }

      InitializeComponent();
	  this.tvValue.ItemsSource=valueVC;
	  this.icChildren.DataContext=this;
    }

	public ObservableCollection<ValueControl> valueVC { get; private set; }
	public DTopic data { get; private set; }
    public void DataChanged(JSC.JSValue val) {
      data.SetValue(val).Wait();
      valueVC[0].UpdateData(data.value);
    }

    private void data_PropertyChanged(object sender, PropertyChangedEventArgs e) {
      if(e.PropertyName == "schema") {
        valueVC[0].UpdateSchema((data==null || data.schema==null)?null:data.schema.data);
      }
    }


    private void StackPanel_MouseUp(object sender, MouseButtonEventArgs e) {
      FrameworkElement p;
      DTopic t;
      if((p = sender as FrameworkElement) != null && (t = p.DataContext as DTopic) != null) {
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

	#region INotifyPropertyChanged Members
	public event PropertyChangedEventHandler PropertyChanged;

	internal void OnPropertyChanged(string propertyName) {
	  if(PropertyChanged != null) {
		PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
	  }
	}
	#endregion INotifyPropertyChanged Members
  }
}
