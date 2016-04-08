﻿using JSL = NiL.JS.BaseLibrary;
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

namespace X13.Data {
  internal class DWorkspace : INotifyPropertyChanged {
    #region static
    private static DWorkspace _this;
    public static System.Windows.Threading.Dispatcher ui;
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
    private SortedList<string, A04Client> _clients;
    private Thread _bw;
    private bool _runing;
    private System.Collections.Concurrent.ConcurrentQueue<INotMsg> _msgs;
    private UiBaseForm _activeDocument;
    private ObservableCollection<UiBaseForm> _files;
    private ReadOnlyObservableCollection<UiBaseForm> _readonyFiles;

    #endregion instance variables

    private DWorkspace() {
      _msgs = new System.Collections.Concurrent.ConcurrentQueue<INotMsg>();
      _clients = new SortedList<string, A04Client>();
      _files = new ObservableCollection<UiBaseForm>();
      _bw = new Thread(ThFunction);
      _runing = true;
      _bw.Start();
      _activeDocument = null;
    }
    public Task<DTopic> GetAsync(Uri url, bool create) {
      var up = Uri.UnescapeDataString(url.UserInfo).Split(':');
      string uName = (up.Length > 0 && !string.IsNullOrWhiteSpace(up[0])) ? (up[0] + "@") : string.Empty;
      string host = url.Scheme + "://" + uName + url.DnsSafeHost + (url.IsDefaultPort ? string.Empty : ":" + url.Port.ToString()) + "/";
      A04Client cl;
      if(!_clients.TryGetValue(host, out cl)) {
        lock(_clients) {
          if(!_clients.TryGetValue(host, out cl)) {
            cl = new A04Client(host, up.Length == 2 ? up[1] : string.Empty);
            _clients[host] = cl;
          }
        }
      }
      return cl.root.GetAsync(url.LocalPath, create);
    }
    public UiBaseForm Open(string path, string view=null) {
      if(string.IsNullOrEmpty(view)) {
        view = "IN";
      } else if(view.StartsWith("?view=")) {
        view = view.Substring(6);
      }
      UiBaseForm ui;
      ui = _files.FirstOrDefault(z => z != null && z.data != null && z.data.fullPath == path && z.viewArt == view);
      if(ui == null) {
        ui = new UI.InspectorForm(path);
        _files.Add(ui);
      }
      ActiveDocument = ui;
      return ui;
    }
    internal void Open(DTopic t, string view=null) {
      //TODO: if(view==null) t.schema => view
      this.Open(t.fullPath, view);
    }
    internal void Close(string path, string view) {
      UiBaseForm ui;
      if(string.IsNullOrEmpty(view)) {
        view = "IN";
      } else if(view.StartsWith("?view=")) {
        view = view.Substring(6);
      }
      ui = _files.FirstOrDefault(z => z != null && z.data != null && z.data.fullPath == path && z.viewArt == view);
      if(ui != null) {
        _files.Remove(ui);
      }
    }

    public void Exit() {
      lock(_clients) {
        foreach(var cl in _clients) {
          cl.Value.Close();
        }
        _clients.Clear();
      }
      _runing = false;
      lock(this) {
        if(_bw != null) {
          if(!_bw.Join(300)) {
            _bw.Abort();
          }
          _bw = null;
        }
      }

    }

    public UiBaseForm ActiveDocument {
      get { return _activeDocument; }
      set {
        if(_activeDocument != value) {
          _activeDocument = value;
          RaisePropertyChanged("ActiveDocument");
        }
      }
    }
    public ReadOnlyObservableCollection<UiBaseForm> Files {
      get {
        if(_readonyFiles == null)
          _readonyFiles = new ReadOnlyObservableCollection<UiBaseForm>(_files);

        return _readonyFiles;
      }
    }

    #region Background worker
    public void AddMsg(INotMsg msg) {
      _msgs.Enqueue(msg);
    }
    private void ThFunction() {
      INotMsg msg;
      while(_runing || _msgs.Any()) {
        if(_msgs.TryDequeue(out msg)) {
          msg.Process(this);
        } else {
          Thread.Sleep(50);
        }
      }
    }
    #endregion Background worker

    #region INotifyPropertyChanged
    private void RaisePropertyChanged(string propertyName) {
      if(PropertyChanged != null)
        PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
    }
    public event PropertyChangedEventHandler PropertyChanged;
    #endregion INotifyPropertyChanged



  }
  internal interface INotMsg {
    void Process(DWorkspace ws);
    void Response(DWorkspace ws, bool success, JSC.JSValue value);
  }
}
