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
      cur.code.Add(new Inst(InstCode.NOP, null, node));
      Log.Warning("//{0}//", node.GetType().Name);
      return this;
    }
    protected override Compiler Visit(Addition node) {
      AddCommon(node, node.FirstOperand, node.SecondOperand);
      return this;
    }
    protected override Compiler Visit(BitwiseConjunction node) {
      node.SecondOperand.Visit(this);
      node.FirstOperand.Visit(this);
      cur.code.Add(new Inst(InstCode.AND));
      _sp--;
      return this;
    }
    protected override Compiler Visit(ArrayDefinition node) {
      return Visit(node as Expression);
    }
    protected override Compiler Visit(Assignment node) {
      node.SecondOperand.Visit(this);
      Store(node, node.FirstOperand as GetVariable);
      return this;
    }
    protected override Compiler Visit(Call node) {
      GetVariable f = node.FirstOperand as GetVariable;
      if(f != null) {
        var m = GetMerker(f.Descriptor);
        if(m.scope != null) {
          var al = m.scope.memory.Where(z => z.type == VM_DType.PARAMETER).OrderBy(z => z.Addr).ToArray();
          if(al.Length == 0) {
            cur.code.Add(new Inst(InstCode.LDI_0));
          } else {
            for(int i = al.Length - 1; i >= 0; i--) {
              if(i < node.Arguments.Length) {
                node.Arguments[i].Visit(this);
              } else if(al[i].init != null) {  //TODO: check function(a, b=7)
                al[i].init.Visit(this);
              } else {
                cur.code.Add(new Inst(InstCode.LDI_0));
              }
            }
          }
          cur.code.Add(new Inst(InstCode.CALL, m));
          for(int i = al.Length - 1; i > 0; i--) {
            cur.code.Add(new Inst(InstCode.DROP));
            _sp--;
          }
        } else {
          throw new ApplicationException(m.vd.Name + ".scope null pointer exception");
        }
      } else {
        if(node.Arguments.Length == 0) {
          cur.code.Add(new Inst(InstCode.LDI_0));
        } else {
          for(int i = node.Arguments.Length - 1; i >= 0; i--) {
            node.Arguments[i].Visit(this);
          }
        }

        node.FirstOperand.Visit(this);
        cur.code.Add(new Inst(InstCode.SCALL));
        _sp--;

        for(int i = node.Arguments.Length - 1; i > 0; i--) {
          cur.code.Add(new Inst(InstCode.DROP));
          _sp--;
        }
      }
      return this;
    }
    protected override Compiler Visit(ClassDefinition node) {
      return Visit(node as Expression);
    }
    protected override Compiler Visit(Constant node) {
      int v = (int)node.Value;
      LoadConstant(node, v);
      return this;
    }
    protected override Compiler Visit(Decrement node) {
      var a = node.FirstOperand as GetVariable;
      if(a != null) {
        a.Visit(this);
        if(node.Type == DecrimentType.Predecriment) {
          cur.code.Add(new Inst(InstCode.DEC));
          cur.code.Add(new Inst(InstCode.DUP));
        } else {
          cur.code.Add(new Inst(InstCode.DUP));
          cur.code.Add(new Inst(InstCode.DEC));
        }
        _sp++;
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
      return Visit(node as Expression);
    }
    protected override Compiler Visit(Equal node) {
      node.SecondOperand.Visit(this);
      node.FirstOperand.Visit(this);
      cur.code.Add(new Inst(InstCode.CEQ));
      _sp--;
      return this;
    }
    protected override Compiler Visit(Expression node) {
      return Visit(node as CodeNode);
    }
    protected override Compiler Visit(FunctionDefinition node) {
      ScopePush("Function " + node.Name);
      for(int i = 0; i < node.Parameters.Count; i++) {
        var m = new Merker() { Addr = (uint)i, type = VM_DType.PARAMETER, vd = node.Parameters[i], init = node.Parameters[i].Initializer };
        cur.memory.Add(m);
      }
      node.Body.Visit(this);
      if(cur.code.Count == 0 || cur.code[cur.code.Count - 1]._code.Length != 1 || cur.code[cur.code.Count - 1]._code[0] != (byte)InstCode.RET) {
        cur.code.Add(new Inst(InstCode.RET));
      }
      var fm = GetMerker(node.Reference.Descriptor);
      fm.scope = cur;
      fm.scope.entryPoint = fm;
      ScopePop();
      return this;
    }
    protected override Compiler Visit(Property node) {
      return Visit(node as Expression);
    }
    protected override Compiler Visit(GetVariable node) {
      Merker m = GetMerker(node.Descriptor);
      switch(m.type) {
      case VM_DType.BOOL:
        cur.code.Add(new Inst(InstCode.LDM_B1_C16, m, node));
        break;
      case VM_DType.UINT8:
        cur.code.Add(new Inst(InstCode.LDM_U1_C16, m, node));
        break;
      case VM_DType.SINT8:
        cur.code.Add(new Inst(InstCode.LDM_S1_C16, m, node));
        break;
      case VM_DType.UINT16:
        cur.code.Add(new Inst(InstCode.LDM_U2_C16, m, node));
        break;
      case VM_DType.SINT16:
        cur.code.Add(new Inst(InstCode.LDM_S2_C16, m, node));
        break;
      case VM_DType.SINT32:
        cur.code.Add(new Inst(InstCode.LDM_S4_C16, m, node));
        break;
      case VM_DType.PARAMETER:
        cur.code.Add(new Inst((InstCode)(InstCode.LD_P0 + (byte)m.Addr), null, node));
        break;
      case VM_DType.LOCAL:
        cur.code.Add(new Inst((InstCode)(InstCode.LD_L0 + (byte)m.Addr), null, node));
        break;
      default:
        throw new NotImplementedException(node.ToString());
      }
      _sp++;
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
      if(a != null) {
        a.Visit(this);
        if(node.Type == IncrimentType.Preincriment) {
          cur.code.Add(new Inst(InstCode.INC));
          cur.code.Add(new Inst(InstCode.DUP));
        } else {
          cur.code.Add(new Inst(InstCode.DUP));
          cur.code.Add(new Inst(InstCode.INC));
        }
        _sp++;
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
      node.SecondOperand.Visit(this);
      node.FirstOperand.Visit(this);
      cur.code.Add(new Inst(InstCode.CLE));
      _sp--;
      return this;
    }
    protected override Compiler Visit(LessOrEqual node) {
      node.SecondOperand.Visit(this);
      node.FirstOperand.Visit(this);
      cur.code.Add(new Inst(InstCode.CLE));
      _sp--;
      return this;
    }
    protected override Compiler Visit(LogicalConjunction node) {
      return Visit(node as Expression);
    }
    protected override Compiler Visit(LogicalNegation node) {
      return Visit(node as Expression);
    }
    protected override Compiler Visit(LogicalDisjunction node) {
      return Visit(node as Expression);
    }
    protected override Compiler Visit(Modulo node) {
      return Visit(node as Expression);
    }
    protected override Compiler Visit(More node) {
      node.SecondOperand.Visit(this);
      node.FirstOperand.Visit(this);
      cur.code.Add(new Inst(InstCode.CGT));
      _sp--;
      return this;
    }
    protected override Compiler Visit(MoreOrEqual node) {
      node.SecondOperand.Visit(this);
      node.FirstOperand.Visit(this);
      cur.code.Add(new Inst(InstCode.CGE));
      _sp--;
      return this;
    }
    protected override Compiler Visit(Multiplication node) {
      return Visit(node as Expression);
    }
    protected override Compiler Visit(Negation node) {
      return Visit(node as Expression);
    }
    protected override Compiler Visit(New node) {
      return Visit(node as Expression);
    }
    protected override Compiler Visit(Comma node) {
      return Visit(node as Expression);
    }
    protected override Compiler Visit(BitwiseNegation node) {
      node.FirstOperand.Visit(this);
      cur.code.Add(new Inst(InstCode.NOT, null, node));
      return this;
    }
    protected override Compiler Visit(NotEqual node) {
      node.SecondOperand.Visit(this);
      node.FirstOperand.Visit(this);
      cur.code.Add(new Inst(InstCode.CNE));
      _sp--;
      return this;
    }
    protected override Compiler Visit(NumberAddition node) {
      AddCommon(node, node.FirstOperand, node.SecondOperand);
      return this;
    }
    protected override Compiler Visit(NumberLess node) {
      node.SecondOperand.Visit(this);
      node.FirstOperand.Visit(this);
      cur.code.Add(new Inst(InstCode.CLT));
      _sp--;
      return this;
    }
    protected override Compiler Visit(NumberLessOrEqual node) {
      node.SecondOperand.Visit(this);
      node.FirstOperand.Visit(this);
      cur.code.Add(new Inst(InstCode.CLE));
      _sp--;
      return this;
    }
    protected override Compiler Visit(NumberMore node) {
      node.SecondOperand.Visit(this);
      node.FirstOperand.Visit(this);
      cur.code.Add(new Inst(InstCode.CGT));
      _sp--;
      return this;
    }
    protected override Compiler Visit(NumberMoreOrEqual node) {
      node.SecondOperand.Visit(this);
      node.FirstOperand.Visit(this);
      cur.code.Add(new Inst(InstCode.CGE));
      _sp--;
      return this;
    }
    protected override Compiler Visit(BitwiseDisjunction node) {
      node.FirstOperand.Visit(this);
      node.SecondOperand.Visit(this);
      cur.code.Add(new Inst(InstCode.OR, null, node));
      _sp--;
      return this;
    }
    protected override Compiler Visit(RegExpExpression node) {
      return Visit(node as Expression);
    }
    protected override Compiler Visit(SetProperty node) {
      return Visit(node as Expression);
    }
    protected override Compiler Visit(SignedShiftLeft node) {
      return Visit(node as Expression);
    }
    protected override Compiler Visit(SignedShiftRight node) {
      return Visit(node as Expression);
    }
    protected override Compiler Visit(StrictEqual node) {
      node.SecondOperand.Visit(this);
      node.FirstOperand.Visit(this);
      cur.code.Add(new Inst(InstCode.CEQ));
      _sp--;
      return this;
    }
    protected override Compiler Visit(StrictNotEqual node) {
      node.SecondOperand.Visit(this);
      node.FirstOperand.Visit(this);
      cur.code.Add(new Inst(InstCode.CNE));
      _sp--;
      return this;
    }
    protected override Compiler Visit(StringConcatenation node) {
      return Visit(node as Expression);
    }
    protected override Compiler Visit(Substract node) {
      return Visit(node as Expression);
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
      return Visit(node as Expression);
    }
    protected override Compiler Visit(BitwiseExclusiveDisjunction node) {
      return Visit(node as Expression);
    }
    protected override Compiler Visit(Yield node) {
      return Visit(node as Expression);
    }
    protected override Compiler Visit(Break node) {
      return Visit(node as CodeNode);
    }
    protected override Compiler Visit(CodeBlock node) {
      Merker m;
      uint addr;
      VM_DType type;

      foreach(var v in node.Variables.OrderByDescending(z => z.ReferenceCount)) {
        m = null;
        addr = uint.MaxValue;
        if(v.Initializer != null && v.Initializer is FunctionDefinition) {
          type = VM_DType.FUNCTION;
        } else if(v.Name.Length>2 && _predefs.TryGetValue(v.Name.Substring(0, 2), out type) && UInt32.TryParse(v.Name.Substring(2), out addr)) {
          addr &= 0xFFFF;
          if(type == VM_DType.INPUT || type == VM_DType.OUTPUT) {
            addr = (uint)((uint)(((byte)v.Name[0]) << 24) | (uint)(((byte)v.Name[1]) << 16) | addr);
          }
        } else {
          addr = (uint)cur.memory.Where(z => z.type == VM_DType.LOCAL).Count();
          if(addr < 16) {
            type = VM_DType.LOCAL;
          } else {
            type = VM_DType.SINT32;
            addr = uint.MaxValue;
          }
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
          cur.code.Add(new Inst(InstCode.LDI_0));
        }
      }
      int sp = _sp;
      for(var i = 0; i < node.Body.Length; i++) {
        node.Body[i].Visit(this);
        while(_sp > sp) {
          cur.code.Add(new Inst(InstCode.DROP));
          _sp--;
        }
      }
      return this;
    }
    protected override Compiler Visit(Continue node) {
      return Visit(node as CodeNode);
    }
    protected override Compiler Visit(Debugger node) {
      return Visit(node as CodeNode);
    }
    protected override Compiler Visit(DoWhile node) {
      return Visit(node as CodeNode);
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
      //cur.code.Append("for(");
      //if(node.Initializator != null) {
      //  node.Initializator.Visit(this);
      //}
      //cur.code.Append(";");
      //if(node.Condition != null) {
      //  node.Condition.Visit(this);
      //}
      //cur.code.Append(";");
      //if(node.Post != null) {
      //  node.Post.Visit(this);
      //}
      //if(node.Body != null) {
      //  cur.code.Append("){\n");
      //  node.Body.Visit(this);
      //  cur.code.Append("}\n");
      //} else {
      //  cur.code.Append(");");
      //}
      //return this;
      return Visit(node as CodeNode);
    }
    protected override Compiler Visit(IfElse node) {
      Inst j1, j2, j3;
      node.Condition.Visit(this);
      
      j1 = new Inst(InstCode.JZ, null, node.Condition);
      cur.code.Add(j1);
      _sp--;
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
        _sp--;
      }
      cur.code.Add(new Inst(InstCode.RET, null, null));
      return this;
    }
    protected override Compiler Visit(Switch node) {
      return Visit(node as CodeNode);
    }
    protected override Compiler Visit(Throw node) {
      return Visit(node as CodeNode);
    }
    protected override Compiler Visit(TryCatch node) {
      return Visit(node as CodeNode);
    }
    protected override Compiler Visit(VariableDefinition node) {
      int i;
      for(i = 0; i < node.Initializers.Length; i++) {
        node.Initializers[i].Visit(this);
      }
      return this;
    }
    protected override Compiler Visit(While node) {
      return Visit(node as CodeNode);
    }
    protected override Compiler Visit(With node) {
      return Visit(node as CodeNode);
    }
  }
}
