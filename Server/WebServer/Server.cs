///<remarks>This file is part of the <see cref="https://github.com/X13home">X13.Home</see> project.<remarks>
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using WebSocketSharp;
using WebSocketSharp.Server;

namespace X13.WebServer {
  [System.ComponentModel.Composition.Export(typeof(IPlugModul))]
  [System.ComponentModel.Composition.ExportMetadata("priority", 9)]
  [System.ComponentModel.Composition.ExportMetadata("name", "Server")]
    public class Server : IPlugModul {
      private List<HttpServer> _srvList;
      private HttpStaticServer _hss;
      private Timer _pingTimer;

      public Server() {
        _srvList=new List<HttpServer>();
        _pingTimer=new Timer(PingF);
        enabled=true;
      }

      private void PingF(object o) {
        for(int i=_srvList.Count-1; i>=0; i--) {
          var r=_srvList[i].WebSocketServices;
          if(r!=null) {
            r.Broadping();
          }
        }
      }
      private HttpServer GetSrv(System.Net.IPAddress address, int port, bool secure) {
        HttpServer r;
        lock(_srvList) {
          r=_srvList.FirstOrDefault(z => z.Port==port);
          if(r==null) {
            r=new HttpServer(address, port, secure);
            _srvList.Add(r);
          }
        }
        return r;
      }

      #region IPlugModul
      public void Init() {
      }

      public void Start() {
        var server=this.GetSrv(System.Net.IPAddress.Any, 80, false);
        _hss=new HttpStaticServer(server);
        server.AddWebSocketService<ApiV04>("/api/v04");
        lock(_srvList) {
          foreach(var srv in _srvList) {
            srv.Start();
            if(srv.IsListening) {
              Log.Info("HttpServer started on {0}:{1} ", srv.Address, srv.Port.ToString());
            } else {
              Log.Error("HttpServer start on {0}:{1} failed", srv.Address, srv.Port.ToString());
            }
          }
        }
        _pingTimer.Change(270000, 300000);
      }

      public void Tick() {
      }

      public void Stop() {
        _pingTimer.Change(-1, -1);
        lock(_srvList) {
          foreach(var srv in _srvList) {
            srv.Stop(CloseStatusCode.Normal, "Exit");
          }
        }
      }

      public bool enabled { get; set; }
      #endregion IPlugModul
    }
}
