﻿///<remarks>This file is part of the <see cref="https://github.com/X13home">X13.Home</see> project.<remarks>
using JSC = NiL.JS.Core;
using JSL = NiL.JS.BaseLibrary;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using X13.Repository;

namespace X13.DeskHost {
  [System.ComponentModel.Composition.Export(typeof(IPlugModul))]
  [System.ComponentModel.Composition.ExportMetadata("priority", 8)]
  [System.ComponentModel.Composition.ExportMetadata("name", "DeskHost")]
  public class DeskHostPl : IPlugModul {
    #region internal Members
    private TcpListener _tcp;
    private System.Collections.Concurrent.ConcurrentBag<DeskConnection> _connections;
    private System.Collections.Concurrent.ConcurrentBag<DeskMessage> _msgs;

    private void Connect(IAsyncResult ar) {
      try {
        TcpClient c = _tcp.EndAcceptTcpClient(ar);
        _connections.Add(new DeskConnection(this, c));
      }
      catch(ObjectDisposedException) {
        return;   // Socket allready closed
      }
      catch(NullReferenceException) {
        return;   // Socket allready destroyed
      }
      catch(SocketException) {
      }
      _tcp.BeginAcceptTcpClient(new AsyncCallback(Connect), null);
    }

    internal void AddRMsg(DeskMessage msg) {
      _msgs.Add(msg);
    }

    #endregion internal Members

    public DeskHostPl() {
      _connections = new System.Collections.Concurrent.ConcurrentBag<DeskConnection>();
      _msgs = new System.Collections.Concurrent.ConcurrentBag<DeskMessage>();
      enabled = true;
    }

    #region IPlugModul Members
    public void Init() {
      _tcp = new TcpListener(IPAddress.Any, 10013);
      _tcp.Start();
    }
    public void Start() {
      _tcp.BeginAcceptTcpClient(new AsyncCallback(Connect), null);
    }
    public void Tick() {
      DeskMessage msg;
      while(_msgs.TryTake(out msg)) {
        if(msg.Count == 0) {
          continue;
        }
        try {
          if(msg[0].IsNumber) {
            var conn = (DeskConnection)msg._conn;
            switch((int)msg[0]) {
            case 4:
              conn.Subscribe(msg);
              break;
            }
          }
        }
        catch(Exception ex) {
          Log.Warning("{0} - {1}", msg, ex);
        }
      }
    }
    public void Stop() {
      if(_tcp == null) {
        return;
      }
      foreach(var cl in _connections.ToArray()) {
        try {
          cl.Dispose();
        }
        catch(Exception) {
        }
      }
      _tcp.Stop();
      _tcp = null;
    }

    public bool enabled { get; set; }
    #endregion IPlugModul Members
  }
}
