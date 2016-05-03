﻿using System;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using JSC = NiL.JS.Core;
using JSL = NiL.JS.BaseLibrary;

namespace X13.Data {
  public class DTopic : NPC_UI {
    private const string valueString = "value";
    private const string itemsString = "children";
    private const string schemaString = "schema";

    private A04Client _client;
    private int _flags;  //  1 - acl.subscribe, 2 - acl.create, 4 - acl.change, 8 - acl.remove, 16 - hat children
    private JSC.JSValue _value;
    private Schema _schemaOriginal;
    private DTopic _schemaTopic;
    private int _schemaRequsted;

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
    public Task<DTopic> GetAsync(string path, bool create) {
      var req = new TopicReq((!string.IsNullOrEmpty(path) && path[0] == '/') ? _client.root : this, path, create);
      DWorkspace.This.AddMsg(req);
      return req.Task;
    }
    public Schema schema {
      get {
        if(System.Threading.Interlocked.Exchange(ref _schemaRequsted, 1)==0) {
          var task = _client.root.GetAsync("/etc/schema/" + this.schemaStr, false);
          task.ContinueWith(ExtractSchema);
          return null;
        } else {
          return _schemaTopic == null ? null : _schemaTopic._schemaOriginal;
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
        if(_value == null && parent!=null) {
          var req = new TopicReq(parent, this.name, false);
          DWorkspace.This.AddMsg(req);
          return JSC.JSValue.NotExists;
        }
        return _value;
      }
    }
    public Task<bool> SetValue(JSC.JSValue val) {
      var ds = new TopicPublish(this, val);
      DWorkspace.This.AddMsg(ds);
      return ds.Task;
    }
    public DChildren children { get; private set; }

    private void ValuePublished(JSC.JSValue val) {
      if(!JSC.JSValue.Equals(_value, val)) {
        _value = val;
        if(schemaStr == "schema" && _value != null && _value.ValueType == JSC.JSValueType.Object && _value.Value != null) {
          this._schemaOriginal = new Schema(_value);
        }
        PropertyChangedReise(valueString);
      }
    }
    private void ExtractSchema(Task<DTopic> t) {
      if(t != null) {
        if(t.IsFaulted) {
          Log.Warning("ExtractSchema({0}) - {1}", schemaStr, t.Exception.Message);
        } else if(t.IsCompleted) {
          if(this._schemaTopic != t.Result) {
            if(this._schemaTopic != null) {
              this._schemaTopic.PropertyChanged -= _schemaTopic_PropertyChanged;
            }
            this._schemaTopic = t.Result;
            this._schemaTopic.PropertyChanged += _schemaTopic_PropertyChanged;
            PropertyChangedReise(schemaString);
            PropertyChangedReise("view");
          }
        }
      }
    }
    private void _schemaTopic_PropertyChanged(object sender, PropertyChangedEventArgs e) {
      if(e.PropertyName == "value") {
        PropertyChangedReise(schemaString);
      }
    }

    public override string ToString() {
      return this.fullPath + "<" + this.schemaStr + ">";
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
        if(idx1 > 1) {
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
          if(_cur.children == null) {
            _cur._client.Request(_cur.path, 2, this);
            return;
          }

          if(!_cur.children.TryGetValue(name, out next)) {
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
          bool childrenPC = false;
          DTopic next;
          JSL.Array ca = value as JSL.Array, cc;
          if(ca == null || (int)ca.length != 1 || (cc = ca[0].Value as JSL.Array) == null) {
            _tcs.SetException(new ApplicationException("TopicReq bad answer:" + (value == null ? string.Empty : string.Join(", ", value))));
            return;
          }
          string aName, aPath;
          int aFlags;
          if(_cur.children == null) {
            _cur.children = new DChildren();
            childrenPC = true;
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
              aName = aPath.Substring(_cur.path.Length == 1 ? 1 : (_cur.path.Length + 1));
              next = new DTopic(_cur, aName);
              _cur.children.AddItem(next);
            } else {
              next = _cur;
            }
            next._flags = aFlags;
            next.schemaStr = cb[2].Value as string;
            if((int)cb.length == 4) {
              next.ValuePublished(cb[3]);
            }
          }
          if(childrenPC) {
            _cur.PropertyChangedReise(DTopic.itemsString);
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
          _topic._client.Publish(_topic.path, _value, this);
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
  }
}
