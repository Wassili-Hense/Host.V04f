﻿///<remarks>This file is part of the <see cref="https://github.com/X13home">X13.Home</see> project.<remarks>
using JSC = NiL.JS.Core;
using JSL = NiL.JS.BaseLibrary;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections.ObjectModel;

namespace X13.Data {
  public class DTopic {
    private static char[] FIELDS_SEPARATOR = new char[] { '.' };
    private static char[] PATH_SEPARATOR = new char[] { '/' };

    internal readonly Client Connection;

    private bool _disposed;
    private List<DTopic> _children;
    private JSC.JSValue _value;
    private JSC.JSValue _manifest;
    private DTopic _typeTopic;
    private TopicReq _req;

    private DTopic(DTopic parent, string name) {
      this.parent = parent;
      this.Connection = this.parent.Connection;
      this.name = name;
      this.path = this.parent == Connection.root ? ("/" + name) : (this.parent.path + "/" + name);
    }
    internal DTopic(Client cl) {
      Connection = cl;
      this.name = Connection.ToString();
      this.path = "/";
    }

    public virtual string name { get; protected set; }
    public string path { get; private set; }
    public string fullPath { get { return Connection.ToString() + this.path; } }
    public DTopic parent { get; private set; }
    public JSC.JSValue value { get { return _value; } }
    public JSC.JSValue type { get { return _manifest; } }  // TODO: rename
    public ReadOnlyCollection<DTopic> children { get { return _children == null ? null : _children.AsReadOnly(); } }

    public Task<DTopic> CreateAsync(string name, JSC.JSValue st, JSC.JSValue manifest) {
      var req = new TopicReq(this, this == Connection.root ? ("/" + name) : (this.path + "/" + name), st, manifest);
      App.PostMsg(req);
      return req.Task;
    }
    public Task<DTopic> GetAsync(string p) {
      DTopic ts;
      if(string.IsNullOrEmpty(p)) {
        ts = this;
      } else if(p[0] == '/') {
        ts = Connection.root;
      } else {
        ts = this;
        p = this == Connection.root ? ("/" + p) : (this.path + "/" + p);
      }
      var req = new TopicReq(ts, p);
      App.PostMsg(req);
      return req.Task;

    }
    public Task<bool> SetValue(JSC.JSValue val) {
      var ds = new TopicPublish(this, val);
      App.PostMsg(ds);
      return ds.Task;
    }
    public Task<JSC.JSValue> SetField(string fPath, JSC.JSValue val) {
      var ds = new TopicField(this, fPath, val);
      App.PostMsg(ds);
      return ds.Task;
    }

    public void Move(DTopic nParent, string nName) {
      Connection.SendReq(10, null, this.path, nParent.path, nName);
    }
    public void Delete() {
      Connection.SendReq(12, null, this.path);
    }

    public event Action<Art, DTopic> changed;

    private void ValuePublished(JSC.JSValue value) {
      _value = value;
      ChangedReise(Art.value, this);
    }
    private void ManifestPublished(JSC.JSValue manifest) {
      _manifest = manifest;
      bool send = true;
      if(_manifest.ValueType == JSC.JSValueType.Object && _manifest.Value != null) {
        var tt = _manifest["type"];
        if(tt.ValueType == JSC.JSValueType.String && tt.Value != null) {
          this.GetAsync("/$YS/TYPES/" + (tt.Value as string)).ContinueWith(TypeLoaded);
          send = false;
        }
      }
      if(send) {
        ChangedReise(Art.type, this);
      }
    }
    private void TypeLoaded(Task<DTopic> td) {
      if(td.IsCompleted && !td.IsFaulted && td.Result != null) {
        _typeTopic = td.Result;
        _typeTopic.changed += _typeTopic_changed;
      }
      _typeTopic_changed(Art.value, _typeTopic);
    }
    private void _typeTopic_changed(DTopic.Art art, DTopic t) {
      if(art == Art.value) {
        ProtoDeep(_manifest, (_typeTopic == null || _typeTopic.value.ValueType!=JSC.JSValueType.Object) ? null : _typeTopic.value.ToObject());
        ChangedReise(Art.type, this);
      }
    }
    private void ProtoDeep(JSC.JSValue m, JSC.JSObject p) {
      if(m.ValueType >= JSC.JSValueType.Object && m.Value != null) {
        m.__proto__ = p;
        var o = m.ToObject();
        JSC.JSObject p_c;
        JSC.JSValue pv_c;
        foreach(var kv in o) {
          if(p != null && (pv_c = p[kv.Key]).ValueType == JSC.JSValueType.Object) {
            p_c = pv_c.ToObject();
          } else {
            p_c = null;
          }
          ProtoDeep(kv.Value, p_c);
        }
      }
    }
    private void ChangedReise(Art art, DTopic src) {
      if(changed != null && App.mainWindow != null) {
        App.mainWindow.Dispatcher.BeginInvoke(changed, System.Windows.Threading.DispatcherPriority.DataBind, art, src);
      }
    }
    private DTopic GetChild(string cName, bool create) {
      if(_children == null) {
        if(create) {
          _children = new List<DTopic>();
        } else {
          return null;
        }
      }
      int cmp, mid;
      for(mid = _children.Count - 1; mid >= 0; mid--) {
        cmp = string.Compare(_children[mid].name, cName);
        if(cmp == 0) {
          return _children[mid];
        }
        if(cmp < 0) {
          break;
        }
      }

      if(create) {
        var t = new DTopic(this, cName);
        this._children.Insert(mid + 1, t);
        ChangedReise(Art.addChild, t);
        return t;
      }
      return null;
    }
    private void SetChild(DTopic t) {
      if(_children == null) {
        _children = new List<DTopic>();
      }
      int cmp, mid;
      for(mid = _children.Count - 1; mid >= 0; mid--) {
        cmp = string.Compare(_children[mid].name, t.name);
        if(cmp == 0) {
          _children[mid] = t;
          return;
        }
        if(cmp < 0) {
          break;
        }
      }
      this._children.Insert(mid + 1, t);
    }
    private void UpdatePath() {
      this.path = this.parent == Connection.root ? ("/" + name) : (this.parent.path + "/" + name);
      if(_children != null) {
        foreach(var c in _children) {
          c.parent = this;
          c.UpdatePath();
        }
      }
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
      }
    }

    public override string ToString() {
      return this.fullPath;
    }

    private class TopicReq : INotMsg {
      private DTopic _cur;
      private string _path;
      private bool _create;
      private JSC.JSValue _state;
      private JSC.JSValue _manifest;
      private TaskCompletionSource<DTopic> _tcs;
      private List<TopicReq> _reqs;

      public TopicReq(DTopic cur, string path) {
        this._cur = cur;
        this._path = path;
        this._create = false;
        this._tcs = new TaskCompletionSource<DTopic>();
      }
      public TopicReq(DTopic cur, string path, JSC.JSValue st, JSC.JSValue manifest) {
        this._cur = cur;
        this._path = path;
        this._create = true;
        this._state = st;
        this._manifest = manifest;
        this._tcs = new TaskCompletionSource<DTopic>();
      }
      public Task<DTopic> Task { get { return _tcs.Task; } }

      public void Process() {
        int idx1 = _cur.path.Length;
        if(idx1 > 1) {
          idx1++;
        }
        if(_path == null || _path.Length <= _cur.path.Length) {
          if(_cur._disposed) {
            _tcs.SetResult(null);
            lock(_cur) {
              _cur._req = null;
              if(this._reqs != null) {
                foreach(var r in _reqs) {
                  App.PostMsg(r);
                }
              }
            }
          } else if(_cur._value != null) {
            _tcs.SetResult(_cur);
            lock(_cur) {
              _cur._req = null;
              if(this._reqs != null) {
                foreach(var r in _reqs) {
                  App.PostMsg(r);
                }
              }
            }
          } else {
            lock(_cur) {
              if(_cur._req != null && _cur._req != this) {
                if(_cur._req._reqs == null) {
                  _cur._req._reqs = new List<TopicReq>();
                }
                _cur._req._reqs.Add(this);
                return;
              } else {
                _cur._req = this;
              }
            }
            _cur.Connection.SendReq(4, this, _cur.path, 3);
          }
          return;
        }
        DTopic next = null;
        int idx2 = _path.IndexOf('/', idx1);
        if(idx2 < 0) {
          idx2 = _path.Length;
        }
        string name = _path.Substring(idx1, idx2 - idx1);

        if(_cur._children == null && _cur._value == null) {
          lock(_cur) {
            if(_cur._req != null && _cur._req != this) {
              if(_cur._req._reqs == null) {
                _cur._req._reqs = new List<TopicReq>();
              }
              _cur._req._reqs.Add(this);
              return;
            } else {
              _cur._req = this;
            }
          }
          _cur.Connection.SendReq(4, this, _cur.path, 3);
          return;
        }
        next = _cur.GetChild(name, false);
        if(next == null) {
          if(_create) {
            _create = false;
            if(_path.Length <= idx2 && _state!=null) {
              _cur.Connection.SendReq(8, this, _path.Substring(0, idx2), _state, _manifest);
            } else {
              _cur.Connection.SendReq(8, this, _path.Substring(0, idx2));
            }
          } else {
            _tcs.SetResult(null);
            lock(_cur) {
              _cur._req = null;
              if(this._reqs != null) {
                foreach(var r in _reqs) {
                  App.PostMsg(r);
                }
              }
            }
          }
          return;
        }
        _cur = next;
        App.PostMsg(this);
      }
      public void Response(bool success, JSC.JSValue value) {
        if(success) {   // value == null after connect
          if(value != null && (value.ValueType != JSC.JSValueType.Boolean || !((bool)value))) {
            _cur._disposed = true;
          }
        } else {
          _tcs.SetException(new ApplicationException((value == null ? "TopicReqError" : value.ToString())));
        }
      }

      public override string ToString() {
        return "TopicReq(" + _cur.path + ")";
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

      public void Process() {
        if(!_complete) {
          if(_value == null ? _topic.value != null : _value.Equals(_topic.value)) {
            _tcs.SetResult(true);
          } else {
            _topic.Connection.SendReq(6, this, _topic.path, _value);
          }
        }
      }
      public void Response(bool success, JSC.JSValue value) {
        if(success) {
          _topic.ValuePublished(this._value);
          _tcs.SetResult(true);
        } else {
          _tcs.SetException(new ApplicationException(value == null ? "TopicSetError" : value.ToString()));
        }
        _complete = true;
      }
    }
    private class TopicField : INotMsg {
      private TaskCompletionSource<JSC.JSValue> _tcs;
      private DTopic _topic;
      private string _fPath;
      private JSC.JSValue _value;
      private bool _complete;

      public TopicField(DTopic t, string fPath, JSC.JSValue value) {
        _topic = t;
        _fPath = fPath;
        _value = value;
        _tcs = new TaskCompletionSource<JSC.JSValue>();
      }
      public Task<JSC.JSValue> Task { get { return _tcs.Task; } }

      public void Process() {
        if(!_complete) {
          _topic.Connection.SendReq(14, this, _topic.path, _fPath, _value);
        }
      }
      public void Response(bool success, JSC.JSValue value) {
        if(success) {
          _tcs.SetResult(true);
        } else {
          _tcs.SetException(new ApplicationException((value == null ? "FieldSetError" : value.ToString())));
        }
        _complete = true;
      }
    }
    internal class ClientEvent : INotMsg {
      private DTopic _root;
      private string _path;
      private int _cmd;
      private JSC.JSValue _p1;
      private JSC.JSValue _p2;

      public ClientEvent(DTopic root, string path, int cmd, JSC.JSValue p1, JSC.JSValue p2) {
        if(root == null) {
          throw new ArgumentNullException("root");
        }
        if(path == null) {
          throw new ArgumentNullException("path");
        }

        _root = root;
        _path = path;
        _cmd = cmd;
        _p1 = p1;
        _p2 = p2;
      }
      public void Process() {
        var ps = _path.Split(PATH_SEPARATOR, StringSplitOptions.RemoveEmptyEntries);
        DTopic cur = _root, next;
        bool noCreation = (_cmd == 12 && _p1 == null && _p2 == null) || (_cmd == 10 && _p1 != null && _p1.ValueType == JSC.JSValueType.String && _p2 != null && _p2.ValueType == JSC.JSValueType.String);
        for(int i = 0; i < ps.Length; i++) {
          next = cur.GetChild(ps[i], !noCreation);
          if(next == null) {  // Topic not exist
            return;
          }
          cur = next;
        }
        if(noCreation) {
          if(_cmd == 10) {  // move
            DTopic parent = _root;
            ps = (_p1.Value as string).Split(PATH_SEPARATOR, StringSplitOptions.RemoveEmptyEntries);
            for(int i = 0; i < ps.Length; i++) {
              next = parent.GetChild(ps[i], false);
              if(next == null) {  // Topic not exist
                return;
              }
              parent = next;
            }
            next = new DTopic(parent, _p2.Value as string);
            next._children = cur._children;
            next._value = cur._value;
            next._manifest = cur._manifest;
            next._typeTopic = cur._typeTopic;

            cur.parent.ChangedReise(Art.RemoveChild, cur);
            cur.parent._children.Remove(cur);

            parent.SetChild(next);
            next.UpdatePath();
            parent.ChangedReise(Art.addChild, next);

          } else if(_cmd == 12) {  // delete
            cur._disposed = true;
            var parent = cur.parent;
            if(parent != null) {
              parent.RemoveChild(cur);
              parent.ChangedReise(Art.RemoveChild, cur);
            }
          }
        } else {
          if(_p1 != null) {
            cur.ValuePublished(_p1);
          }
          if(_p2 != null) {
            cur.ManifestPublished(_p2);
          }
        }
      }
      public void Response(bool success, JSC.JSValue value) {
        throw new NotImplementedException();
      }
    }

    public enum Art {
      value,
      type,
      addChild,
      RemoveChild,
    }

  }
}
