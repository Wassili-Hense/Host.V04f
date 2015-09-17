using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.ComponentModel.Composition.Hosting;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;

namespace X13 {
  internal class Programm {
    private static void Main(string[] args) {
      string name=Assembly.GetExecutingAssembly().Location;
      string path=Path.GetDirectoryName(name);
      string cfgPath=Path.Combine(path, "../data/Server.xst");
      int flag=Environment.UserInteractive?0:1;
      for(int i=0; i<args.Length; i++) {
        if(string.IsNullOrWhiteSpace(args[i])) {
          continue;
        }
        if(args[i].Length>1 && (args[i][0]=='/' || args[i][0]=='-')) {
          switch(args[i][1]) {
          case 's':
            flag=1;
            break;
          case 'i':
            flag=2;
            break;
          case 'u':
            flag=3;
            break;
          }
        } else if(File.Exists(args[i])) {
          cfgPath=Path.GetFullPath(args[i]);
        }
      }
      Directory.SetCurrentDirectory(path);
      if(flag!=1) {
        if(!CSWindowsServiceRecoveryProperty.Win32.AttachConsole(-1))  // Attach to a parent process console
          CSWindowsServiceRecoveryProperty.Win32.AllocConsole(); // Alloc a new console if none available
      }
      if(flag==0) {
        var srv=new Programm(cfgPath);
        if(srv.Start()) {
          Console.ForegroundColor=ConsoleColor.Green;
          Console.WriteLine("X13 Home automation server started; press Enter to Exit");
          Console.ResetColor();
          Console.Read();
          srv.Stop();
        } else {
          Console.ForegroundColor=ConsoleColor.Magenta;
          Console.WriteLine("X13 Home automation server start FAILED; press Enter to Exit");
          Console.ResetColor();
          Console.Read();
        }
        Console.ForegroundColor=ConsoleColor.Gray;
      } else if(flag==1) {
        try {
          HAServer.Run(cfgPath);
        }
        catch(Exception ex) {
          Log.Error("{0}", ex.ToString());
        }
      } else if(flag==2) {
        try {
          HAServer.InstallService(name);
        }
        catch(Exception ex) {
          Log.Error("{0}", ex.ToString());
        }
      } else if(flag==3) {
        try {
          HAServer.UninstallService(name);
        }
        catch(Exception ex) {
          Log.Error("{0}", ex.ToString());
        }
      }
    }
    public static bool IsLinux {
      get {
        int p = (int)Environment.OSVersion.Platform;
        return (p == 4) || (p == 6) || (p == 128);
      }
    }

    private string _cfgPath;
    private Mutex _singleInstance;
    private string _lfPath;
    private DateTime _firstDT;
    private Thread _thread;
    private AutoResetEvent _tick;
    private bool _terminate;
    private Timer _tickTimer;

    internal Programm(string cfgPath) {
      _cfgPath=cfgPath;
    }
    internal bool Start() {
      string siName=string.Format("Global\\X13.HAServer@{0}", Path.GetFullPath(_cfgPath).Replace('\\', '$'));
      _singleInstance=new Mutex(true, siName);

      if(!Directory.Exists("../log")) {
        Directory.CreateDirectory("../log");
      }
      AppDomain.CurrentDomain.UnhandledException+=new UnhandledExceptionEventHandler(CurrentDomain_UnhandledException);
      Log.Write+=Log_Write;
      if(!_singleInstance.WaitOne(TimeSpan.Zero, true)) {
        Log.Error("only one instance at a time");
        _singleInstance=null;
        return false;
      }
      if(!Directory.Exists("../data")) {
        Directory.CreateDirectory("../data");
      }
      if(!LoadPlugins()) {
        return false;
      }
      _tick=new AutoResetEvent(false);
      _terminate=false;
      _thread=new Thread(new ThreadStart(PrThread));
      _thread.Priority=ThreadPriority.AboveNormal;
      _thread.IsBackground=false;
      _thread.Start();

      return true;
    }

    internal void Stop() {
      _terminate=true;
      _tick.Set();
      if(!_thread.Join(3500)) {
        _thread.Abort();
      }
      if(_singleInstance!=null) {
        _singleInstance.ReleaseMutex();
      }
    }
    private void PrThread() {
      InitPlugins();
      StartPlugins();
      _tickTimer=new Timer(Tick, null, 30, 500);
      do {
        _tick.WaitOne();
        TickPlugins();
        Log.Debug("Tick");
      } while(!_terminate);
      _tickTimer.Change(-1, -1);
      StopPlugins();
    }
    private void Tick(object o) {
      _tick.Set();
    }
    private void Log_Write(LogLevel ll, DateTime dt, string msg) {
      char ll_c;
      switch(ll) {
      case LogLevel.Error:
        ll_c='E';
        break;
      case LogLevel.Warning:
        ll_c='W';
        break;
      case LogLevel.Info:
        ll_c='I';
        break;
      default:
        ll_c='D';
        break;
      }
      string rez=string.Concat(dt.ToString("HH:mm:ss.ff"), "[", ll_c, "] ", msg);
      LogLevel lt=LogLevel.Info;
      //if(_lThreshold!=null) {
      //  lt=_lThreshold.value;
      //}
      if((int)ll>=(int)lt) {
        if(_lfPath==null || _firstDT!=dt.Date) {
          _firstDT=dt.Date;
          try {
            foreach(string f in Directory.GetFiles("../log/", "*.log", SearchOption.TopDirectoryOnly)) {
              if(File.GetLastWriteTime(f).AddDays(6)<_firstDT)
                File.Delete(f);
            }
          }
          catch(System.IO.IOException) {
          }
          _lfPath="../log/"+_firstDT.ToString("yyMMdd")+".log";
        }
        for(int i=2; i>=0; i--) {
          try {
            using(FileStream fs=File.Open(_lfPath, FileMode.OpenOrCreate, FileAccess.Write, FileShare.ReadWrite)) {
              fs.Seek(0, SeekOrigin.End);
              byte[] ba=Encoding.UTF8.GetBytes(rez+"\r\n");
              fs.Write(ba, 0, ba.Length);
            }
            break;
          }
          catch(System.IO.IOException) {
            Thread.Sleep(15);
          }
        }
      }
    }
    private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e) {
      try {
        Log.Error("unhandled Exception {0}", e.ExceptionObject.ToString());
      }
      catch {
      }
      try {
        this.Stop();
      }
      catch {
      }
    }


    #region Plugins
#pragma warning disable 649
    [ImportMany(typeof(IPlugModul), RequiredCreationPolicy=CreationPolicy.Shared)]
    private IEnumerable<Lazy<IPlugModul, IPlugModulData>> _modules;
#pragma warning restore 649

    private bool LoadPlugins() {
      string path=Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

      var catalog = new AggregateCatalog();
      catalog.Catalogs.Add(new AssemblyCatalog(Assembly.GetExecutingAssembly()));
      catalog.Catalogs.Add(new DirectoryCatalog(path));
      CompositionContainer _container = new CompositionContainer(catalog);
      try {
        _container.ComposeParts(this);
      }
      catch(CompositionException ex) {
        Log.Error("Load plugins - {0}", ex.ToString());
        return false;
      }
      _modules=_modules.OrderBy(z => z.Metadata.priority).ToArray();
      return true;
    }
    private void InitPlugins() {
      string pName;
      foreach(var i in _modules) {
        if(!i.Metadata.enabled) {
          continue;
        }
        pName=i.Metadata.name??i.Value.GetType().FullName;
        try {
          i.Value.Init();
          Log.Debug("plugin {0} Loaded", pName);
        }
        catch(Exception ex) {
          Log.Error("Load plugin {0} failure - {1}", pName, ex.ToString());
        }
      }
    }
    private void StartPlugins() {
      string pName;
      foreach(var i in _modules) {
        if(!i.Metadata.enabled) {
          continue;
        }
        pName=i.Metadata.name??i.Value.GetType().FullName;
        try {
          i.Value.Start();
          Log.Debug("plugin {0} Started", pName);
        }
        catch(Exception ex) {
          Log.Error("Start plugin {0} failure - {1}", pName, ex.ToString());
        }
      }
    }
    private void TickPlugins() {
      foreach(var i in _modules) {
        if(!i.Metadata.enabled) {
          continue;
        }
        try {
          i.Value.Tick();
        }
        catch(Exception ex) {
          Log.Error("{0}.Tick() - {1}", i.Metadata.name??i.Value.GetType().FullName, ex.ToString());
        }
      }
    }
    private void StopPlugins() {
      foreach(var i in _modules.Reverse()) {
        if(!i.Metadata.enabled) {
          continue;
        }
        try {
          i.Value.Stop();
        }
        catch(Exception ex) {
          Log.Error("Stop plugin {0} failure - {1}", i.Metadata.name??i.Value.GetType().FullName, ex.ToString());
        }
      }
    }
    #endregion Plugins
  }
}
