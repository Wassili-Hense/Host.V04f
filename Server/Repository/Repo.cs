using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace X13.Repository {
  [System.ComponentModel.Composition.Export(typeof(IPlugModul))]
  [System.ComponentModel.Composition.ExportMetadata("priority", 1)]
  [System.ComponentModel.Composition.ExportMetadata("name", "Repository")]
  public class Repo : IPlugModul {

    public Repo() {

    }

    public void Init() {
      Topic.Init(this);
    }

    public void Start() {
    }

    public void Tick() {
    }

    public void Stop() {
    }

    public bool enabled { get { return true; } set {  } }

    internal void DoCmd(Perform c, bool inter) {

    }
  }
}
