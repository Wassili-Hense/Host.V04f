using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace X13.Data {
  public class DChildren : ObservableCollection<DTopic> {
    public void AddItem(DTopic item) {
      if(item == null) {
        throw new ArgumentNullException("item");
      }
      int idx;
      if(TryGetIndex(item.name, out idx)) {
        var oItem = this[idx];
        if(Equals(oItem, item))
          return;
        this[idx] = item;
      } else {
        this.Insert(idx, item);
      }
    }
    public bool TryGetValue(string key, out DTopic value) {
      int idx;
      if(TryGetIndex(key, out idx)) {
        value = this[idx];
        return true;
      } else {
        value = null;
        return false;
      }
    }
    private bool TryGetIndex(string name, out int mid) {
      int min = 0, max = this.Count - 1, cmp;
      mid = 0;

      while(min <= max) {
        mid = (min + max) / 2;
        cmp = string.Compare(this[mid].name, name);
        if(cmp < 0) {
          min = mid + 1;
          mid = min;
        } else if(cmp > 0) {
          max = mid - 1;
          mid = max;
        } else {
          return true;
        }
      }
      return false;
    }
    protected override event PropertyChangedEventHandler PropertyChanged;
    public override event NotifyCollectionChangedEventHandler CollectionChanged;
    protected override void OnPropertyChanged(PropertyChangedEventArgs e) {
      if(PropertyChanged != null) {
        DWorkspace.ui.BeginInvoke(PropertyChanged, System.Windows.Threading.DispatcherPriority.DataBind, this, e);
      }
    }
    protected override void OnCollectionChanged(NotifyCollectionChangedEventArgs e) {
      if(CollectionChanged != null) {
        DWorkspace.ui.BeginInvoke(CollectionChanged, System.Windows.Threading.DispatcherPriority.DataBind, this, e);
      }
    }
  }
}
