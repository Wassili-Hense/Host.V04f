﻿///<remarks>This file is part of the <see cref="https://github.com/X13home">X13.Home</see> project.<remarks>
using JSC = NiL.JS.Core;
using JSL = NiL.JS.BaseLibrary;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using X13.Repository;

namespace X13.Periphery {
  internal class MsDevice : IMsGate {
    private const int ACK_TIMEOUT = 600;
    private const ushort RTC_EXCH = 0xFF07;
    private const ushort LOG_D_ID = 0xFFE0;
    private const ushort LOG_I_ID = 0xFFE1;
    private const ushort LOG_W_ID = 0xFFE2;
    private const ushort LOG_E_ID = 0xFFE3;
    private static Random _rand;

    static MsDevice() {
      _rand = new Random((int)DateTime.Now.Ticks);
    }

    private List<SubRec> _subsscriptions;
    private Queue<MsMessage> _sendQueue;
    private List<TopicInfo> _topics;
    private State _state;
    private bool _waitAck;
    private int _tryCounter;
    private int _duration;
    private int _messageIdGen;
    private Timer _activeTimer;
    private MsPublish _lastInPub;
    private bool _has_RTC;
    private DateTime _last_RTC;

    public readonly Topic owner;
    public IMsGate _gate;
    public byte[] addr;

    public MsDevice(Topic owner) {
      this.owner = owner;
      _activeTimer = new Timer(new TimerCallback(TimeOut));
      _subsscriptions = new List<SubRec>(4);
      _sendQueue = new Queue<MsMessage>();
      _topics = new List<TopicInfo>(16);
      _duration = 3000;
      _messageIdGen = 0;
    }

    #region IMsGate Members
    public void SendGw(byte[] addr, MsMessage msg) {
      if(_gate != null && addr != null) {
        _gate.SendGw(this, new MsForward(addr, msg));
      }
    }
    public void SendGw(MsDevice dev, MsMessage msg) {
      if(_gate != null) {
        _gate.SendGw(this, new MsForward(dev.addr, msg));
      }
    }
    public byte gwIdx { get { return (byte)(_gate == null ? 0xFF : _gate.gwIdx); } }
    public byte gwRadius { get { return 0; } }
    public string name { get { return owner.name; } }
    public string Addr2If(byte[] addr) {
      return _gate != null ? _gate.Addr2If(addr) : string.Concat(BitConverter.ToString(addr), " via ", this.name);
    }

    public void Stop() {
      throw new NotImplementedException();
    }
    #endregion IMsGate Members

    public State state {
      get {
        return _state;
      }
      set {
        if(_state != value) {
          _state = value;
          if(_state == State.Connected || _state == State.AWake) {
            owner.SetState(1);
          } else if(_state == State.ASleep) {
            owner.SetState(2);
          } else {
            owner.SetState(0);
          }
        }
      }
    }

    /// <summary>Check Address for DHCP</summary>
    /// <param name="addr">checked address</param>
    /// <returns>busy</returns>
    public bool CheckAddr(byte[] addr) {
      if(addr == null) {
        return false;
      }
      if(this.addr != null && this.addr.Length - 1 == addr.Length && this.addr.Skip(1).SequenceEqual(addr)) {
        return true;
      }
      return false;
    }
    public void Connect(MsConnect msg) {
      if(msg.CleanSession) {
        foreach(var s in _subsscriptions) {
          s.Dispose();
        }
        _subsscriptions.Clear();
        _topics.Clear();
        lock(_sendQueue) {
          _sendQueue.Clear();
        }
        _waitAck = false;
        //  if(_statistic.value) {
        //    StatConnectTime();
        //  }
      }
      _duration = msg.Duration * 1100;
      ResetTimer();
      //if(msg.Will) {
      //  _willPath = string.Empty;
      //  _wilMsg = null;
      //  if(msg.CleanSession) {
      //    Log.Info("{0}.state {1} => WILLTOPICREQ", Owner.path, state);
      //  }
      //  state = State.WillTopic;
      //  Send(new MsMessage(MsMessageType.WILLTOPICREQ));
      //} else {
      if(msg.CleanSession) {
        Log.Info("{0} {1} => PreConnect", owner.path, state);
        state = State.PreConnect;
      } else {
        state = State.Connected;
      }
      Send(new MsConnack(MsReturnCode.Accepted));
      //}
      //via = _gate.name;
      //if(_statistic.value) {
      //  Stat(false, MsMessageType.CONNECT, msg.CleanSession);
      //}
    }
    public void ProcessInPacket(MsMessage msg) {
      //if(_statistic.value && msg.MsgTyp != MsMessageType.EncapsulatedMessage && msg.MsgTyp != MsMessageType.PUBLISH) {
      //  Stat(false, msg.MsgTyp);
      //}
      switch(msg.MsgTyp) {
      //case MsMessageType.WILLTOPIC: {
      //    var tmp = msg as MsWillTopic;
      //    if(state == State.WillTopic) {
      //      _willPath = tmp.Path;
      //      _willRetain = tmp.Retain;
      //      state = State.WillMsg;
      //      ProccessAcknoledge(msg);
      //    }
      //  }
      //  break;
      //case MsMessageType.WILLMSG: {
      //    var tmp = msg as MsWillMsg;
      //    if(state == State.WillMsg) {
      //      _wilMsg = tmp.Payload;
      //      Log.Info("{0}.state {1} => WILLTOPICREQ", Owner.path, state);
      //      state = State.PreConnect;
      //      ProccessAcknoledge(msg);
      //      Send(new MsConnack(MsReturnCode.Accepted));
      //    }
      //  }
      //  break;
      case MsMessageType.SUBSCRIBE: {
          var tmp = msg as MsSubscribe;

          SyncMsgId(msg.MessageId);
          //SubRec s = null;
          ushort topicId = tmp.topicId;
          if(tmp.topicIdType != TopicIdType.Normal || tmp.path.IndexOfAny(new[] { '+', '#' }) < 0) {
            TopicInfo ti = null;
            if(tmp.topicIdType == TopicIdType.Normal) {
              ti = GetTopicInfo(tmp.path, false);
            } else {
              ti = GetTopicInfo(tmp.topicId, tmp.topicIdType);
            }
            topicId = ti.TopicId;
          }
          Send(new MsSuback(tmp.qualityOfService, topicId, msg.MessageId, MsReturnCode.Accepted));
          if(state == State.PreConnect) {
            state = State.Connected;
          }
          //s = owner.Subscribe(SubRec.SubMask.All | SubRec.SubMask.Value, PublishTopic);
          //_subsscriptions.Add(s);
        }
        break;
      case MsMessageType.REGISTER: {
          var tmp = msg as MsRegister;
          ResetTimer();
          try {
            TopicInfo ti;

            ti = GetTopicInfo(tmp.TopicPath, false);
            if(ti != null) {
              Send(new MsRegAck(ti.TopicId, tmp.MessageId, MsReturnCode.Accepted));
            } else {
              Send(new MsRegAck(0, tmp.MessageId, MsReturnCode.NotSupportes));
              Log.Warning("Unknown variable type by register {0}, {1}", owner.path, tmp.TopicPath);
            }
          }
          catch(Exception ex) {
            Send(new MsRegAck(0, tmp.MessageId, MsReturnCode.Congestion));
            Log.Warning("Error by register {0}, {1}", owner.path, tmp.TopicPath, ex.Message);
          }
        }
        break;
      case MsMessageType.REGACK: {
          var tmp = msg as MsRegAck;
          ProccessAcknoledge(tmp);
          TopicInfo ti = _topics.FirstOrDefault(z => z.TopicId == tmp.TopicId);
          if(ti == null) {
            if(tmp.TopicId != 0xFFFF) { // 0xFFFF - remove variable
              Log.Warning("{0} RegAck({1:X4}) for unknown variable", owner.path, tmp.TopicId);
            }
            return;
          }
          if(tmp.RetCode == MsReturnCode.Accepted) {
            ti.registred = true;
            //if(ti.it != TopicIdType.PreDefined) {
            //  Send(new MsPublish(ti.topic, ti.TopicId, QoS.AtLeastOnce));
            //}
          } else {
            Log.Warning("{0} registred failed: {1}", ti.topic.path, tmp.RetCode.ToString());
            _topics.Remove(ti);
            ti.topic.SetField("MQTT-SN.subIdx", null);
            //UpdateInMute();
          }
        }
        break;
      case MsMessageType.PUBLISH: {
          var tmp = msg as MsPublish;
          //    if(_statistic.value) {
          //      Stat(false, msg.MsgTyp, tmp.Dup);
          //    }
          TopicInfo ti = _topics.Find(z => z.TopicId == tmp.TopicId && z.it == tmp.topicIdType);
          if(ti == null && tmp.topicIdType != TopicIdType.Normal) {
            ti = GetTopicInfo(tmp.TopicId, tmp.topicIdType, false);
          }
          if(tmp.qualityOfService == QoS.AtMostOnce || (tmp.qualityOfService == QoS.MinusOne && (tmp.topicIdType == TopicIdType.PreDefined || tmp.topicIdType == TopicIdType.ShortName))) {
            ResetTimer();
          } else if(tmp.qualityOfService == QoS.AtLeastOnce) {
            SyncMsgId(tmp.MessageId);
            Send(new MsPubAck(tmp.TopicId, tmp.MessageId, (ti != null || tmp.TopicId == RTC_EXCH) ? MsReturnCode.Accepted : MsReturnCode.InvalidTopicId));
          } else if(tmp.qualityOfService == QoS.ExactlyOnce) {
            SyncMsgId(tmp.MessageId);
            // QoS2 not supported, use QoS1
            Send(new MsPubAck(tmp.TopicId, tmp.MessageId, ti != null ? MsReturnCode.Accepted : MsReturnCode.InvalidTopicId));
          } else {
            throw new NotSupportedException("QoS -1 not supported " + owner.path);
          }
          if(tmp.topicIdType == TopicIdType.PreDefined && ((tmp.TopicId >= LOG_D_ID && tmp.TopicId <= LOG_E_ID) || tmp.TopicId == RTC_EXCH)) {
            string str = string.Format("{0} msgId={2:X4}  msg={1}", owner.name, tmp.Data == null ? "null" : (BitConverter.ToString(tmp.Data) + "[" + Encoding.ASCII.GetString(tmp.Data.Select(z => (z < 0x20 || z > 0x7E) ? (byte)'.' : z).ToArray()) + "]"), tmp.MessageId);
            switch(tmp.TopicId) {
            case RTC_EXCH:
              if(tmp.Data != null && tmp.Data.Length == 6) {
                try {
                  _last_RTC = new DateTime((DateTime.Now.Year / 100) * 100 + BCD2int(tmp.Data[5]), BCD2int(tmp.Data[4] & 0x1F), BCD2int(tmp.Data[3] & 0x3F)
                    , ((tmp.Data[2] & 0x40) != 0 ? 12 : 0) + BCD2int(tmp.Data[2] & 0x3F), BCD2int(tmp.Data[1] & 0x7F), BCD2int(tmp.Data[0] & 0x7F));
                }
                catch(Exception ex) {
                  Log.Warning("{0}.RTC({1}) - {2}", owner.name, BitConverter.ToString(tmp.Data), ex.Message);
                }
                if(Math.Abs((_last_RTC - DateTime.Now).TotalSeconds) > 2) {
                  _last_RTC = new DateTime(1);
                }
                _has_RTC = true;
              }
              break;
            case LOG_D_ID:
              Log.Debug("{0}", str);
              break;
            case LOG_I_ID:
              Log.Info("{0}", str);
              break;
            case LOG_W_ID:
              Log.Warning("{0}", str);
              break;
            case LOG_E_ID:
              Log.Error("{0}", str);
              break;
            }
          } else if(ti != null) {
            if(tmp.Dup && _lastInPub != null && tmp.MessageId == _lastInPub.MessageId) {  // arready recieved
            } else {
              SetValue(ti, tmp.Data, tmp.Retained);
            }
            _lastInPub = tmp;
          }
        }
        break;
      case MsMessageType.PUBACK: {
          ProccessAcknoledge(msg);
        }
        break;
      case MsMessageType.PINGREQ: {
          var tmp = msg as MsPingReq;
          if(state == State.ASleep) {
            if(string.IsNullOrEmpty(tmp.ClientId) || tmp.ClientId == owner.name) {
              state = State.AWake;
              ProccessAcknoledge(msg);    // resume send proccess
            } else {
              SendGw(this, new MsDisconnect());
              state = State.Lost;
              Log.Warning("{0} PingReq from unknown device: {1}", owner.path, tmp.ClientId);
            }
          } else {
            ResetTimer();
            if(_gate != null) {
              _gate.SendGw(this, new MsMessage(MsMessageType.PINGRESP));
              //if(_statistic.value) {
              //  Stat(true, MsMessageType.PINGRESP, false);
              //}
            }
          }
        }
        break;
      case MsMessageType.DISCONNECT:
        Disconnect((msg as MsDisconnect).Duration);
        break;
      case MsMessageType.CONNECT:
        Connect(msg as MsConnect);
        break;
      //case MsMessageType.EncapsulatedMessage: {
      //    Topic devR = Topic.root.Get("/dev");
      //    var fm = msg as MsForward;
      //    if(fm.msg == null) {
      //      if(_verbose.value) {
      //        Log.Warning("bad message {0}:{1}", _gate, fm.ToString());
      //      }
      //      return;
      //    }
      //    if(fm.msg.MsgTyp == MsMessageType.SEARCHGW) {
      //      _gate.SendGw(this, new MsGwInfo(gwIdx));
      //    } else if(fm.msg.MsgTyp == MsMessageType.DHCP_REQ) {
      //      var dr = fm.msg as MsDhcpReq;
      //      //******************************
      //      List<byte> ackAddr = new List<byte>();
      //      byte[] respPrev = null;

      //      foreach(byte hLen in dr.hLen) {
      //        if(hLen == 0) {
      //          continue;
      //        } else if(hLen <= 8) {
      //          byte[] resp;
      //          if(respPrev != null && respPrev.Length == hLen) {
      //            resp = respPrev;
      //          } else {
      //            resp = new byte[hLen];

      //            for(int i = 0; i < 5; i++) {
      //              for(int j = 0; j < resp.Length; j++) {
      //                resp[j] = (byte)_rand.Next((i < 3 && hLen == 1) ? 32 : 1, (i < 3 && hLen == 1) ? 126 : (j == 0 ? 254 : 255));
      //              }
      //              if(devR.children.Select(z => z as DVar<MsDevice>).Where(z => z != null && z.value != null).All(z => !z.value.CheckAddr(resp))) {
      //                break;
      //              } else if(i == 4) {
      //                for(int j = 0; j < resp.Length; j++) {
      //                  resp[j] = 0xFF;
      //                }
      //              }
      //            }
      //            respPrev = resp;
      //          }
      //          ackAddr.AddRange(resp);
      //        } else {
      //          if(_verbose.value) {
      //            Log.Warning("{0}:{1} DhcpReq.hLen is too high", BitConverter.ToString(fm.addr), fm.msg.ToString());
      //          }
      //          ackAddr = null;
      //          break;
      //        }
      //      }
      //      if(ackAddr != null) {
      //        _gate.SendGw(this, new MsForward(fm.addr, new MsDhcpAck(gwIdx, dr.xId, ackAddr.ToArray())));
      //      }
      //      //******************************
      //    } else {
      //      if(fm.msg.MsgTyp == MsMessageType.CONNECT) {
      //        var cm = fm.msg as MsConnect;
      //        if(fm.addr != null && fm.addr.Length == 2 && fm.addr[1] == 0xFF) {    // DHCP V<0.3
      //          _gate.SendGw(this, new MsForward(fm.addr, new MsConnack(MsReturnCode.Accepted)));

      //          byte[] nAddr = new byte[1];
      //          do {
      //            nAddr[0] = (byte)(_rand.Next(32, 254));
      //          } while(!devR.children.Select(z => z as DVar<MsDevice>).Where(z => z != null && z.value != null).All(z => !z.value.CheckAddr(nAddr)));
      //          Log.Info("{0} new addr={1:X2}", cm.ClientId, nAddr[0]);
      //          _gate.SendGw(this, new MsForward(fm.addr, new MsPublish(null, PredefinedTopics[".cfg/XD_DeviceAddr"], QoS.AtLeastOnce) { MessageId = 1, Data = nAddr }));
      //        } else {
      //          DVar<MsDevice> dDev = devR.Get<MsDevice>(cm.ClientId);
      //          if(dDev.value == null) {
      //            dDev.value = new MsDevice(this, fm.addr);
      //            Thread.Sleep(0);
      //            dDev.value.Owner = dDev;
      //          } else {
      //            this.RemoveNode(dDev.value);
      //            dDev.value._gate = this;
      //            dDev.value.Addr = fm.addr;
      //          }
      //          this.AddNode(dDev.value);
      //          dDev.value.Connect(cm);
      //          foreach(var dub in devR.children.Select(z => z.GetValue() as MsDevice).Where(z => z != null && z != dDev.value && z.Addr != null && z.Addr.SequenceEqual(fm.addr) && z._gate == this).ToArray()) {
      //            dub.Addr = null;
      //            dub._gate = null;
      //            dub.state = State.Disconnected;
      //          }
      //        }
      //      } else {
      //        MsDevice dev = devR.children.Select(z => z.GetValue() as MsDevice).FirstOrDefault(z => z != null && z.Addr != null && z.Addr.SequenceEqual(fm.addr) && z._gate == this);
      //        if(dev != null
      //          && ((dev.state != State.Disconnected && dev.state != State.Lost)
      //            || fm.msg.MsgTyp == MsMessageType.CONNECT
      //            || (fm.msg.MsgTyp == MsMessageType.PUBLISH && (fm.msg as MsPublish).qualityOfService == QoS.MinusOne))) {
      //          dev.ProcessInPacket(fm.msg);
      //        } else if(fm.msg.MsgTyp == MsMessageType.PUBLISH && (fm.msg as MsPublish).qualityOfService == QoS.MinusOne) {
      //          var tmp = fm.msg as MsPublish;
      //          if(tmp.topicIdType == TopicIdType.PreDefined && tmp.TopicId >= LOG_D_ID && tmp.TopicId <= LOG_E_ID) {
      //            string str = string.Format("{0}: msgId={2:X4} msg={1}", BitConverter.ToString(this.Addr), tmp.Data == null ? "null" : (BitConverter.ToString(tmp.Data) + "[" + Encoding.ASCII.GetString(tmp.Data.Select(z => (z < 0x20 || z > 0x7E) ? (byte)'.' : z).ToArray()) + "]"), tmp.MessageId);
      //            switch(tmp.TopicId) {
      //            case LOG_D_ID:
      //              Log.Debug(str);
      //              break;
      //            case LOG_I_ID:
      //              Log.Info(str);
      //              break;
      //            case LOG_W_ID:
      //              Log.Warning(str);
      //              break;
      //            case LOG_E_ID:
      //              Log.Error(str);
      //              break;
      //            }
      //          }
      //        } else {
      //          if(dev == null || dev.Owner == null) {
      //            if(_verbose.value) {
      //              Log.Debug("{0} via {1} unknown device", BitConverter.ToString(fm.addr), this.name);
      //            }
      //          } else {
      //            if(_verbose.value) {
      //              Log.Debug("{0} via {1} inactive", dev.Owner.name, this.name);
      //            }
      //          }
      //          _gate.SendGw(this, new MsForward(fm.addr, new MsDisconnect()));
      //        }
      //      }
      //    }
      //  }
      //  break;
      }
    }

    private ushort CalculateTopicId(string path) {
      ushort id;
      byte[] buf = Encoding.UTF8.GetBytes(path);
      id = Crc16.ComputeChecksum(buf);
      while(id == 0 || id == 0xF000 || id == 0xFFFF || _topics.Any(z => z.it == TopicIdType.Normal && z.TopicId == id)) {
        id = Crc16.UpdateChecksum(id, (byte)_rand.Next(0, 255));
      }
      return id;
    }
    /// <summary>Find or create TopicInfo by Topic</summary>
    /// <param name="tp">Topic as key</param>
    /// <param name="sendRegister">Send MsRegister for new TopicInfo</param>
    /// <returns>found TopicInfo or null</returns>
    private TopicInfo GetTopicInfo(Topic tp, bool sendRegister = true, string subIdx = null) {
      if(tp == null) {
        return null;
      }
      TopicInfo rez = null;
      bool field = !string.IsNullOrEmpty(subIdx) && subIdx[0] == '.';
      for(int i = _topics.Count - 1; i >= 0; i--) {
        if(_topics[i].topic == tp && (!field || _topics[i].subIdx == subIdx)) {
          rez = _topics[i];
          break;
        }
      }
      if(rez == null) {
        if(subIdx == null) {
          var siv = tp.GetField("MQTT-SN.subIdx");
          if(siv.ValueType != NiL.JS.Core.JSValueType.String || (subIdx = siv.Value as string) == null) {
            if(tp != owner) {
              subIdx = (tp.path.StartsWith(owner.path)) ? tp.path.Substring(owner.path.Length + 1) : tp.path;
            } else {
              return null;
            }
          }
        }
        rez = new TopicInfo();
        rez.topic = tp;
        rez.subIdx = subIdx;
        var pt = PredefinedTopics.FirstOrDefault(z => z.Item2 == subIdx);
        if(pt != null) {
          rez.TopicId = pt.Item1;
          rez.dType = pt.Item3;
          rez.it = TopicIdType.PreDefined;
          rez.registred = true;
        } else {
          var nt = _NTTable.FirstOrDefault(z => subIdx.StartsWith(z.Item1));
          if(nt != null) {
            rez.TopicId = CalculateTopicId(rez.topic.path);
            rez.dType = nt.Item2;
            rez.it = TopicIdType.Normal;
          } else {
            Log.Warning(owner.path + ".register(" + subIdx + ") - unknown type");
            return null;
          }
          _topics.Add(rez);
        }
        //UpdateInMute();
      }
      if(!rez.registred) {
        if(sendRegister) {
          Send(new MsRegister(rez.TopicId, rez.subIdx));
        } else {
          rez.registred = true;
        }
      }
      return rez;
    }
    private TopicInfo GetTopicInfo(string subIdx, bool sendRegister = true) {
      if(string.IsNullOrEmpty(subIdx)) {
        return null;
      }
      TopicInfo ti;
      Topic cur = null;
      int idx = subIdx.LastIndexOf('/');
      string cName = subIdx.Substring(idx + 1);
      if(subIdx[0] == '.') {
        cur = owner;
      } else {
        cur = owner.all.FirstOrDefault(z => {
          var nf = z.GetField("MQTT-SN.subIdx");
          return nf.ValueType == NiL.JS.Core.JSValueType.String && (nf.Value as string) == subIdx;
        });
        if(cur == null) {
          cur = owner.Get(subIdx, true, owner);
          cur.SetField("MQTT-SN.subIdx", subIdx, owner);
        }
      }
      ti = GetTopicInfo(cur, sendRegister, subIdx);
      return ti;
    }
    private TopicInfo GetTopicInfo(ushort topicId, TopicIdType topicIdType, bool sendRegister = true) {
      var ti = _topics.Find(z => z.it == topicIdType && z.TopicId == topicId);
      if(ti == null) {
        if(topicIdType == TopicIdType.PreDefined) {
          var pt = PredefinedTopics.FirstOrDefault(z => z.Item1 == topicId);
          if(pt != null) {
            ti = GetTopicInfo(pt.Item2, sendRegister);
          }
        } else if(topicIdType == TopicIdType.ShortName) {
          ti = GetTopicInfo(string.Format("{0}{1}", (char)(topicId >> 8), (char)(topicId & 0xFF)), sendRegister);
        }
        if(ti != null) {
          ti.it = topicIdType;
        }
      }
      return ti;
    }

    private void SetValue(TopicInfo ti, byte[] msgData, bool retained) {
      if(ti != null) {
        if(!ti.topic.path.StartsWith(owner.path)) {
          return;     // not allowed publish
        }
        JSC.JSValue val;
        switch(ti.dType) {
        case DType.Boolean:
          val = new JSL.Boolean((msgData[0] != 0));
          break;
        case DType.Integer: {
            long rv = (msgData[msgData.Length - 1] & 0x80) == 0 ? 0 : -1;
            for(int i = msgData.Length - 1; i >= 0; i--) {
              rv <<= 8;
              rv |= msgData[i];
            }
            val = new JSL.Number(rv);
          }
          break;
        case DType.String:
          val = new JSL.String(Encoding.Default.GetString(msgData));
          break;
        case DType.ByteArray: {
            //var arr = new JSL.Uint8Array(msgData.Length);
            //for(int i = 0; i < msgData.Length; i++) {
            //  arr[i.ToString()] = new JSL.Number(msgData[i]);
            //}
            val = JSC.JSValue.Marshal(msgData);
          }
          break;
        /*
        if(ti.topic.valueType == typeof(SmartTwi)) {
          var sa = (ti.topic.GetValue() as SmartTwi);
          if(sa == null) {
            sa = new SmartTwi(ti.topic);
            sa.Recv(msgData);
            val = sa;
          } else {
            sa.Recv(msgData);
            return;
          }
          break;
        } else if(ti.topic.valueType == typeof(TWIDriver)) {
          var twi = (ti.topic.GetValue() as TWIDriver);
          if(twi == null) {
            twi = new TWIDriver(ti.topic);
            twi.Recv(msgData);
            val = twi;
          } else {
            twi.Recv(msgData);
            return;
          }
          break;
        } else if(ti.topic.valueType == typeof(DevicePLC)) {
          var plc = (ti.topic.GetValue() as DevicePLC);
          if(plc == null) {
            plc = new DevicePLC(ti.topic);
            plc.Recv(msgData);
            val = plc;
          } else {
            plc.Recv(msgData);
            return;
          }
          break;
        } else {
          return;
        }*/
        default:
          return;
        }
        if(ti.subIdx[0] == '.') {
          if(ti.subIdx == ".MQTT-SN.declarer" && val.ValueType==JSC.JSValueType.String) {
            var v = val.Value as string;
            var type = "MQTT-SN/" + v.Substring(0, v.IndexOf('.'));
            ti.topic.SetField("type", type, owner);
          }
          ti.topic.SetField(ti.subIdx.Substring(1), val, owner);
        } else {
          if(retained) {
            if(!ti.topic.CheckAttribute(Topic.Attribute.Saved, Topic.Attribute.DB)) {
              ti.topic.SetAttribute(Topic.Attribute.DB);
            }
          } else {
            if(ti.topic.CheckAttribute(Topic.Attribute.Saved, Topic.Attribute.DB)) {
              ti.topic.ClearAttribute(Topic.Attribute.DB);
            }
          }
          //TODO: convert
          ti.topic.SetState(val, owner);
        }
      }
    }

    private ushort NextMsgId() {
      int rez = Interlocked.Increment(ref _messageIdGen);
      Interlocked.CompareExchange(ref _messageIdGen, 1, 0xFFFF);
      return (ushort)rez;
    }
    private void SyncMsgId(ushort p) {
      ResetTimer();
      int nid = p;
      if(nid == 0xFFFE) {
        nid++;
        nid++;
      }
      if(nid > (int)_messageIdGen || (nid < 0x0100 && _messageIdGen > 0xFF00)) {
        _messageIdGen = (ushort)nid;      // synchronize messageId
      }
    }

    private int BCD2int(int c) {
      return (c >> 4) * 10 + (c & 0x0F);
    }
    private byte int2BCD(int c) {
      return (byte)((c / 10) * 16 + (c % 10));
    }

    private void ProccessAcknoledge(MsMessage rMsg) {
      MsMessage msg = null;
      lock(_sendQueue) {
        MsMessage reqMsg;
        if(_sendQueue.Count > 0 && (reqMsg = _sendQueue.Peek()).MsgTyp == rMsg.ReqTyp && reqMsg.MessageId == rMsg.MessageId) {
          _sendQueue.Dequeue();
          _waitAck = false;
          if(_sendQueue.Count > 0 && !(msg = _sendQueue.Peek()).IsRequest) {
            _sendQueue.Dequeue();
          }
        }
      }
      if(msg == null && !_waitAck && state == State.AWake) {
        //ReisePool(null);
        if(_waitAck) {
          return; // sended from pool
        }
      }
      if(msg != null || state == State.AWake) {
        if(msg != null && msg.IsRequest) {
          _tryCounter = 2;
        }
        SendIntern(msg);
      } else if(!_waitAck) {
        ResetTimer();
      }
    }
    private void Send(MsMessage msg) {
      if(state != State.Disconnected && state != State.Lost) {
        bool send = true;
        if(msg.MessageId == 0 && msg.IsRequest) {
          msg.MessageId = NextMsgId();
          lock(_sendQueue) {
            if(_sendQueue.Count > 0 || state == State.ASleep) {
              send = false;
            }
            _sendQueue.Enqueue(msg);
          }
        }
        if(send) {
          if(msg.IsRequest) {
            _tryCounter = 2;
          }
          SendIntern(msg);
        }
      }
    }
    private void SendIntern(MsMessage msg) {
      while(state == State.AWake || (msg != null && (state != State.ASleep || msg.MsgTyp == MsMessageType.DISCONNECT))) {
        if(msg != null) {
          if(_gate != null) {
            //if(_statistic.value) {
            //  Stat(true, msg.MsgTyp, ((msg is MsPublish && (msg as MsPublish).Dup) || (msg is MsSubscribe && (msg as MsSubscribe).dup)));
            //}
            try {
              _gate.SendGw(this, msg);
            }
            catch(ArgumentOutOfRangeException ex) {
              Log.Warning("{0} - {1}", this.name, ex.Message);
              if(msg.IsRequest) {
                lock(_sendQueue) {
                  if(_sendQueue.Count > 0 && _sendQueue.Peek() == msg) {
                    _sendQueue.Dequeue();
                    _waitAck = false;
                  }
                }
              }
              msg = null;
            }
          }
          if(msg != null && msg.IsRequest) {
            ResetTimer(_rand.Next(ACK_TIMEOUT, ACK_TIMEOUT * 5 / 3) / (_tryCounter + 1));  // 600, 1000
            _waitAck = true;
            break;
          }
          if(_waitAck) {
            break;
          }
        }
        msg = null;
        lock(_sendQueue) {
          if(_sendQueue.Count == 0 && state == State.AWake) {
            if(_gate != null) {
              _gate.SendGw(this, new MsMessage(MsMessageType.PINGRESP));
              //if(_statistic.value) {
              //  Stat(true, MsMessageType.PINGRESP, false);
              //}
            }
            //var st = Owner.Get<long>(".cfg/XD_SleepTime", Owner);
            //ResetTimer(st.value > 0 ? (3100 + (int)st.value * 1550) : _duration);  // t_wakeup
            ResetTimer(_duration);
            state = State.ASleep;
            break;
          }
          if(_sendQueue.Count > 0 && !(msg = _sendQueue.Peek()).IsRequest) {
            _sendQueue.Dequeue();
          }
        }
      }
    }
    private void ResetTimer(int period = 0) {
      if(period == 0) {
        if(_waitAck) {
          return;
        }
        if(_sendQueue.Count > 0) {
          period = _rand.Next(ACK_TIMEOUT * 3 / 4, ACK_TIMEOUT);  // 450, 600
        } else if(_duration > 0) {
          period = _duration;
          _tryCounter = 1;
        }
      }
      //Log.Debug("$ {0}._activeTimer={1}", Owner.name, period);
      _activeTimer.Change(period, Timeout.Infinite);
    }
    private void TimeOut(object o) {
      //Log.Debug("$ {0}.TimeOut _tryCounter={1}", Owner.name, _tryCounter);
      if(_tryCounter > 0) {
        MsMessage msg = null;
        lock(_sendQueue) {
          if(_sendQueue.Count > 0) {
            msg = _sendQueue.Peek();
          }
        }
        _waitAck = false;
        if(msg != null) {
          _tryCounter--;
          SendIntern(msg);
        } else {
          ResetTimer();
          _tryCounter = 0;
        }
        return;
      }
      state = State.Lost;
      if(owner != null) {
        Disconnect();
        //if(_statistic.value) {
        //  Stat(false, MsMessageType.GWINFO);
        //}
        Log.Warning("{0} Lost", owner.path);
      }
      lock(_sendQueue) {
        _sendQueue.Clear();
      }
      if(_gate != null) {
        _gate.SendGw(this, new MsDisconnect());
        //if(_statistic.value) {
        //  Stat(true, MsMessageType.DISCONNECT, false);
        //}
      }
    }
    private void Disconnect(ushort duration = 0) {
      //if(duration == 0 && !string.IsNullOrEmpty(_willPath)) {
      //  TopicInfo ti = GetTopicInfo(_willPath, false);
      //  SetValue(ti, _wilMsg, false);
      //}
      if(duration > 0) {
        if(state == State.ASleep) {
          state = State.AWake;
        }
        ResetTimer(3100 + duration * 1550);  // t_wakeup
        this.Send(new MsDisconnect());
        _tryCounter = 0;
        state = State.ASleep;
        //var st = Owner.Get<long>(".cfg/XD_SleepTime", Owner);
        //st.saved = true;
        //st.SetValue((short)duration, new TopicChanged(TopicChanged.ChangeArt.Value, Owner) { Source = st });
      } else {
        _activeTimer.Change(Timeout.Infinite, Timeout.Infinite);
        this._gate = null;
        if(state != State.Lost) {
          state = State.Disconnected;
          if(owner != null) {
            Log.Info("{0} Disconnected", owner.path);
          }
        }
      }
      _waitAck = false;
    }

    private class TopicInfo {
      public Topic topic;
      public ushort TopicId;
      public TopicIdType it;
      public bool registred;
      public string subIdx;
      public DType dType;
    }
    private enum DType {
      Boolean,
      Integer,
      String,
      ByteArray,
    }
    private static Tuple<string, DType>[] _NTTable = new Tuple<string, DType>[]{ 
      new Tuple<string, DType>("In", DType.Boolean),
      new Tuple<string, DType>("Ip", DType.Boolean),
      new Tuple<string, DType>("Op", DType.Boolean),
      new Tuple<string, DType>("On", DType.Boolean),
      new Tuple<string, DType>("OA", DType.Boolean),   // output high if active
      new Tuple<string, DType>("Oa", DType.Boolean),   // output low if active
      new Tuple<string, DType>("Mz", DType.Boolean),   // Merkers

      new Tuple<string, DType>("Ai", DType.Integer),   //uint16 Analog ref
      new Tuple<string, DType>("AI", DType.Integer),   //uint16 Analog ref2
      new Tuple<string, DType>("Av", DType.Integer),   //uint16
      new Tuple<string, DType>("Ae", DType.Integer),   //uint16
      new Tuple<string, DType>("Pp", DType.Integer),   //uint16 PWM positive
      new Tuple<string, DType>("Pn", DType.Integer),   //uint16 PWM negative
      new Tuple<string, DType>("Mb", DType.Integer),   //int8
      new Tuple<string, DType>("MB", DType.Integer),   //uint8
      new Tuple<string, DType>("Mw", DType.Integer),   //int16
      new Tuple<string, DType>("MW", DType.Integer),   //uint16
      new Tuple<string, DType>("Md", DType.Integer),   //int32
      new Tuple<string, DType>("MD", DType.Integer),   //uint32
      new Tuple<string, DType>("Mq", DType.Integer),   //int64

      new Tuple<string, DType>("Ms", DType.String),

      new Tuple<string, DType>("St", DType.ByteArray),  // Serial port transmit
      new Tuple<string, DType>("Sr", DType.ByteArray),  // Serial port recieve
      new Tuple<string, DType>("Ma", DType.ByteArray),  // Merkers

      //new Tuple<string, DType>("pa", typeof(DevicePLC)),    // Program
      //new Tuple<string, DType>("sa", typeof(SmartTwi)),    // Smart TWI
      new Tuple<string, DType>("pa", DType.ByteArray),    // Program

    };
    private static Tuple<ushort, string, DType>[] PredefinedTopics = new Tuple<ushort, string, DType>[]{
      new Tuple<ushort, string, DType>(0xFFC0, ".MQTT-SN.declarer", DType.String),
      new Tuple<ushort, string, DType>(0xFFC1, ".MQTT-SN.phy1_addr", DType.ByteArray),
    };
    //internal static Dictionary<string, ushort> PredefinedTopics = new Dictionary<string, ushort>(){
    //  {".MQTT-SN.declarer",          0xFFC0},
    //  {".MQTT-SN.a_phy1",            0xFFC1},

    //  {"_logD",              LOG_D_ID},
    //  {"_logI",              LOG_I_ID},
    //  {"_logW",              LOG_W_ID},
    //  {"_logE",              LOG_E_ID},
    //};
  }
}
