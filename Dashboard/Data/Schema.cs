using JSL = NiL.JS.BaseLibrary;
using JSC = NiL.JS.Core;
using JSF = NiL.JS.Core.Functions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;

namespace X13.Data {
  public class Schema {
    private JSC.JSValue _data;

    public Schema(NiL.JS.Core.JSValue data) {
      _data = data;
      var ji = _data["icon"];
      string si;
      if(ji.ValueType == JSC.JSValueType.String && !string.IsNullOrWhiteSpace(si = ji.Value as string)) {
        if(si.StartsWith("data:image/png;base64,")) {
          var bitmapData = Convert.FromBase64String(si.Substring(22));
          var streamBitmap = new System.IO.MemoryStream(bitmapData);
          var decoder = new PngBitmapDecoder(streamBitmap, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.None);
          icon = decoder.Frames[0];
        } else if(si.StartsWith("component/Images/")){
          var url = new Uri("pack://application:,,,/Dashboard;" + si, UriKind.Absolute);
          var decoder = new PngBitmapDecoder(url, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.None);
          icon = decoder.Frames[0];
        }
      }
    }

    public BitmapSource icon { get; private set; }
  }
}
