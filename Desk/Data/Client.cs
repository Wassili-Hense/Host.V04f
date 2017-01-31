///<remarks>This file is part of the <see cref="https://github.com/X13home">X13.Home</see> project.<remarks>
using JSC = NiL.JS.Core;
using JST = NiL.JS.BaseLibrary;
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

    public readonly string server;
    public readonly int port;
    public readonly string userName;
    public readonly string password;
    public string alias;
    public DTopic root { get; private set; }

    public Client(string server, int port, string userName, string password) {
      this.server = server;
      this.port = port;
      this.userName = userName;
      this.password = password;
    }
    public bool Connect() {
      try {
        var tcp = new TcpClient();
        tcp.Connect(server, port);
        _socket = new DeskHost.DeskSocket(tcp, onRecv);
        _socket.verbose = true;
        root = new DTopic(this);
      }
      catch(Exception ex) {
        Log.Warning("{0}.Connect - {1}", this.ToString(), ex.Message);
        return false;
      }
      return true;
    }
    public void Close() {
      var sc = Interlocked.Exchange(ref _socket, null);
      if(sc != null) {
        sc.Dispose();
      }
    }

    private void onRecv(DeskHost.DeskMessage msg) {
      int cmd;
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
        }
        break;
      }
    }
    public override string ToString() {
      return alias??((userName == null ? string.Empty : (userName + "@")) + server + (port != DeskHost.DeskSocket.portDefault ? (":" + port.ToString()) : string.Empty));
    }

  }
}
