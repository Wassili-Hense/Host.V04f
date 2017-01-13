using NiL.JS.Core;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

namespace X13.Repository {
  [System.ComponentModel.Composition.Export(typeof(IPlugModul))]
  [System.ComponentModel.Composition.ExportMetadata("priority", 1)]
  [System.ComponentModel.Composition.ExportMetadata("name", "Repository")]
  public class Repo : IPlugModul {
    #region exemplar internal
    private ConcurrentQueue<Perform> _tcQueue;
    private List<Perform> _prOp;
    private int _busyFlag;
    private int _pfPos;

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
          if(_prOp[i].EqualsEx(cmd)) {
            return i;
          }
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
      switch(c.art) {
      case Perform.Art.changed:
      case Perform.Art.set:
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
        Topic.I.SetValue(cmd.src, JSValue.Marshal(cmd.o));
        if(cmd.art != Perform.Art.remove) {
          cmd.art = Perform.Art.changed;
        }
      }
      if(cmd.art == Perform.Art.remove) {
        Topic.I.Remove(cmd.src);
      }

    }
    #endregion exemplar internal

    public Repo() {
      _tcQueue = new ConcurrentQueue<Perform>();
      _prOp = new List<Perform>(128);
    }

    #region IPlugModul Members

    public void Init() {
      Topic.I.Init(this);
      _busyFlag = 1;
    }

    public void Start() {
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

      //for(_pfPos = 0; _pfPos < _prOp.Count; _pfPos++) {
      //  cmd = _prOp[_pfPos];
      //  if(cmd.art != Perform.Art.set) {
      //    cmd.src.Publish(cmd);
      //  }
      //}

      //X13.lib.Log.Debug("PLC.Tick QC={0}, PC={1}", qc, _prOp.Count);

      _busyFlag = 1;
    }

    public void Stop() {
    }

    public bool enabled { get { return true; } set {  } }
    #endregion IPlugModul Members
  }
}
