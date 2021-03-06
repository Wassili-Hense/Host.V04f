﻿///<remarks>This file is part of the <see cref="https://github.com/X13home">X13.Home</see> project.<remarks>
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using X13.PLC;
using JSC = NiL.JS.Core;
using JSL = NiL.JS.BaseLibrary;

namespace X13.WebServer {
  internal sealed class ApiV04 : SIO_Connection {
    private SortedSet<Topic> _subscriptions;

    public ApiV04()
      : base() {
      _subscriptions = new SortedSet<Topic>();
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
    /// RESPONSE: array of topics, topic - [path, flags, type[, value]], flags: 1 - acl.subscribe, 2 - acl.create, 4 - acl.change, 8 - acl.remove, 16 - hat children
    /// </param>
    private void Subscribe(EventArguments args) {
      string path = args[1].ToString();
      int req = (int)args[2];
      Topic parent;
      List<Topic> resp = new List<Topic>();
      if(Topic.root.Exist(path, out parent)) {
        resp.Add(parent);
        if((req & 2) == 2) {
          resp.AddRange(parent.children);
        }
        var arr = new JSL.Array();
        foreach(var t in resp) {
          if(_subscriptions.Contains(t)) {
            if((req & 1) != 1 || t != parent) {
              continue;
            }
          } else {
            _subscriptions.Add(t);
            t.Subscribe(SubscriptionChanged, SubRec.SubMask.Once | SubRec.SubMask.Chldren, false);
          }
          JSL.Array r;
          if((req & 1) == 1 && t == parent) {
            r = new JSL.Array(4);
            r[3] = t.valueRaw;
          } else {
            r = new JSL.Array(3);
          }
          r[0] = new JSL.String(t.path);
          r[1] = new JSL.Number((t.children.Any() ? 16 : 0) | 15);
          var pr = t.type;
          r[2] = pr == null ? JSC.JSValue.Null : new JSL.String(pr);
          arr.Add(r);
        }
        args.Response(arr);
      } else {
        args.Response(JSC.JSObject.Null);
      }
    }
    /// <summary>set topics value</summary>
    /// <param name="args">
    /// REQUEST: [6, path, value]
    /// RESPONSE: [success, oldvalue]
    /// </param>
    private void SetValue(EventArguments args) {
      string path = args[1].ToString();
      //TODO: check acl
      /*
       if(!acl(publish)){
         if(acl(subscribe)){
           args.Error(false, t.valueRaw);
         } else {
           args.Error(false);
         }
       }
       */
      Topic t = Topic.root.Get(path, true, _owner);
      t.SetJson(args[2], _owner);
      args.Response(true);
    }
    /// <summary>Create topic</summary>
    /// <param name="args">
    /// REQUEST: [8, path]
    /// RESPONSE: array of topics, topic - [path, flags, type[, value]], flags: 1 - acl.subscribe, 2 - acl.create, 4 - acl.change, 8 - acl.remove, 16 - hat children
    /// </param>
    private void Create(EventArguments args) {
      if(args.Count < 3 || args[1].ValueType != JSC.JSValueType.String) {
        args.Error("BAD request");
      }
      string path = args[1].Value as string;
      string sName = args[2].Value as string;
      JSC.JSValue def = null;

      if(args.Count > 3) {
        def = args[3];
      } else {
        if(sName != null) {
          Topic t = Topic.root.Get("/etc//" + sName, false);
          if(t != null) {
            def = t.valueRaw["default"];
          }
        }
      }
      if(def == null || !def.Defined) {
        def = JSC.JSValue.Null;
      }
      var t2 = Topic.root.Create(path, _owner, sName, def);

      var arr = new JSL.Array();
      JSL.Array r=new JSL.Array(1);
      r[0] = new JSL.String(t2.path);
      r[1] = new JSL.Number((t2.children.Where(z => z.name != "$type").Any() ? 16 : 0) | 15);
      r[2] = sName;
      r[3] = def;
      arr.Add(r);
      args.Response(arr);
    }
    /// <summary>Remove topic</summary>
    /// <param name="args">
    /// REQUEST: [10, path]
    /// </param>
    private void Remove(EventArguments args) {
      Topic t;
      string path = args[1].ToString();
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
      string pathO = args[1].ToString();
      string pathP = args[2].ToString();
      if(Topic.root.Exist(pathO, out t) && Topic.root.Exist(pathP, out p)) {
        CopyTopic(t, p);
      }
    }
    private void CopyTopic(Topic t, Topic p) {
      Topic n = p.Get(t.name, true);
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
      string pathS = args[1].ToString();
      string pathD = args[2].ToString();
      string nname;
      if(Topic.root.Exist(pathS, out t) && Topic.root.Exist(pathD, out p)) {
        if(args.Count < 4) {
          nname = t.name;
        } else {
          nname = args[3].ToString();
        }
        t.Move(p, nname);
      }
    }

    private void SubscriptionChanged(SubRec s, Perform p) {
      if(s.path == p.src.path) {
        if(p.art == Perform.Art.changed) {
          var pr = p.src.type;
          base.Emit(5, p.src.path, new JSL.Number((p.src.children.Any() ? 16 : 0) | 15), pr == null ? JSC.JSValue.Null : new JSL.String(pr), p.src.valueRaw);
        }
      } else {
        if(p.art == Perform.Art.create) {
          var pr = p.src.type;
          base.Emit(5, p.src.path, new JSL.Number((p.src.children.Any() ? 16 : 0) | 15), pr == null ? JSC.JSValue.Null : new JSL.String(pr), p.src.valueRaw);
          if(!_subscriptions.Contains(p.src)) {
            _subscriptions.Add(p.src);
            p.src.Subscribe(SubscriptionChanged, SubRec.SubMask.Once | SubRec.SubMask.Chldren, false);
          }
        } else if(p.art == Perform.Art.remove) {
          base.Emit(9, p.src.path);
          _subscriptions.Remove(p.src);
        } else if(p.art == Perform.Art.move) {
          base.Emit(9, p.src.path);
        }
      }
    }

    protected override void OnClose(WebSocketSharp.CloseEventArgs e) {
      foreach(var t in _subscriptions) {
        t.Unsubscribe(SubscriptionChanged, SubRec.SubMask.Once | SubRec.SubMask.Chldren, false);
      }
      base.OnClose(e);
    }
  }
}
