///<remarks>This file is part of the <see cref="https://github.com/X13home">X13.Home</see> project.<remarks>
using JSL = NiL.JS.BaseLibrary;
using JSC = NiL.JS.Core;
using JSF = NiL.JS.Core.Functions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using System.ComponentModel;
using System.Windows.Controls;
using System.Collections.ObjectModel;
using X13.UI;
using System.Windows.Media.Imaging;

namespace X13.Data {
  internal class DWorkspace : NPC_UI {
    #region static
    private static DWorkspace _this;
    private static System.Windows.Threading.Dispatcher _ui;
    public static System.Windows.Threading.Dispatcher ui {
      get { return _ui; }
      set {
        _ui = value;
        if(_ui != null) {
          This._tick = new System.Windows.Threading.DispatcherTimer(TimeSpan.FromMilliseconds(50), System.Windows.Threading.DispatcherPriority.Normal, This.TickFunction, _ui);
        }
      }
    }
    public static JSF.ExternalFunction _JSON_Replacer;


    static DWorkspace() {
      _JSON_Replacer = new JSF.ExternalFunction(ConvertDate);
      _this = new DWorkspace();
    }

    public static DWorkspace This {
      get { return _this; }
    }
    private static JSC.JSValue ConvertDate(JSC.JSValue thisBind, JSC.Arguments args) {
      if(args.Length == 2 && args[1].ValueType == JSC.JSValueType.String) {
        // 2015-09-16T14:15:18.994Z
        var s = args[1].ToString();
        if(s != null && s.Length == 24 && s[4] == '-' && s[7] == '-' && s[10] == 'T' && s[13] == ':' && s[16] == ':' && s[19] == '.') {
          var a = new JSC.Arguments();
          a.Add(args[1]);
          var jdt = new JSL.Date(a);
          return JSC.JSValue.Wrap(jdt);
        }
      }
      return args[1];
    }
    #endregion static

    #region instance variables
    private System.Windows.Threading.DispatcherTimer _tick;
    private SortedList<string, A04Client> _clients;
    private System.Collections.Concurrent.ConcurrentQueue<INotMsg> _msgs;
    private UIDocument _activeDocument;
    private ObservableCollection<UIDocument> _files;
    private ReadOnlyObservableCollection<UIDocument> _readonyFiles;
    #endregion instance variables

    private DWorkspace() {
      _msgs = new System.Collections.Concurrent.ConcurrentQueue<INotMsg>();
      _clients = new SortedList<string, A04Client>();
      _files = new ObservableCollection<UIDocument>();
      _activeDocument = null;
    }
    public Task<DTopic> GetAsync(Uri url) {
      var up = Uri.UnescapeDataString(url.UserInfo).Split(':');
      string uName = (up.Length > 0 && !string.IsNullOrWhiteSpace(up[0])) ? (up[0] + "@") : string.Empty;
      string host = url.Scheme + "://" + uName + url.DnsSafeHost + (url.IsDefaultPort ? string.Empty : (":" + url.Port.ToString())) + "/";
      A04Client cl;
      if(!_clients.TryGetValue(host, out cl)) {
        lock(_clients) {
          if(!_clients.TryGetValue(host, out cl)) {
            cl = new A04Client(host, up.Length == 2 ? up[1] : string.Empty);
            _clients[host] = cl;
          }
        }
      }
      return cl.root.GetAsync(url.LocalPath);
    }
    public UIDocument Open(string path, string view = null) {
      string id;
      if(string.IsNullOrEmpty(path)) {
        id = null;
        path = null;
        view = null;
      } else {
        if(view != null) {
          id = path + "?view=" + view;
        } else {
          id = path;
        }
      }
      UIDocument ui;
      ui = _files.FirstOrDefault(z => z != null && z.ContentId == id);
      if(ui == null) {
        ui = new UI.UIDocument(path, view);
        _files.Add(ui);
      }
      ActiveDocument = ui;
      return ui;
    }
    public void Close(string path, string view) {
      UIDocument d;
      if(string.IsNullOrEmpty(view)) {
        view = "IN";
      } else if(view.StartsWith("?view=")) {
        view = view.Substring(6);
      }
      string id = path + "?view=" + view;
      d = _files.FirstOrDefault(z => z != null && z.ContentId == id);
      if(d != null) {
        _files.Remove(d);
      }
    }
    public void Close(UIDocument doc) {
      var d = _files.FirstOrDefault(z => z == doc);
      if(d != null) {
        _files.Remove(d);
      }
    }
    public void Exit() {
      lock(_clients) {
        foreach(var cl in _clients) {
          cl.Value.Close();
        }
        _clients.Clear();
      }
      //_runing = false;
      //lock(this) {
      //  if(_bw != null) {
      //    if(!_bw.Join(300)) {
      //      _bw.Abort();
      //    }
      //    _bw = null;
      //  }
      //}
    }

    public UIDocument ActiveDocument {
      get { return _activeDocument; }
      set {
        if(_activeDocument != value) {
          _activeDocument = value;
          base.PropertyChangedReise("ActiveDocument");
        }
      }
    }
    public ReadOnlyObservableCollection<UIDocument> Files {
      get {
        if(_readonyFiles == null)
          _readonyFiles = new ReadOnlyObservableCollection<UIDocument>(_files);

        return _readonyFiles;
      }
    }

    #region Background worker
    public void AddMsg(INotMsg msg) {
      _msgs.Enqueue(msg);
    }
    private void TickFunction(object sender, EventArgs e){
      INotMsg msg;
      while(_msgs.Any()) {
        if(_msgs.TryDequeue(out msg)) {
          try {
            msg.Process(this);
          }
          catch(Exception ex) {
            Log.Warning("TickFunction - {0}", ex.ToString());
          }
        }
      }
    }
    #endregion Background worker
  }
  internal interface INotMsg {
    void Process(DWorkspace ws);
    void Response(DWorkspace ws, bool success, JSC.JSValue value);
  }
}
