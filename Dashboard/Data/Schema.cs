using JSL = NiL.JS.BaseLibrary;
using JSC = NiL.JS.Core;
using JSF = NiL.JS.Core.Functions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;

namespace X13.Data {
  public class Schema {
    private JSC.JSValue _data;

    public Schema(JSC.JSValue data) {
      _data = data;
      var ji = _data["icon"];
      if(ji.ValueType == JSC.JSValueType.String) {
        icon = DWorkspace.This.GetIcon(ji.Value as string);
      }
    }
    public JSC.JSValue data { get { return _data; } }
    public BitmapSource icon { get; private set; }
  }
}
