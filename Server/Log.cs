#region license
//Copyright (c) 2011-2015 Wassili Hense

//This file is part of the X13.Home project.
//https://github.com/X13home

//BSD License
//See LICENSE.txt file for license details.
#endregion license

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace X13 {
  public class Log {
    private static bool _useDiagnostic;
    private static bool _useConsole;

    static Log() {
      if(!Directory.Exists("../log")) {
        Directory.CreateDirectory("../log");
      }
      _useDiagnostic=System.Diagnostics.Debugger.IsAttached;
      _useConsole=Environment.UserInteractive;
    }
    public static void Debug(string format, params object[] arg) {
      onWrite(LogLevel.Debug, format, arg);
    }
    public static void Info(string format, params object[] arg) {
      onWrite(LogLevel.Info, format, arg);
    }
    public static void Warning(string format, params object[] arg) {
      onWrite(LogLevel.Warning, format, arg);
    }
    public static void Error(string format, params object[] arg) {
      onWrite(LogLevel.Error, format, arg);
    }
    public static void onWrite(LogLevel ll, string format, params object[] arg) {
      string msg=string.Format(format, arg);
      DateTime now=DateTime.Now;
      if(Write!=null) {
        Write(ll, now, msg);
      }
      if(_useConsole || _useDiagnostic) {
        string dts=now.ToString("HH:mm:ss.ff");
        if(_useDiagnostic) {
          System.Diagnostics.Debug.WriteLine(string.Format("{0}[{1}] {2}", dts, ll.ToString(), msg));
        }
        if(_useConsole) {
          switch(ll) {
          case LogLevel.Debug:
            Console.ForegroundColor=ConsoleColor.Gray;
            Console.WriteLine(dts+"[D] "+msg);
            break;
          case LogLevel.Info:
            Console.ForegroundColor=ConsoleColor.White;
            Console.WriteLine(dts+"[I] "+msg);
            break;
          case LogLevel.Warning:
            Console.ForegroundColor=ConsoleColor.Yellow;
            Console.WriteLine(dts+"[W] "+msg);
            break;
          case LogLevel.Error:
            Console.ForegroundColor=ConsoleColor.Red;
            Console.WriteLine(dts+"[E] "+msg);
            break;
          }
        }
      }
    }
    public static event Action<LogLevel, DateTime, string> Write;
  }
  public enum LogLevel {
    Debug,
    Info,
    Warning,
    Error
  }
}
