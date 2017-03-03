///<remarks>This file is part of the <see cref="https://github.com/X13home">X13.Home</see> project.<remarks>
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using X13.Repository;

namespace X13.Periphery {
  internal class MsDevice : IMsGate {
    public readonly Topic owner;
    public IMsGate _gate;
    public byte[] addr;

    public MsDevice(Topic owner) {
      this.owner = owner;
    }
    #region IMsGate Members
    public void SendGw(byte[] addr, MsMessage msg) {
      throw new NotImplementedException();
    }

    public void SendGw(MsDevice dev, MsMessage msg) {
      throw new NotImplementedException();
    }

    public byte gwIdx {
      get { throw new NotImplementedException(); }
    }

    public byte gwRadius {
      get { throw new NotImplementedException(); }
    }

    public string name {
      get { throw new NotImplementedException(); }
    }

    public string Addr2If(byte[] addr) {
      throw new NotImplementedException();
    }

    public void Stop() {
      throw new NotImplementedException();
    }
    #endregion IMsGate Members

    public bool CheckAddr(byte[] addr) {
      if(addr == null) {
        return false;
      }
      if(this.addr != null && this.addr.Length - 1 == addr.Length && this.addr.Skip(1).SequenceEqual(addr)) {
        return true;
      }
      return false;
    }

    public State state { get; set; }
  }
}
