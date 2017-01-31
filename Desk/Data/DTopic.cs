using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace X13.Data {
  internal class DTopic {
    private static char[] FIELDS_SEPARATOR = new char[] { '.' };
    private Client _client;

    private bool _disposed;
    private List<DTopic> _children;

    private DTopic(DTopic parent, string name) {
      this.parent = parent;
      this._client = this.parent._client;
      this.name = name;
      this.path = this.parent == _client.root ? ("/" + name) : (this.parent.path + "/" + name);
    }
    internal DTopic(Client cl) {
      _client = cl;
      this.name = _client.ToString();
      this.path = "/";
    }

    public virtual string name { get; protected set; }
    public string path { get; private set; }
    public string fullPath { get { return _client.ToString() + this.path; } }
    public DTopic parent { get; private set; }

  }
}
