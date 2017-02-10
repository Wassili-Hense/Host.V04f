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
    private SortedSet<Topic> _subscriptions;
    private Topic _owner;

    public DeskConnection(DeskHostPl pl, TcpClient tcp)
      : base(tcp, null) { //
      base._callback = new Action<DeskMessage>(RcvMsg);
      this._basePl = pl;
      this._subscriptions = new SortedSet<Topic>();
      base.verbose = true;

      // Hello
      var arr = new JSL.Array(2);
      arr[0] = 1;
      arr[1] = Environment.MachineName;
      this.SendArr(arr);
      _owner = Topic.root.Get("/$SYS/Desk").Get(base.ToString());
      
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
          case 99: {
              var o = Interlocked.Exchange(ref _owner, null);
              if(o != null) {
                Log.Info("{0} connection dropped", o.path);
                o.Remove(o);
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
    /// RESPONSE: [5, msgId, [topics]], topic -  array of [path, flags [, state, object]], flags: 1 - present, 16 - hat children
    /// </param>
    private void Subscribe(DeskMessage msg) {
      if(msg.Count != 4 || !msg[1].IsNumber || msg[2].ValueType != JSC.JSValueType.String || !msg[3].IsNumber) {
        Log.Warning("Syntax error: {0}", msg);
        return;
      }
      string path = msg[2].ToString();
      int req = (int)msg[3];
      Topic parent;
      List<Topic> resp = new List<Topic>();
      if(Topic.root.Exist(path, out parent)) {
        resp.Add(parent);
        if((req & 2) == 2) {
          resp.AddRange(parent.children);
        }
        var arr = new JSL.Array();
        foreach(var t in resp) {
          if(_subscriptions.Contains(t)) {
            if((req & 1) != 1 || t != parent) {
              continue;
            }
          } else {
            _subscriptions.Add(t);
            t.Subscribe(SubRec.SubMask.Once | SubRec.SubMask.Chldren, SubscriptionChanged);
          }
          JSL.Array r;
          if((req & 1) == 1 && t == parent) {
            r = new JSL.Array(4);
            r[2] = t.GetState();
            r[3] = t.GetField(null);
          } else {
            r = new JSL.Array(2);
          }
          r[0] = new JSL.String(t.path);
          r[1] = new JSL.Number(t.children.Any() ? 17 : 1);
          arr.Add(r);
        }
        msg.Response(5, msg[1], arr);
      } else {
        msg.Response(5, msg[1], null);
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
    /// REQUEST: [8, msgId, path[, value]]
    /// RESPONSE: [9, msgId, success]
    /// </param>
    private void Create(DeskMessage msg) {
      if(msg.Count < 3 || !msg[1].IsNumber || msg[2].ValueType != JSC.JSValueType.String) {
        Log.Warning("Syntax error: {0}", msg);
        return;
      }
      string path = msg[2].ToString();

      Topic t = Topic.root.Get(path, true, _owner);
      t.SetState(msg[3], _owner);
      msg.Response(9, msg[1], true);
    }

    private void SubscriptionChanged(Perform p) {
    }
  }
}
