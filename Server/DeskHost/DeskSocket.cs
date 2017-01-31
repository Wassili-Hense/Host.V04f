﻿///<remarks>This file is part of the <see cref="https://github.com/X13home">X13.Home</see> project.<remarks>
using JSC = NiL.JS.Core;
using JST = NiL.JS.BaseLibrary;
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
    private TcpClient _socket;
    private NetworkStream _stream;
    private byte[] _rcvBuf;
    private byte[] _rcvMsgBuf;
    private int _connected;
    private AsyncCallback _rcvCB;
    private int _rcvState;
    private int _rcvLength;
    private Action<JST.Array> _callback;

    public DeskSocket(TcpClient tcp, Action<JST.Array> cb) {
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
      int st = 2;
      int tmp = len;
      while(tmp > 0x7F) {
        tmp = tmp >> 7;
        st++;
      }
      var buf = new byte[len + st + 1];
      Encoding.UTF8.GetBytes(ms, 0, ms.Length, buf, st);
      tmp = len;
      buf[0] = 0;
      st = 1;
      while(tmp > 0) {
        buf[st] = (byte)((tmp & 0x7F) | (tmp > 0x7F ? 0x80 : 0));
        tmp = tmp >> 7;
        st++;
      }
      buf[buf.Length - 1] = 0xFF;
      this._stream.Write(buf, 0, buf.Length);
      Log.Debug("{0}.Send({1})", this.ToString(), ms);
    }
    public void Dispose() {
      if(Interlocked.Exchange(ref _connected, 0) != 0) {
        _stream.Close();
        _socket.Close();
      }
    }
    public override string ToString() {
      return ((IPEndPoint)_socket.Client.RemoteEndPoint).ToString();
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
        this.Dispose();
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
                  var mj = JST.JSON.parse(ms) as JST.Array;
                  if(verbose) {
                    Log.Debug("{0}.Rcv({1})", this.ToString(), ms);
                  }
                  _callback(mj);
                }
                catch(Exception ex) {
                  if(verbose) {
                    Log.Warning("{0}.Rcv({1}) - {2}", this.ToString(), ms ?? BitConverter.ToString(_rcvMsgBuf, 0, _rcvState), ex.Message);
                  }
                }
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
        if(_connected != 0) {
          //TODO: DeskMessage(Disconnect)
          this.Dispose();
        }
        return;
      }

      try {
        _stream.BeginRead(_rcvBuf, 0, 1, _rcvCB, _stream);
      }
      catch(IOException ex) {
        if(_connected != 0) {
          this.Dispose();
          Log.Warning("DeskConnection.RcvProcess {0}", ex.Message);
        }
        return;
      }
      catch(ObjectDisposedException ex) {
        Log.Warning("DeskConnection.RcvProcess {0}", ex.Message);
        return;
      }
    }

  }
}
