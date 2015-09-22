using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using X13.PLC;
using JSC=NiL.JS.Core;
using JSL=NiL.JS.BaseLibrary;

namespace X13.WebServer {
  internal sealed class ApiV04 : SIO_Connection {
    public ApiV04()
      : base() {
        base.Register(8, Create);
        base.Register(9, Dir);
        base.Register(10, Remove);
    }
    /// <summary>Create topic</summary>
    /// <param name="args">
    /// REQUEST: [8, path]
    /// RESPONSE: success=true/false
    /// </param>
    private void Create(EventArguments args) {
      string path=args[1].As<string>();
      var t=Topic.root.Get(path, true);
      args.Response(true);
    }
    /// <summary>Dir topics</summary>
    /// <param name="args">
    /// REQUEST: [9, path, type] type: 0 - once, 1 - children, 2 - all
    /// RESPONSE: array of items, item - [path, flags, value type], flags: 1 - acl.subscribe, 2 - acl.create, 4 - acl.change, 8 - acl.remove, 16 - hat children
    /// </param>
    private void Dir(EventArguments args) {
      string path=args[1].As<string>();
      int req=args[2].As<int>();
      Topic parent;
      List<Topic> resp=new List<Topic>();
      if(Topic.root.Exist(path, out parent)) {
        if((req & 1)==1) {
          resp.Add(parent);
        }
        if((req & 2)==2) {
          resp.AddRange(parent.children);
        }
        if((req & 4)==4) {
          resp.AddRange(parent.all);
        }
      }
      var arr=new JSL.Array();
      foreach(var t in resp) {
        var r=new JSL.Array(3);
        r[0]=new JSL.String(t.path);
        r[1]=new JSL.Number((t.children.Any()?16:0)  | 15);
        r[2]=new JSL.String(t.vType==null?"null":t.vType.Name);
        arr.Add(r);
      }
      args.Response(arr);
    }
    /// <summary>Remove topic</summary>
    /// <param name="args">
    /// REQUEST: [10, path]
    /// </param>
    private void Remove(EventArguments args) {
      Topic t;
      string path=args[1].As<string>();
      if(Topic.root.Exist(path, out t)) {
        t.Remove();
      }
    }
  }
}
