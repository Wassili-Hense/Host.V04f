function ApiV04Ctor() {
  window.API = {};
  API.socket = io((window.location.protocol == "https:" ? "wss://" : "ws://") + window.location.host
      , { "path": "/api/v04", "transports": ['websocket'] });
}
ApiV04Ctor();