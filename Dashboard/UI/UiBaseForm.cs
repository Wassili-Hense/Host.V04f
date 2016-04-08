using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using X13.Data;

namespace X13.UI {
  public abstract class UiBaseForm : UserControl, INotifyPropertyChanged {
    protected DTopic _data;

    public abstract string viewArt { get; }
    public DTopic data { get { return _data; } }
    public string ContentId { get { return _data == null ? "Inspector" : (_data.fullPath + "?view=" + viewArt); } }
    public event PropertyChangedEventHandler PropertyChanged;

    protected void OnPropertyChanged(string propertyName) {
      if(PropertyChanged != null) {
        PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
      }
    }

  }
}
