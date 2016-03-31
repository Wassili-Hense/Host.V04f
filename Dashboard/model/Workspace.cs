using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading;

namespace X13 {
  internal class Workspace : INotifyPropertyChanged {
    #region static
    static Workspace() {
      _this = new Workspace();
    }

    static Workspace _this;
    public static Workspace This {
      get { return _this; }
    }
    #endregion static

    #region instance variables

    private ObservableCollection<Topic> _files;
    private ReadOnlyObservableCollection<Topic> _readonyFiles;
    private Thread _bw;
    private bool _runing;
    private System.Collections.Concurrent.ConcurrentQueue<INotMsg> _msgs;

    #endregion instance variables

    private Workspace() {
      _files = new ObservableCollection<Topic>();
      _readonyFiles = null;
      _msgs = new System.Collections.Concurrent.ConcurrentQueue<INotMsg>();
      _runing = true;
      _bw=new Thread(ThFunction);
      _bw.Start();
    }

    public ReadOnlyObservableCollection<Topic> Files {
      get {
        if(_readonyFiles == null)
          _readonyFiles = new ReadOnlyObservableCollection<Topic>(_files);

        return _readonyFiles;
      }
    }
    public void AddFile(Topic i) {
      _files.Add(i);
      ActiveDocument = i;
    }
    private Topic _activeDocument = null;
    public Topic ActiveDocument {
      get { return _activeDocument; }
      set {
        if(_activeDocument != value) {
          _activeDocument = value;
          RaisePropertyChanged("ActiveDocument");
        }
      }
    }
    public Topic Open(string p) {
      if(p == null || p.Length < 3) {
        return null;
      }
      var fileViewModel = _files.FirstOrDefault(fm => fm.path.ToString() == p);
      if(fileViewModel != null) {
        this.ActiveDocument = fileViewModel; // File is already open so show it

        return fileViewModel;
      }

      fileViewModel = _files.FirstOrDefault(fm => fm.path.ToString() == p);
      if(fileViewModel != null) {
        return fileViewModel;
      }
      var r = Client.Get(new Uri(p), false);
      _files.Add(r);
      return r;
    }
    public void Finish() {
      _runing = false;
      lock(this){
        if(_bw != null) {
          if(!_bw.Join(300)) {
            _bw.Abort();
          }
          _bw = null;
        }
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
          msg.Parse(this);
        } else {
          Thread.Sleep(50);
        }
      }
      List<Topic> a;
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
    void Parse(Workspace ws);
    void Complete(bool success);
  }
}
