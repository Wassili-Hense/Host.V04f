///<remarks>This file is part of the <see cref="https://github.com/X13home">X13.Home</see> project.<remarks>
using JSL = NiL.JS.BaseLibrary;
using JSC = NiL.JS.Core;
using JSF = NiL.JS.Core.Functions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WebSocketSharp;

namespace X13.Data {
  internal class SioClient {
    private string _url;
    private string _clientId;
    private WebSocket _ws;
    private State _st;
    private System.Threading.Timer _reconn;
    private int _rccnt;
    private bool? _verbose;
    private bool _waitPong;
    private long _respCnt;
    private Action<Event, INotMsg> _callback;
    private System.Collections.Generic.LinkedList<Request> _reqs;

    public SioClient(string url, Action<Event, INotMsg> cb) {
      _url = url;
      _callback = cb;
      _respCnt = 0;
      _reqs = new LinkedList<Request>();
      _st = State.Connecting;
      _rccnt = 1;
      _verbose = true;
      _reconn = new System.Threading.Timer(CheckState, null, 100, -1);
      Connect();
    }
    public void Send(INotMsg msg) {
      Request req;
      //Response resp;
      if((req = msg as Request) != null) {
        string header;
        if(req.msgId == -1) {
          header = "42";
        } else {
          req.msgId = System.Threading.Interlocked.Increment(ref _respCnt);
          lock(_reqs) {
            _reqs.AddFirst(req);
          }
          header = "42" + req.msgId.ToString();
        }
        this.Send(header + JSL.JSON.stringify(req.data, null, null));
      //} else if((resp = msg as Response) != null) {
      //  if(resp.req == null) {
      //    throw new ArgumentNullException("msg.req");
      //  }
      //  if(resp.req.msgId == -1) {
      //    return;         // response is not required
      //  }
      //  this.Send((resp.error?"44":"43") + resp.req.msgId.ToString() + JSL.JSON.stringify(resp.data, null, null));
      } else {
        throw new ArgumentException("msg");
      }
    }
    public void Close() {
      _st = State.Dispose;
      _reconn.Change(-1, -1);
      Send("41");
      _ws.Close(CloseStatusCode.Normal);
      _ws = null;
    }

    private void CheckState(object o) {
      if(_st == State.Ready && (_ws == null || _ws.ReadyState != WebSocketState.Open)) {
        _rccnt = 1;
      } else if(_st == State.NoAnswer) {
        if(_rccnt < 120) {
          _rccnt++;
        }
      } else {
        _rccnt = 1;
        if(!_waitPong || (_rccnt++) < 3) {
          _waitPong = true;
          Send("2");
          return;
        } else {
          Log.Warning("Pong timeout");
        }
      }
      Connect();
    }
    private void Connect() {
      if(_ws != null) {
        if(_ws.IsAlive) {
          _ws.Close(CloseStatusCode.Normal);
        }
        _ws = null;
      }
      _ws = new WebSocket(_url);
      _ws.Log.Output = WsLog;
      _ws.OnOpen += _ws_OnOpen;
      _ws.OnMessage += _ws_OnMessage;
      _ws.OnError += _ws_OnError;
      _ws.OnClose += _ws_OnClose;
      _ws.ConnectAsync();
      _reconn.Change(_rccnt * 15000 - (DateTime.Now.Ticks & 0x1FFF), _rccnt * 30000);
    }
    private void _ws_OnOpen(object sender, EventArgs e) {
      if(_verbose == true) {
        Log.Info("SioClient connected to {0}", _url);
      }
    }
    private void _ws_OnClose(object sender, CloseEventArgs e) {
      if(e.Code == 1000 || e.Code == 1001) {
        Log.Info("SioClient - disconnected[{0}]", (CloseStatusCode)e.Code);
      } else {
        Log.Warning("SioClient - disconnected[{0}]", (CloseStatusCode)e.Code);
      }
      if(_st == State.Dispose) {
        _reconn.Change(-1, -1);
        _ws = null;
      } else {
        _callback(Event.Disconnected, null);
      }
    }
    private void _ws_OnError(object sender, WebSocketSharp.ErrorEventArgs e) {
      _st = State.NoAnswer;
      if(_verbose.Value) {
        Log.Warning("client - " + e.Message);
      }
      _reconn.Change(_rccnt * 15000 - (DateTime.Now.Ticks & 0x1FFF), _rccnt * 30000);
      _callback(Event.Disconnected, null);
    }
    private void WsLog(LogData d, string f) {
      if(_verbose.Value) {
        Log.Debug("client({0}) - {1}", d.Level, d.Message);
      }
    }
    private void _ws_OnMessage(object sender, MessageEventArgs e) {
      if(e.Type == Opcode.Text && !string.IsNullOrEmpty(e.Data)) {
        if(e.Data == null || e.Data.Length == 0) {
          return;
        }
        if(_verbose.Value) {
          Log.Debug("{0} R {1}", _url, e.Data);
        }
        JSC.JSValue jv;
        switch(e.Data[0]) {   // Engine.IO
        case '0':   // open: Sent from the server when a new transport is opened
          if(e.Data.Length > 1 && e.Data[1] == '{') {
            jv = JSL.JSON.parse(e.Data.Substring(1));
            var vi = jv.GetProperty("sid");
            if(vi.ValueType == JSC.JSValueType.String) {
              _clientId = vi.Value as string;
            }
            vi = jv.GetProperty("pingInterval");
            if(vi.ValueType == JSC.JSValueType.Integer || vi.ValueType == JSC.JSValueType.Double) {
              int pi = (int)vi;
              _reconn.Change(pi, pi);
            }
          }
          break;
        case '1':   // close: Request the close of this transport but does not shutdown the connection itself.
          break;
        case '3':   // pong: Sent by the server to respond to ping packets.
          _waitPong = false;
          _rccnt = 1;
          break;
        case '4':   // message: actual message, client and server should call their callbacks with the data.
          if(e.Data.Length > 1) {
            long msgId;
            int idx = e.Data.IndexOf('[', 2);
            if(idx < 3 || !long.TryParse(e.Data.Substring(2, idx - 2), out msgId)) {
              msgId = -1;
            }
            JSL.Array jo;

            jo = idx>1?JSL.JSON.parse(e.Data.Substring(idx), DWorkspace._JSON_Replacer) as JSL.Array:null;

            switch(e.Data[1]) {
            case '0':  // CONNECT
              _st = State.Ready;
              _callback(Event.Connected, null);
              break;
            case '1':  // DISCONNECT
              _st = State.Idle;
              _callback(Event.Disconnected, null);
              break;
            case '2':  // EVENT
              _callback(Event.Event, new DTopic.Event(jo));
              break;
            case '3':  // ACK
              {
                Request req;
                lock(_reqs) {
                  req=_reqs.FirstOrDefault(z => z.msgId == msgId);
                  if(req != null) {
                    _reqs.Remove(req);
                  }
                }
                if(req != null) {
                  req.Response(null, true, jo);
                  _callback(Event.Ack, req);
                }
              }
              break;
            case '4':  // ERROR
              {
                Request req;
                lock(_reqs) {
                  req=_reqs.FirstOrDefault(z => z.msgId == msgId);
                  if(req != null) {
                    _reqs.Remove(req);
                  }
                }
                if(req != null) {
                  req.Response(null, false, jo);
                  _callback(Event.Error, req);
                }
              }
              break;
            case '5':  // BINARY_EVENT
            case '6':  // BINARY_ACK
              break;
            }
          }
          break;
        }
      }
    }
    private void Send(string msg) {
      if(_ws != null && _ws.ReadyState == WebSocketState.Open) {
        _ws.Send(msg);
        if(_verbose.Value) {
          Log.Debug("{0} S {1}", _url, msg);
        }
      }
    }

    private enum State {
      Idle,
      Connecting,
      Ready,
      NoAnswer,
      Dispose,
    }
    public enum Event {
      Connected, 
      Disconnected,
      Event,
      Ack,
      Error,
    }
    public class Request : INotMsg {
      public long msgId;
      public JSL.Array data;
      private JSC.JSValue _resp;
      private INotMsg _req;
      private bool _success;

      public Request(long msgId, JSL.Array jo, INotMsg req) {
        this.msgId = msgId;
        this.data = jo;
        this._req = req;
      }
      public void Process(DWorkspace ws) {
        if(_req != null) {
          _req.Response(ws, _success, _resp);
          ws.AddMsg(_req);
        }
      }
      public void Response(DWorkspace ws, bool success, JSC.JSValue value) {
        _resp = value;
        _success = success;
      }
    }
  }
}
