///<remarks>This file is part of the <see cref="https://github.com/X13home">X13.Home</see> project.<remarks>
using JSC = NiL.JS.Core;
using JST = NiL.JS.BaseLibrary;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace X13.DeskHost {
  internal class DeskMessage {
    private DeskSocket _conn;
    private JST.Array _request;
    private JST.Array _response;

    public DeskMessage(DeskSocket conn, JST.Array req) {
      this._conn = conn;
      this._request = req;
      this.Count = req.Count();
    }
    

    public JSC.JSValue this[int idx] {
      get {
        return _request[idx];
      }
    }
    public readonly int Count;
    public void Response(params JSC.JSValue[] args) {
      _response = new JST.Array(args);
    }
  }
}
