///<remarks>This file is part of the <see cref="https://github.com/X13home">X13.Home</see> project.<remarks>
using JSC = NiL.JS.Core;
using JSL = NiL.JS.BaseLibrary;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text;
using X13.Repository;

namespace X13.Periphery {
  [Export(typeof(IPlugModul))]
  [ExportMetadata("priority", 7)]
  [ExportMetadata("name", "MQTT_SN")]
  public class MQTT_SNPl : IPlugModul {
    public MQTT_SNPl() {
    }

    public void Init() {
    }

    public void Start() {
    }

    public void Tick() {
    }

    public void Stop() {
    }

    public bool enabled {
      get {
        var en = Topic.root.Get("/$YS/MQTT-SN", true);
        if(en.GetState().ValueType != JSC.JSValueType.Boolean) {
          en.SetAttribute(Topic.Attribute.Required | Topic.Attribute.Readonly | Topic.Attribute.Config);
          en.SetState(true);
          return true;          
        }
        return (bool)en.GetState();
      }
      set {
        var en = Topic.root.Get("/$YS/MQTT-SN", true);
        en.SetState(value);
      }
    }
  }
}
