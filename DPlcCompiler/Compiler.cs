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
    private void Store(CodeNode node, GetVariable a) {
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

      var module = new Module(code);
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
  }
  internal class Scope {
    public Scope(string name) {
      this.name = name;
      code = new List<Inst>();
      memory = new List<Merker>();
    }
    public string name;
    public List<Inst> code;
    public List<Merker> memory;
    public Merker entryPoint;

    public override string ToString() {
      var sb = new StringBuilder();
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
        sb.Append("\t| ").Append(c.ToString());
        if(c._cn != null) {
          sb.Append("  \t\t; ").Append(c._cn.ToString());
        }
        sb.Append("\r\n");
      }
      return sb.ToString();
    }
  }
  internal enum InstCode : byte {
    NOP = 0x00,

    DUP = 0x02,
    DROP,
    NIP,
    SWAP,
    OVER,
    ROT,

    NOT = 0x08,
    AND,
    OR,
    XOR,
    LSL,
    LSR,
    ASR,

    ADD = 0x10,
    SUB,
    MUL,
    DIV,
    MOD,
    INC,
    DEC,
    NEG,

    CEQ = 0x20,
    CNE,
    CGT,
    CGE,
    CLT,
    CLE,

    NOT_L = 0x28,
    AND_L,
    OR_L,
    XOR_L,

    LDI_0 = 0x38,
    LDI_S1,
    LDI_S2,
    LDI_S4,
    LDI_U1,
    LDI_U2,
    LDI_1,
    LDI_M1,


    LD_P0 = 0x40,
    LD_P1,
    LD_P2,
    LD_P3,
    LD_P4,
    LD_P5,
    LD_P6,
    LD_P7,
    LD_P8,
    LD_P9,
    LD_PA,
    LD_PB,
    LD_PC,
    LD_PD,
    LD_PE,
    LD_PF,

    LD_L0 = 0x50,
    LD_L1,
    LD_L2,
    LD_L3,
    LD_L4,
    LD_L5,
    LD_L6,
    LD_L7,
    LD_L8,
    LD_L9,
    LD_LA,
    LD_LB,
    LD_LC,
    LD_LD,
    LD_LE,
    LD_LF,

    ST_P0 = 0x60,
    ST_P1,
    ST_P2,
    ST_P3,
    ST_P4,
    ST_P5,
    ST_P6,
    ST_P7,
    ST_P8,
    ST_P9,
    ST_PA,
    ST_PB,
    ST_PC,
    ST_PD,
    ST_PE,
    ST_PF,

    ST_L0 = 0x70,
    ST_L1,
    ST_L2,
    ST_L3,
    ST_L4,
    ST_L5,
    ST_L6,
    ST_L7,
    ST_L8,
    ST_L9,
    ST_LA,
    ST_LB,
    ST_LC,
    ST_LD,
    ST_LE,
    ST_LF,

    LDM_B1_S = 0x80,
    LDM_S1_S,
    LDM_S2_S,
    LDM_S4_S,
    LDM_U1_S,
    LDM_U2_S,

    LDM_B1_CS8 = 0x88,
    LDM_S1_CS8,
    LDM_S2_CS8,
    LDM_S4_CS8,
    LDM_U1_CS8,
    LDM_U2_CS8,

    LDM_B1_CS16 = 0x90,
    LDM_S1_CS16,
    LDM_S2_CS16,
    LDM_S4_CS16,
    LDM_U1_CS16,
    LDM_U2_CS16,

    LDM_B1_C16 = 0x98,
    LDM_S1_C16,
    LDM_S2_C16,
    LDM_S4_C16,
    LDM_U1_C16,
    LDM_U2_C16,

    STM_B1_S = 0xA0,
    STM_S1_S,
    STM_S2_S,
    STM_S4_S,

    STM_B1_CS8 = 0xA8,
    STM_S1_CS8,
    STM_S2_CS8,
    STM_S4_CS8,

    STM_B1_CS16 = 0xB0,
    STM_S1_CS16,
    STM_S2_CS16,
    STM_S4_CS16,

    STM_B1_C16 = 0xB8,
    STM_S1_C16,
    STM_S2_C16,
    STM_S4_C16,

    IN = 0xC0,
    OUT = 0xC1,

    SJMP = 0xF0,
    JZ = 0xF1,
    JNZ = 0xF2,
    JMP = 0xF3,
    SCALL = 0xF4,
    CALL = 0xF7,

    LABEL = 0xF8,

    TEST_EQ = 0xFE,
    RET = 0xFF,
  }

  internal class Inst {
    internal uint addr;
    internal byte[] _code;
    internal Merker _param;
    internal CodeNode _cn;
    internal Inst _ref;

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
      if(_code.Length == 1) {
        return ((InstCode)_code[0]).ToString();
      } else if(_code.Length == 2) {
        return ((InstCode)_code[0]).ToString() + "  \t0x" + _code[1].ToString("X2");
      } else if(_code.Length == 3) {
        return ((InstCode)_code[0]).ToString() + "  \t0x" + _code[2].ToString("X2") + _code[1].ToString("X2");
      } else if(_code.Length == 5) {
        return ((InstCode)_code[0]).ToString() + "  \t0x" + _code[4].ToString("X2") + _code[3].ToString("X2") + _code[2].ToString("X2") + _code[1].ToString("X2");
      } else {
        return null;
      }
    }
    public bool canOptimized;
  }
  internal enum VM_DType {
    NONE,
    BOOL,
    UINT8,
    SINT8,
    UINT16,
    SINT16,
    SINT32,
    FUNCTION,
    PARAMETER,
    LOCAL,
    INPUT,
    OUTPUT,
  }
}
