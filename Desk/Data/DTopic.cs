﻿///<remarks>This file is part of the <see cref="https://github.com/X13home">X13.Home</see> project.<remarks>
using JSC = NiL.JS.Core;
using JSL = NiL.JS.BaseLibrary;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace X13.Data {
  internal class DTopic {
    private static char[] FIELDS_SEPARATOR = new char[] { '.' };
    private Client _client;

    private int _flags;  //  16 - hat children
    private bool _disposed;
    private List<DTopic> _children;
    private JSC.JSValue _value;
    private JSC.JSValue _meta;

    private DTopic(DTopic parent, string name) {
      this.parent = parent;
      this._client = this.parent._client;
      this.name = name;
      this.path = this.parent == _client.root ? ("/" + name) : (this.parent.path + "/" + name);
    }
    internal DTopic(Client cl) {
      _client = cl;
      this.name = _client.ToString();
      this.path = "/";
    }

    public virtual string name { get; protected set; }
    public string path { get; private set; }
    public string fullPath { get { return _client.ToString() + this.path; } }
    public DTopic parent { get; private set; }

    public Task<DTopic> GetAsync(string p) {
      DTopic ts;
      if(string.IsNullOrEmpty(p)) {
        ts = this;
      } else if(p[0] == '/') {
        ts = _client.root;
      } else {
        ts = this;
        p = this == _client.root ? ("/" + p) : (this.path + "/" + p);
      }
      var req = new TopicReq(ts, p);
      App.Workspace.AddMsg(req);
      return req.Task;

    }

    public event Action<Art, DTopic> changed;

    private void ValuePublished(JSC.JSValue value) {
      _value = value;
    }
    private void MetaPublished(JSC.JSValue meta) {
      _meta = meta;
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


    private class TopicReq : INotMsg {
      private DTopic _cur;
      private string _path;
      private bool _create;
      private string _typeName;
      private JSC.JSValue _value;
      private TaskCompletionSource<DTopic> _tcs;

      public TopicReq(DTopic cur, string path) {
        this._cur = cur;
        this._path = path;
        this._create = false;
        this._tcs = new TaskCompletionSource<DTopic>();
      }
      public TopicReq(DTopic cur, string path, string typeName, JSC.JSValue value) {
        this._cur = cur;
        this._path = path;
        this._create = true;
        _typeName = typeName;
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

          next = _cur.GetChild(name, false);
        }
        if(next == null) {
          if(_create) {
            _create = false;
            if(_path.Length <= idx2) {
              _cur._client.Create(_path.Substring(0, idx2), _typeName, _value, this);
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
          JSL.Array ca = value.Value as JSL.Array;
          if(ca == null || (int)ca.length != 1) {
            _tcs.SetException(new ApplicationException("TopicReq bad answer:" + (value == null ? string.Empty : string.Join(", ", value))));
            return;
          }
          if(ca[0].IsNull) {
            _cur._disposed = true;
            return;
          }
          string aName, aPath;
          int aFlags;
          foreach(var cb in ca.Select(z => z.Value.Value as JSL.Array)) {
            if(cb == null || (int)cb.length < 2 || cb[0].ValueType != JSC.JSValueType.String || !cb[1].IsNumber) {
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
            if((int)cb.length > 2) {
              next.ValuePublished(cb[2]);
              if((int)cb.length == 4) {
                next.MetaPublished(cb[3]);
              }
            }
          }
        } else {
          _tcs.SetException(new ApplicationException("TopicReq failed:" + (value == null ? string.Empty : string.Join(", ", value))));
        }
      }
    }
    public enum Art {
      value,
      addChild,
      RemoveChild,
    }
  }
}
