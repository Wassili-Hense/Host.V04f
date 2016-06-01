///<remarks>This file is part of the <see cref="https://github.com/X13home">X13.Home</see> project.<remarks>
using NiL.JS.Core;
using JST = NiL.JS.BaseLibrary;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace X13.PLC {
  public sealed class Topic : IComparable<Topic> {
    public static readonly Topic root;

    static Topic() {
      root = new Topic(null, "/");
    }
    #region variables
    private Topic _parent;
    private string _name;
    private string _path;
    /// <summary>[0] - saved, [1] - local, [2] - disposed, [3] - disposed fin., [4] - config </summary>
    private BitArray _flags;
    internal ConcurrentDictionary<string, Topic> _children;
    internal List<SubRec> _subRecords;
    internal string _json;
    internal JSValue _value;
    #endregion variables

    private Topic(Topic parent, string name) {
      _flags = new System.Collections.BitArray(5);
      _flags[0] = true;  // saved
      _name = name;
      _parent = parent;
      _value = JSValue.Undefined;

      if(parent == null) {
        _path = "/";
      } else if(parent == root) {
        _path = "/" + name;
      } else {
        _path = parent.path + "/" + name;
        _flags[1] = parent.local;
      }
    }

    public Topic parent {
      get { return _parent; }
      internal set { _parent = value; }
    }
    public string name {
      get { return _name; }
    }
    public string path {
      get { return _path; }
      internal set { _path = value; }
    }
    public Type vType {
      get {
        switch(_value.ValueType) {
        case JSValueType.NotExists:
        case JSValueType.NotExistsInObject:
        case JSValueType.Undefined:
          return null;
        case JSValueType.Boolean:
          return typeof(bool);
        case JSValueType.Integer:
          return typeof(long);
        case JSValueType.Double:
          return typeof(double);
        case JSValueType.Date:
          return typeof(DateTime);
        case JSValueType.String:
          return typeof(string);
        case JSValueType.Object:
          return _value.Value.GetType();
        }
        return null;
      }
    }
    public string schema {
      get {
        Topic dr;
        string sh;
        if(this.Exist("$schema", out dr) && !string.IsNullOrWhiteSpace(sh = dr.As<string>())) {
          return sh;
        }
        switch(_value.ValueType) {
        case JSValueType.NotExists:
        case JSValueType.NotExistsInObject:
        case JSValueType.Undefined:
          return "Null";
        case JSValueType.Boolean:
          return "Boolean";
        case JSValueType.Integer:
          return "Integer";
        case JSValueType.Double:
          return "Double";
        case JSValueType.Date:
          return "Date";
        case JSValueType.String:
          return "String";
        case JSValueType.Object:
          if(_value == null) {
            return "Null";
          }
          JSValue drf = _value.GetProperty("$schema", PropertyScope.Сommon);
          if(drf == null || !drf.Exists || string.IsNullOrWhiteSpace(sh = drf.ToString())) {
            return "Object";
          }
          return drf.ToString();
        }
        return null;
      }
    }
    public Bill all { get { return new Bill(this, true); } }
    public Bill children { get { return new Bill(this, false); } }
    /// <summary>save defaultValue in persistent storage</summary>
    public bool saved {
      get { return _flags[0]; }
      set {
        if(_flags[0] != value) {
          _flags[0] = value;
          var c = Perform.Create(this, Perform.Art.changed, null);
          PLC.instance.DoCmd(c, false);
        }
      }
    }
    /// <summary>only for this instance</summary>
    public bool local { get { return _flags[1]; } set { _flags[1] = value; } }
    /// <summary>removed</summary>
    public bool disposed { get { return _flags[2]; } internal set { _flags[2] = value; } }
    /// <summary>save defaultValue only in config file</summary>
    public bool config {
      get { return _flags[4]; }
      set {
        if(_flags[4] != value) {
          _flags[4] = value;
          var c = Perform.Create(this, Perform.Art.changed, null);
          PLC.instance.DoCmd(c, false);
        }
      }
    }

    /// <summary> Get item from tree</summary>
    /// <param name="path">relative or absolute path</param>
    /// <param name="create">true - create, false - check</param>
    /// <returns>item or null</returns>
    public Topic Get(string path, bool create = true, Topic prim = null) {
      return GetI(path, create, prim, false);
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
          throw new ArgumentException(string.Format("{0}[{1}] dont allow wildcard", this.path, path));
        }
        if(pt[i] == Bill.maskParent) {
          home = home.parent;
          if(home == null) {
            throw new ArgumentException(string.Format("{0}[{1}] BAD path: excessive nesting", this.path, path));
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
              PLC.instance.DoCmd(c, inter);
            }
          } else {
            return null;
          }
        }
        home = next;
      }
      return home;
    }
    internal Topic Create(string path, Topic prim, string schemaName, JSValue value) {
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
          throw new ArgumentException(string.Format("{0}[{1}] dont allow wildcard", this.path, path));
        }
        if(pt[i] == Bill.maskParent) {
          home = home.parent;
          if(home == null) {
            throw new ArgumentException(string.Format("{0}[{1}] BAD path: excessive nesting", this.path, path));
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
          if(home._children.TryGetValue(pt[i], out next)) {
            home = next;
          } else {
            next = new Topic(home, pt[i]);
            home._children[pt[i]] = next;
            var c = Perform.Create(next, Perform.Art.create, prim);
            if(i + 1 == pt.Length) {
              next._value = value;
              c.o = value;
              if(!string.IsNullOrEmpty(schemaName) && next.schema != schemaName) {
                next._children = new ConcurrentDictionary<string, Topic>();
                Topic sh = new Topic(next, "$schema");
                next._children["$schema"] = sh;
                sh._value = schemaName;
                PLC.instance.DoCmd(c, false);
                c = Perform.Create(sh, Perform.Art.create, prim);
                c.o = schemaName;
              }
            }
            PLC.instance.DoCmd(c, false);
          }
        }
        home = next;
      }
      return home;
    }

    internal void SetFlagI(int fl, bool value) {
      _flags[fl] = value;
    }
    public bool Exist(string path) {
      return GetI(path, false, null, false) != null;
    }
    public bool Exist(string path, out Topic topic) {
      return (topic = GetI(path, false, null, false)) != null;
    }
    public void Remove(Topic prim = null) {
      var c = Perform.Create(this, Perform.Art.remove, prim);
      PLC.instance.DoCmd(c, false);
    }
    public Topic Move(Topic nParent, string nName, Topic prim = null) {
      if(nParent == null) {
        nParent = this.parent;
      }
      if(string.IsNullOrEmpty(nName)) {
        nName = this.name;
      }
      //if(nParent.Exist(nName)) {
      //  throw new ArgumentException(string.Concat(this.path, ".Move(", nParent.path, "/", nName, ") - destination already exist"));
      //}
      Topic dst = new Topic(nParent, nName);
      lock(nParent._children) {
        nParent._children[nName] = dst;
      }
      var c = Perform.Create(this, Perform.Art.move, prim);
      c.o = dst;
      PLC.instance.DoCmd(c, false);
      return dst;
    }
    public override string ToString() {
      return _path;
    }
    public int CompareTo(Topic other) {
      if(other == null) {
        return 1;
      }
      return string.Compare(this._path, other._path);
    }
    public JSValue valueRaw { get { return _value; } }
    public object value { get { return (_value.ValueType >= JSValueType.Object && !(_value.Value is JSValue)) ? _value.Value : _value; } set { this.Set(value); } }
    public T As<T>() {
      try {
        return (T)(((IConvertible)_value).ToType(typeof(T), null));
      }
      catch(Exception) {

      }
      return default(T);
    }
    public void Set(object val, Topic prim = null) {
      var c = Perform.Create(this, val, prim);
      PLC.instance.DoCmd(c, false);
    }
    internal void SetI(object val, Topic prim = null) {
      var c = Perform.Create(this, val, prim);
      PLC.instance.DoCmd(c, true);
    }
    public void SetJson(JSValue jso, Topic prim = null) {
      var c = Perform.Create(this, Perform.Art.setJson, prim);
      c.o = jso;
      PLC.instance.DoCmd(c, false);
    }

    public void SetJson(string json, Topic prim = null) {
      var c = Perform.Create(this, Perform.Art.setJson, prim);
      c.o = JST.JSON.parse(json);
      PLC.instance.DoCmd(c, false);
    }

    internal void SetIJson(string json, Topic prim = null) {
      var c = Perform.Create(this, Perform.Art.setJson, prim);
      c.o = json;
      PLC.instance.DoCmd(c, true);
    }

    public string ToJson() {
      if(_json == null) {
        lock(this) {
          if(_json == null) {
            var t = _value.ValueType;
            JST.Date jd;
            if(t == JSValueType.NotExists || t == JSValueType.NotExistsInObject || t == JSValueType.Undefined) {
              _json = "null";
            } else if(t == JSValueType.Object && (jd = _value.Value as JST.Date) != null) {
              _json = jd.toISOString().ToString();
            } else {
              _json = JST.JSON.stringify(_value, null, null);
            }
          }
        }
      }
      return _json;
    }

    public event Action<SubRec, Perform> changed {
      add {
        Subscribe(value, SubRec.SubMask.Once, false);
      }
      remove {
        Unsubscribe(value, SubRec.SubMask.Once, false);
      }
    }

    internal void Publish(Perform cmd) {
      SubRec sb;
      if((cmd.art == Perform.Art.subscribe || cmd.art == Perform.Art.unsubscribe || cmd.art == Perform.Art.subAck || cmd.art == Perform.Art.unsubAck)
        && (sb = cmd.o as SubRec) != null && sb.f != null) {
        try {
          sb.f(sb, cmd);
        }
        catch(Exception ex) {
          Log.Warning("{0}.{1}({2}, {4}) - {3}", sb.f.Method.DeclaringType.Name, sb.f.Method.Name, this.path, ex.ToString(), cmd.art.ToString());
        }
      } else {
        if(_subRecords != null) {
          for(int i = 0; i < _subRecords.Count; i++) {
            sb = _subRecords[i];
            if(sb.f != null && ((sb.flags & SubRec.SubMask.OnceOrAll) != SubRec.SubMask.None
              || ((sb.flags & SubRec.SubMask.Chldren) == SubRec.SubMask.Chldren && this.parent != null && sb.path == this.parent.path))) {
              try {
                sb.f(sb, cmd);
              }
              catch(Exception ex) {
                Log.Warning("{0}.{1}({2}, {4}) - {3}", sb.f.Method.DeclaringType.Name, sb.f.Method.Name, this.path, ex.ToString(), cmd.art.ToString());
              }
            }
          }
        }
      }
    }

    internal void Subscribe(SubRec sr) {
      if(_subRecords == null) {
        _subRecords = new List<SubRec>();
      }
      lock(_subRecords) {
        if(!_subRecords.Any(z => z.path == sr.path && z.flags == sr.flags && z.f == sr.f)) {
          _subRecords.Add(sr);
        }
      }
    }

    internal SubRec Subscribe(Action<SubRec, Perform> func, SubRec.SubMask mask, bool intern) {
      SubRec sb;
      if(_subRecords == null) {
        _subRecords = new List<SubRec>();
      }
      lock(_subRecords) {
        sb = _subRecords.FirstOrDefault(z => z.f == func && z.path == this.path && z.flags == mask);
        if(sb == null) {
          sb = new SubRec() { path = this.path, flags = mask, f = func };
          _subRecords.Add(sb);
        }
      }
      var c = Perform.Create(this, Perform.Art.subscribe, this);
      c.o = sb;
      PLC.instance.DoCmd(c, intern);
      return sb;
    }
    internal SubRec Unsubscribe(Action<SubRec, Perform> func, SubRec.SubMask mask, bool intern) {
      SubRec sb;
      if(_subRecords == null) {
        sb = null;
      } else {
        lock(_subRecords) {
          sb = _subRecords.FirstOrDefault(z => z.f == func && z.path == this.path && z.flags == mask);
          if(sb != null) {
            _subRecords.Remove(sb);
            var c = Perform.Create(this, Perform.Art.unsubscribe, this);
            c.o = sb;
            PLC.instance.DoCmd(c, intern);
          } else {
            sb = null;
          }
        }
      }
      return sb;
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
      public event Action<SubRec, Perform> changed {
        add {
          _home.Subscribe(value, _deep ? SubRec.SubMask.All : SubRec.SubMask.Chldren, false);
        }
        remove {
          _home.Unsubscribe(value, _deep ? SubRec.SubMask.All : SubRec.SubMask.Chldren, false);
        }
      }
      System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() {
        return GetEnumerator();
      }
    }

    #endregion nested types
  }
  public class SubRec {
    public string path { get; internal set; }
    public SubMask flags { get; internal set; }
    public Action<SubRec, Perform> f { get; internal set; }

    public override string ToString() {
      return string.Format("{0}{1} > {2}.{3}", path, (flags & (SubMask.Chldren | SubMask.All)) != SubMask.None ? ((flags & SubMask.Chldren) != SubMask.None ? "/+" : "/#") : string.Empty,
        f.Target == null ? f.Method.DeclaringType.Name : f.Target.ToString(), f.Method.Name);
    }
    [Flags]
    public enum SubMask {
      None = 0,
      Once = 1,
      Chldren = 2,
      All = 4,
      OnceOrAll = 5,
    }
  }

  public interface ITenant {
    Topic owner { get; set; }
  }
}
