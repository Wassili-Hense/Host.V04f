using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
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

namespace X13.UI {
  /// <summary></summary>
  public partial class UIDocument : UserControl, INotifyPropertyChanged {
	private ObservableCollection<DTopic> _pathItems;

	public UIDocument(string path) {
	  _pathItems=new ObservableCollection<DTopic>();
	  this.DataContext = this;
	  InitializeComponent();
	  this.icPanel.ItemsSource=_pathItems;
	  DWorkspace.This.GetAsync(new Uri(path), false).ContinueWith((t) => this.Dispatcher.BeginInvoke(new Action<Task<DTopic>>(this.DataUpd), t));
	}

	private DTopic _data;

	public DTopic data { get { return _data; } }
	public string ContentId { get { return _data == null ? "Inspector" : (_data.fullPath + "?view=" + "IN"); } }

	private void DataUpd(Task<DTopic> t) {
	  if(t.IsCompleted) {
		_data = t.Result;
		OnPropertyChanged("data");
		OnPropertyChanged("ContentId");

		DTopic c = _data;
		_pathItems.Clear();
		while(c != null) {
		  _pathItems.Insert(0, c);
		  c = c.parent;
		}
	  }
	}
	#region Address bar
	private void TextBox_IsKeyboardFocusedChanged(object sender, DependencyPropertyChangedEventArgs e) {
	  this.icPanel.Visibility = ((bool)e.NewValue == true ? System.Windows.Visibility.Collapsed : System.Windows.Visibility.Visible);
	}

	private void Button_Click(object sender, RoutedEventArgs e) {
	  var bu = sender as Button;
	  DTopic t;
	  if(bu != null && (t = bu.DataContext as DTopic) != null) {
		DWorkspace.This.Open(t);
	  }
	}
	#endregion Address bar

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
