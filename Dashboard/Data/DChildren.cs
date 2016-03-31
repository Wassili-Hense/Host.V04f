using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace X13.Data {
  internal class DChildren : ICollection<DTopic>, INotifyCollectionChanged, INotifyPropertyChanged {
    private const string CountString = "Count";
    private readonly Action<NotifyCollectionChangedEventArgs> _ActNTC;
    private readonly Action<string> _ActNPC;
    private SortedList<string, DTopic> _data;

    public DChildren() {
      _data = new SortedList<string, DTopic>();
      _ActNTC = new Action<NotifyCollectionChangedEventArgs>(OnCollectionChanged);
      _ActNPC = new Action<string>(OnPropertyChanged);
    }
    public bool TryGetValue(string key, out DTopic value) {
      return _data.TryGetValue(key, out value);
    }


    #region ICollection<Topic> Members
    public void Add(DTopic item) {
      if(item == null) {
        throw new ArgumentNullException("value");
      }

      DTopic oItem;
      if(_data.TryGetValue(item.name, out oItem)) {
        if(Equals(oItem, item))
          return;
        _data[item.name] = item;
        DWorkspace.ui.BeginInvoke(_ActNTC
          , System.Windows.Threading.DispatcherPriority.DataBind
          , new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Replace, item, oItem));

      } else {
        _data[item.name] = item;
        DWorkspace.ui.BeginInvoke(_ActNTC
          , System.Windows.Threading.DispatcherPriority.DataBind
          , new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, item));
      }
    }
    public void Clear() {
      throw new NotSupportedException();
    }
    public bool Contains(DTopic item) {
      return _data.ContainsValue(item);
    }
    public void CopyTo(DTopic[] array, int arrayIndex) {
      _data.Values.CopyTo(array, arrayIndex);
    }
    public int Count { get { return _data.Count; } }
    public bool IsReadOnly {
      get { return false; }
    }
    public bool Remove(DTopic item) {
      throw new NotImplementedException();
    }
    #endregion ICollection<Topic> Members

    #region IEnumerable<Topic> Members
    public IEnumerator<DTopic> GetEnumerator() {
      return _data.Values.GetEnumerator();
    }
    #endregion IEnumerable<topc> Members

    #region IEnumerable Members
    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() {
      return _data.Values.GetEnumerator();
    }
    #endregion IEnumerable Members

    #region INotifyCollectionChanged Members
    public event NotifyCollectionChangedEventHandler CollectionChanged;
    #endregion

    #region INotifyPropertyChanged Members
    public event PropertyChangedEventHandler PropertyChanged;
    #endregion

    protected virtual void OnPropertyChanged(string propertyName) {
      if(PropertyChanged != null) {
        PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
      }
    }
    private void OnCollectionChanged(NotifyCollectionChangedEventArgs e) {
      OnPropertyChanged(CountString);
      if(CollectionChanged != null)
        CollectionChanged(this, e);
    }
  }
}
