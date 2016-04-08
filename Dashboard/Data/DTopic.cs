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
  public class DTopic : INotifyPropertyChanged {
    private const string childrenString = "children";
    private readonly Action<string> _ActNPC;

    private A04Client _client;
    private int _flags;  //  1 - acl.subscribe, 2 - acl.create, 4 - acl.change, 8 - acl.remove, 16 - hat children
    private DChildren _children;
    private JSC.JSValue _value;


    private DTopic(DTopic parent, string name) {
      this.parent = parent;
      this._client = this.parent._client;
      this.name = name;
      this.path = this.parent == _client.root ? ("/" + name) : (this.parent.path + "/" + name);
      _ActNPC = new Action<string>(OnPropertyChanged);
    }
    internal DTopic(A04Client cl) {
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
    public DTopic parent { get; private set; }
    public string schema { get; private set; }
    public JSC.JSValue value { get { return _value; } set { _value = value; } }
    public DChildren children { get { return _children; } }

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
        int idx1 = _cur.path.Length;
        if(idx1>1) {
          idx1++;
        }
        if(_path == null || _path.Length <= _cur.path.Length) {
          if(_cur._value != null) {
            _tcs.SetResult(_cur);
          } else {
            _cur._client.Request(_cur.path, 3, this);
          }
          return;
        }
        DTopic next = null;
        int idx2 = _path.IndexOf('/', idx1);
        if(idx2 < 0) {
          idx2 = _path.Length;
        }
        string name = _path.Substring(idx1, idx2 - idx1);

        if((_cur._flags & 16) == 16 || _cur._flags == 0) {  // 0 => 1st request
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
          } else {
            _tcs.SetResult(null);
          }
          return;
        }
        _cur = next;
        ws.AddMsg(this);
      }
      public void Response(DWorkspace ws, bool success, JSC.JSValue value) {
        if(success) {
		  bool childrenPC=false;
          DTopic next;
          JSL.Array ca = value as JSL.Array, cc;
          if(ca == null || (int)ca.length != 1 || (cc = ca[0].Value as JSL.Array) == null) {
            _tcs.SetException(new ApplicationException("TopicReq bad answer:" + (value == null ? string.Empty : string.Join(", ", value))));
            return;
          }
          string aName, aPath;
          int aFlags;
          if(_cur._children == null) {
            _cur._children = new DChildren();
			childrenPC=true;
          }
          foreach(var cb in cc.Select(z => z.Value.Value as JSL.Array)) {
            if(cb == null || (int)cb.length < 3 || cb[0].ValueType != JSC.JSValueType.String || (cb[1].ValueType != JSC.JSValueType.Double && cb[1].ValueType != JSC.JSValueType.Integer)) {
              continue;
            }
            aPath = cb[0].Value as string;
            aFlags = (int)cb[1];
            if(aPath != _cur.path) {
              if(!aPath.StartsWith(_cur.path)) {
                continue;
              }
              aName = aPath.Substring(_cur.path.Length==1?1:(_cur.path.Length+1));
              next = new DTopic(_cur, aName);
              _cur._children.AddItem(next);
            } else {
              next = _cur;
            }
            next._flags = aFlags;
            next.schema = cb[2].Value as string;
            if((int)cb.length == 4) {
              next._value = cb[3];
            }
          }
		  if(childrenPC) {
			DWorkspace.ui.BeginInvoke(_cur._ActNPC, System.Windows.Threading.DispatcherPriority.DataBind, DTopic.childrenString);
		  }
        } else {
          _tcs.SetException(new ApplicationException("TopicReq failed:" + (value == null ? string.Empty : string.Join(", ", value))));
        }
      }
    }

    public string fullPath { get { return _client.url.GetLeftPart(UriPartial.Path) + this.path.Substring(1); } }
  }
}
