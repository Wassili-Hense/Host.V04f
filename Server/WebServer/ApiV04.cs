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
      base.Register(11, Copy);
      base.Register(12, Move);
      base.Register(13, GetValue);
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
    /// REQUEST: [9, path, type] type: 1 - once, 2 - children, 4 - all
    /// RESPONSE: array of items, item - [path, flags, icon url], flags: 1 - acl.subscribe, 2 - acl.create, 4 - acl.change, 8 - acl.remove, 16 - hat children
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
        r[2]=t.vType==null?JSC.JSObject.JSNull:new JSL.String(t.vType.Name);
        arr.Add(r);
        //X13.Log.Debug("  [{0}, {1}, {2}]", t.path, r[1].As<int>(), r[2].As<string>());
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
    /// <summary>copy topic</summary>
    /// <param name="args">
    /// REQUEST: [11, path original, path new parent]
    /// </param>
    private void Copy(EventArguments args) {
      Topic t, p;
      string pathO=args[1].As<string>();
      string pathP=args[2].As<string>();
      if(Topic.root.Exist(pathO, out t) && Topic.root.Exist(pathP, out p)) {
        CopyTopic(t, p);
      }
    }
    private void CopyTopic(Topic t, Topic p) {
      Topic n=p.Get(t.name, true);
      foreach(var c in t.children.ToArray()) {
        CopyTopic(c, n);
      }
      n.Set(t.value);
    }
    /// <summary>Move topic</summary>
    /// <param name="args">
    /// REQUEST: [12, path source, path destinations parent, new name(optional rename)]
    /// </param>
    private void Move(EventArguments args) {
      Topic t, p;
      string pathS=args[1].As<string>();
      string pathD=args[2].As<string>();
      string nname;
      if(Topic.root.Exist(pathS, out t) && Topic.root.Exist(pathD, out p)) {
        if(args.Count<4) {
          nname=t.name;
        } else {
          nname=args[3].As<string>();
        }
        t.Move(p, nname);
      }
    }
    /// <summary>Get value</summary>
    /// <param name="args">
    /// REQUEST: [13, path]
    /// RESPONSE: value
    /// </param>
    private void GetValue(EventArguments args) {
      string path=args[1].As<string>();
      Topic t;
      if(Topic.root.Exist(path, out t)) {
        args.Response(t.valueRaw);
      } else {
        args.Error("NOT EXIST", path);
      }

    }
  }
}
