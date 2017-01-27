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

namespace X13 {
  /// <summary>
  /// Interaction logic for MainWindow.xaml
  /// </summary>
  public partial class MainWindow : Window {
    private string _cfgPath;
    private XmlDocument _cfgDoc;

    public MainWindow(string cfgPath) {
      _cfgPath = cfgPath;

      try {
        if(!System.IO.Directory.Exists(System.IO.Path.GetDirectoryName(_cfgPath))) {
          System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(_cfgPath));
        } else if(System.IO.File.Exists(_cfgPath)) {
          _cfgDoc = new XmlDocument();
          _cfgDoc.Load(_cfgPath);
          var title = System.Reflection.Assembly.GetExecutingAssembly().GetCustomAttributes(typeof(System.Reflection.AssemblyTitleAttribute), false).First() as System.Reflection.AssemblyTitleAttribute;


          //TODO: check signature App.Name+version major+version minor
          var window = _cfgDoc.SelectSingleNode("/Config/Window");
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
        }
      }
      catch(Exception ex) {
        Log.Error("Load config - {0}", ex.Message);
      }
      InitializeComponent();
#if !DEBUG
      System.Diagnostics.PresentationTraceSources.DataBindingSource.Switch.Level = System.Diagnostics.SourceLevels.Critical;
#endif
    }
    private void Window_Loaded(object sender, RoutedEventArgs e) {
      try {
        XmlNode xlay;
        if(_cfgDoc != null
           && (xlay = _cfgDoc.SelectSingleNode("/Config/LayoutRoot")) != null
           && xlay.OuterXml != null) {

          var layoutSerializer = new Xceed.Wpf.AvalonDock.Layout.Serialization.XmlLayoutSerializer(this.dmMain);
          layoutSerializer.LayoutSerializationCallback += LSF;
          layoutSerializer.Deserialize(new System.IO.StringReader(xlay.OuterXml));
        }
      }
      catch(Exception ex) {
        Log.Error("Load layout - {0}", ex.Message);
      }
    }
    private void Window_Closed(object sender, EventArgs e) {
      var layoutSerializer = new Xceed.Wpf.AvalonDock.Layout.Serialization.XmlLayoutSerializer(this.dmMain);
      try {
        var lDoc = new XmlDocument();
        using(var ix = lDoc.CreateNavigator().AppendChild()) {
          layoutSerializer.Serialize(ix);
        }

        var xd = new XmlDocument();
        var root = xd.CreateElement("Config");
        //TODO: signature
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
      //try {
      //  DWorkspace.This.Exit();
      //}
      //catch(Exception ex) {
      //  Log.Error("DWorkspace.Exit() - {0}", ex.Message);
      //}
      Log.Finish();
    }

    private void LSF(object sender, Xceed.Wpf.AvalonDock.Layout.Serialization.LayoutSerializationCallbackEventArgs arg) {
      if(!string.IsNullOrWhiteSpace(arg.Model.ContentId)) {
        Uri u;
        if(!Uri.TryCreate(arg.Model.ContentId, UriKind.Absolute, out u)) {
          Log.Warning("Restore Layout({0}) - Bad ContentID", arg.Model.ContentId);
          arg.Cancel = true;
          return;
        }
        string view = u.Query;
        if(view != null && view.StartsWith("?view=")) {
          view = view.Substring(6);
        } else {
          view = null;
        }
        //arg.Content = DWorkspace.This.Open(u.GetLeftPart(UriPartial.Path), view);
        if(arg.Content == null) {
          arg.Cancel = true;
        }
      }
    }

    private void buNewDocument_Click(object sender, RoutedEventArgs e) {
      //DWorkspace.This.Open(null);
    }
    private void dmMain_DocumentClosed(object sender, Xceed.Wpf.AvalonDock.DocumentClosedEventArgs e) {
      //var form = e.Document.Content as UIDocument;
      //if(form != null) {
      //  DWorkspace.This.Close(form);
      //}
    }

    private void CloseButtonClick(object sender, RoutedEventArgs e) {
      SystemCommands.CloseWindow(this);
    }
    private void MinButtonClick(object sender, RoutedEventArgs e) {
      SystemCommands.MinimizeWindow(this);
    }
    private void MaxButtonClick(object sender, RoutedEventArgs e) {
      if(this.WindowState == WindowState.Maximized) {
        SystemCommands.RestoreWindow(this);
      } else {
        SystemCommands.MaximizeWindow(this);
      }
    }
  }
}
