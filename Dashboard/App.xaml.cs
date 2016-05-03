using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Imaging;

namespace X13 {
  /// <summary>
  /// Interaction logic for App.xaml
  /// </summary>
  public partial class App : Application {
    private static SortedDictionary<string, BitmapSource> _icons;

    public static BitmapSource GetIcon(string icData) {
      BitmapSource rez;
      if(string.IsNullOrEmpty(icData)) {
        icData = string.Empty;
      }
      if(_icons.TryGetValue(icData, out rez)) {
        return rez;
      }
      lock(_icons) {
        if(!_icons.TryGetValue(icData, out rez)) {
          if(icData.StartsWith("data:image/png;base64,")) {
            var bitmapData = Convert.FromBase64String(icData.Substring(22));
            var streamBitmap = new System.IO.MemoryStream(bitmapData);
            var decoder = new PngBitmapDecoder(streamBitmap, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.None);
            rez = decoder.Frames[0];
            _icons[icData] = rez;
          } else if(icData.StartsWith("component/Images/")) {
            var url = new Uri("pack://application:,,,/Dashboard;" + icData, UriKind.Absolute);
            var decoder = new PngBitmapDecoder(url, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.None);
            rez = decoder.Frames[0];
            _icons[icData] = rez;
          }
        }
      }
      return rez;
    }

    protected override void OnStartup(StartupEventArgs e) {
      base.OnStartup(e);
      _icons = new SortedDictionary<string, BitmapSource>();

      LoadIcon(string.Empty, "ty_topic.png");
      LoadIcon("Null", "ty_null.png");
      LoadIcon("Boolean", "ty_bool.png");
      LoadIcon("Integer", "ty_i64.png");
      LoadIcon("Double", "ty_f02.png");
      LoadIcon("String", "ty_str.png");
      LoadIcon("Date", "ty_dt.png");
      LoadIcon("Folder", "ty_topic.png");
      LoadIcon("schema", "ty_schema.png");
      LoadIcon("children", "children.png");
    }
    private void LoadIcon(string name, string path) {
      var decoder = new PngBitmapDecoder(new Uri("pack://application:,,,/Dashboard;component/Images/" + path, UriKind.Absolute), BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.None);
      _icons[name] = decoder.Frames[0];
    }
  }
}
