"use strict";

var servConn = {
  _socket: null,
  _root: null,
  init: function () {
    this._socket = io((window.location.protocol == "https:" ? "wss://" : "ws://") + window.location.host
      , { "path": "/api/v04", "transports": ['websocket'] });
    this._socket.on('connect', function () { document.title = window.location.host; });
    this._socket.on('disconnect', function () { document.title = "OFFLINE" });
    this._root = Object.create(this.TopicOr, { _conn: { value: this, writable: false, enumerable: false }, name: { value: window.location.host } });
    var Self=this._root;
    this._socket.emit(4, "/", 0, function (arr) { Self.onDataResp(arr); });
  },
  /*
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
  */
  GetValue: function (path, callback) {
    this._socket.emit(13, path, callback);
  },


  get root() {
    return this._root.createReflex();
  },

  TopicOr: {
    reflexes: null,
    _conn: null,
    parent: null,
    name: "",
    get path() {
      if (this.parent == null) {
        return "/";
      } else if (this.parent == this._conn._root) {
        return "/" + this.name;
      } else {
        return this.parent.path + "/" + this.name;
      }
    },
    mask: 0,  // 1-value, 2-children
    flags: 0,
    children: null,
    dataType: null,
    value: null,
    createReflex: function () {
      var t = Object.create(this.Topic, { Base: { value: this, writable: false, enumerable: false } });
      if (this.reflexes == null) {
        this.reflexes = [];
      }
      this.reflexes.push(t);
      console.log(t.path + "+[" + this.reflexes.length + "]");
      return t;
    },
    updateSubscriptions: function () {
      var nm = 0;
      var i, r;
      for (i = 0; this.reflexes!=null && i < this.reflexes.length; i++) {
        r = this.reflexes[i];
        if (r != null) {
          nm |= r._mask;
        }
      }
      if (this.mask != nm) {
        this.mask = nm;
        var Self = this;
        this._conn._socket.emit(4, this.path, this.mask, function (arr) { Self.onDataResp(arr); });
      }
    },
    onDataResp: function (arr) {
      var mask = 0;
      var nc = null;
      for (var i = 0; i < arr.length; i++) {
        if (arr[i][0] == this.path) {
          this.flags = arr[i][1];
          this.dataType = arr[i][2];
          if (arr[i].length == 4) {
            if (this.value != arr[i][3]) {
              this.value = arr[i][3];
              mask |= 1;
            }
          } else if (this.dataType == null && this.value != null) {
            this.value=null;
            mask |= 1;
          }
        } else {
          var path = arr[i][0];
          var name = path.substr(path.lastIndexOf("/") + 1);
          var item;
          if (nc == null) {
            nc = {};
          }
          if (this.children == null || (item = this.children[name]) == null) {
            item = Object.create(this._conn.TopicOr, {
              _conn: { value: this._conn, writable: false, enumerable: false },
              name: { value: name },
              parent: { value: this },
            });
          }
          item.flags = arr[i][1];
          item.dataType = arr[i][2];
          nc[name] = item;
        }
      }
      if (nc == null && (this.flags & 16) == 16) {
        nc = {};
      }
      if (this.children != nc) {
        this.children = nc;
        mask |= 2;
      }
      this.postEvent(mask);
    },
    postEvent: function (mask) {
      var i, r;
      for (i = 0; this.reflexes!=null && i < this.reflexes.length; i++) {
        r = this.reflexes[i];
        if (r != null && (r._mask & mask) != 0 && typeof (r.onChange) == "function") {
          try {
            r.onChange.call(r, r, mask);
          } catch (err) {
            console.log(err);
          }
        }
      }

    },
    Topic: {
      Base: null,
      _mask: 0,
      onChange: null,
      get mask() {
        return this._mask;
      },
      set mask(m) {
        this._mask = m;
        var upd = this._mask & (this._mask ^ this.Base.mask);
        if (upd != 0) {
          this.Base.updateSubscriptions();
        }

      },
      get name() {
        return this.Base.name;
      },
      get path() {
        return this.Base.path;
      },
      get value() {
        return this.Base.value;
      },
      get children() {
        if (this.Base.children != null) {
          return Object.getOwnPropertyNames(this.Base.children);
        } else if ((this.Base.flags & 16) == 16) {
          return [];
        }
        return null;
      },
      get dataType() {
        return this.Base.dataType;
      },
      get flags() {
        return this.Base.flags;
      },

      getChild: function (name) {
        var c;
        if (this.Base.children != null && (c = this.Base.children[name]) != null) {
          return c.createReflex();
        }
        return null;
      },
      createReflex: function () { return this.Base.createReflex(); },
      dispose: function () {
        var pos = this.Base.reflexes.indexOf(this);
        if (pos >= 0) {
          this.Base.reflexes.splice(pos, 1);
        }
        console.log(this.path + "-[" + this.Base.reflexes.length + "]");
      },
    },
  },
}
