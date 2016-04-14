using JSC = NiL.JS.Core;
using JSL = NiL.JS.BaseLibrary;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace X13.UI {
  public interface IValueEditor {
    void ValueChanged(JSC.JSValue value);
    void SchemaChanged(JSC.JSValue schema);
  }
}
