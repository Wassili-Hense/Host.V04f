using NiL.JS;
using NiL.JS.Core;
using NiL.JS.Expressions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace X13 {
  internal partial class Compiler : Visitor<Compiler> {
    private Stack<Inst> _sp;
    private List<Merker> _memory;
    private List<Scope> _programm;
    private Stack<Scope> _scope;
    private Scope cur;
    private SortedList<string, VM_DType> _predefs;

    public Compiler() {
      _predefs = new SortedList<string, VM_DType>();
      _predefs["Op"] = VM_DType.OUTPUT;
    }
    private void ScopePush(string name) {
      cur = new Scope(name);
      _scope.Push(cur);
      _programm.Add(cur);
    }
    private void ScopePop() {
      _scope.Pop();
      cur = _scope.Peek();
    }
    private void LoadConstant(CodeNode node, int v) {
      InstCode c;
      if(v == 0) {
        c = InstCode.LDI_0;
      } else if(v > 0) {
        if(v < 256) {
          if(v == 1) {
            c = InstCode.LDI_1;
          } else {
            c = InstCode.LDI_U1;
          }
        } else {
          if(v < 65536) {
            c = InstCode.LDI_U2;
          } else {
            c = InstCode.LDI_S4;
          }
        }
      } else {
        if(v > -128) {
          if(v == -1) {
            c = InstCode.LDI_M1;
          } else {
            c = InstCode.LDI_S1;
          }
        } else {
          if(v > -32768) {
            c = InstCode.LDI_S2;
          } else {
            c = InstCode.LDI_S4;
          }
        }
      }
      var d = new Inst(c, null, node) { canOptimized = true };
      cur.code.Add(d);
      _sp.Push(d);
    }
    private void Store(CodeNode node, Expression e) {
      GetVariable a = e as GetVariable;
      if(a == null) {
        AssignmentOperatorCache a2 = e as AssignmentOperatorCache;
        if(a2 != null) {
          a = a2.Source as GetVariable;
        }
      }
      if(a != null) {
        var m = GetMerker(a.Descriptor);
        switch(m.type) {
        case VM_DType.BOOL:
          cur.code.Add(new Inst(InstCode.STM_B1_C16, m, node));
          break;
        case VM_DType.UINT8:
        case VM_DType.SINT8:
          cur.code.Add(new Inst(InstCode.STM_S1_C16, m, node));
          break;
        case VM_DType.UINT16:
        case VM_DType.SINT16:
          cur.code.Add(new Inst(InstCode.STM_S2_C16, m, node));
          break;
        case VM_DType.SINT32:
          cur.code.Add(new Inst(InstCode.STM_S4_C16, m, node));
          break;
        case VM_DType.PARAMETER:
          cur.code.Add(new Inst((InstCode)(InstCode.ST_P0 + (byte)m.Addr), null, node));
          break;
        case VM_DType.LOCAL:
          cur.code.Add(new Inst((InstCode)(InstCode.ST_L0 + (byte)m.Addr), null, node));
          break;
        case VM_DType.OUTPUT:
          cur.code.Add(new Inst(InstCode.OUT, m, node));
          break;
        default:
          throw new NotImplementedException(node.ToString());
        }
        _sp.Pop();
      } else {
        throw new NotImplementedException(node.ToString());
      }
    }
    private void AddCommon(CodeNode node, Expression a, Expression b) {
      var c1 = a as Constant;
      var c2 = b as Constant;
      Inst d = null;
      if(c1 != null && c2 != null) {
        LoadConstant(node, (int)(c1.Value.Value) + (int)(c2.Value.Value));
      } else if(c1 != null && (int)(c1.Value.Value) == 1) {
        b.Visit(this);
        _sp.Pop();
        d = new Inst(InstCode.INC);
      } else if(c1 != null && (int)(c1.Value.Value) == -1) {
        b.Visit(this);
        _sp.Pop();
        d = new Inst(InstCode.DEC);
      } else if(c2 != null && (int)(c2.Value.Value) == 1) {
        a.Visit(this);
        cur.code.Add(new Inst(InstCode.INC));
      } else if(c2 != null && (int)(c2.Value.Value) == -1) {
        a.Visit(this);
        _sp.Pop();
        d = new Inst(InstCode.DEC, null, node);
      } else {
        b.Visit(this);
        a.Visit(this);
        _sp.Pop();
        _sp.Pop();
        d = new Inst(InstCode.ADD);
      }
      if(d != null) {
        cur.code.Add(d);
        _sp.Push(d);
      }
    }
    private void Arg2Op(Expression node, InstCode c) {
      Inst d;
      node.SecondOperand.Visit(this);
      node.FirstOperand.Visit(this);
      _sp.Pop();
      _sp.Pop();
      d = new Inst(c);
      cur.code.Add(d);
      _sp.Push(d);
    }
    public void Parse(string code) {
      _memory = new List<Merker>();
      _scope = new Stack<Scope>();
      _programm = new List<Scope>();
      _sp = new Stack<Inst>();
      ScopePush("");

      var module = new Module(code, null, Options.SuppressConstantPropogation);
      module.Root.Visit(this);
      cur = _programm[0];
      if(cur.code.Count == 0 || cur.code[cur.code.Count - 1]._code.Length != 1 || cur.code[cur.code.Count - 1]._code[0] != (byte)InstCode.RET) {
        cur.code.Add(new Inst(InstCode.RET));
      }

      uint addr = 0;
      foreach(var p in _programm) {
        if(p.entryPoint != null) {
          p.entryPoint.Addr = addr;
        }
        foreach(var c in p.code) {
          c.addr = addr;
          addr += (uint)c._code.Length;
        }
      }
      foreach(var p in _programm) {
        foreach(var c in p.code) {
          c.Link();
        }
        Log.Info("{0}", p.ToString());
      }
    }

    private Merker GetMerker(VariableDescriptor v) {
      Merker m = null;
      m = cur.memory.FirstOrDefault(z => z.vd.Name == v.Name);
      if(m == null) {
        m = _memory.FirstOrDefault(z => z.vd.Name == v.Name);
      }
      return m;
    }
  }
  internal class Merker {
    public uint Addr;
    public VM_DType type;
    public VariableDescriptor vd;
    public Expression init;
    public Scope scope;
    public bool initialized;
  }
  internal class Scope {
    public Scope(string name) {
      this.name = name;
      code = new List<Inst>();
      memory = new List<Merker>();
      loops = new Stack<DPC_Loop>();
    }
    public string name;
    public List<Inst> code;
    public List<Merker> memory;
    public Merker entryPoint;
    public Stack<DPC_Loop> loops;

    public override string ToString() {
      var sb = new StringBuilder();
      int ls = 0;
      sb.Append(this.name).Append("\n");
      byte[] hex;
      int j;
      for(int i = 0; i < code.Count; i++) {
        var c = code[i];
        sb.Append(c.addr.ToString("X4"));
        sb.Append(" ");
        hex = c._code;
        for(j = 0; j < 5; j++) {
          if(j < hex.Length) {
            sb.Append(hex[j].ToString("X2"));
            sb.Append(" ");
          } else {
            sb.Append("   ");
          }
        }
        sb.Append("| ").Append(c.ToString());
        if(c._cn != null) {
          while((sb.Length-ls) < 46) {
            sb.Append(" ");
          }
          sb.Append("; ").Append(c._cn.ToString());
        }
        sb.Append("\r\n");
        ls = sb.Length;
      }
      return sb.ToString();
    }
  }
  internal class DPC_Loop {
    public DPC_Loop(int sp1, ICollection<string> labels) {
      this.sp1 = sp1;
      this.labels=labels;
      L1=new Inst(InstCode.LABEL);
      L2=new Inst(InstCode.LABEL);
      L3=new Inst(InstCode.LABEL);
    }
    public Inst L1, L2, L3;
    public int sp1, sp2;
    public ICollection<string> labels;
  }
  internal class Inst {
    internal uint addr;
    internal byte[] _code;
    internal Merker _param;
    internal CodeNode _cn;
    internal Inst _ref;

    public bool canOptimized;

    public Inst(InstCode cmd, Merker param = null, CodeNode cn = null) {
      _param = param;
      _cn = cn;
      Prepare(cmd);
    }
    public bool Link() {
      if(_code.Length <= 1 || (_param == null && _ref == null)) {
        return true;
      }
      Prepare((InstCode)_code[0]);
      return false;
    }
    private void Prepare(InstCode cmd) {
      int tmp_d;
      uint tmp_D;
      switch(cmd) {
      case InstCode.LABEL:
        if(_code == null || _code.Length != 0) {
          _code = new byte[0];
        }
        break;
      case InstCode.NOP:
      case InstCode.DUP:
      case InstCode.DROP:
      case InstCode.NIP:
      case InstCode.SWAP:
      case InstCode.OVER:
      case InstCode.ROT:
      case InstCode.NOT:
      case InstCode.AND:
      case InstCode.OR:
      case InstCode.XOR:
      case InstCode.ADD:
      case InstCode.SUB:
      case InstCode.MUL:
      case InstCode.DIV:
      case InstCode.MOD:
      case InstCode.INC:
      case InstCode.DEC:
      case InstCode.NEG:
      case InstCode.CEQ:
      case InstCode.CNE:
      case InstCode.CGT:
      case InstCode.CGE:
      case InstCode.CLT:
      case InstCode.CLE:
      case InstCode.NOT_L:
      case InstCode.AND_L:
      case InstCode.OR_L:
      case InstCode.XOR_L:
      case InstCode.LDI_0:
      case InstCode.LDI_1:
      case InstCode.LDI_M1:
      case InstCode.LD_P0:
      case InstCode.LD_P1:
      case InstCode.LD_P2:
      case InstCode.LD_P3:
      case InstCode.LD_P4:
      case InstCode.LD_P5:
      case InstCode.LD_P6:
      case InstCode.LD_P7:
      case InstCode.LD_P8:
      case InstCode.LD_P9:
      case InstCode.LD_PA:
      case InstCode.LD_PB:
      case InstCode.LD_PC:
      case InstCode.LD_PD:
      case InstCode.LD_PE:
      case InstCode.LD_PF:
      case InstCode.LD_L0:
      case InstCode.LD_L1:
      case InstCode.LD_L2:
      case InstCode.LD_L3:
      case InstCode.LD_L4:
      case InstCode.LD_L5:
      case InstCode.LD_L6:
      case InstCode.LD_L7:
      case InstCode.LD_L8:
      case InstCode.LD_L9:
      case InstCode.LD_LA:
      case InstCode.LD_LB:
      case InstCode.LD_LC:
      case InstCode.LD_LD:
      case InstCode.LD_LE:
      case InstCode.LD_LF:
      case InstCode.ST_P0:
      case InstCode.ST_P1:
      case InstCode.ST_P2:
      case InstCode.ST_P3:
      case InstCode.ST_P4:
      case InstCode.ST_P5:
      case InstCode.ST_P6:
      case InstCode.ST_P7:
      case InstCode.ST_P8:
      case InstCode.ST_P9:
      case InstCode.ST_PA:
      case InstCode.ST_PB:
      case InstCode.ST_PC:
      case InstCode.ST_PD:
      case InstCode.ST_PE:
      case InstCode.ST_PF:
      case InstCode.ST_L0:
      case InstCode.ST_L1:
      case InstCode.ST_L2:
      case InstCode.ST_L3:
      case InstCode.ST_L4:
      case InstCode.ST_L5:
      case InstCode.ST_L6:
      case InstCode.ST_L7:
      case InstCode.ST_L8:
      case InstCode.ST_L9:
      case InstCode.ST_LA:
      case InstCode.ST_LB:
      case InstCode.ST_LC:
      case InstCode.ST_LD:
      case InstCode.ST_LE:
      case InstCode.ST_LF:
      case InstCode.LDM_B1_S:
      case InstCode.LDM_S1_S:
      case InstCode.LDM_S2_S:
      case InstCode.LDM_S4_S:
      case InstCode.LDM_U1_S:
      case InstCode.LDM_U2_S:
      case InstCode.STM_B1_S:
      case InstCode.STM_S1_S:
      case InstCode.STM_S2_S:
      case InstCode.STM_S4_S:
      case InstCode.SJMP:
      case InstCode.SCALL:
      case InstCode.RET:
        if(_code == null || _code.Length != 1) {
          _code = new byte[1];
        }
        _code[0] = (byte)cmd;
        break;
      case InstCode.STM_B1_C16:
      case InstCode.STM_S1_C16:
      case InstCode.STM_S2_C16:
      case InstCode.STM_S4_C16:
      case InstCode.LDM_B1_C16:
      case InstCode.LDM_S1_C16:
      case InstCode.LDM_S2_C16:
      case InstCode.LDM_S4_C16:
      case InstCode.LDM_U1_C16:
      case InstCode.LDM_U2_C16:
      case InstCode.CALL:
        if(_code == null || _code.Length != 3) {
          _code = new byte[3];
        }
        _code[0] = (byte)cmd;
        _code[1] = (byte)_param.Addr;
        _code[2] = (byte)(_param.Addr >> 8);
        break;
      case InstCode.LDI_S1:
      case InstCode.LDI_U1:
        if(_code == null || _code.Length != 2) {
          _code = new byte[2];
        }
        _code[0] = (byte)cmd;
        _code[1] = (byte)((Constant)_cn).Value;
        break;
      case InstCode.LDI_S2:
      case InstCode.LDI_U2:
        tmp_d = (int)((Constant)_cn).Value;
        if(_code == null || _code.Length != 3) {
          _code = new byte[3];
        }
        _code[0] = (byte)cmd;
        _code[1] = (byte)tmp_d;
        _code[2] = (byte)(tmp_d >> 8);
        break;
      case InstCode.LDI_S4:
        tmp_d = (int)((Constant)_cn).Value;
        if(_code == null || _code.Length != 5) {
          _code = new byte[5];
        }
        _code[0] = (byte)cmd;
        _code[1] = (byte)tmp_d;
        _code[2] = (byte)(tmp_d >> 8);
        _code[3] = (byte)(tmp_d >> 16);
        _code[4] = (byte)(tmp_d >> 24);
        break;
      case InstCode.OUT:
      case InstCode.IN:
        if(_code == null || _code.Length != 5) {
          _code = new byte[5];
        }
        _code[0] = (byte)cmd;
        _code[1] = (byte)_param.Addr;
        _code[2] = (byte)(_param.Addr >> 8);
        _code[3] = (byte)(_param.Addr >> 16);
        _code[4] = (byte)(_param.Addr >> 24);
        break;

      case InstCode.JZ:
      case InstCode.JNZ:
      case InstCode.JMP:
        tmp_D = _ref == null ? uint.MaxValue : _ref.addr;
        if(_code == null || _code.Length != 3) {
          _code = new byte[3];
        }
        _code[0] = (byte)cmd;
        _code[1] = (byte)tmp_D;
        _code[2] = (byte)(tmp_D >> 8);
        break;
      default:
        throw new NotImplementedException(this.ToString());
      }
    }
    public override string ToString() {
      if(_code.Length > 0) {
        StringBuilder sb = new StringBuilder();
        sb.Append(((InstCode)_code[0]).ToString());
        if(_code.Length > 1) {
          while(sb.Length < 12) {
            sb.Append(" ");
          }
          sb.Append("0x");
          for(int i = _code.Length - 1; i > 0; i--) {
            sb.Append(_code[i].ToString("X2"));
          }
        }
        return sb.ToString();
      } else {
        return null;
      }
    }
  }
}
