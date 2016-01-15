using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using WebSocketSharp;
using WebSocketSharp.Net;
using WebSocketSharp.Server;

namespace X13.WebServer {
  internal class HttpStaticServer {
    private SortedList<string, Tuple<Stream, string>> _resources;
    private HttpServer _srv;

    public HttpStaticServer(HttpServer srv) {
      _srv=srv;
      {
        var assembly = Assembly.GetExecutingAssembly();

        var etf=assembly.GetName().Version.ToString(4).GetHashCode().ToString("X8")+"-";
        _resources=new SortedList<string, Tuple<Stream, string>>();
        foreach(var resourceName in assembly.GetManifestResourceNames().Where(z => z.StartsWith("X13.WebServer.www."))) {  //
          var stream=assembly.GetManifestResourceStream(resourceName);
          string eTag=etf+stream.Length.ToString("X4");
          _resources[resourceName.Substring(18)]=new Tuple<Stream, string>(stream, eTag);
        }
      }
      _srv.Log.Output=WsLog;
//#if DEBUG
//      _srv.Log.Level=WebSocketSharp.LogLevel.Trace;
//#endif
      _srv.RootPath=Path.GetFullPath("../htdocs");
      _srv.OnGet+=OnGet;
    }
    private void WsLog(LogData d, string f) {
#if DEBUG
      Log.Debug("WS({0}) - {1}", d.Level, d.Message);
#endif
    }
    private void OnGet(object sender, HttpRequestEventArgs e) {
      var req = e.Request;
      var res = e.Response;
      if(req.RemoteEndPoint==null) {
        res.StatusCode=(int)HttpStatusCode.NotAcceptable;
        return;
      }
      System.Net.IPEndPoint remoteEndPoint = req.RemoteEndPoint;
      {
        System.Net.IPAddress remIP;
        if(req.Headers.Contains("X-Real-IP") && System.Net.IPAddress.TryParse(req.Headers["X-Real-IP"], out remIP)) {
          remoteEndPoint=new System.Net.IPEndPoint(remIP, remoteEndPoint.Port);
        }
      }
      string path = req.Url.LocalPath == "/" ? "/index.html" : req.Url.LocalPath;
      string client;
      client=remoteEndPoint.Address.ToString();

      try {
        Tuple<Stream, string> rsc;
        HttpStatusCode statusCode;
        if(_resources.TryGetValue(path.Substring(1), out rsc)) {
          string et;
          if(req.Headers.Contains("If-None-Match") && (et=req.Headers["If-None-Match"])==rsc.Item2) {
            res.Headers.Add("ETag", rsc.Item2);
            statusCode=HttpStatusCode.NotModified;
            res.StatusCode=(int)statusCode;
            res.WriteContent(Encoding.UTF8.GetBytes("Not Modified"));
          } else {
            res.Headers.Add("ETag", rsc.Item2);
            res.ContentType=Ext2ContentType(Path.GetExtension(path));
            rsc.Item1.Position=0;
            rsc.Item1.CopyTo(res.OutputStream);
            res.ContentLength64=rsc.Item1.Length;
            statusCode=HttpStatusCode.OK;
          }
        } else {
          FileInfo f = new FileInfo(Path.Combine(_srv.RootPath, path.Substring(1)));
          if(f.Exists) {
            string eTag=f.LastWriteTimeUtc.Ticks.ToString("X8")+"-"+f.Length.ToString("X4");
            string et;
            if(req.Headers.Contains("If-None-Match") && (et=req.Headers["If-None-Match"])==eTag) {
              res.Headers.Add("ETag", eTag);
              statusCode=HttpStatusCode.NotModified;
              res.StatusCode=(int)statusCode;
              res.WriteContent(Encoding.UTF8.GetBytes("Not Modified"));
            } else {
              res.Headers.Add("ETag", eTag);
              res.ContentType=Ext2ContentType(f.Extension);
              using(var fs=f.OpenRead()) {
                fs.CopyTo(res.OutputStream);
                res.ContentLength64=fs.Length;
              }
              statusCode=HttpStatusCode.OK;
            }
          } else {
            statusCode=HttpStatusCode.NotFound;
            res.StatusCode = (int)statusCode;
            res.WriteContent(Encoding.UTF8.GetBytes("404 Not found"));
          }
        }
        if(true) {
          Log.Debug("{0} [{1}]{2} - {3}", client, req.HttpMethod, req.RawUrl, statusCode.ToString());
        }
      }
      catch(Exception ex) {
        if(true) {
          Log.Debug("{0} [{1}]{2} - {3}", client, req.HttpMethod, req.RawUrl, ex.Message);
        }
      }
    }
    private string Ext2ContentType(string ext) {
      switch(ext) {
      case ".jpg":
      case ".jpeg":
        return "image/jpeg";
      case ".png":
        return "image/png";
      case ".css":
        return "text/css";
      case ".csv":
        return "text/csv";
      case ".htm":
      case ".html":
        return "text/html";
      case ".js":
        return "application/javascript";
      }
      return "application/octet-stream";
    }

  }
}
