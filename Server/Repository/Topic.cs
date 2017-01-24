﻿using LiteDB;
using NiL.JS.Core;
using JST = NiL.JS.BaseLibrary;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Collections.Concurrent;

namespace X13.Repository {
  public sealed class Topic {
    private static Repo _repo;
    public static Topic root { get; private set; }

    #region Member variables
    private Topic _parent;
    private string _name;
    private string _path;
    private ConcurrentDictionary<string, Topic> _children;
    private List<SubRec> _subRecords;

    private BsonDocument _state;
    private BsonDocument _meta;
    private JSValue _value;
    private bool _saved;

    #endregion Member variables

    private Topic(Topic parent, string name, bool fill) {
      _name = name;
      _parent = parent;
      _value = JSValue.Undefined;
      disposed = false;
      if(parent == null) {
        _path = "/";
      } else if(parent == root) {
        _path = "/" + name;
      } else {
        _path = parent._path + "/" + name;
      }
      if(fill) {
        _meta = new BsonDocument();
        var id = ObjectId.NewObjectId();
        _meta["_id"] = id;
        _meta["p"] = new BsonValue(_path);
      }
    }

    public Topic parent {
      get { return _parent; }
      internal set { _parent = value; }
    }
    public string name {
      get { return _name; }
    }
    public string path { get { return _path; } }
    public bool disposed { get; private set; }
    public bool saved {
      get {
        return _saved;
      }
      set {
        if(_saved != value) {
          _saved = value;
          Perform c;
          if(!_saved) {
            _state = null;
          }
          c = Perform.Create(this, "s", _saved ? new BsonValue(true) : null, null);
          _repo.DoCmd(c, false);
        }
      }
    }
    public Bill all { get { return new Bill(this, true); } }
    public Bill children { get { return new Bill(this, false); } }

    /// <summary> Get item from tree</summary>
    /// <param name="path">relative or absolute path</param>
    /// <param name="create">true - create, false - check</param>
    /// <returns>item or null</returns>
    public Topic Get(string path, bool create = true, Topic prim = null) {
      return Topic.I.Get(this, path, create, prim, false, true);
    }
    public bool Exist(string path) {
      return Topic.I.Get(this, path, false, null, false, false) != null;
    }
    public bool Exist(string path, out Topic topic) {
      return (topic = Topic.I.Get(this, path, false, null, false, false)) != null;
    }
    public void Move(Topic nParent, string nName, Topic prim = null) {
      if(this._parent == null) {
        return;
      }
      if(nParent == null) {
        nParent = this.parent;
      }
      if(string.IsNullOrEmpty(nName)) {
        nName = this.name;
      }
      Topic tmp;
      if(nParent._children == null) {
        lock(nParent) {
          if(nParent._children == null) {
            nParent._children = new ConcurrentDictionary<string, Topic>();
          }
        }
      }
      if(!nParent._children.TryAdd(nName, this)) {
        throw new ArgumentException(this._path + ".Move(" + nParent._path + ", " + nName + ") FAILED");
      }
      if(!_parent._children.TryRemove(this._name, out tmp)) {
        Log.Warning("{0}.Move({1}, {2}) remove FAILED", this._path, nParent._path, nName);
        return;
      }
      _parent = nParent;
      this._name = nName;
      I.UpdatePath(this);
    }
    public void Remove(Topic prim = null) {
      this.disposed = true;
      var c = Perform.Create(this, Perform.Art.remove, prim);
      _repo.DoCmd(c, false);
    }
    public SubRec Subscribe(SubRec.SubMask mask, Action<Perform> func) {
      return Subscribe(mask, null, func);
    }
    public SubRec Subscribe(SubRec.SubMask mask, string prefix, Action<Perform> func) {
      if(func == null) {
        throw new ArgumentNullException(this.path + ".Subscribe(func == NULL, "+mask.ToString()+(prefix==null?string.Empty:", "+prefix)+")");
      }
      SubRec sb;
      if(_subRecords == null) {
        lock(this) {
          if(_subRecords == null) {
            _subRecords = new List<SubRec>();
          }
        }
      }
      lock(_subRecords){
        sb = _subRecords.FirstOrDefault(z => z.func == func && z.setTopic == this && z.mask == mask && ((z.mask & SubRec.SubMask.Field)==SubRec.SubMask.None || z.prefix==prefix));
        if(sb == null) {
          sb = new SubRec(this, func, mask, prefix);
          _subRecords.Add(sb);
        }
      }
      var c = Perform.Create(this, Perform.Art.subscribe, this);
      c.o = sb;
      _repo.DoCmd(c, false);
      return sb;
    }

    public JSValue GetValue() {
      return _value;
    }
    public void SetValue(JSValue val, Topic prim = null) {
      var c = Perform.Create(this, val, prim);
      _repo.DoCmd(c, false);
    }

    public BsonValue GetField(string fPath) {
      return _meta == null ? BsonValue.Null : _meta.Get(fPath);
    }
    public void SetField(string fPath, BsonValue value, Topic prim = null) {
      if(fPath == "_id" || fPath == "p" || fPath == "s") {
        throw new FieldAccessException(fPath);
      }
      var c = Perform.Create(this, fPath, value, prim);
      _repo.DoCmd(c, false);
    }

    #region nested types
    public class Bill : IEnumerable<Topic> {
      public const char delmiter = '/';
      public const string delmiterStr = "/";
      public const string maskAll = "#";
      public const string maskChildren = "+";
      public const string maskParent = "..";
      public static readonly char[] delmiterArr = new char[] { delmiter };
      public static readonly string[] curArr = new string[0];
      public static readonly string[] allArr = new string[] { maskAll };
      public static readonly string[] childrenArr = new string[] { maskChildren };

      private Topic _home;
      private bool _deep;

      public Bill(Topic home, bool deep) {
        _home = home;
        _deep = deep;
      }
      public IEnumerator<Topic> GetEnumerator() {
        if(!_deep) {
          if(_home._children != null) {
            foreach(var t in _home._children.OrderBy(z => z.Key)) {
              yield return t.Value;
            }
          }
          yield break;
        } else {
          var hist = new Stack<Topic>();
          Topic cur;
          hist.Push(_home);
          do {
            cur = hist.Pop();
            yield return cur;
            if(cur._children != null) {
              foreach(var t in cur._children.OrderByDescending(z => z.Key)) {
                hist.Push(t.Value);
              }
            }
          } while(hist.Any());
        }
      }
      //public event Action<SubRec, Perform> changed {
      //  add {
      //    _home.Subscribe(value, _deep ? SubRec.SubMask.All : SubRec.SubMask.Chldren, false);
      //  }
      //  remove {
      //    _home.Unsubscribe(value, _deep ? SubRec.SubMask.All : SubRec.SubMask.Chldren, false);
      //  }
      //}
      System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() {
        return GetEnumerator();
      }
    }
    internal static class I {
      public static void Init(Repo repo) {
        Topic._repo = repo;
        Topic.root = new Topic(null, "/", false);
      }

      public static void Create(BsonDocument obj, BsonDocument state) {
        Topic t = I.Get(Topic.root, obj["p"].AsString, true, null, false, false);
        t._meta = obj;
        t._saved = obj["s"].AsBoolean;
        if(state != null) {
          if(t._saved) {
            t._state = state;
          }
          t._value = Bs2Js(state["v"]);
        }
      }

      public static Topic Get(Topic home, string path, bool create, Topic prim, bool inter, bool fill) {
        if(string.IsNullOrEmpty(path)) {
          return home;
        }
        Topic next;
        if(path[0] == Bill.delmiter) {
          if(path.StartsWith(home._path)) {
            path = path.Substring(home._path.Length);
          } else {
            home = Topic.root;
          }
        }
        var pt = path.Split(Bill.delmiterArr, StringSplitOptions.RemoveEmptyEntries);
        for(int i = 0; i < pt.Length; i++) {
          if(pt[i] == Bill.maskAll || pt[i] == Bill.maskChildren) {
            throw new ArgumentException(string.Format("{0}[{1}] dont allow wildcard", home._path, path));
          }
          if(pt[i] == Bill.maskParent) {
            home = home.parent;
            if(home == null) {
              throw new ArgumentException(string.Format("{0}[{1}] BAD path: excessive nesting", home._path, path));
            }
            continue;
          }
          next = null;
          if(home._children == null) {
            lock(home) {
              if(home._children == null) {
                home._children = new ConcurrentDictionary<string, Topic>();
              }
            }
          } else if(home._children.TryGetValue(pt[i], out next) && next.disposed) {
            next = null;
          }
          if(next == null) {
            if(create) {
              if(home._children.TryGetValue(pt[i], out next)) {
                home = next;
              } else {
                next = new Topic(home, pt[i], fill);
                home._children[pt[i]] = next;
                var c = Perform.Create(next, Perform.Art.create, prim);
                _repo.DoCmd(c, inter);
              }
            } else {
              return null;
            }
          }
          home = next;
        }
        return home;
      }
      public static void SetValue(Topic t, JSValue val) {
        t._value = val;
        if(t._saved) {
          if(t._state == null) {
            t._state = new BsonDocument();
            t._state["_id"] = t._meta["_id"];
          }
          t._state["v"] = Js2Bs(val);
        }
      }
      public static void SetField(Topic t, string fPath, BsonValue bsonValue) {
        t._meta.Set(fPath, bsonValue);
      }
      public static void UpdatePath(Topic t) {
        t._path = t.parent == root ? "/" + t._name : t.parent._path + "/" + t._name;
        var c = Perform.Create(t, "p", new BsonValue(t._path), null);
        _repo.DoCmd(c, false);
        if(t._children != null) {
          foreach(var ch in t._children) {
            UpdatePath(ch.Value);
          }
        }
      }
      public static void Remove(Topic t) {
        t.disposed = true;
        if(t._parent != null) {
          Topic tmp;
          t._parent._children.TryRemove(t._name, out tmp);
        }
      }
      public static void ReqData(Topic t, out BsonDocument obj, out BsonDocument state) {
        obj = t._meta;
        state = t._state;
      }
      public static void Publish(Perform cmd) {
        SubRec sb;
        Topic t = cmd.src;
        string tmp_s;

        if((cmd.art == Perform.Art.subscribe || cmd.art == Perform.Art.subAck) && (sb = cmd.o as SubRec) != null) {
          try {
            sb.func(cmd);
          }
          catch(Exception ex) {
            Log.Warning("{0}.{1}({2}) - {3}", sb.func.Method.DeclaringType.Name, sb.func.Method.Name, cmd.ToString(), ex.ToString());
          }
        } else {
          if(t._subRecords != null) {
            for(int i = 0; i < t._subRecords.Count; i++) {
              sb = t._subRecords[i];
              if(((sb.mask & SubRec.SubMask.OnceOrAll) != SubRec.SubMask.None || ((sb.mask & SubRec.SubMask.Chldren) == SubRec.SubMask.Chldren && sb.setTopic == t.parent))
                  && (cmd.art!=Perform.Art.changed || (sb.mask & SubRec.SubMask.Value)==SubRec.SubMask.Value) 
                  && (cmd.art!=Perform.Art.changedField || ((sb.mask & SubRec.SubMask.Field)==SubRec.SubMask.Field && (tmp_s=cmd.o as string)!=null && tmp_s.StartsWith(sb.prefix))) ) {
                try {
                  sb.func(cmd);
                }
                catch(Exception ex) {
                  Log.Warning("{0}.{1}({2}) - {3}", sb.func.Method.DeclaringType.Name, sb.func.Method.Name, cmd.ToString(), ex.ToString());
                }
              }
            }
          }
        }

      }
      public static void SubscribeByCreation(Topic t_c) {
        Topic p;
        if((p = t_c.parent) != null) {
          if(p._subRecords != null) {
            lock(p._subRecords) {
              foreach(var st in p._subRecords.Where(z => z.setTopic == p && (z.mask & SubRec.SubMask.Chldren) == SubRec.SubMask.Chldren)) {
                Subscribe(t_c, st);
              }
            }
          }
          while(p != null) {
            if(p._subRecords != null) {
              lock(p._subRecords) {
                foreach(var st in p._subRecords.Where(z => (z.mask & SubRec.SubMask.All) == SubRec.SubMask.All)) {
                  Subscribe(t_c, st);
                }
              }
            }
            p = p.parent;
          }
        }
      }

      public static void Subscribe(Topic t, SubRec sr) {

        if(t._subRecords == null) {
          lock(t) {
            if(t._subRecords == null) {
              t._subRecords = new List<SubRec>();
            }
          }
        }
        lock(t._subRecords) {
          if(!t._subRecords.Any(z => z.func == sr.func && z.setTopic == sr.setTopic && z.mask == sr.mask && ((z.mask & SubRec.SubMask.Field)==SubRec.SubMask.None || z.prefix==sr.prefix))) {
            t._subRecords.Add(sr);
          }
        }
      }
      public static bool Unsubscribe(Topic t, SubRec sr) {
        if(RemoveSubscripton(t, sr)) {
          var c = Perform.Create(t, Perform.Art.unsubscribe, null);
          c.o = sr;
          _repo.DoCmd(c, false);
          return true;
        }
        return false;
      }
      public static bool RemoveSubscripton(Topic t, SubRec sr) {
        if(t._subRecords == null) {
          return false;
        }
        bool fl;
        lock(t._subRecords) {
          fl = t._subRecords.Remove(sr);
        }
        return fl;
      }


      private static BsonValue Js2Bs(JSValue val) {
        if(val == null) {
          return BsonValue.Null;
        }
        switch(val.ValueType) {
        case JSValueType.Boolean:
          return new BsonValue((bool)val);
        case JSValueType.Date: {
            var jsd = val.Value as JST.Date;
            if(jsd != null) {
              return new BsonValue(jsd.ToDateTime().ToUniversalTime());
            }
            return BsonValue.Null;
          }
        case JSValueType.Double:
          return new BsonValue((double)val);
        case JSValueType.Integer:
          return new BsonValue((int)val);
        case JSValueType.String:
          return new BsonValue(val.ToString());
        case JSValueType.Object:
          if(val.IsNull) {
            return BsonValue.Null;
          }
          var arr = val as JST.Array;
          if(arr != null) {
            var r = new BsonArray();
            int i;
            foreach(var f in arr) {
              if(int.TryParse(f.Key, out i)) {
                while(i >= r.Count()) { r.Add(BsonValue.Null); }
                r[i] = Js2Bs(f.Value);
              }
            }
            return r;
          }
          var obj = val as JSObject;
          if(obj != null) {
            var r = new BsonDocument();
            foreach(var f in obj) {
              r[f.Key] = Js2Bs(f.Value);
            }
            return r;
          }
          throw new NotImplementedException("js2Bs(" + val.ToString() + ")");
        default:
          throw new NotImplementedException("js2Bs(" + val.ValueType.ToString() + ")");
        }
      }
      private static JSValue Bs2Js(BsonValue val) {
        if(val == null) {
          return JSValue.Undefined;
        }
        switch(val.Type) {
        case BsonType.Array: {
            var arr = val.AsArray;
            var r = new JST.Array(arr.Count);
            for(int i = 0; i < arr.Count; i++) {
              if(!arr[i].IsNull) {
                r[i] = Bs2Js(arr[i]);
              }
            }
            return r;
          }
        case BsonType.Boolean:
          return new JST.Boolean(val.AsBoolean);
        case BsonType.DateTime:
          return JSValue.Marshal(val.AsDateTime.ToLocalTime());
        case BsonType.Document: {
            var r = JSObject.CreateObject();
            var o = val.AsDocument;
            foreach(var i in o) {
              r[i.Key] = Bs2Js(i.Value);
            }
            return r;
          }
        case BsonType.Double:
          return new JST.Number(val.AsDouble);
        case BsonType.Int32:
          return new JST.Number(val.AsInt32);
        case BsonType.Int64:
          return new JST.Number(val.AsInt64);
        case BsonType.Null:
          return JSValue.Null;
        case BsonType.String:
          return new JST.String(val.AsString);
        }
        throw new NotImplementedException("Bs2Js(" + val.Type.ToString() + ")");
      }
    }
    #endregion nested types

    public override string ToString() {
      return _path;
    }



  }
}