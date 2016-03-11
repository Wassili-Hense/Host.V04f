using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace X13 {
  internal class Topic : INotifyPropertyChanged {
    private static char[] _delmiter = new char[] { '/' };

    private System.Collections.ObjectModel.ObservableDictionary<string, Topic> _childs;
    private Client _client;

    public Topic(Client client) {
      this.parent = null;
      this.name = string.Empty;
      this._client = client;
      this.path = _client.url;
    }
    private Topic(Topic parent, string name) {
      this.parent = parent;
      this.name = name;
      this._client = parent._client;
      this.path = new Uri(parent.path.ToString() + (parent==_client.root?string.Empty:"/") + name);
    }

    public string name { get; private set; }
    public Uri path { get; private set; }
    public Topic parent { get; private set; }
    public System.Collections.ObjectModel.ObservableDictionary<string, Topic> children {
      get {
        if(_childs == null) {
          lock(this) {
            if(_childs == null) {
              _childs = new System.Collections.ObjectModel.ObservableDictionary<string, Topic>();
            }
          }
          this.RaisePropertyChanged("children");
        }
        return _childs;
      }
    }

    public Topic Get(string path, bool create) {
      Topic cur = (!string.IsNullOrEmpty(path) && path.StartsWith("/"))?_client.root:this;
      Topic next=null;
      bool chExist;
      int i;

      string[] pe = path.Split(_delmiter, StringSplitOptions.RemoveEmptyEntries);
      for(i = 0; i < pe.Length; i++, cur = next) {
        chExist = cur.children.TryGetValue(pe[i], out next);
        if(!chExist) {
          if(create) {
            lock(cur) {
              chExist = chExist = cur._childs.TryGetValue(pe[i], out next);
              if(!chExist) {
                if(pe[i] == "+" || pe[i] == "#") {
                  throw new ArgumentException("path (" + path + ") is not valid");
                }
                next = new Topic(cur, pe[i]);
                cur._childs[pe[i]] = next;
              }
            }
          } else {
            return null;
          }
        }
      }
      return cur;
    }

    #region INotifyPropertyChanged
    private void RaisePropertyChanged(string propertyName) {
      if(PropertyChanged != null)
        PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
    }
    public event PropertyChangedEventHandler PropertyChanged;
    #endregion INotifyPropertyChanged
  }
}
