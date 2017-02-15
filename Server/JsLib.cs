using NiL.JS.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace X13 {
  internal static class JsLib {
    public static readonly char[] SPLITTER_OBJ = new char[] { '.' };

    public static void SetField(ref JSValue obj, string path, JSValue val) {
      var ps = path.Split(SPLITTER_OBJ, StringSplitOptions.RemoveEmptyEntries);
      if(obj == null) {
        obj = JSObject.CreateObject();
      }
      JSValue p = obj, c;
      for(int i = 0; i < ps.Length - 1; i++) {
        c = p.GetProperty(ps[i]);
        if(c.ValueType <= JSValueType.Undefined || c.IsNull) {
          c = JSObject.CreateObject();
          p[ps[i]] = c;
        } else if(c.ValueType != JSValueType.Object) {
          return;
        }
        p = c;
      }
      if(val == null) {
        p.DeleteProperty(ps[ps.Length - 1]);
      } else {
        p[ps[ps.Length - 1]] = val;
      }
    }
  }
}
