using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using X13.Repository;

namespace X13.MQTT {
  internal class MqSite {
    private Uri _uri;
    private MQTTPl _pl;

    public readonly Topic Owner;
    public readonly MqClient Client;

    public MqSite(MQTTPl pl, MqClient client, Topic owner, Uri uUri) {
      this.Client = client;
      this.Owner = owner;
      this._pl = pl;
      this._uri = uUri;
      //Client.Subscribe(_uri.PathAndQuery, PubRcv);
    }

    public void Disconnect() {
    }
  }
}
