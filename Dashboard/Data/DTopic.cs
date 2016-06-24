///<remarks>This file is part of the <see cref="https://github.com/X13home">X13.Home</see> project.<remarks>
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using JSC = NiL.JS.Core;
using JSL = NiL.JS.BaseLibrary;

namespace X13.Data {
  public class DTopic {
    private static char[] FIELDS_SEPARATOR = new char[] { '.' };

    private A04Client _client;
    private int _flags;  //  1 - acl.subscribe, 2 - acl.create, 4 - acl.update, 8 - acl.delete, 16 - hat children
    private JSC.JSValue _value;
    private DTopic _schemaTopic;
    private int _schemaRequsted;
    private bool _disposed;
    private List<DTopic> _children;

    private DTopic(DTopic parent, string name) {
      this.parent = parent;
      this._client = this.parent._client;
      this.name = name;
      this.path = this.parent == _client.root ? ("/" + name) : (this.parent.path + "/" + name);
    }
    internal DTopic(A04Client cl) {
      _client = cl;
      this.name = _client.url.ToString().TrimEnd('/');
      this.path = "/";
    }

    public JSC.JSValue schema {
      get {
        if(System.Threading.Interlocked.Exchange(ref _schemaRequsted, 1) == 0) {
          var task = _client.root.GetAsync("/etc/schema/" + this.schemaStr);
          task.ContinueWith(td => DWorkspace.ui.BeginInvoke(new Action<Task<DTopic>>(ExtractSchema), td));
          return null;
        } else {
          return _schemaTopic == null ? null : _schemaTopic._value;
        }
      }
    }
    public virtual string name { get; protected set; }
    public string path { get; private set; }
    public DTopic parent { get; private set; }
    public string schemaStr { get; private set; }
    public string fullPath { get { return _client.url.GetLeftPart(UriPartial.Authority) + this.path; } }
    public JSC.JSValue value {
      get {
        if(_value == null && parent != null) {
          var req = new TopicReq(parent, this.name);
          DWorkspace.This.AddMsg(req);
          return JSC.JSValue.NotExists;
        }
        return _value;
      }
    }
    public ReadOnlyCollection<DTopic> children { get { return _children == null ? null : _children.AsReadOnly(); } }

    public event Action<Art, DTopic> changed;

    public bool CheckAcl(ACL acl){
      return (_flags & (int)acl) == (int)acl;
    }
    public Task<DTopic> CreateAsync(string name, string schemaName, JSC.JSValue value) {
      var req = new TopicReq(this, this == _client.root ? ("/" + name) : (this.path + "/" + name), schemaName, value.Defined?value:null);
      DWorkspace.This.AddMsg(req);
      return req.Task;
    }
    public Task<DTopic> GetAsync(string path) {
      DTopic ts;
      if(string.IsNullOrEmpty(path)) {
        ts = this;
      } else if(path[0] == '/') {
        ts = _client.root;
      } else {
        ts = this;
        path = this == _client.root ? ("/" + path) : (this.path + "/" + path);
      }
      var req = new TopicReq(ts, path);
      DWorkspace.This.AddMsg(req);
      return req.Task;
    }
    public Task<bool> SetValue(JSC.JSValue val) {
      var ds = new TopicPublish(this, val);
      DWorkspace.This.AddMsg(ds);
      return ds.Task;
    }
	public void Move(DTopic nParent, string nName) {
	  _client.Move(this.path, nParent.path, nName);
	}
	public void Delete() {
      _client.Delete(this.path);
    }

    public bool TryGetField<T>(string path, out T value) {
      JSC.JSValue cur = _value;
      if(!string.IsNullOrEmpty(path)) {
        var pp = path.Split(FIELDS_SEPARATOR, StringSplitOptions.RemoveEmptyEntries);
        for(int i = 0; i < pp.Length; i++) {
          cur = cur.GetProperty(pp[i]);
          if(!cur.Defined) {
            break;
          }
        }
      }
      if(cur != null && cur.Defined) {
        try {
          value = (T)(((IConvertible)cur).ToType(typeof(T), null));
          return true;
        }
        catch(Exception) {

        }
      }
      value = default(T);
      return false;
    }
    public T GetField<T>(string path) {
      T val;
      TryGetField<T>(path, out val);
      return val;
    }

    private void ValuePublished(JSC.JSValue val) {
      if(!JSC.JSValue.Equals(_value, val)) {
        _value = val;
        ChangedReise(Art.value, this);
      }
    }
    private void ExtractSchema(Task<DTopic> t) {
      if(t != null) {
        if(t.IsFaulted) {
          Log.Warning("ExtractSchema({0}) - {1}", schemaStr, t.Exception.Message);
        } else if(t.IsCompleted) {
          if(this._schemaTopic != t.Result) {
            if(this._schemaTopic != null) {
              this._schemaTopic.changed -= _schemaTopic_PropertyChanged;
            }
            this._schemaTopic = t.Result;
            this._schemaTopic.changed += _schemaTopic_PropertyChanged;
            ChangedReise(Art.schema, this);
          }
        }
      }
    }
    private void _schemaTopic_PropertyChanged(Art art, DTopic child) {
      if(art==Art.value) {
        ChangedReise(Art.schema, this);
      }
    }
    private void ChangedReise(Art art, DTopic src) {
      if(changed != null) {
        changed(art, src);
      }
    }
    private DTopic GetChild(string name, bool create) {
      if(_children == null) {
        if(create) {
          _children = new List<DTopic>();
          _flags |= 16;
        } else {
          return null;
        }
      }
      int min = 0, max = _children.Count - 1, cmp, mid = 0;

      while(min <= max) {
        mid = (min + max) / 2;
        cmp = string.Compare(_children[mid].name, name);
        if(cmp < 0) {
          min = mid + 1;
          mid = min;
        } else if(cmp > 0) {
          max = mid - 1;
          mid = max;
        } else {
          return _children[mid];
        }
      }
      if(create) {
        var t = new DTopic(this, name);
        this._children.Insert(mid, t);
        ChangedReise(Art.addChild, t);
        return t;
      }
      return null;
    }
    private void RemoveChild(DTopic t) {
      if(_children == null) {
        return;
      }
      int min = 0, max = _children.Count - 1, cmp, mid = 0;

      while(min <= max) {
        mid = (min + max) / 2;
        cmp = string.Compare(_children[mid].name, t.name);
        if(cmp < 0) {
          min = mid + 1;
          mid = min;
        } else if(cmp > 0) {
          max = mid - 1;
          mid = max;
        } else {
          _children.RemoveAt(mid);
          ChangedReise(Art.RemoveChild, t);
          break;
        }
      }
      if(!_children.Any()) {
        _children = null;
        _flags &= ~16;
      }
    }

    public override string ToString() {
      return this.fullPath + "<" + this.schemaStr + ">";
    }

    private class TopicReq : INotMsg {
      private DTopic _cur;
      private string _path;
      private bool _create;
      private string _schemaName;
      private JSC.JSValue _value;
      private TaskCompletionSource<DTopic> _tcs;

      public TopicReq(DTopic cur, string path) {
        this._cur = cur;
        this._path = path;
        this._create = false;
        this._tcs = new TaskCompletionSource<DTopic>();
      }
      public TopicReq(DTopic cur, string path, string schemaName, JSC.JSValue value) {
        this._cur = cur;
        this._path = path;
        this._create = true;
        _schemaName = schemaName;
        _value = value;
        this._tcs = new TaskCompletionSource<DTopic>();
      }
      public Task<DTopic> Task { get { return _tcs.Task; } }

      public void Process(DWorkspace ws) {
        int idx1 = _cur.path.Length;
        if(idx1 > 1) {
          idx1++;
        }
        if(_path == null || _path.Length <= _cur.path.Length) {
          if(_cur._value != null) {
            _tcs.SetResult(_cur);
          } else if(_cur._disposed) {
            _tcs.SetResult(null);
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

          next=_cur.GetChild(name, false);
        }
        if(next == null) {
          if(_create) {
			_create=false;
            if(_path.Length <= idx2) {
              _cur._client.Create(_path.Substring(0, idx2), _schemaName, _value, this);
            } else {
              _cur._client.Create(_path.Substring(0, idx2), null, null, this);
            }
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
          DTopic next;
          JSL.Array ca = value as JSL.Array, cc;
          if(ca == null || (int)ca.length != 1) {
            _tcs.SetException(new ApplicationException("TopicReq bad answer:" + (value == null ? string.Empty : string.Join(", ", value))));
            return;
          }
          if(ca[0].IsNull) {
            _cur._disposed = true;
            return;
          }
          if((cc = ca[0].Value as JSL.Array) == null) {
            _tcs.SetException(new ApplicationException("TopicReq bad answer:" + (value == null ? string.Empty : string.Join(", ", value))));
            return;
          }
          string aName, aPath;
          int aFlags;
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
              aName = aPath.Substring(_cur.path.Length == 1 ? 1 : (_cur.path.Length + 1));
              next = _cur.GetChild(aName, true);
            } else {
              next = _cur;
            }
            next._flags = aFlags;
            next.schemaStr = cb[2].Value as string;
            if((int)cb.length == 4) {
              next.ValuePublished(cb[3]);
            }
          }
        } else {
          _tcs.SetException(new ApplicationException("TopicReq failed:" + (value == null ? string.Empty : string.Join(", ", value))));
        }
      }
    }
    private class TopicPublish : INotMsg {
      private TaskCompletionSource<bool> _tcs;
      private DTopic _topic;
      private JSC.JSValue _value;
      private bool _complete;

      public TopicPublish(DTopic t, JSC.JSValue value) {
        _topic = t;
        _value = value;
        _tcs = new TaskCompletionSource<bool>();
      }
      public Task<bool> Task { get { return _tcs.Task; } }

      public void Process(DWorkspace ws) {
        if(!_complete) {
          if(_value == null ? _topic.value != null : _value.Equals(_topic.value)) {
            _tcs.SetResult(true);
          } else {
            _topic._client.Publish(_topic.path, _value, this);
          }
        }
      }
      public void Response(DWorkspace ws, bool success, JSC.JSValue value) {
        if(success) {
          _topic.ValuePublished(this._value);
          _tcs.SetResult(true);
        } else {
          _tcs.SetException(new ApplicationException("TopicSet failed:" + (value == null ? string.Empty : string.Join(", ", value))));
        }
        _complete = true;
      }
    }
    internal class Event : INotMsg {
      private JSL.Array _data;
      public A04Client client;

      public Event(JSL.Array data) {
        this._data = data;
      }
      public void Process(DWorkspace ws) {
        string path;
        if(_data == null || (int)_data.length < 2 || _data[0].ValueType != JSC.JSValueType.Double || _data[1].ValueType != JSC.JSValueType.String || string.IsNullOrEmpty(path = _data[1].Value as string)) {
          Log.Warning("{0} BAD Event - {1}", client.url.GetLeftPart(UriPartial.Authority), _data == null ? "null" : JSL.JSON.stringify(_data, null, "  "));
          return;
        }
        int cmd = (int)_data[0];
        if(cmd != 5 && cmd != 9) {  // 5 - publish, 9 - delete
          Log.Warning("{0} Unknown Event - {1}", client.url.GetLeftPart(UriPartial.Authority), _data == null ? "null" : JSL.JSON.stringify(_data, null, "  "));
          return;
        } else if(cmd == 5) {
          if((int)_data.length != 5 || _data[2].ValueType != JSC.JSValueType.Double) {
            Log.Warning("{0} Event: BAD format - {1}", client.url.GetLeftPart(UriPartial.Authority), _data == null ? "null" : JSL.JSON.stringify(_data, null, "  "));
            return;
          }
        }

        DTopic cur = client.root, next;
        var ns = path.Split(new char[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
        for(int i = 0; i < ns.Length; i++) {
          if((next = cur.GetChild(ns[i], cmd == 5)) == null) {
            break;
          }
          if(i == ns.Length - 1) {
            if(cmd == 5) {
              next._flags = (int)_data[2];
              next.schemaStr = _data[3].Value as string;
              next.ValuePublished(_data[4]);
            } else {
              next._disposed = true;
              cur.RemoveChild(next);
            }
          }
          cur = next;
        }
      }
      public void Response(DWorkspace ws, bool success, JSC.JSValue value) {
      }
    }

    public enum Art{
      value,
      schema,
      addChild,
      RemoveChild,
    }
    [Flags]
    public enum ACL {
      Empty=0,
      Subscribe=1,
      Create=2,
      Update=4,
      Delete=8,
    }

  }
}
