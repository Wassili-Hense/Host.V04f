using JSL = NiL.JS.BaseLibrary;
using JSC = NiL.JS.Core;
using JSF = NiL.JS.Core.Functions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WebSocketSharp;

namespace X13 {
  internal class Client {
    private static SortedList<string, Client> _clients;

    static Client() {
      _clients = new SortedList<string, Client>();
    }

    public static Topic Get(Uri url, bool create) {
      var up = Uri.UnescapeDataString(url.UserInfo).Split(':');
      string uName = (up.Length > 0 && !string.IsNullOrWhiteSpace(up[0])) ? (up[0] + "@") : string.Empty;
      string host= url.Scheme + "://" + uName + url.DnsSafeHost + (url.IsDefaultPort ? string.Empty : ":" + url.Port.ToString()) + "/";
      Client cl;
      if(!_clients.TryGetValue(host, out cl)) {
        lock(_clients) {
          if(!_clients.TryGetValue(host, out cl)) {
            cl = new Client(host, up.Length == 2 ? up[1] : string.Empty);
            _clients[host] = cl;
          }
        }
      }
      return cl.root.Get(url.LocalPath, create);
    }

    public readonly Topic root;
    public readonly Uri url;
    private readonly string _uPass;

    private string _clientId;
    private WebSocket _ws;
    private State _st;
    private System.Threading.Timer _reconn;
    private int _rccnt;
    private bool? _verbose;
    private bool _waitPong;
    private long _respCnt;
    private SortedList<string, Action<EventArguments>> _events;
    private SortedList<long, Action<EventArguments>> _response;

    public Client(string host, string password) {
      if(!Uri.TryCreate(host, UriKind.Absolute, out url)) {
        throw new ArgumentException("host");
      }
      _events = new SortedList<string, Action<EventArguments>>();
      _response = new SortedList<long, Action<EventArguments>>();
      _respCnt = 1;
      _uPass = password;
      root = new Topic(this);
      _st = State.Connecting;
      _reconn = new System.Threading.Timer(CheckState, null, 100, -1);
      _rccnt = 1;
      _verbose = true;
      Connect();
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
      if(_st == State.BadAuth) {
        return;
      }
      _ws = new WebSocket(url.Scheme+ "://"+ url.DnsSafeHost+ ":"+ url.Port.ToString() + "/api/v04");
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
        Log.Info("client connected to {0}", url);
      }
    }
    private void _ws_OnClose(object sender, CloseEventArgs e) {
      if(e.Code == 1000 || e.Code==1001) {
        Log.Info("Client - disconnected[{0}]", (CloseStatusCode)e.Code);
      } else {
        Log.Warning("Client - disconnected[{0}]", (CloseStatusCode)e.Code);
      }
      if(_st == State.Dispose) {
        _reconn.Change(-1, -1);
        _ws = null;
      }
    }
    private void _ws_OnError(object sender, WebSocketSharp.ErrorEventArgs e) {
      _st = State.NoAnswer;
      if(_verbose.Value) {
        Log.Warning("client - " + e.Message);
      }
      _reconn.Change(_rccnt * 15000-(DateTime.Now.Ticks&0x1FFF), _rccnt * 30000);
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
          Log.Debug("{0} R {1}", url, e.Data);
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
            if(vi.ValueType == JSC.JSValueType.Integer || vi.ValueType==JSC.JSValueType.Double ) {
              int pi=(int)vi;
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
            EventArguments ea;
            switch(e.Data[1]) {
            case '0':  // CONNECT
              _st = State.Ready;
              this.Emit(4, "/", 2, new Action<EventArguments>(SubscribeResp));
              break;
            case '2':  // EVENT
              EventArguments.ParseEvent(this, e.Data);
              break;
            case '1':  // DISCONNECT
            case '3':  // ACK
              ea = EventArguments.Parse(e.Data);
              if(ea != null) {
                Action<EventArguments> f;
                lock(_response) {
                  if(!_response.TryGetValue(ea.msgId, out f)) {
                    f = null;
                  } else {
                    _response.Remove(ea.msgId);
                  }
                }
                if(f != null) {
                  try {
                    f(ea);
                  }
                  catch(Exception ex) {
                    Log.Warning("{0} R {1} - {2}", url, e.Data, ex);
                  }
                }
              }
              break;
            case '4':  // ERROR
              Log.Warning("{0} R {1}", url, e.Data);
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
          Log.Debug("{0} S {1}", url, msg);
        }
      }
    }
    private void SubscribeResp(EventArguments a) {

    }

    public void Register(JSC.JSValue name, Action<EventArguments> func) {
      lock(_events) {
        _events[JSL.JSON.stringify(name, null, null)] = func;
      }
    }
    public void Emit(params object[] args) {
      if(args == null || args.Length == 0) {
        return;
      }
      int len = args.Length;
      string header;
      Action<EventArguments> cb;
      if((cb = args[len - 1] as Action<EventArguments>) != null) {
        len--;
        long c=System.Threading.Interlocked.Increment(ref _respCnt);
        header = "42" + c.ToString();
        lock(_response) {
          _response[c] = cb;
        }
      } else {
        header = "42";
      }
      var r = new JSL.Array(len);
      for(int i = 0; i < len; i++) {
        r[i] = args[i] as JSC.JSValue??JSC.JSValue.Marshal(args[i]);
      }
      string msg = JSL.JSON.stringify(r, null, null);
      this.Send(header + msg);
    }

    public class EventArguments {
      private static JSF.ExternalFunction _JSON_Replacer;
      static EventArguments() {
        _JSON_Replacer = new JSF.ExternalFunction(ConvertDate);
      }
      private static JSC.JSValue ConvertDate(JSC.JSValue thisBind, JSC.Arguments args) {
        if(args.Length == 2 && args[1].ValueType == JSC.JSValueType.String) {
          // 2015-09-16T14:15:18.994Z
          var s = args[1].ToString();
          if(s != null && s.Length == 24 && s[4] == '-' && s[7] == '-' && s[10] == 'T' && s[13] == ':' && s[16] == ':' && s[19] == '.') {
            var a = new JSC.Arguments();
            a.Add(args[1]);
            var jdt = new JSL.Date(a);
            return JSC.JSValue.Wrap(jdt);
          }
        }
        return args[1];
      }
      public static EventArguments Parse(string msg) {
        long msgId;
        int idx = msg.IndexOf('[', 2);
        if(idx < 3 || !long.TryParse(msg.Substring(2, idx - 2), out msgId)) {
          msgId = -1;
        }
        var jo = JSL.JSON.parse(msg.Substring(idx), _JSON_Replacer) as JSL.Array;
        if(jo == null) {
          return null;
        }
        return new EventArguments(msgId, jo);
      }
      public static bool ParseEvent(Client conn, string msg) {
        string sb;
        string tmpS;
        long msgId;
        int idx = msg.IndexOf('[', 2);
        if(idx < 3 || !long.TryParse(msg.Substring(2, idx - 2), out msgId)) {
          msgId = -1;
        }
        tmpS = msg.Substring(idx);
        sb = conn.ToString() + "\n  REQ : " + tmpS + "\n  ";
        var jo = JSL.JSON.parse(tmpS, _JSON_Replacer) as JSL.Array;
        if(jo == null) {
          return false;
        }
        Action<EventArguments> f;
        lock(conn._events) {
          if(!conn._events.TryGetValue(JSL.JSON.stringify(jo[0], null, null), out f)) {
            f = null;
          }
        }
        if(f == null) {
          tmpS = string.Concat(msgId < 0 ? string.Empty : msgId.ToString(), "[\"Not found\",", JSL.JSON.stringify(jo[0], null, null), "]");
          conn.Send("44" + tmpS);
          Log.Warning("{0}ERROR: {1}", sb, tmpS);
          return false;
        }
        EventArguments ea = new EventArguments(msgId, jo);
        try {
          f(ea);
          if(ea.msgId >= 0 || (ea._error && ea._response != null)) {
            tmpS = ea._response == null ? "[]" : JSL.JSON.stringify(ea._response, null, null);
            conn.Send(string.Concat("4", ea._error ? "4" : "3", msgId < 0 ? string.Empty : msgId.ToString(), tmpS));
            if(ea._error) {
              Log.Warning("{0}ERROR: {1}", sb, tmpS);
            } else {
              Log.Debug("{0}RESP: {1}", sb, tmpS);
            }
          }
        }
        catch(Exception ex) {
          tmpS = "[\"Internal error\"," + JSL.JSON.stringify(jo[0], null, null) + "]";
          conn.Send(string.Concat("44", msgId < 0 ? string.Empty : msgId.ToString(), tmpS));
          Log.Warning("{0}EXCEPTION: {1}", sb.ToString(), ex.ToString());
        }
        return true;
      }

      public readonly long msgId;
      private JSL.Array _request;
      private JSL.Array _response;
      private bool _error;

      public readonly int Count;

      private EventArguments(long msgId, JSL.Array req) {
        this.msgId = msgId;
        this._request = req;
        this.Count = req.Count();
      }

      public JSC.JSValue this[int idx] {
        get {
          return _request[idx];
        }
      }
      public void Response(params object[] args) {
        _response = new JSL.Array(args.Length);
        for(int i = 0; i < args.Length; i++) {
          _response[i] = args[i] as JSC.JSValue??JSC.JSValue.Marshal(args[i]);
        }
        _error = false;
      }
      public void Error(params object[] args) {
        Response(args);
        _error = true;
      }
    }

    public override string ToString() {
      return url.ToString();
    }

    private enum State {
      Connecting,
      Ready,
      NoAnswer,
      BadAuth,
      Dispose,
    }
  }
}
