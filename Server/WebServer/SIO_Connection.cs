using JSL=NiL.JS.BaseLibrary;
using JSC=NiL.JS.Core;
using JSF=NiL.JS.Core.Functions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using WebSocketSharp;
using WebSocketSharp.Net;
using WebSocketSharp.Server;

namespace X13.WebServer {
  public class SIO_Connection : WebSocketBehavior {  // Socket.IO servers connection
    public static JSC.JSObject obg2jso(object o) {
      JSC.JSObject jo;
      switch(Type.GetTypeCode(o==null?null:o.GetType())) {
      case TypeCode.Boolean:
        jo=new JSL.Boolean((bool)o);
        break;
      case TypeCode.Byte:
      case TypeCode.SByte:
      case TypeCode.Int16:
      case TypeCode.Int32:
      case TypeCode.UInt16:
        jo=new JSL.Number(Convert.ToInt32(o));
        break;
      case TypeCode.Int64:
      case TypeCode.UInt32:
      case TypeCode.UInt64:
        jo=new JSL.Number(Convert.ToInt64(o));
        break;
      case TypeCode.Single:
      case TypeCode.Double:
      case TypeCode.Decimal:
        jo=new JSL.Number(Convert.ToDouble(o));
        break;
      case TypeCode.DateTime: {
          var dt = ((DateTime)o);
          var a=new JSC.Arguments();
          a.Add(new JSL.Number((dt.ToUniversalTime()-new DateTime(1970, 1, 1, 0, 0, 0)).TotalMilliseconds));
          var jdt=new JSL.Date(a);
          jo=new JSC.JSObject(jdt);  //.getTime() .valueOf()
        }
        break;
      case TypeCode.Empty:
        jo=JSC.JSObject.Undefined;
        break;
      case TypeCode.String:
        jo=new JSL.String((string)o);
        break;
      case TypeCode.Object:
      default: {
          if((jo = o as JSC.JSObject)==null) {
            jo=new JSC.JSObject(o);
          }
        }
        break;
      }
      return jo;
    }

    private SortedList<string, Action<EventArguments>> _events;
    protected SIO_Connection()
      : base() {
      _events=new SortedList<string, Action<EventArguments>>();
    }
    protected override void OnOpen() {
      if(true) {
        X13.Log.Info("{0} Connect", this.ID);
      }
      this.Send(string.Format("0{{\"sid\":\"{0}\", \"upgrades\":[], \"pingTimeout\":600000, \"pingInterval\":300000 }}", this.ID));
      this.Send("40");
    }
    protected override void OnMessage(MessageEventArgs e) {
      if(e.Type==Opcode.Text && !string.IsNullOrEmpty(e.Data)) {
        if(true) {
          X13.Log.Debug("ws.msg({0})", e.Data);
        }
        switch(e.Data[0]) {
        case '1':  // Close
          //TODO: Close
          break;
        case '2':  // Ping
          if(e.Data.Length>1) {
            this.Send("3"+e.Data.Substring(1));  // Pong
          } else {
            this.Send("3");
          }
          break;
        case '4':  // message
          if(e.Data.Length>1) {
            switch(e.Data[1]) {
            case '1':  // DISCONNECT
              break;
            case '2':  // EVENT
              EventArguments.ParseEvent(this, e.Data);
              break;
            case '3':  // ACK
            case '4':  // ERROR
            case '5':  // BINARY_EVENT
            case '6':  // BINARY_ACK
              break;
            }
          }
          break;
        case '5': // upgrade
          //supported only websockets, ignory
          break;
        case '6':  // noop - Used primarily to force a poll cycle when an incoming websocket connection is received.
          // ignory
          break;
        }
      }
    }
    protected override void OnClose(CloseEventArgs e) {
      if(true) {
        X13.Log.Info("{0} Disconnect: [{1}]{2}", this.ID, e.Code, e.Reason);
      }
    }

    protected void Register(JSC.JSObject name, Action<EventArguments> func) {
      lock(_events) {
        _events[JSL.JSON.stringify(name, null, null)]=func;
      }
    }
    protected void Emit(params object[] args) {
      if(args==null || args.Length==0){
        return;
      }
      var r=new JSL.Array(args.Length);
      for(int i=0; i<args.Length; i++) {
        r[i]=obg2jso(args[i]);
      }
      this.Send(string.Concat("42", JSL.JSON.stringify(r, null, null)));
    }

    public class EventArguments {
      private static JSF.ExternalFunction _JSON_Replacer;
      static EventArguments() {
        _JSON_Replacer=new JSF.ExternalFunction(ConvertDate);
      }
      private static JSC.JSObject ConvertDate(JSC.JSObject thisBind, JSC.Arguments args) {
        if(args.Length==2 && args[1].ValueType==JSC.JSObjectType.String) {
          // 2015-09-16T14:15:18.994Z
          var s=args[1].As<string>();
          if(s!=null && s.Length==24 && s[4]=='-' && s[7]=='-' && s[10]=='T' && s[13]==':' && s[16]==':' && s[19]=='.') {
            var a=new JSC.Arguments();
            a.Add(args[1]);
            var jdt=new JSL.Date(a);
            return new JSC.JSObject(jdt);
          }
        }
        return args[1];
      }
      public static bool ParseEvent(SIO_Connection conn, string msg) {
        long msgId;
        int idx=msg.IndexOf('[', 2);
        if(idx<3 || !long.TryParse(msg.Substring(2, idx-2), out msgId)) {
          msgId=-1;
        }
        var jo=JSL.JSON.parse(msg.Substring(idx), _JSON_Replacer) as JSL.Array;
        if(jo==null) {
          return false;
        }
        Action<EventArguments> f;
        lock(conn._events) {
          if(!conn._events.TryGetValue(JSL.JSON.stringify(jo[0], null, null), out f)) {
            f=null;
          }
        }
        if(f==null) {
          conn.Send(string.Concat("44", msgId<0?string.Empty:msgId.ToString(), "[\"Not found\",", JSL.JSON.stringify(jo[0], null, null), "]"));
          return false;
        }
        EventArguments ea=new EventArguments(conn, msgId, jo);
        try {
          f(ea);
          if(ea._msgId>=0 || (ea._error && ea._response!=null)) {
            conn.Send(string.Concat("4", ea._error?"4":"3", msgId<0?string.Empty:msgId.ToString(), ea._response==null?"[]":JSL.JSON.stringify(ea._response, null, null)));
          }
        }
        catch(Exception ex) {
          conn.Send(string.Concat("44", msgId<0?string.Empty:msgId.ToString(), "[\"Internal error\",", JSL.JSON.stringify(jo[0], null, null), "]"));
          if(true) {
            X13.Log.Warning("SIO({0}, {1}) - {2}", conn.ID, jo[0].ToString(), ex.ToString());
          }
        }
        return true;
      }

      private SIO_Connection _conn;
      private long _msgId;
      private JSL.Array _request;
      private JSL.Array _response;
      private bool _error;

      public readonly int Count;

      private EventArguments(SIO_Connection conn, long msgId, JSL.Array req) {
        this._conn=conn;
        this._msgId=msgId;
        this._request=req;
        this.Count=req.Count();
      }

      public JSC.JSObject this[int idx] {
        get {
          return _request[idx];
        }
      }
      public void Response(params object[] args) {
        _response=new JSL.Array(args.Length);
        for(int i=0; i<args.Length; i++) {
          _response[i]=obg2jso(args[i]);
        }
        _error=false;
      }
      public void Error(params object[] args) {
        Response(args);
        _error=true;
      }
    }
  }
}
