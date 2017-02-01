///<remarks>This file is part of the <see cref="https://github.com/X13home">X13.Home</see> project.<remarks>
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

    public App() {
      AppDomain.CurrentDomain.UnhandledException += new UnhandledExceptionEventHandler(CurrentDomain_UnhandledException);
      AppDomain.CurrentDomain.AssemblyResolve += CurrentDomain_AssemblyResolve;
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

      mainWindow.Show();
    }
  }
}
