using LiteDB;
using NiL.JS.Core;
using JST = NiL.JS.BaseLibrary;
using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using X13.Repository;
using System.IO;

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
      Assert.IsTrue(id.IsObjectId);
      var path = a.GetField("p");
      Assert.IsTrue(path.IsString);
      Assert.AreEqual(path.AsString, "/A");
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
      Assert.IsTrue(c.GetField("s"));
      Assert.AreEqual("/dev/Node1", c.GetField("MQTT.path").AsString);
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
      Assert.AreEqual("/B", a.GetField("p").AsString);
      Assert.AreEqual("/B/C", a_c.GetField("p").AsString);
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
      Assert.AreEqual("/D/B", a_b.GetField("p").AsString);
      Assert.AreEqual("/D/B/C", a_b_c.GetField("p").AsString);
    }
  }
}
