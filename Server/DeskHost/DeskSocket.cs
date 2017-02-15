///<remarks>This file is part of the <see cref="https://github.com/X13home">X13.Home</see> project.<remarks>
using JSC = NiL.JS.Core;
using JST = NiL.JS.BaseLibrary;
using JSF = NiL.JS.Core.Functions;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace X13.DeskHost {
  internal class DeskSocket : IDisposable {
    private static JSF.ExternalFunction _JSON_Replacer;
    static DeskSocket() {
      _JSON_Replacer = new JSF.ExternalFunction(ConvertDate);
    }
    private static JSC.JSValue ConvertDate(JSC.JSValue thisBind, JSC.Arguments args) {
      if(args.Length == 2 && args[1].ValueType == JSC.JSValueType.String) {
        // 2015-09-16T14:15:18.994Z
        var s = args[1].Value as string;
        if(s != null) {
          if(s.Length == 24 && s[4] == '-' && s[7] == '-' && s[10] == 'T' && s[13] == ':' && s[16] == ':' && s[19] == '.') {
            var a = new JSC.Arguments();
            a.Add(args[1]);
            return JSC.JSValue.Marshal(new JST.Date(a));
          }
        }
      }
      return args[1];
    }
    public static JSC.JSValue ParseJson(string json) {
      return JST.JSON.parse(json, _JSON_Replacer);
    }
    public const int portDefault = 10013;

    private TcpClient _socket;
    private NetworkStream _stream;
    private byte[] _rcvBuf;
    private byte[] _rcvMsgBuf;
    private int _connected;
    private AsyncCallback _rcvCB;
    private int _rcvState;
    private int _rcvLength;
    protected Action<DeskMessage> _callback;

    public DeskSocket(TcpClient tcp, Action<DeskMessage> cb) {
      this._socket = tcp;
      this._stream = _socket.GetStream();
      this._rcvBuf = new byte[1];
      this._rcvMsgBuf = new byte[2048];
      this._callback = cb;
      this._connected = 1;
      this._rcvState = -2;
      this._stream.Flush();
      this._rcvCB = new AsyncCallback(RcvProcess);
      this._stream.BeginRead(_rcvBuf, 0, 1, _rcvCB, _stream);

    }
    public void SendArr(JST.Array arr) {
      var ms = JST.JSON.stringify(arr, null, null);
      int len = Encoding.UTF8.GetByteCount(ms);
      int st = 1;
      int tmp = len;
      while(tmp > 0x7F) {
        tmp = tmp >> 7;
        st++;
      }
      var buf = new byte[len + st + 2];
      Encoding.UTF8.GetBytes(ms, 0, ms.Length, buf, st + 1);
      tmp = len;
      buf[0] = 0;
      for(int i = st; i > 0; i--) {
        buf[i] = (byte)((tmp & 0x7F) | (i < st ? 0x80 : 0));
        tmp = tmp >> 7;
      }
      buf[buf.Length - 1] = 0xFF;
      this._stream.Write(buf, 0, buf.Length);
      Log.Debug("{0}.Send({1})", this.ToString(), ms);
    }
    private void Dispose(bool info) {
      if(Interlocked.Exchange(ref _connected, 0) != 0) {
        _stream.Close();
        _socket.Close();
        if(info) {
          var arr = new JST.Array(1);
          arr[0] = 99;                            // Lost
          _callback(new DeskMessage(this, arr));
        }
      }
    }
    public void Dispose() {
      Dispose(false);
    }
    protected IPEndPoint EndPoint { get { return (IPEndPoint)_socket.Client.RemoteEndPoint; } }
    public override string ToString() {
      var rep=(IPEndPoint)_socket.Client.RemoteEndPoint;
      return Convert.ToBase64String(rep.Address.GetAddressBytes().Union(BitConverter.GetBytes((ushort)rep.Port)).ToArray()).TrimEnd('=').Replace('+', '-').Replace('/', '=');
    }
    public bool verbose;
    private void RcvProcess(IAsyncResult ar) {
      bool first = true;
      int len;
      byte b;
      try {
        len = _stream.EndRead(ar);
      }
      catch(IOException) {
        this.Dispose(true);
        return;
      }
      catch(ObjectDisposedException) {
        return;
      }
      if(len > 0) {
        try {
          do {
            if(first) {
              first = false;
              b = _rcvBuf[0];
            } else {
              b = (byte)_stream.ReadByte();
            }
            if(_rcvState < 0) {
              if(_rcvState < -1) {
                if(b == 0) {
                  _rcvState = -1;
                  _rcvLength = 0;
                }
              } else {
                _rcvLength = (_rcvLength << 7) | (b & 0x7F);
                if(b < 0x80) {
                  if(_rcvLength < 3 || _rcvLength > int.MaxValue / 2048) {  // 1 MB
                    _rcvState = -2;                                         // Bad Msg.Len
                  } else {
                    _rcvState = 0;
                    if(_rcvLength >= _rcvMsgBuf.Length) {
                      _rcvMsgBuf = new byte[_rcvMsgBuf.Length * 2];
                    }
                  }
                }
              }
            } else if(_rcvState < _rcvLength) {
              _rcvMsgBuf[_rcvState] = b;
              _rcvState++;

            } else {
              if(b == 0xFF) {   // Paranoic mode On
                string ms = null;
                try {
                  ms = Encoding.UTF8.GetString(_rcvMsgBuf, 0, _rcvState);
                  var mj = ParseJson(ms) as JST.Array;
                  if(verbose) {
                    Log.Debug("{0}.Rcv({1})", this.ToString(), ms);
                  }
                  if(mj != null && mj.Count() > 0) {
                    _callback(new DeskMessage(this, mj));
                  }
                }
                catch(Exception ex) {
                  if(verbose) {
                    Log.Warning("{0}.Rcv({1}) - {2}", this.ToString(), ms ?? BitConverter.ToString(_rcvMsgBuf, 0, _rcvState), ex.Message);
                  }
                }
              } else {
                Log.Warning("Paranoic");
              }
              _rcvState = -2;
            }
          } while(_stream.DataAvailable);
        }
        catch(ObjectDisposedException) {
          return;
        }
        catch(Exception ex) {
          _rcvState = -2;
          Log.Warning(ex.ToString());
        }
      } else {
        this.Dispose(true);
        return;
      }

      try {
        _stream.BeginRead(_rcvBuf, 0, 1, _rcvCB, _stream);
      }
      catch(IOException ) {
        this.Dispose(true);
        return;
      }
      catch(ObjectDisposedException ex) {
        Log.Warning("DeskConnection.RcvProcess {0}", ex.Message);
        return;
      }
    }
  }
}
