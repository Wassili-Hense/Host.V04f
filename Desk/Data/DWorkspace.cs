using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;

namespace X13.Data {
  internal class DWorkspace {
    private string _cfgPath;
    public XmlDocument config;
    public List<Client> clients;

    public DWorkspace(string cfgPath) {
      this._cfgPath = cfgPath;
      clients = new List<Client>();
      try {
        if(!System.IO.Directory.Exists(System.IO.Path.GetDirectoryName(_cfgPath))) {
          System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(_cfgPath));
        } else if(System.IO.File.Exists(_cfgPath)) {
          config = new XmlDocument();
          config.Load(_cfgPath);
          var sign = config.DocumentElement.Attributes["Signature"];
          if(config.FirstChild.Name != "Config" || sign == null || sign.Value != "X13.Desk v.0.4") {
            config = null;
            Log.Warning("Load config({0}) - unknown format", _cfgPath);
          } else {
            XmlNode cList = config.SelectSingleNode("/Config/Connections");
            if(cList != null) {
              int i;
              XmlNode xc;
              string server, userName, password;
              int port;
              var xcl=cList.SelectNodes("Server");
              for(i=0; i<xcl.Count; i++) {
                xc = xcl[i];
                var tmp = xc.Attributes["URL"];
                if(tmp == null || string.IsNullOrEmpty(server = tmp.Value)) {
                  continue;
                }
                tmp = xc.Attributes["Port"];
                if(tmp == null || !int.TryParse(tmp.Value, out port) || port == 0) {
                  port = DeskHost.DeskSocket.portDefault;
                }
                tmp = xc.Attributes["User"];
                userName = tmp != null ? tmp.Value : null;
                tmp = xc.Attributes["Password"];
                password = tmp != null ? tmp.Value : null;
                var cl = new Client(server, port, userName, password);
                tmp = xc.Attributes["Alias"];
                if(tmp != null) {
                  cl.alias = tmp.Value;
                }
                clients.Add(cl);
              }
            }
          }
        }
      }
      catch(Exception ex) {
        Log.Error("Load config - {0}", ex.Message);
        config = null;
      }
    }

    public void Close() {
      var clx = config.CreateElement("Connections");
      XmlNode xc;
      foreach(var cl in clients) {
        xc = config.CreateElement("Server");
        var tmp = config.CreateAttribute("URL");
        tmp.Value = cl.server;
        xc.Attributes.Append(tmp);
        if(cl.port != DeskHost.DeskSocket.portDefault) {
          tmp = config.CreateAttribute("Port");
          tmp.Value = cl.port.ToString();
          xc.Attributes.Append(tmp);
        }
        if(cl.userName != null) {
          tmp = config.CreateAttribute("User");
          tmp.Value = cl.userName;
          xc.Attributes.Append(tmp);
        }
        if(cl.password != null) {
          tmp = config.CreateAttribute("Password");
          tmp.Value = cl.password;
          xc.Attributes.Append(tmp);
        }
        if(cl.alias != null) {
          tmp = config.CreateAttribute("Alias");
          tmp.Value = cl.alias;
          xc.Attributes.Append(tmp);
        }
        clx.AppendChild(xc);
        cl.Close();
      }
      config.DocumentElement.AppendChild(clx);
      config.Save(_cfgPath);
    }
  }
}
