﻿///<remarks>This file is part of the <see cref="https://github.com/X13home">X13.Home</see> project.<remarks>
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using JSC = NiL.JS.Core;
using JSL = NiL.JS.BaseLibrary;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace X13.UI {
  internal class veTopicReference : TextBlock, IValueEditor {
    public static IValueEditor Create(InBase owner, JSC.JSValue type) {
      return new veTopicReference(owner, type);
    }


    public veTopicReference(InBase owner, JSC.JSValue type) {
      base.HorizontalAlignment = System.Windows.HorizontalAlignment.Left;
      base.Padding = new System.Windows.Thickness(10, 0, 10, 0);
      ValueChanged(owner.value);
    }

    public void ValueChanged(JSC.JSValue value) {
      string rez;
      if(value != null && value.ValueType==JSC.JSValueType.String && (rez= value.Value as string)!=null && rez.StartsWith("¤TR")) {
        this.Text = rez.Substring(3);
        base.Foreground = Brushes.Black;
      } else {
        this.Text = "###-##";
        base.Foreground = Brushes.OrangeRed;
      }
    }
    public void TypeChanged(JSC.JSValue type) {
    }
  }
}
