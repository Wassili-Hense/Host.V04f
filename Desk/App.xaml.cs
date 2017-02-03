///<remarks>This file is part of the <see cref="https://github.com/X13home">X13.Home</see> project.<remarks>
using JSC = NiL.JS.Core;
using JSL = NiL.JS.BaseLibrary;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace X13 {
  /// <summary>
  /// Interaction logic for App.xaml
  /// </summary>
  public partial class App : Application {
    internal static MainWindow mainWindow { get; set; }
    internal static Data.DWorkspace Workspace { get; set; }

    internal static System.Windows.Media.Imaging.BitmapSource GetIcon(string icData) {
      return null;
    }

    public App() {
      AppDomain.CurrentDomain.UnhandledException += new UnhandledExceptionEventHandler(CurrentDomain_UnhandledException);
      AppDomain.CurrentDomain.AssemblyResolve += CurrentDomain_AssemblyResolve;
      _msgs = new System.Collections.Concurrent.ConcurrentQueue<INotMsg>();
      _msgProcessFunc = new Action(ProcessMessage);
    }

    private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e) {
      try {
        Log.Error("unhandled Exception {0}", e.ExceptionObject.ToString());
      }
      catch {
      }
    }
    private System.Reflection.Assembly CurrentDomain_AssemblyResolve(object sender, ResolveEventArgs args) {
      Log.Error("AssemblyResolve failed: {0}", args.Name);
      return null;
    }

    private void Application_Startup(object sender, StartupEventArgs e) {
      string cfgPath;
      if(e.Args.Length > 0) {
        cfgPath = e.Args[0];
      } else {
        cfgPath = @"../data/Desk.cfg";
      }

      mainWindow = new MainWindow(cfgPath);
      _msgProcessBusy = 1;
      mainWindow.Show();
    }

    #region Background worker
    private static System.Collections.Concurrent.ConcurrentQueue<INotMsg> _msgs;
    private static int _msgProcessBusy;
    private static Action _msgProcessFunc;

    internal static void PostMsg(INotMsg msg) {
      _msgs.Enqueue(msg);
      if(_msgProcessBusy == 1) {
        mainWindow.Dispatcher.BeginInvoke(_msgProcessFunc, System.Windows.Threading.DispatcherPriority.DataBind);
      }
    }
    private static void ProcessMessage() {
      INotMsg msg;
      if(System.Threading.Interlocked.CompareExchange(ref _msgProcessBusy, 2, 1) != 1) {
        return;
      }
      while(_msgs.Any()) {
        if(_msgs.TryDequeue(out msg)) {
          try {
            //Log.Debug("Tick: {0}", msg.ToString());
            msg.Process(Workspace);
          }
          catch(Exception ex) {
            Log.Warning("App.ProcessMessage(0) - {1}", msg, ex.ToString());
          }
        }
      }
      _msgProcessBusy = 1;
    }
    #endregion Background worker

  }
  internal interface INotMsg {
    void Process(Data.DWorkspace ws);
    void Response(Data.DWorkspace ws, bool success, JSC.JSValue value);
  }
}
