using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Xml;
using X13.Data;

namespace X13 {
  /// <summary>
  /// Interaction logic for MainWindow.xaml
  /// </summary>
  public partial class MainWindow : Window {
    private string _cfgPath;

    public MainWindow() {
      _cfgPath = @"../data/Dashboard.cfg";
      X13.Data.DWorkspace.ui = this.Dispatcher;
      InitializeComponent();
      //dmMain.DataContext = Workspace.This;
    }

    private void Window_Loaded(object sender, RoutedEventArgs e) {
      string layoutS = null;
      try {
        if(!System.IO.Directory.Exists(System.IO.Path.GetDirectoryName(_cfgPath))) {
          System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(_cfgPath));
        } else if(System.IO.File.Exists(_cfgPath)) {
          var xd = new XmlDocument();
          xd.Load(_cfgPath);
          var window = xd.SelectSingleNode("/Config/Window");
          if(window != null) {
            WindowState st;
            double tmp;
            if(window.Attributes["Top"] != null && double.TryParse(window.Attributes["Top"].Value, out tmp)) {
              this.Top = tmp;
            }
            if(window.Attributes["Left"] != null && double.TryParse(window.Attributes["Left"].Value, out tmp)) {
              this.Left = tmp;
            }
            if(window.Attributes["Width"] != null && double.TryParse(window.Attributes["Width"].Value, out tmp)) {
              this.Width = tmp;
            }
            if(window.Attributes["Height"] != null && double.TryParse(window.Attributes["Height"].Value, out tmp)) {
              this.Height = tmp;
            }
            if(window.Attributes["State"] != null && Enum.TryParse(window.Attributes["State"].Value, out st)) {
              this.WindowState = st;
            }
          }
          var xlay = xd.SelectSingleNode("/Config/LayoutRoot");
          if(xlay != null) {
            layoutS = xlay.OuterXml;
          }
        }
        if(layoutS != null) {
          var layoutSerializer = new Xceed.Wpf.AvalonDock.Layout.Serialization.XmlLayoutSerializer(this.dmMain);
          layoutSerializer.LayoutSerializationCallback += LSF;
          layoutSerializer.Deserialize(new System.IO.StringReader(layoutS));
        }
      }
      catch(Exception ex) {
        Log.Error("Load config - {0}", ex.Message);
      }
    }
    private void LSF(object sender, Xceed.Wpf.AvalonDock.Layout.Serialization.LayoutSerializationCallbackEventArgs arg) {
      if(!string.IsNullOrWhiteSpace(arg.Model.ContentId)) {
        //arg.Content = Workspace.This.Open(arg.Model.ContentId);
        //if(arg.Content == null) {
        //  arg.Cancel = true;
        //}
      }
    }
    private void Window_Closed(object sender, EventArgs e) {
      try {
        DWorkspace.This.Exit();
      }
      catch(Exception ex) {
        Log.Error("DWorkspace.Exit() - {0}", ex.Message);
      }
      var layoutSerializer = new Xceed.Wpf.AvalonDock.Layout.Serialization.XmlLayoutSerializer(this.dmMain);
      try {
        var lDoc = new XmlDocument();
        using(var ix = lDoc.CreateNavigator().AppendChild()) {
          layoutSerializer.Serialize(ix);
        }

        var xd = new XmlDocument();
        var root = xd.CreateElement("Config");
        xd.AppendChild(root);
        var window = xd.CreateElement("Window");
        {
          var tmp = xd.CreateAttribute("State");
          tmp.Value = this.WindowState.ToString();
          window.Attributes.Append(tmp);

          tmp = xd.CreateAttribute("Left");
          tmp.Value = this.Left.ToString();
          window.Attributes.Append(tmp);

          tmp = xd.CreateAttribute("Top");
          tmp.Value = this.Top.ToString();
          window.Attributes.Append(tmp);

          tmp = xd.CreateAttribute("Width");
          tmp.Value = this.Width.ToString();
          window.Attributes.Append(tmp);

          tmp = xd.CreateAttribute("Height");
          tmp.Value = this.Height.ToString();
          window.Attributes.Append(tmp);
        }
        root.AppendChild(window);
        root.AppendChild(xd.ImportNode(lDoc.FirstChild, true));
        xd.Save(_cfgPath);
      }
      catch(Exception ex) {
        Log.Error("Save config - {0}", ex.Message);
      }
      Log.Finish();
    }

    private async void miConnect_Click(object sender, RoutedEventArgs e) {
      var t = await DWorkspace.This.GetAsync(new Uri("ws://localhost/tmp"), true);
      Log.Debug("t={0}", t == null ? "null" : t.name);
    }
  }
}
