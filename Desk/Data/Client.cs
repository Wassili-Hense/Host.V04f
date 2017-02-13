///<remarks>This file is part of the <see cref="https://github.com/X13home">X13.Home</see> project.<remarks>
using JSC = NiL.JS.Core;
using JSL = NiL.JS.BaseLibrary;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.IO;
using System.Threading;

namespace X13.Data {
  internal class Client {
    private DeskHost.DeskSocket _socket;
    private State _st;
    private List<WaitConnect> _connEvnt;
    private int _msgId;
    private System.Collections.Generic.LinkedList<ClRequest> _reqs;


    public readonly string server;
    public readonly int port;
    public readonly string userName;
    public readonly string password;
    public string alias { get; set; }
    public DTopic root { get; private set; }

    public Client(string server, int port, string userName, string password) {
      this.server = server;
      this.port = port;
      this.userName = userName;
      this.password = password;
      _connEvnt = new List<WaitConnect>();
      _reqs = new LinkedList<ClRequest>();
      root = new DTopic(this);
    }
    public bool Connect() {
      _st = State.Connecting;
      try {
        var tcp = new TcpClient();
        tcp.Connect(server, port);
        _socket = new DeskHost.DeskSocket(tcp, onRecv);
        _socket.verbose = true;
      }
      catch(Exception ex) {
        Log.Warning("{0}.Connect - {1}", this.ToString(), ex.Message);
        _st = State.Idle;
        return false;
      }
      return true;
    }

    public void SendReq(int cmd, INotMsg req, params JSC.JSValue[] arg) {
      int mid = Interlocked.Increment(ref _msgId);
      var arr = new JSL.Array(arg.Length+2);
      arr[0] = cmd;
      arr[1] = mid;
      for(int i=0; i<arg.Length; i++) {
        arr[i+2]=arg[i];
      }
      this.Send(new ClRequest(mid, arr, req));
    }

    public void Close() {
      var sc = Interlocked.Exchange(ref _socket, null);
      if(sc != null) {
        sc.Dispose();
      }
    }

    public override string ToString() {
      return "x13://" + ((userName == null ? string.Empty : (userName + "@")) + server + (port != DeskHost.DeskSocket.portDefault ? (":" + port.ToString()) : string.Empty));
    }

    private void Send(INotMsg msg) {
      if(_st == State.Ready) {
        ClRequest req;
        if((req = msg as ClRequest) != null) {
          if(req.msgId >= 0) {
            lock(_reqs) {
              _reqs.AddFirst(req);
            }
          }
          _socket.SendArr(req.data);
        } else {
          throw new ArgumentException("msg");
        }
      } else if(_st == State.BadAuth) {
        var arr = new JSL.Array(2);
        arr[0] = this.ToString();
        arr[1] = "Bad username or password";
        msg.Response(null, false, arr);
        App.PostMsg(msg);
      } else {
        lock(_connEvnt) {
          _connEvnt.Add(new WaitConnect(msg, this));
        }
        if(_st == State.Idle) {
          this.Connect();
        }
      }
    }
    private void onRecv(DeskHost.DeskMessage msg) {
      int cmd, msgId;
      ClRequest req;

      if(!msg[0].IsNumber || (cmd = (int)msg[0]) <= 0) {
        return;
      }
      switch(cmd) {
      case 1:   // [Hello, (<string> server name)]
        if(msg.Count > 1 && msg[1].ValueType == JSC.JSValueType.String) {
          if(alias == null) {
            alias = msg[1].Value as string;
          }
          Log.Info("{0} connected as {1}", this.ToString(), alias);
          _st = State.Ready;
          lock(_connEvnt) {
            foreach(var ce in _connEvnt) {
              ce.Response(null, true, null);
              App.PostMsg(ce);
            }
            _connEvnt.Clear();
          }
        }
        break;
      case 4:  // [SubscribeResp, path, flags, state, manifest]
        {
          if(msg.Count != 5 || msg[1].ValueType!=JSC.JSValueType.String || !msg[2].IsNumber) {
            Log.Warning("Synax error {0}", msg);
            break;
          }
          DTopic.SubscribeResp(this.root, msg[1].Value as string, (int)msg[2], msg[3], msg[4]);
        }
        break;
      case 5:  // [SubAck, msgId, exist]
      case 7:  // [SetStateAck, msgIs, success<bool>, [oldValue] ]
      case 9:  // [CreateAck, msgId, success]
        msgId = (int)msg[1];
        lock(_reqs) {
          req=_reqs.FirstOrDefault(z => z.msgId == msgId);
          if(req != null) {
            _reqs.Remove(req);
          }
        }
        if(req != null) {
          if(cmd == 5) {
            req.Response(null, true, msg[2]);
          } else {
            req.Response(null, (bool)msg[2], msg.Count > 3 ? msg[3] : null);
          }
          App.PostMsg(req);
        }
        break;
      }
    }

    private enum State {
      Idle,
      Connecting,
      Ready,
      BadAuth,
      Disposed
    }

    private class ClRequest : INotMsg {
      public int msgId;
      public JSL.Array data;
      private JSC.JSValue _resp;
      private INotMsg _req;
      private bool _success;

      public ClRequest(int msgId, JSL.Array jo, INotMsg req) {
        this.msgId = msgId;
        this.data = jo;
        this._req = req;
      }
      public void Process(DWorkspace ws) {
        if(_req != null) {
          _req.Response(ws, _success, _resp);
          App.PostMsg(_req);
        }
      }
      public void Response(DWorkspace ws, bool success, JSC.JSValue value) {
        _resp = value;
        _success = success;
      }
      public override string ToString() {
        return "ClRequest: " + data.ToString() + _resp == null ? string.Empty : (" >> " + _success.ToString());
      }
    }

    private class WaitConnect : INotMsg {
      private INotMsg _req;
      private Client _client;
      private bool _success;
      private JSC.JSValue _value;

      public WaitConnect(INotMsg req, Client client) {
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

      public override string ToString() {
        return "WaitConnect: " + _success.ToString();
      }
    }
  }
}
