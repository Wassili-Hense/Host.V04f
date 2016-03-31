using JSL = NiL.JS.BaseLibrary;
using JSC = NiL.JS.Core;
using JSF = NiL.JS.Core.Functions;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace X13.Data {
  internal class DTopic : INotifyPropertyChanged {
    private const string childrenString = "children";
    private readonly Action<string> _ActNPC;

    private A04Client _client;
    private int _flags;  //  1 - acl.subscribe, 2 - acl.create, 4 - acl.change, 8 - acl.remove, 16 - hat children
    private DChildren _children;
    private JSC.JSValue _value;


    private DTopic(DTopic parent, string name) {
      this._client = parent._client;
      this.name = name;
      this.path = parent == _client.root ? ("/" + name) : (parent.path + "/" + name);
      _ActNPC = new Action<string>(OnPropertyChanged);
    }
    public DTopic(A04Client cl) {
      _client = cl;
      this.name = _client.url.ToString();
      this.path = "/";
      _ActNPC = new Action<string>(OnPropertyChanged);
    }
    public Task<DTopic> GetAsync(string path, bool create) {
      var req = new TopicReq((!string.IsNullOrEmpty(path) && path[0] == '/') ? _client.root : this, path, create);
      DWorkspace.This.AddMsg(req);
      return req.Task;
    }
    public event PropertyChangedEventHandler PropertyChanged;

    public string name { get; private set; }
    public string path { get; private set; }
    public string schema { get; private set; }

    private void OnPropertyChanged(string propertyName) {
      if(PropertyChanged != null) {
        PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
      }
    }

    private class TopicReq : INotMsg {
      private DTopic _cur;
      private string _path;
      private bool _create;
      private TaskCompletionSource<DTopic> _tcs;

      public TopicReq(DTopic cur, string path, bool create) {
        this._cur = cur;
        this._path = path;
        this._create = create;
        this._tcs = new TaskCompletionSource<DTopic>();
      }
      public Task<DTopic> Task { get { return _tcs.Task; } }

      public void Process(DWorkspace ws) {
        int idx1 = _cur.path.Length ;
        if(_path == null || _path.Length <= idx1) {
          if(_cur._value != null) {
            _tcs.SetResult(_cur);
          } else {
            _cur._client.Request(_cur.path, 3, this);
          }
          return;
        }
        DTopic next=null;
          int idx2 = _path.IndexOf('/', idx1);
          if(idx2 < 0) {
            idx2 = _path.Length;
          }
          string name = _path.Substring(idx1, idx2 - idx1);

        if((_cur._flags & 16) == 16 || _cur._flags==0) {  // 0 => 1st request
          if(_cur._children == null) {
            _cur._client.Request(_cur.path, 2, this);
            return;
          }

          if(!_cur._children.TryGetValue(name, out next)) {
            next = null;
          }
        }
        if(next == null) {
          if(_create) {
            _cur._client.Create(_path.Substring(0, idx2), this);
            return;
          } else {
            _tcs.SetResult(null);
          }
        }
        _cur = next;
        ws.AddMsg(this);
      }
      public void Response(DWorkspace ws, bool success, JSC.JSValue value) {
        if(success) {
          DTopic next;
          JSL.Array ca = value as JSL.Array;
          IEnumerable<KeyValuePair<string, JSC.JSValue>> cc;
          if(ca == null || (int)ca.length != 1 || (cc = ca[0] as IEnumerable<KeyValuePair<string, JSC.JSValue>>) == null) {
            _tcs.SetException(new ApplicationException("TopicReq bad answer:" + (value == null ? string.Empty : string.Join(", ", value))));
            return;
          }
          string aName, aPath;
          int aFlags;
          if(_cur._children == null) {
            _cur._children = new DChildren();
            DWorkspace.ui.BeginInvoke(_cur._ActNPC, System.Windows.Threading.DispatcherPriority.DataBind, DTopic.childrenString);
          }
          foreach(var cb in cc.Select(z => z.Value == null ? null : z.Value.Select(y => y.Value).ToArray())) {
            if(cb==null || cb.Length < 3 || cb[0].ValueType != JSC.JSValueType.String || (cb[1].ValueType != JSC.JSValueType.Double && cb[1].ValueType != JSC.JSValueType.Integer)) {
              continue;
            }
            aPath = cb[0].Value as string;
            aFlags = (int)cb[1];
            if(aPath != _cur.path) {
              if(!aPath.StartsWith(_cur.path)) {
                continue;
              }
              aName = aPath.Substring(_cur.path.Length);
              next = new DTopic(_cur, aName);
              _cur._children.Add(next);
            } else {
              next = _cur;
            }
            next._flags = aFlags;
            next.schema = cb[2].Value as string;
            if(cb.Length == 4) {
              next._value = cb[3];
            }
          }
        } else {
          _tcs.SetException(new ApplicationException("TopicReq failed:" + (value == null ? string.Empty : string.Join(", ", value))));
        }
      }
    }
  }
}
