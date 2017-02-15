﻿///<remarks>This file is part of the <see cref="https://github.com/X13home">X13.Home</see> project.<remarks>
using LiteDB;
using NiL.JS.Core;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Xml.Linq;

namespace X13.Repository {
  [System.ComponentModel.Composition.Export(typeof(IPlugModul))]
  [System.ComponentModel.Composition.ExportMetadata("priority", 1)]
  [System.ComponentModel.Composition.ExportMetadata("name", "Repository")]
  public class Repo : IPlugModul {
    #region internal Members
    private ConcurrentQueue<Perform> _tcQueue;
    private List<Perform> _prOp;
    private int _busyFlag;
    private int _pfPos;

    private LiteDatabase _db;
    private LiteCollection<BsonDocument> _objects, _states;

    internal void DoCmd(Perform cmd, bool intern) {
      if(intern && _prOp.Count > 0 && _pfPos < _prOp.Count && _prOp[_pfPos].layer <= cmd.layer) {  // !!! *.layer==-1
        TickStep1(cmd);
        TickStep2(cmd);
      } else {
        _tcQueue.Enqueue(cmd);               // Process in next tick
      }
    }
    private int EnquePerf(Perform cmd) {
      int i;
      for(i = 0; i < _prOp.Count; i++) {
        if(_prOp[i].EqualsGr(cmd)) {
          if(_prOp[i].art == Perform.Art.changedState) {
            cmd.old_o = _prOp[i].old_o;
          }
          _prOp.RemoveAt(i);
          if(_pfPos >= i) {
            _pfPos--;
          }
          break;
        }
      }
      i = ~_prOp.BinarySearch(cmd);
      _prOp.Insert(i, cmd);
      return i;
    }

    private void TickStep1(Perform c) {
      SubRec sr;

      switch(c.art) {
      case Perform.Art.create:
        Topic.I.SubscribeByCreation(c.src);
        EnquePerf(c);
        break;
      case Perform.Art.subscribe:
      case Perform.Art.unsubscribe:
        if((sr = c.o as SubRec) != null) {
          Topic.Bill b = null;
          Perform np;
          if(c.art == Perform.Art.subscribe && (sr.mask & SubRec.SubMask.Once) == SubRec.SubMask.Once) {
            EnquePerf(c);
          }
          if((sr.mask & SubRec.SubMask.Chldren) == SubRec.SubMask.Chldren) {
            b = c.src.children;
          }
          if((sr.mask & SubRec.SubMask.All) == SubRec.SubMask.All) {
            b = c.src.all;
          }
          if(b != null) {
            foreach(Topic tmp in b) {
              if(c.art == Perform.Art.subscribe) {
                Topic.I.Subscribe(tmp, sr);
                if((sr.mask & SubRec.SubMask.Value) == SubRec.SubMask.Value
                  || (sr.mask & SubRec.SubMask.Field) == SubRec.SubMask.None || string.IsNullOrEmpty(sr.prefix) || tmp.GetField(sr.prefix).Defined) {
                  np = Perform.Create(tmp, Perform.Art.subscribe, c.src);
                  np.o = c.o;
                  EnquePerf(np);
                }
              } else {
                Topic.I.RemoveSubscripton(tmp, sr);
              }
            }
          }
          if(c.art == Perform.Art.subscribe) {
            np = Perform.Create(c.src, Perform.Art.subAck, c.src);
            np.o = c.o;
            EnquePerf(np);
          }
        }
        break;

      case Perform.Art.changedState:
      case Perform.Art.setState:
      case Perform.Art.setField:
      case Perform.Art.changedField:
      case Perform.Art.move:
        EnquePerf(c);
        break;
      case Perform.Art.remove:
        foreach(Topic tmp in c.src.all) {
          EnquePerf(Perform.Create(tmp, Perform.Art.remove, c.prim));
        }
        break;
      }
    }
    private void TickStep2(Perform cmd) {
      if(cmd.art == Perform.Art.remove || (cmd.art == Perform.Art.setState && !object.ReferenceEquals(cmd.src.GetState(), cmd.o))) {
        cmd.old_o = cmd.src.GetState();
        Topic.I.SetValue(cmd.src, cmd.o as JSValue);
        if(cmd.art != Perform.Art.remove) {
          cmd.art = Perform.Art.changedState;
        }
      }
      if(cmd.art == Perform.Art.setField) {
        string fPath = cmd.o as string;
        cmd.old_o = cmd.src.GetField(fPath);
        Topic.I.SetField(cmd.src, fPath, cmd.f_v);
        cmd.art = Perform.Art.changedField;
      }
      if(cmd.art == Perform.Art.move) {
        Topic.I.SubscribeByMove(cmd.src);
      }
      if(cmd.art == Perform.Art.remove) {
        Topic.I.Remove(cmd.src);
      }
    }
    private void Store(Perform cmd) {
      if(_objects == null) {
        return;
      }
      BsonDocument obj, state;
      Topic.I.ReqData(cmd.src, out obj, out state);
      switch(cmd.art) {
      case Perform.Art.changedState:
        if(state != null) {
          _states.Upsert(state);
        }
        break;
      case Perform.Art.changedField:
      case Perform.Art.move:
        _objects.Update(obj);
        if((cmd.o as string) == "s") {
          if(cmd.src.saved) {
            Topic.I.SetValue(cmd.src, cmd.src.GetState());
            if(state != null) {
              _states.Upsert(state);
            }
          } else {
            _states.Delete(obj["_id"]);
          }
        }
        break;
      case Perform.Art.create:
        _objects.Upsert(obj);
        break;
      case Perform.Art.remove:
        _states.Delete(obj["_id"]);
        _objects.Delete(obj["_id"]);
        break;
      }
    }

    #endregion internal Members

    public Repo() {
      _tcQueue = new ConcurrentQueue<Perform>();
      _prOp = new List<Perform>(128);
    }

    #region Import
    public static bool Import(string fileName, string path = null) {
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
        doc = XDocument.Load(r);
      }

      if(string.IsNullOrEmpty(path) && doc.Root.Attribute("path") != null) {
        path = doc.Root.Attribute("path").Value;
      }

      Import(doc.Root, null, path);
    }
    private static void Import(XElement xElement, Topic owner, string path) {
      if(xElement == null || ((xElement.Attribute("n") == null || owner==null) && path==null)) {
        return;
      }
      Version ver;
      Topic cur = null;
      bool setVersion;
      if(xElement.Attribute("ver") != null && Version.TryParse(xElement.Attribute("ver").Value, out ver)) {
        if(owner == null ? Topic.root.Exist(path, out cur) : owner.Exist(xElement.Attribute("n").Value, out cur)) {
          Version oldVer;
          var ov_js = cur.GetField("$PS.ver");
          string ov_s;
          if(ov_js.ValueType == JSValueType.String && (ov_s = ov_js.Value as string) != null && ov_s.StartsWith("¤VR") && Version.TryParse(ov_s.Substring(3), out oldVer) && oldVer >= ver) {
            return; // don't import older version
          }
          if(cur != Topic.root) {
            cur.Remove();
          }
        }
        setVersion = true;
      } else {
        ver = default(Version);
        setVersion = false;
      }
      JSValue state = null, manifest = null;
      if(xElement.Attribute("m") != null) {
        try {
          manifest =  DeskHost.DeskSocket.ParseJson(xElement.Attribute("m").Value);
        }
        catch(Exception ex) {
          Log.Warning("Import({0}).m - {1}", xElement.ToString(), ex.Message);
        }
      }
      if(setVersion) {
        JsLib.SetField(ref manifest, "$PS.ver", "¤VR" + ver.ToString());
      }

      if(xElement.Attribute("s") != null) {
        try {
          state = DeskHost.DeskSocket.ParseJson(xElement.Attribute("s").Value);
        }
        catch(Exception ex) {
          Log.Warning("Import({0}).s - {1}", xElement.ToString(), ex.Message);
        }
      }


      if(owner == null) {
        cur = Topic.I.Get(Topic.root, path, true, null, false, false);
      } else {
        cur = Topic.I.Get(owner, xElement.Attribute("n").Value, true, null, false, false);
      }
      foreach(var xNext in xElement.Elements("i")) {
        Import(xNext, cur, null);
      }
      Topic.I.Fill(cur, state, manifest, null);
    }
    #endregion Import

    #region IPlugModul Members

    public void Init() {
      if(!Directory.Exists("../data")) {
        Directory.CreateDirectory("../data");
      }

      Topic.I.Init(this);
      _busyFlag = 1;
    }

    public void Start() {
      bool exist = File.Exists("../data/persist.ldb");
      _db = new LiteDatabase("../data/persist.ldb");
      if(exist && !_db.GetCollectionNames().Any(z => z == "objects")) {
        exist = false;
      }
      _objects = _db.GetCollection<BsonDocument>("objects");
      _states = _db.GetCollection<BsonDocument>("states");
      if(!exist) {
        _objects.EnsureIndex("p", true);

        Import("../data/base.xst");

      } else {
        foreach(var obj in _objects.FindAll().OrderBy(z => z["p"])) {
          Topic.I.Create(obj, _states.FindById(obj["_id"]));
        }
      }
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
        if(cmd.art != Perform.Art.setState && cmd.art != Perform.Art.setField) {
          Topic.I.Publish(cmd);
        }
      }
      if(_db != null) {
        using(var tr = _db.BeginTrans()) {
          for(int i = 0; i < _prOp.Count; i++) {
            Store(_prOp[i]);
          }
          tr.Commit();
        }
      }

      //X13.lib.Log.Debug("PLC.Tick QC={0}, PC={1}", qc, _prOp.Count);
      _prOp.Clear();
      _busyFlag = 1;
    }

    public void Stop() {
      var db = Interlocked.Exchange(ref _db, null);
      if(db != null) {
        db.Dispose();
      }
    }

    public bool enabled { get { return true; } set { if(!value) throw new ApplicationException("Repository disabled"); } }
    #endregion IPlugModul Members
  }
}
