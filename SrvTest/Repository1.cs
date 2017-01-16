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
      Assert.IsFalse(Topic.root.Exist("A0"));
    }
    [TestMethod]
    public void T02() {
      Topic A1 = Topic.root.Get("A1");
      Assert.AreEqual(Topic.root, A1.parent);
      Assert.AreEqual("A1", A1.name);
      Assert.AreEqual("/A1", A1.path);
    }

    [TestMethod]
    public void T03() {
      Topic A1 = Topic.root.Get("A1");
      var val = JSValue.Marshal(42);
      A1.SetValue(val);
      _repo.Tick();
      Assert.AreEqual(val, A1.GetValue());
    }
    [TestMethod]
    public void T04() {
      Topic a1 = Topic.root.Get("A1");
      _repo.Tick();
      a1.Remove();
      Assert.IsTrue(a1.disposed);
      Assert.IsFalse(Topic.root.Exist("A1"));
      _repo.Tick();
      Assert.IsTrue(a1.disposed);
      Assert.IsFalse(Topic.root.Exist("A1"));
    }
    [TestMethod]
    public void T05() {
      var b2 = Topic.root.Get("B2");
      var b2_a = b2.Get("A");
      _repo.Tick();
      b2.Remove();
      Assert.IsFalse(Topic.root.Exist("B2"));
      Assert.IsFalse(b2_a.disposed);
      _repo.Tick();
      Assert.IsTrue(b2.disposed);
      Assert.IsFalse(Topic.root.Exist("B2"));
      Assert.IsTrue(b2_a.disposed);
      Assert.IsFalse(Topic.root.Exist("/B2/A"));
    }
    [TestMethod]
    public void T06() {
      Topic a1 = Topic.root.Get("A1");
      var id=a1.GetField("_id");
      Assert.IsTrue(id.IsObjectId);
      var path = a1.GetField("path");
      Assert.IsTrue(path.IsString);
      Assert.AreEqual(path.AsString, "/A1");
    }
    [TestMethod]
    public void T07() {
      Topic a4 = Topic.root.Get("A4");
      var val = JSValue.Marshal(new DateTime(2017, 1, 16, 10, 48, 15, 19, DateTimeKind.Local) );
      a4.SetValue(val);
      _repo.Tick();
      Assert.AreEqual(val, a4.GetValue());
    }

    [TestMethod]
    public void T08() {
      if(File.Exists(dbPath)) {
        File.Delete(dbPath);
      }
      _repo.Start();

      Topic a1 = Topic.root.Get("A1");
      var val = JSValue.Marshal(43);
      a1.SetValue(val);
      _repo.Tick();
      a1 = null;
      _repo.Stop();

      _repo = new Repo();
      _repo.Init();
      _repo.Start();
      Assert.IsTrue(Topic.root.Exist("A1", out a1));
      Assert.AreEqual(43, (int)a1.GetValue());
      _repo.Stop();
    }
  }
}
