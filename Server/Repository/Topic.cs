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
    #region static internal
    private static Repo _repo;
    internal static void Init(Repo repo) {
      _repo = repo;
      root = new Topic(null, "/");
    }
    #endregion static internal
    public static Topic root { get; private set; }

    #region exemplar internal
    private Topic _parent;
    private string _name;
    private string _path;
    private ConcurrentDictionary<string, Topic> _children;
    private BsonDocument _state;
    private BsonDocument _meta;
    private JSValue _value;

    private Topic(Topic parent, string name) {
      _name = name;
      _parent = parent;
      _value = JSValue.Undefined;

      if(parent == null) {
        _path = "/";
      } else if(parent == root) {
        _path = "/" + name;
      } else {
        _path = parent._path + "/" + name;
      }
    }
    internal void Load(BsonDocument state, BsonDocument meta) {
      _state = state;
      _meta = meta;
    }

    internal Topic GetI(string path, bool create, Topic prim, bool inter) {
      if(string.IsNullOrEmpty(path)) {
        return this;
      }
      Topic home = this, next;
      if(path[0] == Bill.delmiter) {
        if(path.StartsWith(this._path)) {
          path = path.Substring(this._path.Length);
        } else {
          home = Topic.root;
        }
      }
      var pt = path.Split(Bill.delmiterArr, StringSplitOptions.RemoveEmptyEntries);
      for(int i = 0; i < pt.Length; i++) {
        if(pt[i] == Bill.maskAll || pt[i] == Bill.maskChildren) {
          throw new ArgumentException(string.Format("{0}[{1}] dont allow wildcard", this._path, path));
        }
        if(pt[i] == Bill.maskParent) {
          home = home.parent;
          if(home == null) {
            throw new ArgumentException(string.Format("{0}[{1}] BAD path: excessive nesting", this._path, path));
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
        } else if(home._children.TryGetValue(pt[i], out next)) {
          home = next;
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

    #endregion exemplar internal

    public Topic parent {
      get { return _parent; }
      internal set { _parent = value; }
    }
    public string name {
      get { return _name; }
    }
    public string path { get { return _meta != null ? _meta["path"].AsString : _path; } }
    public Bill all { get { return new Bill(this, true); } }
    public Bill children { get { return new Bill(this, false); } }

    /// <summary> Get item from tree</summary>
    /// <param name="path">relative or absolute path</param>
    /// <param name="create">true - create, false - check</param>
    /// <returns>item or null</returns>
    public Topic Get(string path, bool create = true, Topic prim = null) {
      return GetI(path, create, prim, false);
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

    #endregion nested types

    public override string ToString() {
      return _path;
    }
  }
}
