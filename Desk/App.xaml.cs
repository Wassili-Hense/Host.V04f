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
      Log.Error("[{0}] Resolve failed: {1}", args.RequestingAssembly.FullName, args.Name);
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
