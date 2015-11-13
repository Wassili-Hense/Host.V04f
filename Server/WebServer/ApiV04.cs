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
      base.Register(4, Subscribe);
      base.Register(6, SetValue);
      base.Register(8, Create);
      base.Register(10, Remove);
      base.Register(11, Copy);
      base.Register(12, Move);
    }
    /// <summary>Subscribe topics</summary>
    /// <param name="args">
    /// REQUEST: [4, path, mask] mask: 1 - data, 2 - children
    /// RESPONSE: array of topics, topic - [path, flags, draft[, value]], flags: 1 - acl.subscribe, 2 - acl.create, 4 - acl.change, 8 - acl.remove, 16 - hat children
    /// </param>
    private void Subscribe(EventArguments args) {
      string path=args[1].As<string>();
      int req=args[2].As<int>();
      Topic parent;
      List<Topic> resp=new List<Topic>();
      if(Topic.root.Exist(path, out parent)) {
        resp.Add(parent);
        if((req & 2)==2) {
          resp.AddRange(parent.children);
        }
      }
      var arr=new JSL.Array();
      foreach(var t in resp) {
        JSL.Array r;
        if((req & 1)==1 && t==parent) {
          r=new JSL.Array(4);
          r[3]=t.valueRaw;
        } else {
          r=new JSL.Array(3);
        }
        r[0]=new JSL.String(t.path);
        r[1]=new JSL.Number((t.children.Any()?16:0)  | 15);
        var pr=t.draft;
        r[2]=pr==null?JSC.JSObject.JSNull:new JSL.String(pr);
        arr.Add(r);
      }
      args.Response(arr);

    }
    /// <summary>set topics value</summary>
    /// <param name="args">
    /// REQUEST: [6, path, value]
    /// RESPONSE: [success, oldvalue]
    /// </param>
    private void SetValue(EventArguments args) {
      string path=args[1].As<string>();
      //TODO: check acl
      /*
       if(!acl(publish)){
         if(acl(subscribe)){
           args.Response(false, t.valueRaw);
         } else {
           args.Response(false);
         }
       }
       */
      Topic t=Topic.root.Get(path, true, _owner);
      t.SetJson(args[2], _owner);
      args.Response(true);
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
  }
}
