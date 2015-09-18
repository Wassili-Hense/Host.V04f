using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

namespace X13.WebServer {
  internal sealed class ApiV04 : SIO_Connection {
    public ApiV04()
      : base() {
        base.Register(1, Test);
    }
    private void Test(EventArguments args) {
      StringBuilder sb=new StringBuilder();
      sb.Append("ApiV04(");
      for(int i=0; i<args.Count; i++) {
        if(i>0) {
          sb.Append(", ");
        }
        sb.AppendFormat("{0}<{1}>", args[i].ToString(), args[i].ValueType);
      }
      sb.Append(")");
      X13.Log.Debug("{0}", sb.ToString());
      args.Response("Answer", 42);
      new Timer(o => base.Emit(2, DateTime.Now), null, 1500, -1);
    }
  }
}
