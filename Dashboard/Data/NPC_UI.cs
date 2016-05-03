using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace X13.Data {
  public class NPC_UI : INotifyPropertyChanged {

    protected NPC_UI() {
      _propertyChangedAction = new Action<string>(PropertyChangedFunc);
    }

    #region INotifyPropertyChanged Members
    private Action<string> _propertyChangedAction;
    protected virtual void PropertyChangedFunc(string propertyName) {
      if(PropertyChanged != null) {
        PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
      }
    }
    public event PropertyChangedEventHandler PropertyChanged;
    protected void PropertyChangedReise(string propertyName) {
      X13.Data.DWorkspace.This.UiMessage(_propertyChangedAction, propertyName);
    }
    #endregion INotifyPropertyChanged Members

  }
}
