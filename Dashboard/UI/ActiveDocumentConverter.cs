namespace X13.UI {
  using System;
  using System.Collections.Generic;
  using System.Linq;
  using System.Text;
  using System.Windows.Data;

  class ActiveDocumentConverter : IValueConverter {
    public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture) {
      if(value is UiBaseForm)
        return value;

      return Binding.DoNothing;
    }

    public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture) {
      if(value is UiBaseForm)
        return value;

      return Binding.DoNothing;
    }
  }
}
