using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace X13 {
  internal class Assembler {
    private static char[] WHITE_SPACES = new char[] { ' ', '\t' };

    static void Main(string[] args) {
      if(args.Length > 0 && File.Exists(args[0]) && Path.GetExtension(args[0]).ToLower() == ".asm") {
        var asm = new Assembler();
        byte[] prg = asm.Load(args[0]);
        if(prg == null) {
          Log.Warning("programm is empty");
        } else {
          var fo = Path.ChangeExtension(args[0], ".hex");
          var text = BitConverter.ToString(prg);
          File.WriteAllText(fo, text);
          fo = Path.ChangeExtension(args[0], ".lst");
          text = asm.ToLst();
          File.WriteAllText(fo, text);
          Log.Info("ok - {0} bytes", prg.Length);
        }
      } else {
        Log.Warning("USE: Assembler <sourcer file>.asm");
      }
      Log.Finish();
    }


    Dictionary<string, Inst> _labels;
    List<Inst> _prg;


    public byte[] Load(string path) {
      _prg = new List<Inst>();
      _labels = new Dictionary<string, Inst>();

      using(var f = File.OpenText(path)) {
        int line = 0;
        string str;
        string[] sa;
        int idx;
        Inst inst;
        while(!f.EndOfStream) {
          str = f.ReadLine();
          line++;
          if(string.IsNullOrWhiteSpace(str)) {
            continue;
          }
          sa = str.Split(WHITE_SPACES, StringSplitOptions.RemoveEmptyEntries);
          if(sa == null || sa.Length == 0) {
            continue;
          }
          idx = 0;
          inst = new Inst(this, line);
          _prg.Add(inst);
          if(sa[0].StartsWith(":")) { // label
            inst.label = sa[0].Substring(1);
            _labels[inst.label] = inst;
            idx++;
          }
          if(idx < sa.Length) {
            if(!Enum.TryParse(sa[idx].ToUpper(), out inst.op)) {
              Log.Error("[{0}]{1} - unknown OpName", line, str);
              continue;
            }
            if(++idx < sa.Length) {
              inst.args = string.Join(" ", sa, idx, sa.Length - idx);
            }
          }
        }
      }

      int pc = 0;
      for(int i = 0; i < _prg.Count; i++) {
        _prg[i].pos = pc;
        pc += _prg[i].GetBytes(false).Length;
      }
      List<byte> code = new List<byte>(pc);
      for(int i = 0; i < _prg.Count; i++) {
        code.AddRange(_prg[i].GetBytes(true));
      }
      return code.ToArray();
    }
    public string ToLst() {
      StringBuilder sb = new StringBuilder();
      int j = 0;
      byte[] hex;
      foreach(var i in _prg) {
        if(!string.IsNullOrEmpty(i.label)) {
          sb.Append(":");
          sb.Append(i.label);
          sb.Append("\r\n");
        }
        sb.Append(i.pos.ToString("X4"));
        sb.Append(" ");
        hex = i.GetBytes(true);
        for(j = 0; j < 8; j++) {
          if(j < hex.Length) {
            sb.Append(hex[j].ToString("X2"));
            sb.Append(" ");
          } else {
            sb.Append("   ");
          }
        }
        sb.AppendFormat(" | {0:0000}\t\t{1}\t\t{2}\r\n", i.line, i.op, i.args);
        for(; j < hex.Length; j++) {
          if((j & 7) == 0) {
            sb.Append((i.pos + j).ToString("X4"));
            sb.Append(" ");
          }
          sb.Append(hex[j].ToString("X2"));
          if((j & 7) == 7 || j == hex.Length - 1) {
            sb.Append("\r\n");
          } else {
            sb.Append(" ");
          }
        }
      }
      return sb.ToString();
    }
    private class Inst {
      private byte[] cmd;
      private Assembler _own;

      public int line;
      public int pos;
      public string label;
      public OpName op;
      public string args;

      public Inst(Assembler owner, int line) {
        _own = owner;
        this.line = line;
        this.pos = -1;
      }

      public override string ToString() {
        return string.Format("[{0}]{1}  {2}  {3}", line, string.IsNullOrEmpty(label) ? "" : ":" + label, op, args);
      }

      public byte[] GetBytes(bool final) {
        byte[] rCmd = cmd;
        bool save = true;
        if(rCmd == null) {
          int tmp_d;
          byte tmp_z;
          ushort tmp_W;

          switch(op) {
          case OpName.LD:
            if(Int32.TryParse(args, out tmp_d)) {
              if(tmp_d == 0) {
                rCmd = new byte[] { (byte)OP.LDI_ZERO };
              } else if(tmp_d == 1) {
                rCmd = new byte[] { (byte)OP.LDI_TRUE };
              } else if(tmp_d == -1) {
                rCmd = new byte[] { (byte)OP.LDI_MINUS1 };
              } else if(tmp_d > -128 && tmp_d < 256) {
                rCmd = new byte[] { tmp_d > 0 ? (byte)OP.LDI_U1 : (byte)OP.LDI_S1, (byte)tmp_d };
              } else if(tmp_d > -32768 && tmp_d < 65536) {
                rCmd = new byte[] { tmp_d > 0 ? (byte)OP.LDI_U2 : (byte)OP.LDI_S2, (byte)tmp_d, (byte)(tmp_d >> 8) };
              } else if(tmp_d == int.MinValue) {
                rCmd = new byte[] { (byte)OP.LDI_MIN };
              } else {
                rCmd = new byte[] { (byte)OP.LDI_S4, (byte)tmp_d, (byte)(tmp_d >> 8), (byte)(tmp_d >> 16), (byte)(tmp_d >> 24) };
              }
            } else if(args.Length > 1 && "LlPpzBbWwd".Contains(args[0]) && Int32.TryParse(args.Substring(1), System.Globalization.NumberStyles.Integer, CultureInfo.InvariantCulture, out tmp_d)) {
              switch(args[0]) {
              case 'L':
              case 'l':
                rCmd = new byte[] { (byte)((int)OP.LD_L0 + (tmp_d & 0x0F)) };
                break;
              case 'P':
              case 'p':
                rCmd = new byte[] { (byte)((int)OP.LD_P0 - (tmp_d & 0x0F)) };
                break;
              case 'z':
                rCmd = new byte[] { (byte)OP.LDM_B1_C16, (byte)tmp_d, (byte)(tmp_d >> 8) };
                break;
              case 'B':
                rCmd = new byte[] { (byte)OP.LDM_U1_C16, (byte)tmp_d, (byte)(tmp_d >> 8) };
                break;
              case 'b':
                rCmd = new byte[] { (byte)OP.LDM_S1_C16, (byte)tmp_d, (byte)(tmp_d >> 8) };
                break;
              case 'W':
                rCmd = new byte[] { (byte)OP.LDM_U2_C16, (byte)tmp_d, (byte)(tmp_d >> 8) };
                break;
              case 'w':
                rCmd = new byte[] { (byte)OP.LDM_S2_C16, (byte)tmp_d, (byte)(tmp_d >> 8) };
                break;
              case 'd':
                rCmd = new byte[] { (byte)OP.LDM_S4_C16, (byte)tmp_d, (byte)(tmp_d >> 8) };
                break;
              }
            } else if(args.Length > 1 && args[0] == 'A') {
              Inst l;
              if(_own._labels.TryGetValue(args, out l) && l.pos != -1) {
                tmp_d = l.pos;
              } else {
                tmp_d = -1;
                save = false;
                if(final) {
                  Log.Error("unknown label " + this.ToString());
                }
              }
              rCmd = new byte[] { (byte)OP.LDI_U2, (byte)tmp_d, (byte)(tmp_d >> 8) };
            } else if(final) {
              Log.Error("unknown argument " + this.ToString());
            }
            break;
          case OpName.ST:
            if(args.Length > 1 && "LlPpzBbWwd".Contains(args[0]) && Int32.TryParse(args.Substring(1), System.Globalization.NumberStyles.Integer, CultureInfo.InvariantCulture, out tmp_d)) {
              switch(args[0]) {
              case 'L':
              case 'l':
                rCmd = new byte[] { (byte)((int)OP.ST_L0 + (tmp_d & 0x0F)) };
                break;
              case 'P':
              case 'p':
                rCmd = new byte[] { (byte)((int)OP.ST_P0 - (tmp_d & 0x0F)) };
                break;
              case 'z':
                rCmd = new byte[] { (byte)OP.STM_B1_C16, (byte)tmp_d, (byte)(tmp_d >> 8) };
                break;
              case 'B':
              case 'b':
                rCmd = new byte[] { (byte)OP.STM_S1_C16, (byte)tmp_d, (byte)(tmp_d >> 8) };
                break;
              case 'W':
              case 'w':
                rCmd = new byte[] { (byte)OP.STM_S2_C16, (byte)tmp_d, (byte)(tmp_d >> 8) };
                break;
              case 'd':
                rCmd = new byte[] { (byte)OP.STM_S4_C16, (byte)tmp_d, (byte)(tmp_d >> 8) };
                break;
              }
            } else if(final) {
              Log.Error("unknown argument " + this.ToString());
            }
            break;
          case OpName.ADD:
            rCmd = new byte[] { (byte)OP.ADD };
            break;
          case OpName.SUB:
            rCmd = new byte[] { (byte)OP.SUB };
            break;
          case OpName.MUL:
            rCmd = new byte[] { (byte)OP.MUL };
            break;
          case OpName.DIV:
            rCmd = new byte[] { (byte)OP.DIV };
            break;
          case OpName.MOD:
            rCmd = new byte[] { (byte)OP.MOD };
            break;
          case OpName.INC:
            rCmd = new byte[] { (byte)OP.INC };
            break;
          case OpName.DEC:
            rCmd = new byte[] { (byte)OP.DEC };
            break;
          case OpName.NEG:
            rCmd = new byte[] { (byte)OP.NEG };
            break;
          case OpName.NOT:
            rCmd = new byte[] { (byte)OP.NOT };
            break;
          case OpName.AND:
            rCmd = new byte[] { (byte)OP.AND };
            break;
          case OpName.OR:
            rCmd = new byte[] { (byte)OP.OR };
            break;
          case OpName.XOR:
            rCmd = new byte[] { (byte)OP.XOR };
            break;
          case OpName.LSL: {
              if(byte.TryParse(args, out tmp_z)) {
                rCmd = new byte[] { (byte)OP.LSL, tmp_z };
              } else {
                Log.Error("{0} bad argument" + this.ToString());
              }
            }
            break;
          case OpName.LSR: {
              if(byte.TryParse(args, out tmp_z)) {
                rCmd = new byte[] { (byte)OP.LSR, tmp_z };
              } else {
                Log.Error("{0} bad argument" + this.ToString());
              }
            }
            break;
          case OpName.ASR: {
              if(byte.TryParse(args, out tmp_z)) {
                rCmd = new byte[] { (byte)OP.ASR, tmp_z };
              } else {
                Log.Error("{0} bad argument" + this.ToString());
              }
            }
            break;
          case OpName.CGT:
            rCmd = new byte[] { (byte)OP.CGT };
            break;
          case OpName.CGE:
            rCmd = new byte[] { (byte)OP.CGE };
            break;
          case OpName.CEQ:
            rCmd = new byte[] { (byte)OP.CEQ };
            break;
          case OpName.CNE:
            rCmd = new byte[] { (byte)OP.CNE };
            break;
          case OpName.CLT:
            rCmd = new byte[] { (byte)OP.CLT };
            break;
          case OpName.CLE:
            rCmd = new byte[] { (byte)OP.CLE };
            break;
          case OpName.CZE:
            rCmd = new byte[] { (byte)OP.CZE };
            break;
          case OpName.DUP:
            rCmd = new byte[] { (byte)OP.DUP };
            break;
          case OpName.DROP:
            rCmd = new byte[] { (byte)OP.DROP };
            break;
          case OpName.NIP:
            rCmd = new byte[] { (byte)OP.NIP };
            break;
          case OpName.SWAP:
            rCmd = new byte[] { (byte)OP.SWAP };
            break;
          case OpName.ROT:
            rCmd = new byte[] { (byte)OP.ROT };
            break;
          case OpName.OVER:
            rCmd = new byte[] { (byte)OP.OVER };
            break;
          case OpName.IN:
          case OpName.OUT:
            if(args != null && args.Length > 2 && UInt16.TryParse(args.Substring(2), out tmp_W)) {
              rCmd = new byte[] { (byte)(op == OpName.OUT ? OP.OUT : OP.IN), (byte)args[0], (byte)args[1], (byte)tmp_W, (byte)(tmp_W >> 8) };
            }
            break;
          case OpName.JMP:
          case OpName.CALL:
          case OpName.JZ:
          case OpName.JNZ: {
              Inst l;
              if(_own._labels.TryGetValue(args, out l) && l.pos != -1) {
                tmp_d = l.pos;
              } else {
                tmp_d = -1;
                save = false;
                if(final) {
                  Log.Error("unknown label " + this.ToString());
                }
              }
              rCmd = new byte[] { 0xFF, (byte)tmp_d, (byte)(tmp_d >> 8) };
              switch(op) {
              case OpName.JMP:
                rCmd[0] = (byte)OP.JMP;
                break;
              case OpName.CALL:
                rCmd[0] = (byte)OP.CALL;
                break;
              case OpName.JZ:
                rCmd[0] = (byte)OP.JZ;
                break;
              case OpName.JNZ:
                rCmd[0] = (byte)OP.JNZ;
                break;
              }
            }
            break;
          case OpName.SJMP:
            rCmd = new byte[] { (byte)OP.SJMP };
            break;
          case OpName.SCALL:
            rCmd = new byte[] { (byte)OP.SCALL };
            break;
          case OpName.RET:
            rCmd = new byte[] { (byte)OP.RET };
            break;
          case OpName.LPM_S1:
          case OpName.LPM_S2:
          case OpName.LPM_S4:
          case OpName.LPM_U1:
          case OpName.LPM_U2: {
              Inst l;
              if(Int32.TryParse(args, out tmp_d)) {

              } else if(args.Length > 1 && args[0] == 'A' && _own._labels.TryGetValue(args, out l) && l.pos != -1) {
                tmp_d = l.pos;
              } else {
                tmp_d = -1;
                save = false;
                if(final) {
                  Log.Error("BAD argument " + this.ToString());
                }
              }
            }
            switch(op) {
            case OpName.LPM_S1:
              rCmd = new byte[] { (byte)OP.LPM_S1, (byte)tmp_d, (byte)(tmp_d >> 8) };
              break;
            case OpName.LPM_S2:
              rCmd = new byte[] { (byte)OP.LPM_S2, (byte)tmp_d, (byte)(tmp_d >> 8) };
              break;
            case OpName.LPM_S4:
              rCmd = new byte[] { (byte)OP.LPM_S4, (byte)tmp_d, (byte)(tmp_d >> 8) };
              break;
            case OpName.LPM_U1:
              rCmd = new byte[] { (byte)OP.LPM_U1, (byte)tmp_d, (byte)(tmp_d >> 8) };
              break;
            case OpName.LPM_U2:
              rCmd = new byte[] { (byte)OP.LPM_U2, (byte)tmp_d, (byte)(tmp_d >> 8) };
              break;
            }
            break;
          case OpName.DB:
            rCmd = args.Split(',', ' ').Where(z => !string.IsNullOrWhiteSpace(z)).Select(z => ParseByte(z)).ToArray();
            break;
          case OpName.DW:
            rCmd = args.Split(',', ' ').Where(z => !string.IsNullOrWhiteSpace(z)).SelectMany(z => ParseShort(z)).ToArray();
            break;
          case OpName.DD:
            rCmd = args.Split(',', ' ').Where(z => !string.IsNullOrWhiteSpace(z)).SelectMany(z => ParseInt(z)).ToArray();
            break;
          case OpName.TEST_EQ:
            if(Int32.TryParse(args, out tmp_d)) {
              rCmd = new byte[] { (byte)OP.TEST_EQ, (byte)tmp_d, (byte)(tmp_d >> 8), (byte)(tmp_d >> 16), (byte)(tmp_d >> 24) };
            }
            break;
          case OpName.NOP:
            rCmd = new byte[] { (byte)OP.NOP };
            break;
          default:
            if(final) {
              Log.Error("Unknown OP " + this.ToString());
            }
            break;
          }
        }
        if(rCmd != null) {
          if(save) {
            cmd = rCmd;
          }
          return rCmd;
        }
        return new byte[0];
      }
      private static byte ParseByte(string str) {
        byte b;
        if(str.StartsWith("0x") || str.StartsWith("0X")) {
          string s = str.Substring(2);
          byte.TryParse(s, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out b);
        } else {
          int i;
          if(int.TryParse(str, out i)) {
            b = (byte)i;
          } else {
            b = 0;
          }
        }
        return b;
      }
      private static byte[] ParseShort(string str) {
        int b;
        if(str.StartsWith("0x") || str.StartsWith("0X")) {
          string s = str.Substring(2);
          int.TryParse(s, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out b);
        } else {
          int.TryParse(str, out b);
        }
        return BitConverter.GetBytes((ushort)b);
      }
      private static byte[] ParseInt(string str) {
        int b;
        if(str.StartsWith("0x") || str.StartsWith("0X")) {
          string s = str.Substring(2);
          int.TryParse(s, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out b);
        } else {
          int.TryParse(str, out b);
        }
        return BitConverter.GetBytes(b);
      }
    }
    private enum OpName {
      NOP = 0,
      LD,
      ST,
      INC,
      DEC,
      ADD,
      SUB,
      MUL,
      DIV,
      MOD,
      NEG,
      NOT,
      AND,
      OR,
      XOR,
      LSL,
      LSR,
      ASR,
      CGT,
      CGE,
      CLT,
      CLE,
      CEQ,
      CNE,
      CZE,
      DUP,
      DROP,
      NIP,
      SWAP,
      OVER,
      ROT,

      IN,
      OUT,
      JMP,
      CALL,
      SJMP,
      SCALL,
      RET,
      JZ,
      JNZ,
      LPM_S1,
      LPM_S2,
      LPM_S4,
      LPM_U1,
      LPM_U2,

      DB,
      DW,
      DD,

      TEST_EQ,
    }
    public enum OP : byte {
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
      CZE,

      LDI_ZERO = 0x38,
      LDI_S1,
      LDI_S2,
      LDI_S4,
      LDI_U1,
      LDI_U2,
      LDI_TRUE,
      LDI_MINUS1,


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
      LDI_MIN = 0x87,

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

      LPM_S1 = 0xC9,
      LPM_S2,
      LPM_S4,
      LPM_U1,
      LPM_U2,

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
  }
}
