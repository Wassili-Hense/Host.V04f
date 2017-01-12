using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using X13.Repository;

namespace SrvTest {
  [TestClass]
  public class Repository1 {
    Repo _repo;

    [TestInitialize()]
    public void TestInitialize() {
      _repo = new Repo();
      _repo.Init();
    }

    [TestMethod]
    public void T01() {
      Topic A1 = Topic.root.Get("A1");
      Assert.AreEqual(Topic.root, A1.parent);
      Assert.AreEqual("A1", A1.name);
      Assert.AreEqual("/A1", A1.path);
    }
  }
}
