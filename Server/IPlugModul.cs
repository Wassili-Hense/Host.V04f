﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace X13 {
  public interface IPlugModul {
    void Init();
    void Start();
    void Tick();
    void Stop();
    bool enabled { get; set; }
  }
  public interface IPlugModulData {
    int priority { get; }
    string name { get; }
  }
}
