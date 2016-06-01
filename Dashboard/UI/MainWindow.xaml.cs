///<remarks>This file is part of the <see cref="https://github.com/X13home">X13.Home</see> project.<remarks>
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

namespace X13.UI {
  /// <summary>
  /// Interaction logic for MainWindow.xaml
  /// </summary>
  public partial class MainWindow : Window {
    private string _cfgPath;

    public MainWindow() {
	  this.StateChanged += WindowStateChanged;
      _cfgPath = @"../data/Dashboard.cfg";
      X13.Data.DWorkspace.ui = this.Dispatcher;
      InitializeComponent();
      dmMain.DataContext = DWorkspace.This;
#if !DEBUG
      System.Diagnostics.PresentationTraceSources.DataBindingSource.Switch.Level = System.Diagnostics.SourceLevels.Critical;
#endif
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
        arg.Content = DWorkspace.This.Open(u.GetLeftPart(UriPartial.Path), view);
        if(arg.Content == null) {
          arg.Cancel = true;
        }
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
      try {
        DWorkspace.This.Exit();
      }
      catch(Exception ex) {
        Log.Error("DWorkspace.Exit() - {0}", ex.Message);
      }
      Log.Finish();
    }
    private void dmMain_DocumentClosed(object sender, Xceed.Wpf.AvalonDock.DocumentClosedEventArgs e) {
      var form = e.Document.Content as UIDocument;
      if(form!=null) {
        DWorkspace.This.Close(form);
      }
    }
	private void buNewDocument_Click(object sender, RoutedEventArgs e) {
	  DWorkspace.This.Open(null);
	}

	private void WindowStateChanged(object sender, EventArgs e) {
	  if(this.WindowState == WindowState.Maximized) {
		// Make sure window doesn't overlap with the taskbar.
        var handle = new System.Windows.Interop.WindowInteropHelper(this).Handle;
        var screen = System.Windows.Forms.Screen.FromHandle(handle);
		if(screen.Primary) {
		  this.Padding = new Thickness(
			  SystemParameters.WorkArea.Left + 7,
			  SystemParameters.WorkArea.Top + 7,
			  (SystemParameters.PrimaryScreenWidth - SystemParameters.WorkArea.Right) + 7,
			  (SystemParameters.PrimaryScreenHeight - SystemParameters.WorkArea.Bottom) + 5);
		}
	  } else {
		this.Padding = new Thickness(7, 7, 7, 5);
        this.InvalidateVisual();
	  }
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
