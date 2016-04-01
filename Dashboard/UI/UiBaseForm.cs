using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;

namespace X13.UI {
  public abstract class UiBaseForm : UserControl,  INotifyPropertyChanged {
    public abstract string viewArt   { get; }
    public event PropertyChangedEventHandler PropertyChanged;

    protected void OnPropertyChanged(string propertyName) {
      if(PropertyChanged != null) {
        PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
      }
    }

  }
}
