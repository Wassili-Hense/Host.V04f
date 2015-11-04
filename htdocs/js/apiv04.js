"use strict";

var servConn = {
  _socket: null,
  _root: null,
  init: function () {
    this._socket = io((window.location.protocol == "https:" ? "wss://" : "ws://") + window.location.host
      , { "path": "/api/v04", "transports": ['websocket'] });
    this._socket.on('connect', function () { document.title = window.location.host; });
    this._socket.on('disconnect', function () { document.title = "OFFLINE" });
    this._root = Object.create(this.TopicOr, { _conn: { value: this, writable: false, enumerable: false } })
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
  get root() {
    return this._root.CreateTopic();
  },
  
  TopicOr: {
    reflexes: [],
    parent: null,
    name: "",
    value: null,
    CreateTopic: function () {
      var t = Object.create(this.Topic, { original: { value : this, writable: false, enumerable: false} });
      this.reflexes.push(t);
      return t;
    },
  },
  Topic: {
    original: null,
    get name() {
      return this.original.name;
    },
    get path() {
      if (this.original.parent == null) {
        return "/";
      } else {
        return this.original.parent.getPath() + "/" + this.original.name;
      }
    },
    get value() {
      return this.original.value;
    },
    dispose: function () {
      var pos = parent.reflexes.indexOf(this);
      if (pos >= 0) {
        parent.reflexes.splice(pos, 1);
      }
    },
  },
}
