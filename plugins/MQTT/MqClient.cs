///<remarks>This file is part of the <see cref="https://github.com/X13home">X13.Home</see> project.<remarks>
using JSC = NiL.JS.Core;
using JSL = NiL.JS.BaseLibrary;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net.Sockets;
using System.Net;
using System.IO;
using System.Threading;
using System.Diagnostics;
using System.ComponentModel.Composition;

namespace X13.MQTT {
  internal class MqClient {
    private MQTTPl _pl;
    private string _host, _uName, _uPass;
    private int _port;
    private MqStreamer _stream;
    private int _keepAliveMS;
    private bool _waitPingResp;
    private Timer _tOut;

    public readonly List<MqSite> Sites;
    public readonly string Signature;
    public Status status { get; private set; }

    public MqClient(MQTTPl pl, string host, int port, string uName, string uPass) {
      _keepAliveMS = 9950;    // 10 sec
      _tOut = new Timer(new TimerCallback(TimeOut));
      _pl = pl;
      _host = host;
      _port = port;
      _uName = uName;
      _uPass = uPass;
      Signature = "MQTT://" + (_uName == null ? string.Empty : (_uName + "@")) + _host + (_port == 1883 ? string.Empty : (":" + _port.ToString()));
      Sites = new List<MqSite>();
      status = Status.Disconnected;
      Connect();
    }
    public void Close() {
      _tOut.Change(Timeout.Infinite, Timeout.Infinite);
      status = Status.Disconnected;
      var s = Interlocked.Exchange(ref _stream, null);
      if(s != null && s.isOpen) {
        s.Send(new MqDisconnect());
        Thread.Sleep(0);
        s.Close();
      }
    }

    private void Connect() {
      status = Status.Connecting;
      TcpClient _tcp = new TcpClient();
      _tcp.SendTimeout = 900;
      _tcp.ReceiveTimeout = 10;
      _tcp.BeginConnect(_host, _port, new AsyncCallback(ConnectCB), _tcp);
    }
    private void ConnectCB(IAsyncResult rez) {
      var _tcp = rez.AsyncState as TcpClient;
      try {
        _tcp.EndConnect(rez);
        _stream = new MqStreamer(_tcp, Received, SendIdle);
        var id = string.Format("{0}_{1:X4}", Environment.MachineName, System.Diagnostics.Process.GetCurrentProcess().Id);
        var ConnInfo = new MqConnect();
        ConnInfo.keepAlive = (ushort)(_keepAliveMS + 50 / 1000);
        ConnInfo.cleanSession = true;
        ConnInfo.clientId = id;
        if(_uName != null) {
          ConnInfo.userName = _uName;
          if(_uPass != null) {
            ConnInfo.userPassword = _uPass;
          }
        }
        this.Send(ConnInfo);
        _tOut.Change(3000, _keepAliveMS);       // better often than never
      }
      catch(Exception ex) {
        var se = ex as SocketException;
        if(se != null && (se.SocketErrorCode == SocketError.ConnectionRefused || se.SocketErrorCode == SocketError.TryAgain || se.SocketErrorCode == SocketError.TimedOut)) {
          status = Status.Disconnected;
          if(_keepAliveMS < 900000) {
            _keepAliveMS = (new Random()).Next(_keepAliveMS * 3, _keepAliveMS * 6);
          }
          _tOut.Change(_keepAliveMS, Timeout.Infinite);
        } else {
          status = Status.NotAccepted;
          _tOut.Change(Timeout.Infinite, Timeout.Infinite);
        }
        Log.Error("{0} Connection FAILED - {1}", this.Signature, ex.Message);

      }
    }
    private void Received(MqMessage msg) {
      if(_pl.verbose) {
        Log.Debug("R {0} > {1}", this.Signature, msg);
      }
      switch(msg.MsgType) {
      case MessageType.CONNACK: {
          MqConnack cm = msg as MqConnack;
          if(cm.Response == MqConnack.MqttConnectionResponse.Accepted) {
            status = Status.Connected;
            _keepAliveMS = 9950;
            _tOut.Change(_keepAliveMS*2, _keepAliveMS);
            Log.Info("Connected to {0}", Signature);
          } else {
            status = Status.NotAccepted;
            _tOut.Change(Timeout.Infinite, Timeout.Infinite);
          }
        }
        break;
      case MessageType.DISCONNECT:
        status = Status.Disconnected;
        _tOut.Change(3000, _keepAliveMS);
        break;
      case MessageType.PINGRESP:
        _waitPingResp = false;
        break;

      }
    }
    private void SendIdle() {
    }
    private void Send(MqMessage msg) {
      _stream.Send(msg);
      if(_pl.verbose) {
        Log.Debug("S {0} < {1}", this.Signature, msg);
      }
    }
    private void TimeOut(object o) {
      if(status == Status.NotAccepted) {
        _tOut.Change(Timeout.Infinite, Timeout.Infinite);
      } else if(_stream == null) {
        Connect();
      } else if(status == Status.Connected && !_waitPingResp) {
        _waitPingResp = true;
        Send(new MqPingReq());
      } else {
        if(status == Status.Connected) {
          Log.Warning("{0} - PingResponse timeout", Signature);
        } else if(status == Status.Connecting) {
          Log.Warning("{0} - ConnAck timeout", Signature);
        }
        Close();
        _tOut.Change(1500, _keepAliveMS);
      }
    }
    /*
    private string addr;
    private int port;
    private DVar<MqClient> _owner;
    private MqStreamer _stream;
    private static Topic _mq;

    private bool _waitPingResp;
    private bool _connected;
    private Timer _tOut;
    private int _keepAliveMS=89950;  // 90 sec
    private MqConnect ConnInfo;
    private List<Topic.Subscription> _subs;
    private DVar<bool> _verbose;

    public ushort KeepAlive {
      get { return (ushort)(_keepAliveMS>0?(_keepAliveMS+50)/1000:0); }
      set {
        if(!_connected) {             // can not inform the broker only befor connect
          _keepAliveMS=value>0?value*1000-50:Timeout.Infinite;
        }
      }
    }
    public string BrokerName { get; private set; }
    public bool Connected { get { return _connected; } }
    public event Action<bool> StatusChg;

    public MqClient() {
      _waitPingResp=false;
      _mq=Topic.root.Get("/local/MQ");
      ConnInfo=new MqConnect();
      ConnInfo.cleanSession=true;
      ConnInfo.keepAlive=this.KeepAlive;
      _tOut=new Timer(new TimerCallback(TimeOut));
      _settings=Topic.root.Get("/local/cfg/Client");
      _subs=new List<Topic.Subscription>();
      _now=Topic.root.Get<DateTime>("/var/now");
      _nowOffset=_settings.Get<long>("TimeOffset");
    }
    public void Init() {
      _verbose=_settings.Get<bool>("verbose");
      if(!Reconnect()) {
        _settings.Get<bool>("enable").value=false;
        return;
      }
      Topic.SubscriptionsChg+=Topic_SubscriptionsChg;
      Topic.root.Subscribe("/etc/system/#", PLC.PLCPlugin.L_dummy);
      Topic.root.Subscribe("/etc/repository/#", PLC.PLCPlugin.L_dummy);
      Topic.root.Subscribe("/etc/declarers/+", PLC.PLCPlugin.L_dummy);
      Topic.root.Subscribe("/etc/declarers/type/#", PLC.PLCPlugin.L_dummy);
      Topic.root.Subscribe("/etc/PLC/default", PLC.PLCPlugin.L_dummy);
      Topic.root.Subscribe("/var/now", PLC.PLCPlugin.L_dummy);
      Topic.paused=true;
      for(int i=600; i>=0; i--) {
        Thread.Sleep(50);
        if(_connected) {
          break;
        }
      }
    }

    public void Start() {
      for(int i=35; i>=0; i--) {
        Thread.Sleep(100);
        if(_stream==null){
          break;
        }
        if(!_stream.isSndPaused) {
          Log.Info("MqClient: loading completed");
          Topic.paused=false;
          return;
        }
      }
      if(_stream!=null) {
        _stream.isSndPaused=false;
      }
      Topic.paused=false;
      Log.Warning("MqClient: loading timeout");
      //Reconnect();
    }

    public bool Reconnect(bool slow=false) {
      if(_stream!=null) {
        if(_connected) {
          _connected=false;
          if(StatusChg!=null) {
            StatusChg(_connected);
          }
          _tOut.Change(_keepAliveMS*2, Timeout.Infinite);
        } else {
          _tOut.Change(_keepAliveMS*(slow?10:5), Timeout.Infinite);
        }
        _stream.Close();
        _stream=null;
        return false;
      }
      if(slow) {
        _tOut.Change(_keepAliveMS*5, Timeout.Infinite);
        return false;
      }
      string connectionstring=_settings.Get<string>("_URL").value;
      _settings.Get<string>("_URL").saved=true;
      _settings.Get<string>("_username").saved=true;
      _settings.Get<string>("_password").saved=true;
      if(string.IsNullOrEmpty(connectionstring)) {
        return false;
      }
      if(connectionstring=="#local") {
        connectionstring="localhost";
      }

      if(connectionstring.IndexOf(':')>0) {
        addr=connectionstring.Substring(0, connectionstring.IndexOf(':'));
        port=int.Parse(connectionstring.Substring(connectionstring.IndexOf(':')+1));
      } else {
        addr=connectionstring;
        port=1883;
      }
      TcpClient _tcp=new TcpClient();
      _tcp.SendTimeout=900;
      _tcp.ReceiveTimeout=0;
      _tcp.BeginConnect(addr, port, new AsyncCallback(ConnectCB), _tcp);
      return true;
    }

    private void Topic_SubscriptionsChg(Topic.Subscription s, bool added) {
      if(_verbose.value && s!=null) {
        Log.Debug("{0} {3} {1}.{2}", s.path, s.func.Method.DeclaringType.Name, s.func.Method.Name, added?"+=":"-=");
      }
      if((s!=null && s.path.StartsWith("/local")) || !_connected) {
        return;
      }
      if(!added) {
        if(!_subs.Exists(z => z==s)) {
          return;
        } else {
          _subs.Remove(s);
          Unsubscribe(s.path);
        }
      }
      var sAll=Topic.root.subscriptions.Where(z=>!z.path.StartsWith("/local")).ToArray();
      if(_verbose.value) {
        Log.Debug("SUBS={0}", string.Join("\n", sAll.Select(z => z.path)));
      }
      foreach(var sb in _subs.Except(sAll).ToArray()) {
        _subs.Remove(sb);
        Unsubscribe(sb.path);
      }
      foreach(var sb in sAll.Except(_subs).ToArray()) {
        _subs.Add(sb);
        Subscribe(sb.path, QoS.AtMostOnce);
      }
    }

    private void ConnectCB(IAsyncResult rez) {
      var _tcp=rez.AsyncState as TcpClient;
      try {
        _tcp.EndConnect(rez);
        _stream=new MqStreamer(_tcp, Received, SendIdle);
        _stream.isSndPaused=true;
        var re=((IPEndPoint)_stream.Socket.Client.RemoteEndPoint);
        try {
          BrokerName=Dns.GetHostEntry(re.Address).HostName;
        }
        catch(SocketException) {
          BrokerName=re.Address.ToString();
        }
        _owner=_mq.Get<MqClient>(BrokerName);
        _owner.value=this;
        _connected=false;
        string id=Topic.root.Get<string>("/local/cfg/id").value;
        if(string.IsNullOrEmpty(id)) {
          id=string.Format("{0}@{1}_{2:X4}", Environment.UserName, Environment.MachineName, System.Diagnostics.Process.GetCurrentProcess().Id);
        }
        ConnInfo.clientId=id;
        ConnInfo.userName=_settings.Get<string>("_username");
        _settings.Get<string>("_username").saved=true;
        ConnInfo.userPassword=_settings.Get<string>("_password");
        _settings.Get<string>("_password").saved=true;
        if(string.IsNullOrEmpty(ConnInfo.userName) && addr=="localhost") {
          ConnInfo.userName="local";
          ConnInfo.userPassword=string.Empty;
        }

        this.Send(ConnInfo);
        _owner.Subscribe("/#", OwnerChanged);
        _tOut.Change(3000, _keepAliveMS);       // more often than not
      }
      catch(Exception ex) {
        Log.Error("Connect to {0}:{1} failed, {2}", addr, port, ex.Message);
        if(StatusChg!=null) {
          StatusChg(false);
        }
        _tOut.Change(_keepAliveMS*5, Timeout.Infinite);
      }
    }

    public void Subscribe(string topic, QoS sQoS) {
      MqSubscribe msg=new MqSubscribe();
      msg.Add(topic, sQoS);
      Send(msg);
    }
    public void Unsubscribe(string path) {
      MqUnsubscribe msg=new MqUnsubscribe();
      msg.Add(path);
      Send(msg);
    }
    public void Stop() {
      if(_stream!=null) {
        if(_connected) {
          _connected=false;
          _owner.Unsubscribe("/#", OwnerChanged);
          _owner.Remove();
          _tOut.Change(Timeout.Infinite, Timeout.Infinite);
          if(StatusChg!=null) {
            StatusChg(_connected);
          }
          _stream.Close();
          _stream=null;
          Log.Info("{0} Disconnected", BrokerName);
        }
      }
    }
    private void TimeOut(object o) {
      if(_stream==null) {
        Reconnect();
      } else if(!_connected) {
        Log.Warning("ConnAck timeout");
        Reconnect();
      } else if(_waitPingResp) {
        Log.Warning("PingResponse timeout");
        Reconnect();
      } else {
        _waitPingResp=true;
        _stream.Send(new MqPingReq());
      }
    }
    private void Received(MqMessage msg) {
      if(_verbose.value) {
        Log.Debug("R {0}", msg);
      }
      switch(msg.MsgType) {
      case MessageType.CONNACK: {
          MqConnack cm=msg as MqConnack;
          if(cm.Response!=MqConnack.MqttConnectionResponse.Accepted) {
            Reconnect(true);
            Log.Error("Connection to {0}:{1} failed. error={2}", addr, port, cm.Response.ToString());
          } else {
            _connected=true;
            _subs.Clear();
            Topic_SubscriptionsChg(null, true);
          }
          if(StatusChg!=null) {
            StatusChg(_connected);
          }
        }
        break;
      case MessageType.DISCONNECT:
        Reconnect();
        break;
      case MessageType.PINGRESP:
        _waitPingResp=false;
        break;
      case MessageType.PUBLISH: {
          MqPublish pm=msg as MqPublish;
          if(msg.MessageID!=0) {
            if(msg.QualityOfService==QoS.AtLeastOnce) {
              this.Send(new MqMsgAck(MessageType.PUBACK, msg.MessageID));
            } else if(msg.QualityOfService==QoS.ExactlyOnce) {
              this.Send(new MqMsgAck(MessageType.PUBREC, msg.MessageID));
            }
          }
          ProccessPublishMsg(pm);
        }
        break;
      case MessageType.PUBACK:
        break;
      case MessageType.PUBREC:
        if(msg.MessageID!=0) {
          this.Send(new MqMsgAck(MessageType.PUBREL, msg.MessageID));
        }
        break;
      case MessageType.PUBREL:
        if(msg.MessageID!=0) {
          this.Send(new MqMsgAck(MessageType.PUBCOMP, msg.MessageID));
        }
        break;
      case MessageType.PUBCOMP:
        break;
      default:
        break;
      }
      if(_waitPingResp) {
        _tOut.Change(_keepAliveMS, _keepAliveMS);
      }
    }
    private void ProccessPublishMsg(MqPublish pm) {
      if(_stream!=null && _stream.isSndPaused && pm.Path==_mq.path) {
        _stream.isSndPaused=false;
        return;
      }
      Topic cur;
      if(!string.IsNullOrEmpty(pm.Payload)) {         // Publish
        if(!Topic.root.Exist(pm.Path, out cur) || cur.valueType==null) {
          Type vt=X13.WOUM.ExConverter.Json2Type(pm.Payload);
          cur=Topic.GetP(pm.Path, vt, _owner);
        }
        cur.saved=pm.Retained;
        if(cur.valueType!=null) {
          if(cur==_now) {
            try {
              _nowOffset.value=JsonConvert.DeserializeObject<DateTime>(pm.Payload, _jcs).ToLocalTime().Ticks-DateTime.Now.Ticks;
            }
            catch(Exception) {
              return;
            }
          } else if(cur.parent!=_now) {
            cur.FromJson(pm.Payload, _owner);
          }
        }
      } else if(Topic.root.Exist(pm.Path, out cur)) {                      // Remove
        cur.Remove(_owner);
      }
    }

    private void Send(MqMessage msg) {
      _stream.Send(msg);
      if(_verbose.value) {
        Log.Debug("S {0}", msg);
      }
    }
    private void SendIdle() {
      //if(_connected) {
      //  _tOut.Change(_keepAliveMS, _keepAliveMS);
      //}
    }
    private void OwnerChanged(Topic sender, TopicChanged param) {
      if(!_connected || sender.parent==null || sender.path.StartsWith("/local") || sender.path.StartsWith("/var/now") || sender.path.StartsWith("/var/log") || param.Visited(_mq, false) || param.Visited(_owner, false)) {
        return;
      }
      switch(param.Art) {
      case TopicChanged.ChangeArt.Add: {
          MqPublish pm=new MqPublish(sender);
          if(sender.valueType!=null && sender.valueType!=typeof(string) && !sender.valueType.IsEnum && !sender.valueType.IsPrimitive) {
            pm.Payload=(new Newtonsoft.Json.Linq.JObject(new Newtonsoft.Json.Linq.JProperty("+", WOUM.ExConverter.Type2Name(sender.valueType)))).ToString();
          }
          this.Send(pm);
        }
        break;
      case TopicChanged.ChangeArt.Value: {
          MqPublish pm=new MqPublish(sender);
          this.Send(pm);
        }
        break;
      case TopicChanged.ChangeArt.Remove: {
          MqPublish pm=new MqPublish(sender);
          pm.Payload=string.Empty;
          this.Send(pm);
        }
        break;
      }
    }*/

    public enum Status {
      Disconnected,
      Connecting,
      Connected,
      NotAccepted
    }
  }
}
