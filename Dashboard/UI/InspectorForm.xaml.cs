﻿using JSL = NiL.JS.BaseLibrary;
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
    public InspectorForm() {
      valueVC = new ObservableCollection<ValueControl>();
	  this.DataContextChanged+=InspectorForm_DataContextChanged;
      InitializeComponent();
	  this.tvValue.ItemsSource=valueVC;
	  this.icChildren.DataContext=this;
    }

	public ObservableCollection<ValueControl> valueVC { get; private set; }
	public DTopic data { get; private set; }
    public void SetData(object value) {
	  data.value = JSC.JSValue.Marshal(value);
	  valueVC[0].UpdateData(data.value);
    }
	private void InspectorForm_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e) {
	  var t=e.NewValue as DTopic;
	  if(t!=null) {
		data=t;
		OnPropertyChanged("data");
		var v = new ValueControl(this, null, null, data.value);
		if(valueVC.Count == 0) {
		  valueVC.Add(v);
		} else {
		  valueVC[0] = v;
		}
	  }
	}

    private void StackPanel_MouseUp(object sender, MouseButtonEventArgs e) {
      StackPanel p;
      DTopic t;
      if((p = sender as StackPanel) != null && (t = p.DataContext as DTopic) != null) {
        DWorkspace.This.Open(t);
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
