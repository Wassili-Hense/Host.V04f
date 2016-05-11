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
    private SortedList<string, Action<EventArguments>> _events;
    protected X13.PLC.Topic _owner;
    protected SIO_Connection()
      : base() {
      _events=new SortedList<string, Action<EventArguments>>();
    }
    protected override void OnOpen() {
      this.Send(string.Format("0{{\"sid\":\"{0}\", \"upgrades\":[], \"pingTimeout\":500000, \"pingInterval\":300000 }}", this.ID));

      System.Net.IPEndPoint remoteEndPoint = this.Context.UserEndPoint;
      {
        System.Net.IPAddress remIP;
        if(this.Context.Headers.Contains("X-Real-IP") && System.Net.IPAddress.TryParse(this.Context.Headers["X-Real-IP"], out remIP)) {
          remoteEndPoint=new System.Net.IPEndPoint(remIP, remoteEndPoint.Port);
        }
      }
      string host=null;
      int i;
      var r=X13.PLC.Topic.root.Get("/users");
      try {
        var he=System.Net.Dns.GetHostEntry(remoteEndPoint.Address);
        host=string.Format("{0}[{1}]", he.HostName, remoteEndPoint.Address.ToString());
        var tmp=he.HostName.Split('.');
        if(tmp!=null && tmp.Length>0 && !string.IsNullOrEmpty(tmp[0])) {
          i=1;
          while(r.Exist(tmp[0]+"-"+i.ToString())) {
            i++;
          }
          _owner=r.Get(tmp[0]+"-"+i.ToString());
        }
      }
      catch(Exception) {
      }
      if(_owner==null) {
        i=1;
        string pre=remoteEndPoint.Address.ToString()+"-";
        while(r.Exist(pre+i.ToString())) {
          i++;
        }
        _owner=r.Get(pre+i.ToString());
        host="["+remoteEndPoint.Address.ToString()+"]";
      }
      _owner.Set(host);
      this.Send("40");
      if(true) {
        X13.Log.Info("{0} Connected as {1}", host, _owner.name);
      }

    }
    protected override void OnMessage(MessageEventArgs e) {
      if(e.Type==Opcode.Text && !string.IsNullOrEmpty(e.Data)) {
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
        X13.Log.Info("{0} Disconnect: [{1}]{2}", this.ToString(), e.Code, e.Reason);
      }
      if(_owner!=null) {
        _owner.Remove();
      }
    }

    protected void Register(JSC.JSValue name, Action<EventArguments> func) {
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
        r[i]=JSC.JSValue.Marshal(args[i]);
      }
      string msg=JSL.JSON.stringify(r, null, null);
      this.Send("42"+msg);
      X13.Log.Debug("{0}\n  Emit : {1}",this.ToString(), msg);
    }
    public override string ToString() {
      return _owner==null?"unkmown":_owner.path;
    }
    public class EventArguments {
      private static JSF.ExternalFunction _JSON_Replacer;
      static EventArguments() {
        _JSON_Replacer = new JSF.ExternalFunction(ConvertDate);
      }
      private static JSC.JSValue ConvertDate(JSC.JSValue thisBind, JSC.Arguments args) {
        if(args.Length==2 && args[1].ValueType==JSC.JSValueType.String) {
          // 2015-09-16T14:15:18.994Z
          var s=args[1].ToString();
          if(s!=null && s.Length==24 && s[4]=='-' && s[7]=='-' && s[10]=='T' && s[13]==':' && s[16]==':' && s[19]=='.') {
            var a=new JSC.Arguments();
            a.Add(args[1]);
            var jdt=new JSL.Date(a);
            return JSC.JSValue.Wrap(jdt);
          }
        }
        return args[1];
      }
      public static bool ParseEvent(SIO_Connection conn, string msg) {
        string sb;
        string tmpS;
        long msgId;
        int idx=msg.IndexOf('[', 2);
        if(idx<3 || !long.TryParse(msg.Substring(2, idx-2), out msgId)) {
          msgId=-1;
        }
        tmpS=msg.Substring(idx);
        sb=conn.ToString()+"\n  REQ : "+tmpS+"\n  ";
        var jo=JSL.JSON.parse(tmpS, _JSON_Replacer) as JSL.Array;
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
          tmpS=string.Concat(msgId<0?string.Empty:msgId.ToString(), "[\"Not found\",", JSL.JSON.stringify(jo[0], null, null), "]");
          conn.Send("44"+tmpS);
          X13.Log.Warning("{0}ERROR: {1}", sb, tmpS);
          return false;
        }
        EventArguments ea=new EventArguments(conn, msgId, jo);
        try {
          f(ea);
          if(ea._msgId >= 0 || (ea._error && ea._response != null)) {
            tmpS = ea._response == null ? "[]" : JSL.JSON.stringify(ea._response, null, null);
            conn.Send(string.Concat("4", ea._error ? "4" : "3", msgId < 0 ? string.Empty : msgId.ToString(), tmpS));
            if(ea._error) {
              X13.Log.Warning("{0}ERROR: {1}", sb, tmpS);
            } else {
              X13.Log.Debug("{0}RESP: {1}", sb, tmpS);
            }
          } else {
            X13.Log.Debug("{0}", sb, tmpS);
          }
        }
        catch(Exception ex) {
          tmpS="[\"Internal error\"," + JSL.JSON.stringify(jo[0], null, null) + "]";
          conn.Send(string.Concat("44", msgId<0?string.Empty:msgId.ToString(), tmpS));
          X13.Log.Warning("{0}EXCEPTION: {1}", sb.ToString(), ex.ToString());
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

      public JSC.JSValue this[int idx] {
        get {
          return _request[idx];
        }
      }
      public void Response(params object[] args) {
        _response=new JSL.Array(args.Length);
        for(int i=0; i<args.Length; i++) {
          _response[i] = JSC.JSValue.Marshal(args[i]);
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
