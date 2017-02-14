///<remarks>This file is part of the <see cref="https://github.com/X13home">X13.Home</see> project.<remarks>
using JSC = NiL.JS.Core;
using JSL = NiL.JS.BaseLibrary;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using X13.Repository;

namespace X13.DeskHost {
  internal class DeskConnection : DeskSocket {
    private DeskHostPl _basePl;
    private Topic _owner;
    private List<Tuple<SubRec, DeskMessage>> _subscriptions;
    private Action<Perform> _subCB;

    public DeskConnection(DeskHostPl pl, TcpClient tcp)
      : base(tcp, null) { //
      base._callback = new Action<DeskMessage>(RcvMsg);
      this._basePl = pl;
      this._subCB = new Action<Perform>(SubscriptionChanged);
      this._subscriptions = new List<Tuple<SubRec, DeskMessage>>();
      base.verbose = true;

      // Hello
      var arr = new JSL.Array(2);
      arr[0] = 1;
      arr[1] = Environment.MachineName;
      this.SendArr(arr);
      _owner = Topic.root.Get("/$YS/Desk").Get(base.ToString());

      _owner.SetField("Desk.Address", EndPoint.Address.ToString(), _owner);
      _owner.SetField("Desk.Port", EndPoint.Port, _owner);
      System.Net.Dns.BeginGetHostEntry(EndPoint.Address, EndDnsReq, null);
    }
    private void EndDnsReq(IAsyncResult ar) {
      try {
        var h = Dns.EndGetHostEntry(ar);
        _owner.SetState(h.HostName, _owner);
      }
      catch(SocketException) {
        _owner.SetState(EndPoint.ToString(), _owner);
      }
    }
    private void RcvMsg(DeskMessage msg) {
      if(msg.Count == 0) {
        return;
      }
      try {
        if(msg[0].IsNumber) {
          switch((int)msg[0]) {
          case 4:
            this.Subscribe(msg);
            break;
          case 6:
            this.SetState(msg);
            break;
          case 8:
            this.Create(msg);
            break;
          case 10:
            this.Move(msg);
            break;
          case 12:
            this.Remove(msg);
            break;
          case 99: {
              var o = Interlocked.Exchange(ref _owner, null);
              if(o != null) {
                Log.Info("{0} connection dropped", o.path);
                o.Remove(o);
                foreach(var sr in _subscriptions) {
                  sr.Item1.Dispose();
                }
              }
            }
            break;    // Disconnect
          }
        } else {
          _basePl.AddRMsg(msg);
        }
      }
      catch(Exception ex) {
        Log.Warning("{0} - {1}", msg, ex);
      }
    }
    /// <summary>Subscribe topics</summary>
    /// <param name="args">
    /// REQUEST:  [4, msgId, path, mask], mask: 1 - data, 2 - children
    /// RESPONSE: [5, msgId, success, exist]
    /// </param>
    private void Subscribe(DeskMessage msg) {
      if(msg.Count != 4 || !msg[1].IsNumber || msg[2].ValueType != JSC.JSValueType.String || !msg[3].IsNumber) {
        Log.Warning("Syntax error: {0}", msg);
        return;
      }
      Topic parent = Topic.root.Get(msg[2].Value as string, false, _owner);
      if(parent != null) {
        SubRec.SubMask m = SubRec.SubMask.Value | SubRec.SubMask.Field | SubRec.SubMask.Chldren;
        if(parent == Topic.root) {
          m |= SubRec.SubMask.Once;
        }
        var sr = parent.Subscribe(m, string.Empty, _subCB);
        _subscriptions.Add(new Tuple<SubRec,DeskMessage>(sr, msg));
      } else {
        msg.Response(5, msg[1], true, false);
      }
    }
    /// <summary>set topics state</summary>
    /// <param name="args">
    /// REQUEST: [6, msgId, path, value]
    /// RESPONSE: [7, msgId, success, [oldvalue] ]
    /// </param> 
    private void SetState(DeskMessage msg) {
      if(msg.Count != 4 || !msg[1].IsNumber || msg[2].ValueType != JSC.JSValueType.String) {
        Log.Warning("Syntax error: {0}", msg);
        return;
      }
      string path = msg[2].ToString();

      Topic t = Topic.root.Get(path, false, _owner);
      t.SetState(msg[3], _owner);
      msg.Response(7, msg[1], true);
    }
    /// <summary>Create topic</summary>
    /// <param name="args">
    /// REQUEST: [8, msgId, path[, prototype]]
    /// RESPONSE: [9, msgId, success]
    /// </param>
    private void Create(DeskMessage msg) {
      if(msg.Count < 3 || !msg[1].IsNumber || msg[2].ValueType != JSC.JSValueType.String) {
        Log.Warning("Syntax error: {0}", msg);
        return;
      }
      Topic p, t = Topic.root.Get(msg[2].Value as string, true, _owner);
      string pn;
      JSC.JSValue jsp;
      if(msg.Count==4 && msg[3].ValueType==JSC.JSValueType.String && !string.IsNullOrWhiteSpace(pn = msg[3].Value as string)){
        if(( _basePl.TypesCore.Exist(pn, out p) || _basePl.Types.Exist(pn, out p) ) && ( jsp = p.GetState() ).ValueType==JSC.JSValueType.Object && jsp.Value!=null) {
          t.SetState(jsp["default"], _owner);
          jsp = jsp["prototype"];
          if(jsp.ValueType==JSC.JSValueType.Object && jsp.Value!=null) {
            t.SetField("__proto__", jsp, _owner);
          }
        } else {
          Log.Warning("Create({0}, {1}) - unknown prototype", t.path, pn);
        }
      }
      var sr = t.Subscribe(SubRec.SubMask.Value | SubRec.SubMask.Field | SubRec.SubMask.Chldren, string.Empty, _subCB);
      _subscriptions.Add(new Tuple<SubRec, DeskMessage>(sr, msg));
    }
    /// <summary>Move topic</summary>
    /// <param name="args">
    /// REQUEST: [10, msgId, path source, path destinations parent, new name(optional rename)]
    /// </param>
    private void Move(DeskMessage msg) {
      if(msg.Count < 5 || !msg[1].IsNumber || msg[2].ValueType != JSC.JSValueType.String || msg[3].ValueType != JSC.JSValueType.String
        || (msg.Count > 5 && msg[4].ValueType != JSC.JSValueType.String)) {
        Log.Warning("Syntax error: {0}", msg);
        return;
      }
      Topic t = Topic.root.Get(msg[2].Value as string, false, _owner);
      Topic p = Topic.root.Get(msg[3].Value as string, false, _owner);
      if(t != null && p != null) {
        string nname = msg.Count < 5 ? t.name : (msg[4].Value as string);
        t.Move(p, nname, _owner);
      }
    }
    /// <summary>Remove topic</summary>
    /// <param name="args">
    /// REQUEST: [12, msgId, path]
    /// </param>
    private void Remove(DeskMessage msg) {
      if(msg.Count != 3 || !msg[1].IsNumber || msg[2].ValueType != JSC.JSValueType.String) {
        Log.Warning("Syntax error: {0}", msg);
        return;
      }
      Topic t = Topic.root.Get(msg[2].Value as string, false, _owner);
      if(t != null) {
        t.Remove(_owner);
      }
    }

    private void SubscriptionChanged(Perform p) {
      JSL.Array arr;
      switch(p.art) {
      case Perform.Art.create:
      case Perform.Art.subscribe: {
          arr = new JSL.Array(5);
          arr[0] = new JSL.Number(4);
          arr[1] = new JSL.String(p.src.path);
          arr[2] = new JSL.Number(p.src.children.Any() ? 17 : 1);
          arr[3] = p.src.GetState();
          arr[4] = p.src.GetField(null);
          base.SendArr(arr);
        }
        break;
      case Perform.Art.subAck: {
          var sr = p.o as SubRec;
          if(sr != null) {
            foreach(var msg in _subscriptions.Where(z => z.Item1 == sr && z.Item2!=null).Select(z => z.Item2)) {
              msg.Response(1+(int)msg[0], msg[1], true, true);
            }
          }
        }
        break;
      case Perform.Art.changedState:
        if(p.prim != _owner) {
          arr = new JSL.Array(3);
          arr[0] = new JSL.Number(6);
          arr[1] = new JSL.String(p.src.path);
          arr[2] = p.src.GetState();
          base.SendArr(arr);
        }
        break;
      default:
        Log.Debug("Desk.Sub = {0}", p);
        break;
      }
    }
  }
}
