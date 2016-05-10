using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using X13.Data;
using JSC = NiL.JS.Core;
using JSL = NiL.JS.BaseLibrary;

namespace X13.UI {
  public abstract class InBase : NPC_UI {
    protected JSC.JSValue _schema;
    protected string _view;

    public bool IsExpanded { get; set; }
    public bool IsEdited { get; protected set; }
    public string name { get; set; }
    public BitmapSource icon { get; protected set; }
    public IValueEditor editor { get; protected set; }

    public abstract JSC.JSValue value { get; set; }
    public abstract List<Control> MenuItems();
    public void GotFocus(object sender, RoutedEventArgs e) {
      DependencyObject cur;
      TreeViewItem parent;
      DependencyObject parentObject;

      for(cur = sender as DependencyObject; cur != null; cur = parentObject) {
        parentObject = VisualTreeHelper.GetParent(cur);
        if((parent = parentObject as TreeViewItem) != null) {
          parent.IsSelected = true;
          break;
        }
      }
    }

    protected virtual void UpdateSchema(JSC.JSValue schema) {
      this._schema = schema;

      string nv = null;
      BitmapSource ni = null;

      if(_schema != null && _schema.Value != null) {
        var vv = _schema["view"];
        if(vv.ValueType == JSC.JSValueType.String) {
          nv = vv.Value as string;
        }
        var iv = _schema["icon"];
        if(iv.ValueType == JSC.JSValueType.String) {
          ni = App.GetIcon(iv.Value as string);
        }
      }
      if(nv == null) {
        nv = value.ValueType.ToString();
      }
      if(ni == null) {
        ni = App.GetIcon(nv);
      }
      if(ni == null) {
        ni = App.GetIcon(null);
      }
      if(ni != icon) {
        icon = ni;
        PropertyChangedReise("icon");
      }
      if(nv != _view) {
        _view = nv;
        editor = InspectorForm.GetEdititor(_view, this, _schema);
        PropertyChangedReise("editor");
      }
      this.editor.SchemaChanged(_schema);
    }
  }
}
