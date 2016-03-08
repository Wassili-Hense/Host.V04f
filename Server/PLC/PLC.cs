using JSL=NiL.JS.BaseLibrary;
using NiL.JS.Core;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Xml.Linq;

namespace X13.PLC {
  [System.ComponentModel.Composition.Export(typeof(IPlugModul))]
  [System.ComponentModel.Composition.ExportMetadata("priority", 1)]
  [System.ComponentModel.Composition.ExportMetadata("name", "PLC")]
  public class PLC : IPlugModul {
    public static PLC instance { get; private set; }

    private ConcurrentQueue<Perform> _tcQueue;
    private Dictionary<string, Func<JSValue, Topic, Topic, JSValue>> _knownTypes;
    private List<Perform> _prOp;
    private int _busyFlag;
    private int _pfPos;

    private List<PiBlock> _blocks;
    private Dictionary<Topic, PiVar> _vars;
    private List<PiVar> _rLayerVars;

    public PLC() {
      instance=this;
      enabled=true;
      _blocks = new List<PiBlock>();
      _vars = new Dictionary<Topic, PiVar>();
      _tcQueue = new ConcurrentQueue<Perform>();
      _knownTypes=new Dictionary<string, Func<JSValue, Topic, Topic, JSValue>>();
      _rLayerVars = new List<PiVar>();
      _prOp = new List<Perform>(128);
      _busyFlag = 1;
    }
    public void Init() {
      if(Topic.root.children.Any()) {
        lock(Topic.root) {
          Perform c;
          while(_tcQueue.TryDequeue(out c)) {
          }
          _prOp.Clear();
          foreach(var t in Topic.root.all) {
            t.disposed = true;
            if(t._children != null) {
              t._children.Clear();
              t._children = null;
            }
          }
          _busyFlag = 1;
        }
        Topic.root.disposed = false;
      }
      _blocks.Clear();
      _vars.Clear();
      _knownTypes.Clear();

      _knownTypes["PiAlias"]=(j, s, p) => new PiAlias(j, s, p);
      _knownTypes["PiLink"]=(j, s, p) => new PiLink(j, s, p);
      _knownTypes["PiBlock"]=(j, s, p) => new PiBlock(j, s, p);
      _knownTypes["PiDeclarer"]=PiDeclarer.Create;
    }
    public void Start() {
      Import(@"..\data\base.xst");
      Topic.root.Get("/var/started/year").value = DateTime.Now.Year;
      Topic.root.Get("/var/started").value=DateTime.Now;
    }
    public void Tick() {
      if(Interlocked.CompareExchange(ref _busyFlag, 2, 1) != 1) {
        return;
      }
      //var qc=_tcQueue.Count();
      Perform cmd;
      _pfPos = 0;
      while(_tcQueue.TryDequeue(out cmd)) {
        if(cmd == null || cmd.src == null) {
          continue;
        }
        TickStep1(cmd);
      }

      for(int i = 0; i < _prOp.Count; i++) {
        TickStep2(_prOp[i]);
      }

      for(_pfPos = 0; _pfPos < _prOp.Count; _pfPos++) {
        cmd = _prOp[_pfPos];
        if(cmd.art == Perform.Art.changed || cmd.art == Perform.Art.remove) {
          if(cmd.old_o != null) {
            ITenant it;
            if((it = cmd.old_o as ITenant) != null) {
              it.owner = null;
            }
          }
        }
        if(cmd.art == Perform.Art.changed) {
          if(cmd.src._value != null && !cmd.src.disposed) {
            ITenant tt;
            if((tt = cmd.src._value as ITenant) != null) {
              tt.owner = cmd.src;
            }
          }
        }
        if(cmd.art != Perform.Art.set) {
          cmd.src.Publish(cmd);
        }
        //X13.lib.Log.Debug("$ {0} [{1}, {2}] i={3}", cmd.src.path, cmd.art, (cmd.o??"null"), cmd.prim==null?string.Empty:cmd.prim.path);
        if(_rLayerVars.Any()) {
          CalcLayers(new Queue<PiVar>(_rLayerVars.Where(z => z.layer!=0).Union(_rLayerVars.Where(z => !z.ip))));
          if(_rLayerVars.Any(z => z.layer == 0)) {
            CalcLayers(new Queue<PiVar>(_rLayerVars.Where(z => z.layer == 0)));
          }
          _rLayerVars.Clear();
        }

        //if(cmd.src.disposed) {
        //  cmd.src._flags[3]=true;
        //}
      }
      //X13.lib.Log.Debug("PLC.Tick QC={0}, PC={1}", qc, _prOp.Count);
      _prOp.Clear();
      _busyFlag = 1;
    }
    public void Stop() {
      _blocks.Clear();
      _vars.Clear();
    }
    public bool enabled { get; set; }

    public static void Export(string filename, Topic head) {
      if(filename==null || head==null) {
        throw new ArgumentNullException();
      }
      XDocument doc=new XDocument(new XElement("root", new XAttribute("head", head.path)));
      if(head.saved) {
        if(head.vType!=null) {
          doc.Root.Add(new XAttribute("value", head.ToJson()));
        }
        doc.Root.Add(new XAttribute("saved", bool.TrueString));
      }
      foreach(Topic t in head.children) {
        Export(doc.Root, t);
      }

      using(StreamWriter sw = File.CreateText(filename)) {
        using(var writer = new System.Xml.XmlTextWriter(sw)) {
          writer.Formatting = System.Xml.Formatting.Indented;
          writer.QuoteChar = '\'';
          writer.WriteNode(doc.CreateReader(), false);
          writer.Flush();
        }
      }
    }
    private static void Export(XElement xParent, Topic tCur) {
      if(xParent==null || tCur==null) {
        return;
      }
      XElement xCur=new XElement("item", new XAttribute("name", tCur.name));

      if(tCur.saved) {
        if(tCur.vType!=null) {
          string json=tCur.ToJson();
          if(json!=null) {
            xCur.Add(new XAttribute("value", json));
          }
        }
        xCur.Add(new XAttribute("saved", bool.TrueString));
      }

      xParent.Add(xCur);
      foreach(Topic tNext in tCur.children) {
        Export(xCur, tNext);
      }
    }

    public static bool Import(string fileName, string path=null) {
      if(string.IsNullOrEmpty(fileName) || !File.Exists(fileName)) {
        return false;
      }
      X13.Log.Debug("Import {0}", fileName);
      using(StreamReader reader = File.OpenText(fileName)) {
        Import(reader, path);
      }
      return true;
    }
    public static void Import(StreamReader reader, string path) {
      XDocument doc;
      using(var r = new System.Xml.XmlTextReader(reader)) {
        doc=XDocument.Load(r);
      }

      if(string.IsNullOrEmpty(path) && doc.Root.Attribute("head")!=null) {
        path=doc.Root.Attribute("head").Value;
      }

      Topic owner=Topic.root.Get(path);
      foreach(var xNext in doc.Root.Elements("item")) {
        Import(xNext, owner);
      }
      owner.SetFlagI(0, doc.Root.Attribute("saved")!=null && doc.Root.Attribute("saved").Value!=bool.FalseString);
      if(doc.Root.Attribute("value")!=null) {
        owner.SetJson(doc.Root.Attribute("value").Value);
      }
    }
    private static void Import(XElement xElement, Topic owner) {
      if(xElement==null || owner==null || xElement.Attribute("name")==null) {
        return;
      }
      Version ver;
      Topic cur;
      bool setVersion=false;
      if(xElement.Attribute("version")!=null && Version.TryParse(xElement.Attribute("ver").Value, out ver)) {
        if(owner.Exist(xElement.Attribute("name").Value, out cur)) {
          Topic tVer;
          Version oldVer;
          if(!cur.Exist("$INF\version", out tVer) || tVer.vType!=typeof(string) || !Version.TryParse(tVer.As<string>(), out oldVer) || oldVer<ver) {
            setVersion=true;
            cur.Remove();
          } else {
            return; // don't import older version
          }
        } else {
          setVersion=true;
        }
      } else {
        ver=default(Version);
      }
      cur=owner.Get(xElement.Attribute("name").Value);
      foreach(var xNext in xElement.Elements("item")) {
        Import(xNext, cur);
      }
      cur.SetFlagI(0, xElement.Attribute("saved")!=null && xElement.Attribute("saved").Value!=bool.FalseString);
      if(xElement.Attribute("value")!=null) {
        cur.SetJson(xElement.Attribute("value").Value);
      }
      if(setVersion) {
        cur.Get("$INF\version").value=ver.ToString();
      }
    }

    public void RegisterType(string name, Func<JSValue, Topic, Topic, JSValue> f) {
      _knownTypes[name]=f;
    }

    private void TickStep1(Perform c) {
      SubRec sr;
      Topic t;
      switch(c.art) {
      case Perform.Art.create:
        if((t = c.src.parent) != null) {
          //t._children[c.src.name]=c.src;
          if(t._subRecords != null) {
            lock(t._subRecords) {
              foreach(var st in t._subRecords.Where(z => z.path==t.path && (z.flags & SubRec.SubMask.Chldren)==SubRec.SubMask.Chldren)) {
                c.src.Subscribe(st);
              }
            }
          }
          while(t != null) {
            if(t._subRecords != null) {
              lock(t._subRecords) {
                foreach(var st in t._subRecords.Where(z => (z.flags & SubRec.SubMask.All)==SubRec.SubMask.All)) {
                  c.src.Subscribe(st);
                }
              }
            }
            t = t.parent;
          }
        }
        EnquePerf(c);
        break;
      case Perform.Art.subscribe:
      case Perform.Art.unsubscribe:
        if((sr=c.o as SubRec) != null) {
          Topic.Bill b=null;
          Perform np;
          if((sr.flags & SubRec.SubMask.Once)==SubRec.SubMask.Once) {
            EnquePerf(c);
          }
          if((sr.flags & SubRec.SubMask.Chldren)==SubRec.SubMask.Chldren) {
            b = c.src.children;
          }
          if((sr.flags & SubRec.SubMask.All)==SubRec.SubMask.All) {
            b = c.src.all;
          }
          if(b!=null) {
            foreach(Topic tmp in b) {
              if(c.art == Perform.Art.subscribe) {
                tmp.Subscribe(sr);
                np=Perform.Create(tmp, c.art, c.src);
                np.o=c.o;
                EnquePerf(np);
              } else {
                tmp._subRecords.Remove(sr);
              }
            }
          }
          np=Perform.Create(c.src, c.art==Perform.Art.subscribe?Perform.Art.subAck:Perform.Art.unsubAck, c.src);
          np.o=c.o;
          EnquePerf(np);
        }
        break;

      case Perform.Art.remove:
        foreach(Topic tmp in c.src.all) {
          EnquePerf(Perform.Create(tmp, c.art, c.prim));
        }
        break;
      case Perform.Art.move:
        if((t = c.o as Topic) != null) {
          string oPath = c.src.path;
          string nPath = t.path;
          t._children = c.src._children;
          c.src._children = null;
          t._value = c.src._value;
          c.src._value = JSValue.Undefined;
          if(c.src._subRecords != null) {
            foreach(var st in c.src._subRecords) {
              if(st.path.StartsWith(oPath)) {
                t.Subscribe(new SubRec() { path = st.path.Replace(oPath, nPath), flags=st.flags, f = st.f });
              }
            }
          }
          foreach(var t1 in t.children) {
            t1.parent = t;
          }
          foreach(var t1 in t.all) {
            if(t1._subRecords != null) {
              for(int i = t1._subRecords.Count - 1; i >= 0; i--) {
                if(t1._subRecords[i].path.StartsWith(oPath)) {
                  t1._subRecords[i].path=t1._subRecords[i].path.Replace(oPath, nPath);
                } else if(!t1._subRecords[i].path.StartsWith(nPath)) {
                  t1._subRecords.RemoveAt(i);
                }
              }
            }
            t1.path = t1.parent == Topic.root ? string.Concat("/", t1.name) : string.Concat(t1.parent.path, "/", t1.name);
            DoCmd(Perform.Create(t1, Perform.Art.create, c.prim), false);
          }
          EnquePerf(c);
          int idx = _prOp.Count-1;
          while(idx >= 0) {
            Perform c1 = _prOp[idx--];
            if(c1.src == c.src && (c1.art == Perform.Art.set || c1.art==Perform.Art.setJson)) {
              var p = Perform.Create(t, c1.art, c1.prim);
              p.o = c1.o;
              p.i = c1.i;
              EnquePerf(p);
              break;
            }
          }
        }
        break;
      case Perform.Art.changed:
      case Perform.Art.set:
      case Perform.Art.setJson:
        EnquePerf(c);
        break;
      }
    }

    private void TickStep2(Perform cmd) {
      if(cmd.art == Perform.Art.remove || cmd.art == Perform.Art.setJson || (cmd.art == Perform.Art.set && !object.Equals(cmd.src._value, cmd.o))) {
        cmd.old_o = cmd.src._value;
        if(cmd.art == Perform.Art.setJson) {
          var jso=cmd.o as JSValue;
          JSValue ty;
          if(jso.ValueType==JSValueType.Object && jso.Value!=null && (ty=jso.GetProperty("$type")).Defined) {
            Func<JSValue, Topic, Topic, JSValue> f;
            if(_knownTypes.TryGetValue(ty.ToString(), out f) && f!=null) {
              cmd.src._value=f(jso, cmd.src, cmd.prim);
            } else {
              X13.Log.Warning("{0}.setJson({1}) - unknown $type", cmd.src.path, cmd.o);
              cmd.src._value = jso;
            }
          } else {
            cmd.src._value = jso;
          }
        } else {
          cmd.src._value = JSValue.Marshal(cmd.o);
        }
        if(cmd.art != Perform.Art.remove) {
          cmd.art = Perform.Art.changed;
        }
      }
      if(cmd.art == Perform.Art.changed) {
        cmd.src._json = null;
      }
      if(cmd.art == Perform.Art.remove || cmd.art == Perform.Art.move) {
        cmd.src.disposed = true;
        if(cmd.src.parent != null) {
          Topic tmp;
          cmd.src.parent._children.TryRemove(cmd.src.name, out tmp);
        }
      }
      //TODO: save for undo/redo
      /*IHistory h;
      if(cmd.prim!=null && cmd.prim._vt==VT.Object && (h=cmd.prim._o as IHistory)!=null) {
        h.Add(cmd);
      }*/
    }

    internal void DoCmd(Perform cmd, bool intern) {
      if(intern) {
        if(_prOp.Count>0 && (_pfPos>=_prOp.Count || _prOp[_pfPos].layer>cmd.layer)) {
          _tcQueue.Enqueue(cmd);               // Published in next tick
        } else {
          TickStep1(cmd);
          TickStep2(cmd);
        }
      } else {
        _tcQueue.Enqueue(cmd);
      }
    }

    private int EnquePerf(Perform cmd) {
      int i;
      for(i=0; i<_prOp.Count; i++) {
        if(_prOp[i].EqualsGr(cmd)) {
          if(_prOp[i].EqualsEx(cmd)) {
            return i;
          }
          if(_prOp[i].art==Perform.Art.changed) {
            cmd.old_o=_prOp[i].old_o;
          }
          _prOp.RemoveAt(i);
          if(_pfPos>=i) {
            _pfPos--;
          }
          break;
        }
      }
      i = ~_prOp.BinarySearch(cmd);
      _prOp.Insert(i, cmd);
      return i;
    }
    internal void AddBlock(PiBlock bl) {
      _blocks.Add(bl);
    }
    internal PiVar GetVar(Topic t, bool create, bool refresh=false) {
      PiVar v;
      if(!_vars.TryGetValue(t, out v)) {
        if(create) {
          v = new PiVar(t);
          _vars[t] = v;
          _rLayerVars.Add(v);
        } else {
          v = null;
        }
      } else if(refresh) {
        _rLayerVars.Add(v);
      }
      return v;
    }
    private void CalcLayers(Queue<PiVar> vQu) {
      PiVar v1;

      do {
        while(vQu.Count > 0) {
          v1 = vQu.Dequeue();
          if(v1.layer == 0) {
            v1.calcPath = new PiBlock[0];
            if(v1._src!=null) {
              if(v1._src.layer == 0) {
                continue;
              } else {
                v1.layer=v1.block.layer;
              }
            } else {
              v1.layer = 1;
            }
            //X13.lib.Log.Debug("{0}.SetLayer({1})", v1, v1.layer);
          }
          foreach(var l in v1._cont.Select(z => z as PiLink).Where(z => z!=null && z.input == v1)) {
            l.output.layer = l.layer;
            //X13.lib.Log.Debug("{0}.SetLayer({1}) <- {2}", l.output, l.layer, v1);
            l.output.calcPath = v1.calcPath;
            vQu.Enqueue(l.output);
          }
          if(v1.block != null && v1.block._decl.pins[v1.owner.name].ip) {
            if(v1.calcPath.Contains(v1.block)) {
              if(v1.layer > 0) {
                v1.layer = -v1.layer;
                //X13.lib.Log.Debug("{0}.SetLayer({1}) <- {2}", v1, v1.layer, v1.block);
              }
              //X13.lib.Log.Debug("{0} make loop", v1.owner.path);
            } else if(v1.block._pins.Where(z => v1.block._decl.pins[z.Key].ip).All(z => z.Value.layer >= 0)) {
              int nl=v1.block._pins.Where(z => v1.block._decl.pins[z.Key].ip).Max(z => z.Value.layer) + 1;
              var nbl= v1.block.calcPath.Union(v1.calcPath).Distinct().ToArray();
              if(v1.block.layer != nl || nbl.Except(v1.block.calcPath).Any()) {
                v1.block.layer = nl;
                v1.block.calcPath = nbl;
                foreach(var v3 in v1.block._pins.Where(z => v1.block._decl.pins[z.Key].op).Select(z => z.Value)) {
                  v3.layer = v1.block.layer;
                  //X13.lib.Log.Debug("{0}.SetLayer({1}) <- {2}", v3, v3.layer, v1);
                  v3.calcPath = v1.block.calcPath;
                  if(!vQu.Contains(v3)) {
                    vQu.Enqueue(v3);
                  }
                }
              }
            }
          }
        }
        if(vQu.Count == 0 && _blocks.Any(z => z.layer == 0)) { // break a one loop in the graph
          var bl = _blocks.Where(z => z.layer <= 0).Min();
          foreach(var ip in bl._pins.Select(z => z.Value).Where(z => z.ip && z.layer > 0)) {
            bl.calcPath = bl.calcPath.Union(ip.calcPath).ToArray();
          }
          {
            var pl=bl._pins.Where(z => bl._decl.pins[z.Key].ip && z.Value.layer > 0);
            if(pl.Any()) {
              bl.layer=pl.Max(z => z.Value.layer) + 1;
            } else {
              bl.layer=1;     // block with 1 input in loop
            }
          }
          foreach(var v3 in bl._pins.Where(z => bl._decl.pins[z.Key].op).Select(z => z.Value)) {
            v3.layer = bl.layer;
            //X13.lib.Log.Debug("{0}.SetLayer({1}) <- {2}", v3, v3.layer, bl);
            v3.calcPath = bl.calcPath;
            if(!vQu.Contains(v3)) {
              vQu.Enqueue(v3);
            }
          }
        }
      } while(vQu.Count > 0);
    }

    internal void DelVar(PiVar v) {
      _vars.Remove(v.owner);
    }


  }
}
