﻿using System;
using System.Linq;
using System.ComponentModel;
using System.Collections.Generic;
using System.Collections.Specialized;

namespace System.Collections.ObjectModel {
  /// <seealso cref="http://blogs.microsoft.co.il/shimmy/2010/12/26/observabledictionarylttkey-tvaluegt-c/"/>
  internal class ObservableDictionary<TKey, TValue> : IDictionary<TKey, TValue>, INotifyCollectionChanged, INotifyPropertyChanged {
    private const string CountString = "Count";
    private const string IndexerName = "Item[]";
    private const string KeysName = "Keys";
    private const string ValuesName = "Values";

    private IDictionary<TKey, TValue> _Dictionary;
    protected IDictionary<TKey, TValue> Dictionary {
      get { return _Dictionary; }
    }

    #region Constructors
    public ObservableDictionary() {
      _Dictionary = new SortedList<TKey, TValue>();
    }
    #endregion

    #region IDictionary<TKey,TValue> Members

    public void Add(TKey key, TValue value) {
      Insert(key, value, true);
    }

    public bool ContainsKey(TKey key) {
      return Dictionary.ContainsKey(key);
    }

    public ICollection<TKey> Keys {
      get { return Dictionary.Keys; }
    }

    public bool Remove(TKey key) {
      if(key == null)
        throw new ArgumentNullException("key");

      TValue value;
      if(Dictionary.TryGetValue(key, out value) && Dictionary.Remove(key)) {
        OnCollectionChanged();
        return true;
      }
      return false;
    }


    public bool TryGetValue(TKey key, out TValue value) {
      return Dictionary.TryGetValue(key, out value);
    }


    public ICollection<TValue> Values {
      get { return Dictionary.Values; }
    }


    public TValue this[TKey key] {
      get {
        return Dictionary[key];
      }
      set {
        Insert(key, value, false);
      }
    }


    #endregion


    #region ICollection<KeyValuePair<TKey,TValue>> Members


    public void Add(KeyValuePair<TKey, TValue> item) {
      Insert(item.Key, item.Value, true);
    }


    public void Clear() {
      if(Dictionary.Count > 0) {
        Dictionary.Clear();
        OnCollectionChanged();
      }
    }


    public bool Contains(KeyValuePair<TKey, TValue> item) {
      return Dictionary.Contains(item);
    }


    public void CopyTo(KeyValuePair<TKey, TValue>[] array, int arrayIndex) {
      Dictionary.CopyTo(array, arrayIndex);
    }


    public int Count {
      get { return Dictionary.Count; }
    }


    public bool IsReadOnly {
      get { return Dictionary.IsReadOnly; }
    }


    public bool Remove(KeyValuePair<TKey, TValue> item) {
      return Remove(item.Key);
    }


    #endregion


    #region IEnumerable<KeyValuePair<TKey,TValue>> Members


    public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator() {
      return Dictionary.GetEnumerator();
    }


    #endregion


    #region IEnumerable Members


    IEnumerator IEnumerable.GetEnumerator() {
      return ((IEnumerable)Dictionary).GetEnumerator();
    }


    #endregion


    #region INotifyCollectionChanged Members


    public event NotifyCollectionChangedEventHandler CollectionChanged;


    #endregion


    #region INotifyPropertyChanged Members


    public event PropertyChangedEventHandler PropertyChanged;


    #endregion


    public void AddRange(IDictionary<TKey, TValue> items) {
      if(items == null)
        throw new ArgumentNullException("items");


      if(items.Count > 0) {
        if(Dictionary.Count > 0) {
          if(items.Keys.Any((k) => Dictionary.ContainsKey(k)))
            throw new ArgumentException("An item with the same key has already been added.");
          else
            foreach(var item in items)
              Dictionary.Add(item);
        } else
          _Dictionary = new Dictionary<TKey, TValue>(items);


        OnCollectionChanged(NotifyCollectionChangedAction.Add, items.ToArray());
      }
    }


    private void Insert(TKey key, TValue value, bool add) {
      if(key == null)
        throw new ArgumentNullException("key");


      TValue item;
      if(Dictionary.TryGetValue(key, out item)) {
        if(add)
          throw new ArgumentException("An item with the same key has already been added.");
        if(Equals(item, value))
          return;
        Dictionary[key] = value;


        OnCollectionChanged(NotifyCollectionChangedAction.Replace, new KeyValuePair<TKey, TValue>(key, value), new KeyValuePair<TKey, TValue>(key, item));
      } else {
        Dictionary[key] = value;

        OnCollectionChanged(NotifyCollectionChangedAction.Add, new KeyValuePair<TKey, TValue>(key, value));
      }
    }


    private void OnPropertyChanged() {
      OnPropertyChanged(CountString);
      OnPropertyChanged(IndexerName);
      OnPropertyChanged(KeysName);
      OnPropertyChanged(ValuesName);
    }


    protected virtual void OnPropertyChanged(string propertyName) {
      if(PropertyChanged != null)
        PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
    }


    private void OnCollectionChanged() {
      OnPropertyChanged();
      if(CollectionChanged != null)
        CollectionChanged(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
    }


    private void OnCollectionChanged(NotifyCollectionChangedAction action, KeyValuePair<TKey, TValue> changedItem) {
      OnPropertyChanged();
      if(CollectionChanged != null)
        CollectionChanged(this, new NotifyCollectionChangedEventArgs(action, changedItem));
    }


    private void OnCollectionChanged(NotifyCollectionChangedAction action, KeyValuePair<TKey, TValue> newItem, KeyValuePair<TKey, TValue> oldItem) {
      OnPropertyChanged();
      if(CollectionChanged != null)
        CollectionChanged(this, new NotifyCollectionChangedEventArgs(action, newItem, oldItem));
    }


    private void OnCollectionChanged(NotifyCollectionChangedAction action, IList newItems) {
      OnPropertyChanged();
      if(CollectionChanged != null)
        CollectionChanged(this, new NotifyCollectionChangedEventArgs(action, newItems));
    }
  }
}