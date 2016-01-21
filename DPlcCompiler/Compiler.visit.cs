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
      cur.code.AppendFormat("\\{0}\\", node.GetType().Name);
      return this;
    }
    protected override Compiler Visit(Addition node) {
      node.FirstOperand.Visit(this);
      node.SecondOperand.Visit(this);
      cur.code.Append("\tADD\n");
      _sp--;
      return this;
    }
    protected override Compiler Visit(BitwiseConjunction node) {
      node.FirstOperand.Visit(this);
      node.SecondOperand.Visit(this);
      cur.code.Append("\tAND\n");
      _sp--;
      return this;
    }
    protected override Compiler Visit(ArrayDefinition node) {
      return Visit(node as Expression);
    }
    protected override Compiler Visit(Assignment node) {
      node.SecondOperand.Visit(this);
      var a = node.FirstOperand as GetVariable;
      if(a != null) {
        var m = _memory.First(z => z.vd.Name == a.Name);
        cur.code.AppendFormat("\tST\t{0}\n", m.vd.Name);
        _sp--;
      } else {
        throw new NotImplementedException();
      }
      return this;
    }
    protected override Compiler Visit(Call node) {
      if(node.Arguments.Length == 0) {
        cur.code.AppendFormat("\tLD\t0\t\t;for return\n");
      }else{
        for(int i = node.Arguments.Length - 1; i >= 0; i--) {
          node.Arguments[i].Visit(this);
        }
      }

      GetVariable f = node.FirstOperand as GetVariable;
      if(f != null) {
        var m = _memory.First(z => z.vd.Name == f.Name);
        cur.code.AppendFormat("\tCALL\t{0}\t\t;{1}@{2}\n", m.vd.Name, m.type.ToString(), m.Addr);
      } else {
        node.FirstOperand.Visit(this);
        cur.code.AppendFormat("\tCALLA\n");
        _sp--;
      }
      for(int i = node.Arguments.Length - 1; i > 0; i--) {
        cur.code.AppendFormat("\tDROP\t\t\t;P{0}\n", i);
        _sp--;
      }
      return this;
    }
    protected override Compiler Visit(ClassDefinition node) {
      return Visit(node as Expression);
    }
    protected override Compiler Visit(Constant node) {
      cur.code.AppendFormat("\tLD\t{0}\n", node.Value);
      _sp++;
      return this;
    }
    protected override Compiler Visit(Decrement node) {
      return Visit(node as Expression);
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
      return Visit(node as Expression);
    }
    protected override Compiler Visit(Expression node) {
      return Visit(node as CodeNode);
    }
    protected override Compiler Visit(FunctionDefinition node) {
      ScopePush("Function "+node.Name);
      for(int i = 0; i < node.Parameters.Count; i++) {
         var m = new Merker() { Addr = (uint)i, type = VM_DType.PARAMETER, vd = node.Parameters[i] };
         cur.memory.Add(m);
         cur.code.AppendFormat("\tDEF_L\t{0}\t\t;{1}@{2}\n", m.vd.Name, m.type.ToString(), m.Addr);
      }
      node.Body.Visit(this);
      cur.code.AppendFormat("\tRET\n");
      ScopePop();
      return this;
    }
    protected override Compiler Visit(Property node) {
      return Visit(node as Expression);
    }
    protected override Compiler Visit(GetVariable node) {
      Merker m;
      string name;
      m = cur.memory.FirstOrDefault(z => z.vd.Name == node.Name);
      if(m == null) {
        m = _memory.First(z => z.vd.Name == node.Name);
      }
      if(m.type == VM_DType.PARAMETER) {
        name = "P" + m.Addr.ToString();
      } else if(m.type == VM_DType.LOCAL) {
        name = "L" + m.Addr.ToString();
      } else {
        name = m.vd.Name;
      }
      cur.code.AppendFormat("\tLD\t{0}\n", name);
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
          cur.code.Append("\tINC\n");
          cur.code.Append("\tDUP\n");
        } else {
          cur.code.Append("\tDUP\n");
          cur.code.Append("\tINC\n");
        }
        var m = _memory.First(z => z.vd.Name == a.Name);
        cur.code.AppendFormat("\tST\t{0}\n", m.vd.Name);
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
      return Visit(node as Expression);
    }
    protected override Compiler Visit(LessOrEqual node) {
      return Visit(node as Expression);
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
      return Visit(node as Expression);
    }
    protected override Compiler Visit(MoreOrEqual node) {
      return Visit(node as Expression);
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
      cur.code.Append("\tNOT\n");
      _sp--;
      return this;
    }
    protected override Compiler Visit(NotEqual node) {
      return Visit(node as Expression);
    }
    protected override Compiler Visit(NumberAddition node) {
      var c1 = node.FirstOperand as Constant;
      var c2 = node.SecondOperand as Constant;
      if(c1 != null && c2 != null) {
        cur.code.AppendFormat("\tLD\t{0}\n", (int)(c1.Value.Value) + (int)(c2.Value.Value));
      }else if(c1!=null && (int)(c1.Value.Value)==1){
        node.SecondOperand.Visit(this);
        cur.code.Append("\tINC\n");
      } else if(c2 != null && (int)(c2.Value.Value) == 1) {
        node.FirstOperand.Visit(this);
        cur.code.Append("\tINC\n");
      } else {
        node.FirstOperand.Visit(this);
        node.SecondOperand.Visit(this);
        cur.code.Append("\tADD\n");
        _sp--;
      }
      return this;
    }
    protected override Compiler Visit(NumberLess node) {
      return Visit(node as Expression);
    }
    protected override Compiler Visit(NumberLessOrEqual node) {
      return Visit(node as Expression);
    }
    protected override Compiler Visit(NumberMore node) {
      return Visit(node as Expression);
    }
    protected override Compiler Visit(NumberMoreOrEqual node) {
      return Visit(node as Expression);
    }
    protected override Compiler Visit(BitwiseDisjunction node) {
      node.FirstOperand.Visit(this);
      node.SecondOperand.Visit(this);
      cur.code.Append("\tOR\n");
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
      return Visit(node as Expression);
    }
    protected override Compiler Visit(StrictNotEqual node) {
      return Visit(node as Expression);
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
        for(var i = 0; i < node.Variables.Length; i++) {
          var v = node.Variables[i];
          Merker m = null;
          if(v.Initializer != null && v.Initializer is FunctionDefinition) {
            m = new Merker() { Addr = 0, type = VM_DType.FUNCTION, vd = v };
          } else if(v.Name[0] == 'M') {
            if(v.Name[1] == 'd') {
              m = new Merker() { Addr = UInt32.Parse(v.Name.Substring(2)), type = VM_DType.SINT32, vd = v };
            }
          }
          if(m == null) {
            m = new Merker() { Addr = 0, type = VM_DType.SINT32, vd = v };
          }
          _memory.Add(m);

          cur.code.AppendFormat("\tDEF_G\t{0}\t\t;{1}@{2}\n", m.vd.Name, m.type.ToString(), m.Addr);
          if(node.Variables[i].Initializer != null) {
            node.Variables[i].Initializer.Visit(this);
          } 
        }
      int sp = _sp;
      for(var i = 0; i < node.Body.Length; i++) {
        node.Body[i].Visit(this);
        while(_sp > sp) {
          cur.code.AppendFormat("\tDROP\n");
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
    protected override Compiler Visit(For node){
      cur.code.Append("for(");
      if(node.Initializator != null) {
        node.Initializator.Visit(this);
      }
      cur.code.Append(";");
      if(node.Condition != null) {
        node.Condition.Visit(this);
      }
      cur.code.Append(";");
      if(node.Post != null) {
        node.Post.Visit(this);
      }
      if(node.Body != null) {
        cur.code.Append("){\n");
        node.Body.Visit(this);
        cur.code.Append("}\n");
      } else {
        cur.code.Append(");");
      }
      return this;
    }
    protected override Compiler Visit(IfElse node) {
      return Visit(node as CodeNode);
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
        cur.code.AppendFormat("\tST P0\n");
        _sp--;
      }
      cur.code.AppendFormat("\tRET\n");
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
