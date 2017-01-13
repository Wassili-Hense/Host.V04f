using LiteDB;
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

    #region Fields
    private Topic _parent;
    private string _name;
    private string _path;
    private ConcurrentDictionary<string, Topic> _children;
    private BsonDocument _state;
    private BsonDocument _meta;
    private JSValue _value;

    #endregion Fields

    private Topic(Topic parent, string name) {
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
    }

    public Topic parent {
      get { return _parent; }
      internal set { _parent = value; }
    }
    public string name {
      get { return _name; }
    }
    public string path { get { return _meta != null ? _meta["path"].AsString : _path; } }
    public bool disposed { get; private set; }
    public Bill all { get { return new Bill(this, true); } }
    public Bill children { get { return new Bill(this, false); } }

    /// <summary> Get item from tree</summary>
    /// <param name="path">relative or absolute path</param>
    /// <param name="create">true - create, false - check</param>
    /// <returns>item or null</returns>
    public Topic Get(string path, bool create = true, Topic prim = null) {
      return Topic.I.Get(this, path, create, prim, false);
    }
    public bool Exist(string path) {
      return Topic.I.Get(this, path, false, null, false) != null;
    }
    public bool Exist(string path, out Topic topic) {
      return (topic = Topic.I.Get(this, path, false, null, false)) != null;
    }
    public void Remove(Topic prim = null) {
      this.disposed = true;
      var c = Perform.Create(this, Perform.Art.remove, prim);
      _repo.DoCmd(c, false);
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
        Topic.root = new Topic(null, "/");
      }

      public static void Load(Topic t, BsonDocument state, BsonDocument meta) {
        t._state = state;
        t._meta = meta;
      }
      public static Topic Get(Topic home, string path, bool create, Topic prim, bool inter) {
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
                next = new Topic(home, pt[i]);
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
      }
      public static void Remove(Topic t) {
        t.disposed = true;
        if(t._parent != null) {
          Topic tmp;
          t._parent._children.TryRemove(t._name, out tmp);
        }
      }
    }
    #endregion nested types

    public override string ToString() {
      return _path;
    }


  }
}
