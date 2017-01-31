///<remarks>This file is part of the <see cref="https://github.com/X13home">X13.Home</see> project.<remarks>
using LiteDB;
using NiL.JS.Core;
using JSI = NiL.JS.Core.Interop;
using JST = NiL.JS.BaseLibrary;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace X13.Repository {
  public class JSObjectId : JSI.CustomType {
    private ObjectId _id;

    public JSObjectId(ObjectId id) {
      this._id = id;
    }

    [JSI.DoNotEnumerate]
    public JSValue toJSON(JSValue obj) {
      var r = JSObject.CreateObject();
      r["$type"] = "JSObjectId";
      r["id"] = _id.ToString();
      return r;
    }

  }
}
