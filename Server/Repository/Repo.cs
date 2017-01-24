﻿using LiteDB;
using NiL.JS.Core;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;

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
          if(_prOp[i].art == Perform.Art.changed) {
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
                np = Perform.Create(tmp, c.art, c.src);
                np.o = c.o;
                EnquePerf(np);
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

      case Perform.Art.changed:
      case Perform.Art.set:
      case Perform.Art.setField:
      case Perform.Art.changedField:
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
      if(cmd.art == Perform.Art.remove || (cmd.art == Perform.Art.set && !object.ReferenceEquals(cmd.src.GetValue(), cmd.o))) {
        cmd.old_o = cmd.src.GetValue();
        Topic.I.SetValue(cmd.src, cmd.o as JSValue);
        if(cmd.art != Perform.Art.remove) {
          cmd.art = Perform.Art.changed;
        }
      }
      if(cmd.art == Perform.Art.setField) {
        string fPath = cmd.o as string;
        cmd.old_o = cmd.src.GetField(fPath);
        Topic.I.SetField(cmd.src, fPath, cmd.f_v);
        cmd.art = Perform.Art.changedField;
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
      case Perform.Art.changed:
        if(state != null) {
          _states.Upsert(state);
        }
        break;
      case Perform.Art.changedField:
      case Perform.Art.move:
        _objects.Update(obj);
        if((cmd.o as string) == "s") {
          if(cmd.src.saved) {
            Topic.I.SetValue(cmd.src, cmd.src.GetValue());
            if(state != null) {
              _states.Upsert(state);
            }
          } else {
            _states.Delete(obj["_id"]);
          }
        }
        break;
      case Perform.Art.create:
        _objects.Insert(obj);
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
        if(cmd.art != Perform.Art.set && cmd.art!=Perform.Art.setField) {
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

    public bool enabled { get { return true; } set { } }
    #endregion IPlugModul Members
  }
}