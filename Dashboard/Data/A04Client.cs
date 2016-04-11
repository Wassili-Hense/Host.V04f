using JSL = NiL.JS.BaseLibrary;
using JSC = NiL.JS.Core;
using JSF = NiL.JS.Core.Functions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace X13.Data {
  internal class A04Client {
    public readonly DTopic root;
    public readonly Uri url;

    private SioClient _sio;
    private State _st;
    private List<WaitConnect> _connEvnt;

    public A04Client(string host, string password) {
      if(!Uri.TryCreate(host, UriKind.Absolute, out url)) {
        throw new ArgumentException("host");
      }
      _connEvnt = new List<WaitConnect>();
      _sio = new SioClient((url.Scheme == "x13s" ? "wss://" : "ws://") + url.DnsSafeHost + (url.IsDefaultPort ? string.Empty : (":" + url.Port.ToString())) + "/api/v04", ProcessMessage);
      root = new DTopic(this);
    }
    public void Request(string path, int mask, INotMsg req) {
      var arr = new JSL.Array(3);
      arr[0] = 4;
      arr[1] = path;
      arr[2] = mask;
      this.Send(new SioClient.Request(0, arr, req));
    }
    internal void Create(string path, INotMsg req) {
      var arr = new JSL.Array(2);
      arr[0] = 8;
      arr[1] = path;
      this.Send(new SioClient.Request(0, arr, req));
    }
    public void Close() {
      if(_sio != null) {
        _sio.Close();
      }
      _st = State.Disposed;
    }

    private void Send(INotMsg msg) {
      lock(_connEvnt) {
        if(_st == State.Ready) {
          _sio.Send(msg);
        } else if(_st == State.BadAuth) {
          var arr = new JSL.Array(2);
          arr[0] = url.ToString();
          arr[1] = "Bad username or password";
          msg.Response(null, false, arr);
          DWorkspace.This.AddMsg(msg);
        } else {
          _connEvnt.Add(new WaitConnect(msg, this));
        }
      }
    }
    private void ProcessMessage(SioClient.Event e, INotMsg msg) {
      switch(e) {
      case SioClient.Event.Connected:
        if(string.IsNullOrEmpty(url.UserInfo)) {
          _st = State.Ready;
          ReportConnectState(true, null);
        }
        break;
      case SioClient.Event.Disconnected:
        break;
      case SioClient.Event.Ack:
      case SioClient.Event.Error:
        if(msg != null) {
          DWorkspace.This.AddMsg(msg);
        }
        break;
      }
    }
    private void ReportConnectState(bool st, JSC.JSValue value) {
      lock(_connEvnt) {
        foreach(var ce in _connEvnt) {
          ce.Response(null, st, value);
          DWorkspace.This.AddMsg(ce);
        }
        _connEvnt.Clear();
      }
    }
    private enum State {
      Idle,
      Connecting,
      Ready,
      BadAuth,
      Disposed
    }

    private class WaitConnect : INotMsg {
      private INotMsg _req;
      private A04Client _client;
      private bool _success;
      private JSC.JSValue _value;

      public WaitConnect(INotMsg req, A04Client client) {
        _req = req;
        _client = client;
      }
      public void Process(DWorkspace ws) {
        if(_req != null) {
          if(_success) {
            _client.Send(_req);
          } else {
            _req.Response(ws, false, _value);
          }
        }
      }
      public void Response(DWorkspace ws, bool success, JSC.JSValue value) {
        _success = success;
        _value = value;
      }
    }
  }
}
