///<remarks>This file is part of the <see cref="https://github.com/X13home">X13.Home</see> project.<remarks>
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
  internal class DeskConnection : IDisposable {
    private DeskHostPl _basePl;
    private DeskSocket _socket;

    public DeskConnection(DeskHostPl pl, TcpClient tcp) {
      this._basePl = pl;
      this._socket = new DeskSocket(tcp, _basePl.AddRMsg);
      this._socket.verbose = true;

      // Hello
      var arr = new JST.Array(2);
      arr[0] = 1;
      arr[1] = Environment.MachineName;
      this._socket.SendArr(arr);
    }

    public void Dispose() {
      _socket.Dispose();
    }
  }
}
