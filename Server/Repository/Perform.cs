using LiteDB;
using NiL.JS.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace X13.Repository {
  internal class Perform : IComparable<Perform> {

    internal static Perform Create(Topic src, Art art, Topic prim) {
      Perform r;
      r = new Perform(art, src, prim);
      r.o = null;
      r.i = 0;
      return r;
    }
    
    internal static Perform Create(Topic src, JSValue val, Topic prim) {
      Perform r;
      r = new Perform(Art.set, src, prim);
      r.o = val;
      r.i = 0;
      return r;
    }
    internal static Perform Create(Topic src, string fName, BsonValue val, Topic prim) {
      Perform r;
      r = new Perform(Art.set, src, prim);
      r.o = fName;
      r.f_v = val;
      r.i = 0;
      return r;
    }
    internal object o;
    internal int i;
    internal object old_o;
    BsonValue f_v;

    public readonly Topic src;
    public Topic prim { get; internal set; }
    public readonly int layer;
    public Art art { get; internal set; }

    private Perform(Art art, Topic src, Topic prim) {
      this.src = src;
      this.art = art;
      this.prim = prim;
      this.layer = -1;  // TODO: layer
    }

    public int CompareTo(Perform other) {
      if(other == null) {
        return -1;
      }
      int p1 = ((int)this.art) >> 2;
      int p2 = (int)(other.art) >> 2;
      if(p1 != p2) {
        return p1.CompareTo(p2);
      }
      if(this.layer != other.layer) {
        return this.layer > other.layer ? 1 : -1;
      }
      if(this.src == other.src) {
        return 0;
      }
      return -1;  // для различных топиков с однаковым layer & art - this<other ( сохраняется порядок поступления)
    }
    public override string ToString() {
      return string.Concat(src.path, "[", art.ToString(), ", ", layer.ToString(), "]=", o == null ? "null" : o.ToString());
    }
    public enum Art {
      move = 1,
      create = 2,
      subscribe = 4,
      unsubscribe = 8,
      set = 12,
      setField = 13,
      changed = 14,
      changedField = 15,
      remove = 16,
      subAck = 20,
      unsubAck = 24,
    }

    public bool EqualsEx(Perform other) {
      return (this.art == other.art || (this.art == Art.changed && other.art < Art.changed))
        && object.Equals(this.o, other.o);
    }

    internal bool EqualsGr(Perform other) {
      return other != null
        && this.src == other.src
        && (((int)this.art) >> 2) == (((int)other.art) >> 2)
        && ((this.art != Art.subscribe && this.art != Art.unsubscribe) || object.Equals(this.o, other.o));
    }
  }
}
