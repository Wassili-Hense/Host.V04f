using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NiL.JS.Core;
using NiL.JS.Expressions;
using NiL.JS.Statements;


namespace X13 {
  internal partial class Compiler : Visitor<Compiler> {
    protected override Compiler Visit(CodeNode node) {
      throw new NotSupportedException("Visit(" + node.GetType().Name + " " + node.ToString() + ")");
    }
    protected override Compiler Visit(Addition node) {
      AddCommon(node, node.FirstOperand, node.SecondOperand);
      return this;
    }
    protected override Compiler Visit(BitwiseConjunction node) {
      Arg2Op(node, InstCode.AND);
      return this;
    }
    protected override Compiler Visit(ArrayDefinition node) {
      return Visit(node as Expression);
    }
    protected override Compiler Visit(Assignment node) {
      node.SecondOperand.Visit(this);
      Store(node, node.FirstOperand);
      return this;
    }
    protected override Compiler Visit(Call node) {
      GetVariable f = node.FirstOperand as GetVariable;
      Inst d;
      if(f != null) {
        var m = GetMerker(f.Descriptor);
        if(m.scope != null) {
          var al = m.scope.memory.Where(z => z.type == VM_DType.PARAMETER).OrderBy(z => z.Addr).ToArray();
          if(al.Length == 0) {
            d = new Inst(InstCode.LDI_0);
            cur.code.Add(d);
            _sp.Push(d);
          } else {
            for(int i = al.Length - 1; i >= 0; i--) {
              if(i < node.Arguments.Length) {
                node.Arguments[i].Visit(this);
              } else if(al[i].init != null) {  //TODO: check function(a, b=7)
                al[i].init.Visit(this);
              } else {
                d = new Inst(InstCode.LDI_0);
                cur.code.Add(d);
                _sp.Push(d);
              }
            }
          }
          cur.code.Add(new Inst(InstCode.CALL, m));
          for(int i = al.Length - 1; i > 0; i--) {
            cur.code.Add(new Inst(InstCode.NIP));
            d = _sp.Pop();
            _sp.Pop();
            _sp.Push(d);
          }
        } else {
          throw new ApplicationException(m.vd.Name + ".scope null pointer exception");
        }
      } else {
        if(node.Arguments.Length == 0) {
          d = new Inst(InstCode.LDI_0);
          cur.code.Add(d);
          _sp.Push(d);
        } else {
          for(int i = node.Arguments.Length - 1; i >= 0; i--) {
            node.Arguments[i].Visit(this);
          }
        }

        node.FirstOperand.Visit(this);
        cur.code.Add(new Inst(InstCode.SCALL));
        _sp.Pop();

        for(int i = node.Arguments.Length - 1; i > 0; i--) {
          cur.code.Add(new Inst(InstCode.NIP ));
          d=_sp.Pop();
          _sp.Pop();
          _sp.Push(d);
        }
      }
      return this;
    }
    protected override Compiler Visit(ClassDefinition node) {
      return Visit(node as Expression);
    }
    protected override Compiler Visit(Constant node) {
      int v = node.Value==null?0:(int)node.Value;
      LoadConstant(node, v);
      return this;
    }
    protected override Compiler Visit(Decrement node) {
      var a = node.FirstOperand as GetVariable;
      Inst d1, d2;
      if(a != null) {
        a.Visit(this);
        _sp.Pop();
        if(node.Type == DecrimentType.Predecriment) {
          d2 = new Inst(InstCode.DUP) { canOptimized = true };
          _sp.Push(d2);
          cur.code.Add(d1=new Inst(InstCode.DEC));
          _sp.Push(d1);
          cur.code.Add(d2);
        } else {
          cur.code.Add(d1 = new Inst(InstCode.DUP) { canOptimized = true });
          _sp.Push(d1);
          cur.code.Add(d1=new Inst(InstCode.DEC));
          _sp.Push(d1);
        }
        Store(node, a);
      } else {
        throw new NotImplementedException();
      }
      return this;
    }
    protected override Compiler Visit(Delete node) {
      return Visit(node as Expression);
    }
    protected override Compiler Visit(DeleteProperty node) {
      return Visit(node as Expression);
    }
    protected override Compiler Visit(Division node) {
      Arg2Op(node, InstCode.DIV);
      return this;
    }
    protected override Compiler Visit(Equal node) {
      Arg2Op(node, InstCode.CEQ);
      return this;
    }
    protected override Compiler Visit(Expression node) {
      var v = node as AssignmentOperatorCache;
      if(v != null) {
        return v.Source.Visit(this);
      }
      return Visit(node as CodeNode);
    }
    protected override Compiler Visit(FunctionDefinition node) {
      ScopePush("Function " + node.Name);
      var fm = GetMerker(node.Reference.Descriptor);
      fm.scope = cur;
      fm.scope.entryPoint = fm;
      for(int i = 0; i < node.Parameters.Count; i++) {
        var m = new Merker() { Addr = (uint)i, type = VM_DType.PARAMETER, vd = node.Parameters[i], init = node.Parameters[i].Initializer };
        cur.memory.Add(m);
      }
      node.Body.Visit(this);
      if(cur.code.Count == 0 || cur.code[cur.code.Count - 1]._code.Length != 1 || cur.code[cur.code.Count - 1]._code[0] != (byte)InstCode.RET) {
        cur.code.Add(new Inst(InstCode.RET));
      }
      ScopePop();
      return this;
    }
    protected override Compiler Visit(Property node) {
      return Visit(node as Expression);
    }
    protected override Compiler Visit(GetVariable node) {
      Merker m = GetMerker(node.Descriptor);
      Inst d;
      switch(m.type) {
      case VM_DType.BOOL:
        d = new Inst(InstCode.LDM_B1_C16, m, node);
        break;
      case VM_DType.UINT8:
        d = new Inst(InstCode.LDM_U1_C16, m, node);
        break;
      case VM_DType.SINT8:
        d = new Inst(InstCode.LDM_S1_C16, m, node);
        break;
      case VM_DType.UINT16:
        d = new Inst(InstCode.LDM_U2_C16, m, node);
        break;
      case VM_DType.SINT16:
        d = new Inst(InstCode.LDM_S2_C16, m, node);
        break;
      case VM_DType.SINT32:
        d = new Inst(InstCode.LDM_S4_C16, m, node);
        break;
      case VM_DType.PARAMETER:
        d = new Inst((InstCode)(InstCode.LD_P0 + (byte)m.Addr));
        break;
      case VM_DType.INPUT:
      case VM_DType.OUTPUT:
        d = new Inst(InstCode.IN, m, node);
        break;
      case VM_DType.LOCAL:
        d = new Inst((InstCode)(InstCode.LD_L0 + (byte)m.Addr));
        break;
      default:
        throw new NotImplementedException(node.ToString());
      }
      cur.code.Add(d);
      _sp.Push(d);
      return this;
    }
    protected override Compiler Visit(VariableReference node) {
      return Visit(node as Expression);
    }
    protected override Compiler Visit(In node) {
      return Visit(node as Expression);
    }
    protected override Compiler Visit(Increment node) {
      var a = node.FirstOperand as GetVariable;
      Inst d1, d2;
      if(a != null) {
        a.Visit(this);
        _sp.Pop();
        if(node.Type == IncrimentType.Preincriment) {
          d2 = new Inst(InstCode.DUP) { canOptimized = true };
          _sp.Push(d2);
          cur.code.Add(d1 = new Inst(InstCode.INC));
          _sp.Push(d1);
          cur.code.Add(d2);
        } else {
          cur.code.Add(d1 = new Inst(InstCode.DUP) { canOptimized = true });
          _sp.Push(d1);
          cur.code.Add(d2=new Inst(InstCode.INC));
          _sp.Push(d2);
        }
        Store(node, a);
      } else {
        throw new NotImplementedException();
      }
      return this;
    }
    protected override Compiler Visit(InstanceOf node) {
      return Visit(node as Expression);
    }
    protected override Compiler Visit(NiL.JS.Expressions.ObjectDefinition node) {
      return Visit(node as Expression);
    }
    protected override Compiler Visit(Less node) {
      Arg2Op(node, InstCode.CLT);
      return this;
    }
    protected override Compiler Visit(LessOrEqual node) {
      Arg2Op(node, InstCode.CLE);
      return this;
    }
    protected override Compiler Visit(LogicalConjunction node) {
      Inst d, j1, j2;
      node.FirstOperand.Visit(this);
      d = new Inst(InstCode.DUP);
      cur.code.Add(d);
      _sp.Push(d);
      j1 = new Inst(InstCode.JZ);
      cur.code.Add(j1);
      node.SecondOperand.Visit(this);
      _sp.Pop();
      _sp.Pop();
      d = new Inst(InstCode.AND_L);
      cur.code.Add(d);
      _sp.Push(d);
      j2 = new Inst(InstCode.LABEL);
      j1._ref = j2;
      cur.code.Add(j2);
      return this;
    }
    protected override Compiler Visit(LogicalNegation node) {
      node.FirstOperand.Visit(this);
      _sp.Pop();
      var d = new Inst(InstCode.NOT_L);
      cur.code.Add(d);
      _sp.Push(d);
      return this;

    }
    protected override Compiler Visit(LogicalDisjunction node) {
      Inst d, j1, j2;
      node.FirstOperand.Visit(this);
      d = new Inst(InstCode.DUP);
      cur.code.Add(d);
      _sp.Push(d);
      j1 = new Inst(InstCode.JNZ);
      _sp.Pop();
      cur.code.Add(j1);
      node.SecondOperand.Visit(this);
      _sp.Pop();
      _sp.Pop();
      d = new Inst(InstCode.OR_L);
      cur.code.Add(d);
      _sp.Push(d);
      j2 = new Inst(InstCode.LABEL);
      j1._ref = j2;
      cur.code.Add(j2);
      return this;
    }
    protected override Compiler Visit(Modulo node) {
      Arg2Op(node, InstCode.MOD);
      return this;
    }
    protected override Compiler Visit(More node) {
      Arg2Op(node, InstCode.CGT);
      return this;
    }
    protected override Compiler Visit(MoreOrEqual node) {
      Arg2Op(node, InstCode.CGE);
      return this;
    }
    protected override Compiler Visit(Multiplication node) {
      Arg2Op(node, InstCode.MUL);
      return this;
    }
    protected override Compiler Visit(Negation node) {
      Inst d;
      node.FirstOperand.Visit(this);
      _sp.Pop();
      d = new Inst(InstCode.NEG);
      cur.code.Add(d);
      _sp.Push(d);
      return this;
    }
    protected override Compiler Visit(New node) {
      return Visit(node as Expression);
    }
    protected override Compiler Visit(Comma node) {
      return Visit(node as Expression);
    }
    protected override Compiler Visit(BitwiseNegation node) {
      Inst d;
      node.FirstOperand.Visit(this);
      _sp.Pop();
      d = new Inst(InstCode.NOT);
      cur.code.Add(d);
      _sp.Push(d);
      return this;
    }
    protected override Compiler Visit(NotEqual node) {
      Arg2Op(node, InstCode.CNE);
      return this;
    }
    protected override Compiler Visit(NumberAddition node) {
      AddCommon(node, node.FirstOperand, node.SecondOperand);
      return this;
    }
    protected override Compiler Visit(NumberLess node) {
      Arg2Op(node, InstCode.CLT);
      return this;
    }
    protected override Compiler Visit(NumberLessOrEqual node) {
      Arg2Op(node, InstCode.CLE);
      return this;
    }
    protected override Compiler Visit(NumberMore node) {
      Arg2Op(node, InstCode.CGT);
      return this;
    }
    protected override Compiler Visit(NumberMoreOrEqual node) {
      Arg2Op(node, InstCode.CGE);
      return this;
    }
    protected override Compiler Visit(BitwiseDisjunction node) {
      Arg2Op(node, InstCode.OR);
      return this;
    }
    protected override Compiler Visit(RegExpExpression node) {
      return Visit(node as Expression);
    }
    protected override Compiler Visit(SetProperty node) {
      return Visit(node as Expression);
    }
    protected override Compiler Visit(SignedShiftLeft node) {
      Arg2Op(node, InstCode.LSL);
      return this;
    }
    protected override Compiler Visit(SignedShiftRight node) {
      Arg2Op(node, InstCode.ASR);
      return this;
    }
    protected override Compiler Visit(StrictEqual node) {
      Arg2Op(node, InstCode.CEQ);
      return this;
    }
    protected override Compiler Visit(StrictNotEqual node) {
      Arg2Op(node, InstCode.CNE);
      return this;
    }
    protected override Compiler Visit(StringConcatenation node) {
      return Visit(node as Expression);
    }
    protected override Compiler Visit(Substract node) {
      Arg2Op(node, InstCode.SUB);
      return this;
    }
    protected override Compiler Visit(Conditional node) {
      return Visit(node as Expression);
    }
    protected override Compiler Visit(ConvertToBoolean node) {
      return Visit(node as Expression);
    }
    protected override Compiler Visit(ConvertToInteger node) {
      return Visit(node as Expression);
    }
    protected override Compiler Visit(ConvertToNumber node) {
      return Visit(node as Expression);
    }
    protected override Compiler Visit(ConvertToString node) {
      return Visit(node as Expression);
    }
    protected override Compiler Visit(ConvertToUnsignedInteger node) {
      return Visit(node as Expression);
    }
    protected override Compiler Visit(TypeOf node) {
      return Visit(node as Expression);
    }
    protected override Compiler Visit(UnsignedShiftRight node) {
      Arg2Op(node, InstCode.LSR);
      return this;
    }
    protected override Compiler Visit(BitwiseExclusiveDisjunction node) {
      Arg2Op(node, InstCode.XOR);
      return this;
    }
    protected override Compiler Visit(Yield node) {
      return Visit(node as Expression);
    }
    protected override Compiler Visit(Break node) {
      DPC_Loop cl;
      if(node.Label != null) {
        var l = node.Label.ToString();
        cl = cur.loops.FirstOrDefault(z => z.labels.Any(y => y == l));
        if(cl == null) {
          cl = cur.loops.Peek();
        }
      } else {
        cl = cur.loops.Peek();
      }
      int tmp = _sp.Count;
      while(tmp > cl.sp1) {
        tmp--;
        cur.code.Add(new Inst(InstCode.DROP));
      }
      cur.code.Add(new Inst(InstCode.JMP) { _ref = cl.L3 });
      return this;
    }
    protected override Compiler Visit(CodeBlock node) {
      Merker m;
      uint addr;
      VM_DType type;
      int sp2 = _sp.Count;

      List<Assignment> inList=new List<Assignment>();
      foreach(var vd in node.Body.Select(z => z as VariableDefinition).Where(z => z != null)){
        inList.AddRange(vd.Initializers.Select(z => z as Assignment).Where(z => z != null));
      }

      foreach(var v in node.Variables) {
        m = null;
        addr = uint.MaxValue;
        if(v.Initializer != null && v.Initializer is FunctionDefinition) {
          type = VM_DType.FUNCTION;
        } else if(v.Name.Length>2 && _predefs.TryGetValue(v.Name.Substring(0, 2), out type) && UInt32.TryParse(v.Name.Substring(2), out addr)) {
          addr &= 0xFFFF;
          if(type == VM_DType.INPUT || type == VM_DType.OUTPUT) {
            addr = (uint)((uint)(((byte)v.Name[0]) << 24) | (uint)(((byte)v.Name[1]) << 16) | addr);
          }
        } else if(v.LexicalScope) {
          addr = (uint)cur.memory.Where(z => z.type == VM_DType.LOCAL).Count();
          if(addr < 16) {
            type = VM_DType.LOCAL;
          } else {
            throw new ArgumentOutOfRangeException("Too many local variables: " + v.Name + "in \n" + v.Owner.ToString());
          }
        } else {
          type = VM_DType.SINT32;
          addr = uint.MaxValue;
        }
        if(type == VM_DType.LOCAL) {
          m = cur.memory.FirstOrDefault(z => z.vd.Name == v.Name && z.type == type);
          if(m == null) {
            m = new Merker() { Addr = addr, type = type, vd = v };
            cur.memory.Add(m);
          }
        } else {
          m = _memory.FirstOrDefault(z => z.vd.Name == v.Name && z.type == type);
          if(m == null) {
            m = new Merker() { Addr = addr, type = type, vd = v };
            _memory.Add(m);
          }
        }
        //cur.code.AppendFormat("\tDEF_{3}\t{0}\t\t;{1}@{2}\n", m.vd.Name, m.type.ToString(), m.Addr, m.type == VM_DType.LOCAL ? "L" : "G");
        if(v.Initializer != null) {
          v.Initializer.Visit(this);
        } else if(type == VM_DType.LOCAL) {
          var a2 = inList.FirstOrDefault(z => (z.FirstOperand as GetVariable) != null && (z.FirstOperand as GetVariable).Descriptor == m.vd);
          if(a2 != null) {
            a2.SecondOperand.Visit(this);
            m.initialized = true;
          } else {
            var d = new Inst(InstCode.LDI_0);
            cur.code.Add(d);
            _sp.Push(d);
          }
        }
      }
      int sp = _sp.Count;
      for(var i = 0; i < node.Body.Length; i++) {
        node.Body[i].Visit(this);
        while(_sp.Count > sp) {
          var d = _sp.Pop();
          if(!d.canOptimized || !cur.code.Remove(d) ) {
            cur.code.Add(new Inst(InstCode.DROP));
          }
        }
      }
      while(_sp.Count > sp2) {
        var d = _sp.Pop();
        cur.code.Add(new Inst(InstCode.DROP));
        uint idx = (uint)cur.memory.Where(z => z.type == VM_DType.LOCAL).Count();
        if(idx == 0 || cur.memory.RemoveAll(z => z.type == VM_DType.LOCAL && z.Addr == idx - 1) != 1) {
          throw new ApplicationException("Stack error in " + node.ToString());
        }
      }

      return this;
    }
    protected override Compiler Visit(Continue node) {
      DPC_Loop cl;
      if(node.Label!=null) {
        var l = node.Label.ToString();
        cl = cur.loops.FirstOrDefault(z => z.labels.Any(y => y == l));
        if(cl == null) {
          cl = cur.loops.Peek();
        }
      } else {
        cl = cur.loops.Peek();
      }
      int tmp=_sp.Count;
      while(tmp > cl.sp2) {
        tmp--;
        cur.code.Add(new Inst(InstCode.DROP));
      }
      cur.code.Add(new Inst(InstCode.JMP) { _ref = cl.L2 });
      return this;
    }
    protected override Compiler Visit(Debugger node) {
      return Visit(node as CodeNode);
    }
    protected override Compiler Visit(DoWhile node) {
      var cl = new DPC_Loop(_sp.Count, node.Labels);
      cur.loops.Push(cl);

      cur.code.Add(cl.L1);

      cl.sp2 = _sp.Count();

      node.Body.Visit(this);

      while(_sp.Count > cl.sp2) {
        _sp.Pop();
         cur.code.Add(new Inst(InstCode.DROP));
      }

      cur.code.Add(cl.L2);
      node.Condition.Visit(this);
      cur.code.Add(new Inst(InstCode.JNZ, null, node.Condition){ _ref=cl.L1});
      cur.code.Add(cl.L3);
      _sp.Pop();
      cur.loops.Pop();

      return this;
    }
    protected override Compiler Visit(Empty node) {
      return Visit(node as CodeNode);
    }
    protected override Compiler Visit(ForIn node) {
      return Visit(node as CodeNode);
    }
    protected override Compiler Visit(ForOf node) {
      return Visit(node as CodeNode);
    }
    protected override Compiler Visit(For node) {
      var cl = new DPC_Loop(_sp.Count, node.Labels.ToArray());
      cur.loops.Push(cl);

      if(node.Initializator != null) {
        node.Initializator.Visit(this);
      }
      cur.code.Add(cl.L1);
      node.Condition.Visit(this);
      cur.code.Add(new Inst(InstCode.JZ, null, node.Condition) { _ref = cl.L3 });
      _sp.Pop();
      cl.sp2 = _sp.Count;

      node.Body.Visit(this);

      while(_sp.Count > cl.sp2) {
        _sp.Pop();
        cur.code.Add(new Inst(InstCode.DROP));
      }
      cur.code.Add(cl.L2);
      if(node.Post != null) {
        node.Post.Visit(this);
      }
      cur.code.Add(new Inst(InstCode.JMP) { _ref = cl.L1 });
      cur.code.Add(cl.L3);

      cur.loops.Pop();
      return this;
    }
    protected override Compiler Visit(IfElse node) {
      Inst j1, j2, j3;
      node.Condition.Visit(this);
      
      j1 = new Inst(InstCode.JZ, null, node.Condition);
      cur.code.Add(j1);
      _sp.Pop();
      node.Then.Visit(this);
      if(node.Else != null) {
        j2 = new Inst(InstCode.JMP);
        cur.code.Add(j2);
        j3 = new Inst(InstCode.LABEL);
        j1._ref = j3;
        cur.code.Add(j3);
        node.Else.Visit(this);
        j3 = new Inst(InstCode.LABEL);
        j2._ref = j3;
        cur.code.Add(j3);
      } else {
        j3 = new Inst(InstCode.LABEL);
        j1._ref = j3;
        cur.code.Add(j3);
      }
      return this;
    }
    protected override Compiler Visit(InfinityLoop node) {
      return Visit(node as CodeNode);
    }
    protected override Compiler Visit(LabeledStatement node) {
      return Visit(node as CodeNode);
    }
    protected override Compiler Visit(Return node) {
      if(node.Value != null) {
        node.Value.Visit(this);
        cur.code.Add(new Inst(InstCode.ST_P0, null, node));
        _sp.Pop();
      }
      cur.code.Add(new Inst(InstCode.RET, null, null));
      return this;
    }
    protected override Compiler Visit(Switch node) {
      int i, j;
      var labels = new Inst[node.Cases.Length+1];
      labels[labels.Length - 1] = new Inst(InstCode.LABEL);
      var cvs=node.Cases.Where(z=>z.Statement!=null).ToArray();
      node.Image.Visit(this);
      for(i = 0; i < cvs.Length; i++) {
        if(i < cvs.Length - 1) {
          cur.code.Add(new Inst(InstCode.DUP));
        }
        labels[i] = new Inst(InstCode.LABEL);
        cvs[i].Statement.Visit(this);
        cur.code.Add(new Inst(InstCode.CEQ));
        cur.code.Add(new Inst(InstCode.JNZ, null, cvs[i].Statement) { _ref = labels[i] });
        _sp.Pop();
      }
      _sp.Pop();
      if(cvs.Length == node.Cases.Length) {
        cur.code.Add(new Inst(InstCode.JMP) { _ref = labels[labels.Length-1] });
      } else {
        labels[labels.Length - 2] = new Inst(InstCode.LABEL);
        cur.code.Add(new Inst(InstCode.JMP) { _ref = labels[labels.Length - 2] });
        var tmp = new SwitchCase[node.Cases.Length];
        Array.Copy(cvs, tmp, cvs.Length);
        tmp[tmp.Length - 1] = node.Cases.First(z => z.Statement == null);
        cvs=tmp;
      }
      for(i = 0; i < node.Body.Length; i++) {
        for(j = 0; j < cvs.Length; j++) {
          if(cvs[j].Index == i) {
            cur.code.Add(labels[j]);
            break;
          }
        }
        if(node.Body[i] is Break) {
          cur.code.Add(new Inst(InstCode.JMP) { _ref = labels[labels.Length - 1] });
        } else {
          node.Body[i].Visit(this);
        }
        cur.code.Add(labels[labels.Length-1]);
      }
      return this;
    }
    protected override Compiler Visit(Throw node) {
      return Visit(node as CodeNode);
    }
    protected override Compiler Visit(TryCatch node) {
      return Visit(node as CodeNode);
    }
    protected override Compiler Visit(VariableDefinition node) {
      int i;
      Assignment a1;
      for(i = 0; i < node.Initializers.Length; i++) {
        if(node.Initializers[i] is GetVariable) {
          continue;
        } else if((a1 = node.Initializers[i] as Assignment) != null) {
          var m = GetMerker((a1.FirstOperand as GetVariable).Descriptor);
          if(m != null && m.initialized) {
            continue;
          }
        }
        node.Initializers[i].Visit(this);
      }
      return this;
    }
    protected override Compiler Visit(While node) {
      var cl = new DPC_Loop(_sp.Count, node.Labels);
      cur.loops.Push(cl);

      cur.code.Add(cl.L1);
      cur.code.Add(cl.L2);
      node.Condition.Visit(this);
      cur.code.Add(new Inst(InstCode.JZ, null, node.Condition) { _ref = cl.L3 });
      _sp.Pop();
      cl.sp2 = _sp.Count;

      node.Body.Visit(this);

      while(_sp.Count > cl.sp2) {
        _sp.Pop();
        cur.code.Add(new Inst(InstCode.DROP));
      }

      cur.code.Add(new Inst(InstCode.JMP) { _ref = cl.L1 });
      cur.code.Add(cl.L3);
      cur.loops.Pop();
      return this;
    }
    protected override Compiler Visit(With node) {
      return Visit(node as CodeNode);
    }
  }
}
