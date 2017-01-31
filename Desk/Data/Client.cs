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

    public Client(string url="localhost", int port=10013) {
      var tcp = new TcpClient();
      tcp.Connect(url, port);
      _socket = new DeskHost.DeskSocket(tcp, onRecv);
      _socket.verbose = true;
    }

    private void onRecv(JST.Array arr) {
    }
  }
}
