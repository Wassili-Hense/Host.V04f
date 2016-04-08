using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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
  /// <summary>
  /// Interaction logic for AddressBar.xaml
  /// </summary>
  public partial class AddressBar : UserControl {

    private ObservableCollection<DTopic> _items;
    private DTopic _data;
    public AddressBar() {
      _items = new ObservableCollection<DTopic>();
      base.DataContextChanged += AddressBar_DataContextChanged;
      InitializeComponent();
      this.icPanel.DataContext = this;
    }

    private void AddressBar_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e) {
      this.Data = e.NewValue as DTopic;
    }
    public DTopic Data {
      get {
        return _data;
      }
      set {
        _data = value;
        DTopic c = _data;
        _items.Clear();
        if(c != null) {
          do {
            _items.Insert(0, c);
            c = c.parent;
          } while(c != null);
        }
      }
    }
    public ObservableCollection<DTopic> Items {
      get {
        return _items;
      }
    }

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
  }
}
