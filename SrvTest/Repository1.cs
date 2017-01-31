///<remarks>This file is part of the <see cref="https://github.com/X13home">X13.Home</see> project.<remarks>
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NiL.JS.Core;
using JST = NiL.JS.BaseLibrary;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using X13.Repository;

namespace SrvTest {
  [TestClass]
  public class Repository1 {
    private static string dbPath;
    private Repo _repo;


    [ClassInitialize()]
    public static void MyClassInitialize(TestContext testContext) {
      dbPath = "../data/persist.ldb";
      var val = JSValue.Marshal(41);  // Load NiL.JS
      //using(var db = new LiteDatabase(dbPath)) {
      //  db.GetCollectionNames();
      //}
    }
    [TestInitialize()]
    public void TestInitialize() {
      _repo = new Repo();
      _repo.Init();
    }

    [TestMethod]
    public void T01() {
      Assert.IsFalse(Topic.root.Exist("A"));
    }
    [TestMethod]
    public void T02() {
      Topic a = Topic.root.Get("A");
      Assert.AreEqual(Topic.root, a.parent);
      Assert.AreEqual("A", a.name);
      Assert.AreEqual("/A", a.path);
    }

    [TestMethod]
    public void T03() {
      Topic a = Topic.root.Get("A");
      var val = JSValue.Marshal(42);
      a.SetValue(val);
      _repo.Tick();
      Assert.AreEqual(val, a.GetValue());
    }
    [TestMethod]
    public void T04() {
      Topic a = Topic.root.Get("A");
      _repo.Tick();
      a.Remove();
      Assert.IsTrue(a.disposed);
      Assert.IsFalse(Topic.root.Exist("A"));
      _repo.Tick();
      Assert.IsTrue(a.disposed);
      Assert.IsFalse(Topic.root.Exist("A"));
    }
    [TestMethod]
    public void T05() {
      var b = Topic.root.Get("B");
      var b_a = b.Get("A");
      _repo.Tick();
      b.Remove();
      Assert.IsFalse(Topic.root.Exist("B"));
      Assert.IsFalse(b_a.disposed);
      _repo.Tick();
      Assert.IsTrue(b.disposed);
      Assert.IsFalse(Topic.root.Exist("B"));
      Assert.IsTrue(b_a.disposed);
      Assert.IsFalse(Topic.root.Exist("/B/A"));
    }
    [TestMethod]
    public void T06() {
      Topic a = Topic.root.Get("A");
      var id = a.GetField("_id");
      Assert.IsTrue(id.Value is JSObjectId);
      var path = a.GetField("p");
      Assert.AreEqual(JSValueType.String, path.ValueType);
      Assert.AreEqual("/A", path.ToString());
    }
    [TestMethod]
    public void T07() {
      Topic a = Topic.root.Get("A");
      var val = JSValue.Marshal(new DateTime(2017, 1, 16, 10, 48, 15, 19, DateTimeKind.Local));
      a.SetValue(val);
      _repo.Tick();
      Assert.AreEqual(val, a.GetValue());
    }

    [TestMethod]
    public void T08() {
      if(File.Exists(dbPath)) {
        File.Delete(dbPath);
      }
      _repo.Start();

      Topic a = Topic.root.Get("A");
      var val = JSValue.Marshal(43);
      a.saved = true;
      a.SetValue(val);

      Topic b = Topic.root.Get("B");
      b.SetValue(JSValue.Marshal(75));

      Topic c = Topic.root.Get("C");
      c.saved = true;
      c.SetValue(JSValue.Marshal(12.01));

      _repo.Tick();

      c.saved = false;

      _repo.Tick();

      a = null;
      b = null;
      c = null;
      _repo.Stop();

      _repo = new Repo();
      _repo.Init();
      _repo.Start();
      Assert.IsTrue(Topic.root.Exist("A", out a));
      Assert.IsTrue(a.saved);
      Assert.AreEqual(43, (int)a.GetValue());

      Assert.IsTrue(Topic.root.Exist("B", out b));
      Assert.IsFalse(b.saved);
      Assert.AreEqual(JSValue.Undefined, b.GetValue());

      Assert.IsTrue(Topic.root.Exist("C", out c));
      Assert.IsFalse(c.saved);
      Assert.AreEqual(JSValue.Undefined, c.GetValue());
      _repo.Stop();
    }

    [TestMethod]
    public void T09() {
      Topic c = Topic.root.Get("C");
      c.saved = true;
      c.SetField("MQTT.path", "/dev/Node1");
      _repo.Tick();
      Assert.IsTrue((bool)c.GetField("s"));
      Assert.AreEqual("/dev/Node1", c.GetField("MQTT.path").ToString());
    }
    [TestMethod]
    public void T10() {
      var a = Topic.root.Get("A");
      var a_c = a.Get("C");
      _repo.Tick();
      a.Move(null, "B");
      Assert.IsFalse(Topic.root.Exist("A"));
      Assert.AreEqual("B", a.name);
      Assert.AreEqual("/B", a.path);
      Assert.AreEqual("/B/C", a_c.path);

      _repo.Tick();
      Assert.AreEqual("/B", a.GetField("p").ToString());
      Assert.AreEqual("/B/C", a_c.GetField("p").ToString());
    }
    [TestMethod]
    public void T11() {
      var a_b_c = Topic.root.Get("/A/B/C");
      var a_b = a_b_c.parent;
      var d = Topic.root.Get("D");
      a_b.Move(d, null);
      Assert.AreEqual(d, a_b.parent);
      Assert.AreEqual("/D/B", a_b.path);
      Assert.AreEqual("/D/B/C", a_b_c.path);
      Assert.IsFalse(Topic.root.Exist("/A/B"));
      _repo.Tick();
      Assert.AreEqual("/D/B", a_b.GetField("p").ToString());
      Assert.AreEqual("/D/B/C", a_b_c.GetField("p").ToString());
    }
    [TestMethod]
    public void T12() {
      Topic t0 = Topic.root.Get("child");
      var arr = t0.children.ToArray();
      Assert.AreEqual(0, arr.Length);
      var t1 = t0.Get("ch_a");
      arr = t0.children.ToArray();
      Assert.AreEqual(1, arr.Length);
      Assert.AreEqual(t1, arr[0]);
      t1 = t0.Get("ch_b");
      var t2 = t1.Get("a");
      t2 = t1.Get("b");
      t1 = t0.Get("ch_c");
      t2 = t1.Get("a");
      arr = t0.children.ToArray();
      Assert.AreEqual(3, arr.Length);
      Assert.AreEqual(t1, arr[2]);
      arr = t0.all.ToArray();
      Assert.AreEqual(7, arr.Length);  // child, ch_a, ch_b, ch_b/a, ch_b/b, ch_c, ch_c/a
      Assert.AreEqual(t2, arr[6]);
      Assert.AreEqual(t1, arr[5]);
      Assert.AreEqual(t0, arr[0]);
    }
    [TestMethod]
    public void T13() {
      List<Perform> cmds = new List<Perform>();
      Topic t0 = Topic.root.Get("child");
      _repo.Tick();
      var s1 = t0.Subscribe(SubRec.SubMask.Once | SubRec.SubMask.Value, s => cmds.Add(s));
      _repo.Tick();
      Assert.AreEqual(2, cmds.Count);
      Assert.AreEqual(t0, cmds[0].src);
      Assert.AreEqual(Perform.Art.subscribe, cmds[0].art);
      Assert.AreEqual(t0, cmds[1].src);
      Assert.AreEqual(Perform.Art.subAck, cmds[1].art);
      cmds.Clear();

      var t1 = t0.Get("ch_a");
      t1.SetValue(new JST.String("Hi"));
      _repo.Tick();
      Assert.AreEqual(0, cmds.Count);

      s1.Dispose();
      t0.SetValue(new JST.Number(2.98));
      _repo.Tick();
      Assert.AreEqual(0, cmds.Count);
    }
    [TestMethod]
    public void T14() {
      Topic t0 = Topic.root.Get("child2");
      var t1 = t0.Get("ch_a");
      var t1_a = t1.Get("a");
      var cmds = new List<Perform>();
      _repo.Tick();
      var s1 = t0.Subscribe(SubRec.SubMask.Chldren | SubRec.SubMask.Value, s => cmds.Add(s));
      _repo.Tick();
      Assert.AreEqual(2, cmds.Count);
      Assert.AreEqual(Perform.Art.subscribe, cmds[0].art);
      Assert.AreEqual(t1, cmds[0].src);
      Assert.AreEqual(Perform.Art.subAck, cmds[1].art);
      Assert.AreEqual(t0, cmds[1].src);
      cmds.Clear();

      t1.SetValue(new JST.String("Hi"));
      _repo.Tick();
      Assert.AreEqual(1, cmds.Count);
      Assert.AreEqual(Perform.Art.changed, cmds[0].art);
      Assert.AreEqual(t1, cmds[0].src);
      cmds.Clear();

      var t2 = t0.Get("ch_b");
      _repo.Tick();
      Assert.AreEqual(1, cmds.Count);
      Assert.AreEqual(Perform.Art.create, cmds[0].art);
      Assert.AreEqual(t2, cmds[0].src);
      cmds.Clear();

      var t2_a = t2.Get("a");
      _repo.Tick();
      Assert.AreEqual(0, cmds.Count);

      s1.Dispose();
    }
    [TestMethod]
    public void T15() {
      Topic t0 = Topic.root.Get("child");
      var cmds = new List<Perform>();
      _repo.Tick();
      var s1 = t0.Subscribe(SubRec.SubMask.All, s => cmds.Add(s));
      _repo.Tick();
      Assert.AreEqual(2, cmds.Count, "T15.01");
      Assert.AreEqual(Perform.Art.subscribe, cmds[0].art, "T15.02");
      Assert.AreEqual(t0, cmds[0].src, "T15.03");
      Assert.AreEqual(Perform.Art.subAck, cmds[1].art, "T15.04");
      Assert.AreEqual(t0, cmds[1].src, "T15.05");
      cmds.Clear();

      t0.SetValue(new JST.String("Gamma"));
      _repo.Tick();
      Assert.AreEqual(0, cmds.Count, "T15.06");
      cmds.Clear();

      var t2 = t0.Get("ch_b");
      _repo.Tick();
      Assert.AreEqual(1, cmds.Count, "T15.07");
      Assert.AreEqual(Perform.Art.create, cmds[0].art, "T15.08");
      Assert.AreEqual(t2, cmds[0].src, "T15.09");
      cmds.Clear();

      var t2_a = t2.Get("a");
      _repo.Tick();
      Assert.AreEqual(1, cmds.Count, "T15.10");
      Assert.AreEqual(Perform.Art.create, cmds[0].art, "T15.11");
      Assert.AreEqual(t2_a, cmds[0].src, "T15.12");

      s1.Dispose();
    }
    [TestMethod]
    public void T16() {
      Topic t0 = Topic.root.Get("child");
      var t1 = t0.Get("ch_a");
      var t1_a = t1.Get("a");
      var cmds = new List<Perform>();
      _repo.Tick();
      var s1 = t0.Subscribe(SubRec.SubMask.All | SubRec.SubMask.Value, s => cmds.Add(s));
      _repo.Tick();
      Assert.AreEqual(4, cmds.Count, "T16.01");
      Assert.AreEqual(Perform.Art.subscribe, cmds[0].art, "T16.02");
      Assert.AreEqual(t0, cmds[0].src, "T16.03");
      Assert.AreEqual(Perform.Art.subscribe, cmds[1].art, "T16.04");
      Assert.AreEqual(t1, cmds[1].src, "T16.05");
      Assert.AreEqual(Perform.Art.subscribe, cmds[2].art, "T16.06");
      Assert.AreEqual(t1_a, cmds[2].src, "T16.07");
      Assert.AreEqual(Perform.Art.subAck, cmds[3].art, "T16.17");
      Assert.AreEqual(t0, cmds[3].src, "T16.18");
      cmds.Clear();

      t1.SetValue(new JST.String("Omega"));
      _repo.Tick();
      Assert.AreEqual(1, cmds.Count, "T16.08");
      Assert.AreEqual(Perform.Art.changed, cmds[0].art, "T16.09");
      Assert.AreEqual(t1, cmds[0].src, "T16.10");
      cmds.Clear();

      var t2 = t0.Get("ch_b");
      _repo.Tick();
      Assert.AreEqual(1, cmds.Count, "T16.11");
      Assert.AreEqual(Perform.Art.create, cmds[0].art, "T16.12");
      Assert.AreEqual(t2, cmds[0].src, "T16.13");
      cmds.Clear();

      var t2_a = t2.Get("a");
      _repo.Tick();
      Assert.AreEqual(1, cmds.Count, "T16.14");
      Assert.AreEqual(Perform.Art.create, cmds[0].art, "T16.15");
      Assert.AreEqual(t2_a, cmds[0].src, "T16.16");

      s1.Dispose();
    }
    [TestMethod]
    public void T17() {
      var cmds = new List<Perform>();

      Topic a = Topic.root.Get("A");
      Topic b = Topic.root.Get("B");
      _repo.Tick();
      a.SetField("TestA", true);
      b.SetField("TestB", false);
      _repo.Tick();
      var s1 = Topic.root.Subscribe(SubRec.SubMask.All | SubRec.SubMask.Field, "TestB", s => cmds.Add(s));
      _repo.Tick();
      Assert.AreEqual(2, cmds.Count, "T17.01");
      Assert.AreEqual(Perform.Art.subscribe, cmds[0].art, "T17.02");
      Assert.AreEqual(b, cmds[0].src, "T17.03");
      Assert.AreEqual(Perform.Art.subAck, cmds[1].art, "T17.04");
      Assert.AreEqual(Topic.root, cmds[1].src, "T17.05");
      cmds.Clear();
      a.SetField("TestB", "ValB");
      b.SetField("TestA", 34);
      _repo.Tick();
      Assert.AreEqual(1, cmds.Count, "T17.06");
      Assert.AreEqual(Perform.Art.changedField, cmds[0].art, "T17.07");
      Assert.AreEqual(a, cmds[0].src, "T17.08");

      s1.Dispose();
    }
    
    [TestMethod]
    public void T18() {  // Move+Subscribe
      var cmds1 = new List<Perform>();
      var cmds2 = new List<Perform>();
      var cmds3 = new List<Perform>();

      var a = Topic.root.Get("A");
      var b = a.Get("B");
      var c = b.Get("C");
      var d = Topic.root.Get("D");

      var s1 = Topic.root.Subscribe(SubRec.SubMask.All | SubRec.SubMask.Value, s => cmds1.Add(s));
      var s2 = a.Subscribe(SubRec.SubMask.All | SubRec.SubMask.Value, s => cmds2.Add(s));
      var s3 = c.Subscribe(SubRec.SubMask.Once | SubRec.SubMask.Value, s => cmds3.Add(s));
      _repo.Tick();
      cmds1.Clear();
      cmds2.Clear();
      cmds3.Clear();

      b.Move(null, "E");
      _repo.Tick();
      Assert.AreEqual(1, cmds1.Count, "T18.01");
      Assert.AreEqual(1, cmds2.Count, "T18.02");
      cmds1.Clear();
      cmds2.Clear();

      b.SetValue(1);
      _repo.Tick();
      Assert.AreEqual(1, cmds1.Count, "T18.03");
      Assert.AreEqual(1, cmds2.Count, "T18.04");
      cmds1.Clear();
      cmds2.Clear();

      b.Move(d, null);
      _repo.Tick();
      Assert.AreEqual(1, cmds1.Count, "T18.05");
      Assert.AreEqual(0, cmds2.Count, "T18.06");
      Assert.AreEqual(0, cmds3.Count, "T18.07");
      cmds1.Clear();
      cmds2.Clear();

      c.SetValue("Kappa");
      _repo.Tick();
      Assert.AreEqual(1, cmds1.Count, "T18.08");
      Assert.AreEqual(0, cmds2.Count, "T18.09");
      Assert.AreEqual(1, cmds3.Count, "T18.10");
      s1.Dispose();
      s2.Dispose();
      s3.Dispose();
    }
  }
}
