﻿using NiL.JS;
using NiL.JS.Core;
using NiL.JS.Expressions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace X13 {
  internal partial class Compiler : Visitor<Compiler> {
    private int _sp;
    private List<Merker> _memory;
    private List<Scope> _programm;
    private Stack<Scope> _scope;
    private Scope cur;

    public Compiler() {
    }
    public void ScopePush(string name) {
      cur = new Scope(name);
      _scope.Push(cur);
      _programm.Add(cur);
    }
    public void ScopePop() {
      _scope.Pop();
      cur = _scope.Peek();
    }
    public void Compile(string code) {
      _memory = new List<Merker>();
      _scope = new Stack<Scope>();
      _programm = new List<Scope>();
      _sp = 0;
      ScopePush("");

      var module = new Module(code);
      module.Root.Visit(this);
      for(int i = 0; i < _programm.Count; i++) {
        Log.Info("{0}\n{1}", _programm[i].name, _programm[i].code.ToString());
      }
    }
  }
  internal class Merker {
    public uint Addr;
    public VM_DType type;
    public VariableDescriptor vd;
  }
  internal class Scope {
    public Scope(string name) {
      this.name = name;
      code = new StringBuilder();
      memory = new List<Merker>();
    }
    public string name;
    public StringBuilder code;
    public List<Merker> memory;
  }
  internal enum VM_DType {
    BOOL,
    UINT8,
    SINT8,
    UINT16,
    SINT16,
    SINT32,
    FUNCTION,
    PARAMETER,
    LOCAL,
  }

}
