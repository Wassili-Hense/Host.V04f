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
    private string _path;
    private string _view;

	public UIDocument(string path, string view) {
      _path = path;
      _view = view;
	  _pathItems=new ObservableCollection<DTopic>();
	  this.DataContext = this;
	  InitializeComponent();
	  this.icPanel.ItemsSource=_pathItems;
      Uri url;
      if(path != null && Uri.TryCreate(path, UriKind.Absolute, out url)) {
        RequestData(url);
      }
	}

	private DTopic _data;

    public bool connected { get { return _data != null; } }
    public DTopic data { get { return _data; } }
    public string ContentId { get { return (_data == null ?_path:_data.fullPath) + "?view=" + _view??"IN"; } }

    private void RequestData(Uri url) {
      this.Cursor = Cursors.AppStarting;
      DWorkspace.This.GetAsync(url, false).ContinueWith((t) => this.Dispatcher.BeginInvoke(new Action<Task<DTopic>>(this.DataUpd), t));
    }
    private void DataUpd(Task<DTopic> t) {
	  if(t.IsCompleted) {
		_data = t.Result;
        _path = _data.fullPath;
		OnPropertyChanged("data");

		DTopic c = _data;
		_pathItems.Clear();
		while(c != null) {
		  _pathItems.Insert(0, c);
		  c = c.parent;
		}
        if(_view == null) {
          _view = "IN";     // TODO: _data.schema => _view
        }
        OnPropertyChanged("ContentId");
        OnPropertyChanged("connected");
        if(_view == "IN") {
          if((ccMain.Content as InspectorForm)== null) {
            ccMain.Content = new InspectorForm(_data);
          }
        }
	  }
      this.Focus();
      this.Cursor = Cursors.Arrow;
	}

	#region Address bar
    private void tbAddress_Loaded(object sender, RoutedEventArgs e) {
      if(!this.connected && _path==null) {
        tbAddress.Focus();
      }
    }
	private void TextBox_IsKeyboardFocusedChanged(object sender, DependencyPropertyChangedEventArgs e) {
	  this.icPanel.Visibility = ((bool)e.NewValue == true ? System.Windows.Visibility.Collapsed : System.Windows.Visibility.Visible);
	}
    private void TextBox_KeyUp(object sender, KeyEventArgs e) {
      if(e.Key == Key.Enter || e.Key == Key.Tab) {
        Uri url;
        if(Uri.TryCreate(tbAddress.Text, UriKind.Absolute, out url)) {
          tbAddress.Background = null;
          RequestData(url);
        } else {
          tbAddress.Background = Brushes.LightPink;
        }
      }
    }
	private void Button_Click(object sender, RoutedEventArgs e) {
	  var bu = sender as Button;
	  DTopic t;
	  if(bu != null && (t = bu.DataContext as DTopic) != null) {
		DWorkspace.This.Open(t.fullPath);
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
