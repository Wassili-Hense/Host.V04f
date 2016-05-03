using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using X13.Data;
using JSC = NiL.JS.Core;
using JSL = NiL.JS.BaseLibrary;

namespace X13.UI {
  public class InTopic : InBase, IDisposable {
    private DTopic _owner;
    private bool _root;
    private ObservableCollection<InTopic> _items;
    private bool _populated;

    public InTopic(DTopic owner, bool root) {
      _owner = owner;
      _root = root;
      _owner.PropertyChanged += _owner_PropertyChanged;
      if(_root) {
        name = "children";
        icon = App.GetIcon("children");
        editor = null;
      } else {
        name = _owner.name;
        base.UpdateSchema(owner.schema);
      }
      base.IsExpanded = _root;
    }
    public override JSC.JSValue value { get { return _owner.value; } set { _owner.SetValue(value); } }
    public ObservableCollection<InTopic> items {
      get {
        if(_items == null) {
          _populated=true;
          if(_owner.children != null) {
            _owner.children.CollectionChanged += children_CollectionChanged;
            _items = new ObservableCollection<InTopic>();
            foreach(var t in _owner.children) {
              _items.Add(new InTopic(t, false));
            }
          }
        }
        return _items;
      }
    }

    private void children_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e) {
      throw new NotImplementedException();
    }

    private void _owner_PropertyChanged(object sender, PropertyChangedEventArgs e) {
      if(!_root) {
        if(e.PropertyName == "schema") {
          this.UpdateSchema(_owner.schema);
        } else if(e.PropertyName == "value") {
          this.UpdateSchema(_owner.schema);
          this.editor.ValueChanged(_owner.value);
        }
      }
      if(e.PropertyName == "children" && _populated) {
        if(_owner.children != null) {
          if(_items == null) {
            _owner.children.CollectionChanged += children_CollectionChanged;
            _items = new ObservableCollection<InTopic>();
            foreach(var t in _owner.children) {
              _items.Add(new InTopic(t, false));
            }
            PropertyChangedReise("items");
          }
        } else if(_items != null) {
          _items = null;
          PropertyChangedReise("items");
        }
      }
    }

    #region IDisposable Member
    public void Dispose() {
      _owner.PropertyChanged -= _owner_PropertyChanged;
    }
    #endregion IDisposable Member
  }
}
