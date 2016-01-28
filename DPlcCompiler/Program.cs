using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using NiL.JS;
using NiL.JS.BaseLibrary;
using NiL.JS.Core;

namespace X13 {
  internal class Program {
    static void Main(string[] args) {
      int err=-1;
      if(args.Length>0 && File.Exists(args[0])) {
        try {
          string code = File.ReadAllText(args[0]);
          var v = new Compiler();
          v.Parse(code);
          err = 0;
        }
        catch(JSException ex) {
          var syntaxError = ex.Error.Value as SyntaxError;
          if(syntaxError != null) {
            Log.Error("{0}", syntaxError.ToString());
          } else {
            Log.Error("Unknown error: {0}", ex);
          }
          err = -2;
        }
        catch(Exception ex) {
          Log.Error("{0}", ex);
        }
      }
      if(err != 0) {
        Log.Info("USE: DPlcCompiler <sourcer file>.js");
      }
      Log.Finish();
      Console.ReadKey();
    }
  }
}
