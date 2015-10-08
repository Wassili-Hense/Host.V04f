"use strict";

var servConn = {
  _socket: null,
  init: function () {
    this._socket = io((window.location.protocol == "https:" ? "wss://" : "ws://") + window.location.host
      , { "path": "/api/v04", "transports": ['websocket'] });
    this._socket.on('connect', function () { document.title = window.location.host; });
    this._socket.on('disconnect', function () { document.title = "OFFLINE" });
  },
  createTopic: function (path, callback) {
    this._socket.emit(8, path, callback);
  },
  dir: function (path, flags, callback) {
    this._socket.emit(9, path, flags, callback);
  },
  removeTopic: function (path) {
    this._socket.emit(10, path);
  },
  copyTopic: function (path, nparent) {
    this._socket.emit(11, path, nparent);
  },
  moveTopic: function (path, nparent, nname) {
    if (nname == null) {
      this._socket.emit(12, path, nparent);
    } else {
      this._socket.emit(12, path, nparent, nname);
    }
  },
  GetValue: function (path, callback) {
    this._socket.emit(13, path, callback);
  },
}
