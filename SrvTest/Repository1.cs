using LiteDB;
using NiL.JS.Core;
using JST = NiL.JS.BaseLibrary;
using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using X13.Repository;

namespace SrvTest {
  [TestClass]
  public class Repository1 {
    Repo _repo;

    [ClassInitialize()]
    public static void MyClassInitialize(TestContext testContext) {
      var val = JSValue.Marshal(41);  // Load NiL.JS
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
    }
  }
}
