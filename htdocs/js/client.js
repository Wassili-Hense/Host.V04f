"use strict";
if (typeof (X13) !== "object") {
  var X13 = {};
}
X13.conn = {
  _socket: null,
  _root: null,
  _schemas: {},
  _views: {},
  _connectedCB: null,
  _connected: false,
  _connectCnt: 0,
  init: function (cb) {
    this._connectedCB = cb;
    this._socket = io((window.location.protocol == "https:" ? "wss://" : "ws://") + window.location.host
      , { "path": "/api/v04", "transports": ['websocket'] });
    this._socket.on('connect', X13.conn.handleConnect);
    this._socket.on('disconnect', function () { document.title = "OFFLINE" });
    this._root = Object.create(this.TopicOr, { _conn: { value: this, writable: false, enumerable: false }, name: { value: window.location.host } });
    var Self = this._root;
    this._socket.emit(4, "/", 0, function (arr) { Self.onDataResp(arr); });
  },
  handleConnect: function () {
    document.title = window.location.host;
    X13.conn._schemaGroups = [];
    X13.conn._viewGroups = [];
    X13.conn._connectCnt = 2;
    var dr = X13.conn.GetTopic("/etc/schema");
    dr.onChange = X13.conn.handleSchemaViewChild;
    dr.mask = 2;
    X13.conn._schemaRoot = dr;
    var dr2 = X13.conn.GetTopic("/etc/UI");
    dr2.onChange = X13.conn.handleSchemaViewChild;
    dr2.mask = 2;
    X13.conn._viewRoot = dr2;
  },
  handleSchemaViewChild: function (s, e) {
    var i, cn = s.children, ch;
    for (i = 0; i < cn.length; i++) {
      ch = s.getChild(cn[i]);
      if (ch.schema == "schema" && ch.path.slice(0, 12) == "/etc/schema/") {
        X13.conn._schemas[ch.path.substr(12)] = ch;
      } else if (ch.schema == "view" && ch.path.slice(0, 8) == "/etc/UI/") {
        X13.conn._views[ch.path.substr(8)] = ch;
      }
      if ((ch.flags & 16) != 0 && (ch.mask & 2)==0) {
        X13.conn._connectCnt++;
        ch.onChange = X13.conn.handleSchemaViewChild;
        ch.mask |= 2;
      }
    }
    X13.conn._connectCnt--;
    var cb = X13.conn._connectedCB;
    if (cb != null && X13.conn._connectCnt == 0 && X13.conn._connected == false) {
      X13.conn._connected = true;
      cb();
    }
  },
  GetTopic: function (path) {
    var cur = this._root;
    var next;
    if (path != null && typeof (path) == "string") {
      var pt = path.split("/");
      for (var i = 0; i < pt.length; i++) {
        if (pt[i] == "") {
          continue;
        }
        if (cur.children != null && (next = cur.children[pt[i]]) != null) {
          // do nothing
        } else {
          if (cur.children == null) {
            cur.children = {};
          }
          next = Object.create(X13.conn.TopicOr, {
            _conn: { value: X13.conn, writable: false, enumerable: false },
            name: { value: pt[i] },
            parent: { value: cur },
          });
          console.log("c " + next.path);
          cur.children[pt[i]] = next;
        }
        cur = next;
      }
    }
    return cur.createProjection();
  },
  GetSchema: function (name) {
    var rez = null;
    var i;
    if (typeof (name) != "string" || name.length == 0) {
      return null;
    }
    if ((i = name.indexOf("/"))==-1) {
      name = "type/" + name;
    }
    rez = X13.conn._schemas[name];
    if (rez != null && (rez.mask & 1)==0) {
      rez.mask |= 1;
    }
    return rez;
  },
  GetView: function (name) {
    var rez = null;
    rez = X13.conn._views[name];
    if (rez != null) {
      if ((rez.mask & 1) == 0) {
        rez.mask |= 1;
      }
      rez.GetView = X13.conn._getViewFunk;
      return rez;
    }
    return null;
  },
  _getViewFunk: function () {
    if (this.Base.view == null) {
      this.Base.preEvent = X13.conn._updateViewFunk;
      if (typeof (this.Base.value) == "object") {
        this.Base.preEvent.call(this.Base);
      }
    }
    return this.Base.view;
  },
  _updateViewFunk: function () {
    var cmpt = null;
    if (this.schema == "view") {
      try {
        if (typeof (this.value.code) == "string") {
          var o = eval("(" + this.value.code + ")");
          cmpt = React.createClass(o);
        }
      } catch (err) {
        console.log(err);
        cmpt = null;
      }
    }
    this.view = cmpt;
  },

  TopicOr: {
    projections: null,
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
    schema: null,
    value: null,

    createProjection: function () {
      var t = Object.create(this._conn.Topic, { Base: { value: this, writable: false, enumerable: false } });
      if (this.projections == null) {
        this.projections = [];
      }
      this.projections.push(t);
      console.log(t.path + "+[" + this.projections.length + "]");
      return t;
    },
    updateSubscriptions: function () {
      var nm = 0;
      var i, r;
      for (i = 0; this.projections != null && i < this.projections.length; i++) {
        r = this.projections[i];
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
          this.schema = arr[i][2];
          if (arr[i].length == 4) {
            if (this.value != arr[i][3]) {
              this.value = arr[i][3];
              mask |= 1;
            }
          } else if (this.schema == null && this.value != null) {
            this.value = null;
            mask |= 1;
          }
          console.log("U " + this.path)
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
            console.log("C " + item.path);
          } else {
            console.log("u " + item.path);
          }
          item.flags = arr[i][1];
          item.schema = arr[i][2];
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
      this.postEvent(mask, null, null);
    },
    postEvent: function (mask, mode, tpc) {
      var pev = this.preEvent;
      if (pev != null) {
        try {
          pev();
        } catch (err) {
          console.log(err);
        }
      }
      var i, r;
      for (i = 0; this.projections != null && i < this.projections.length; i++) {
        r = this.projections[i];
        if (r != null && (r._mask & mask) != 0 && typeof (r.onChange) == "function") {
          if (mode != null && mode == (r == tpc)) {
            continue;
          }
          try {
            r.onChange.call(r, r, mask);
          } catch (err) {
            console.log(err);
          }
        }
      }
    },
    setValue: function (valN, src) {
      var Self = this;
      this._conn._socket.emit(6, this.path, valN, function (success, valO) { Self.onValueResp(success, success ? valN : valO, src); });
    },
    onValueResp: function (success, val, src) {
      if (success) {
        this.value = val;
        this.postEvent(1, true, src);
      } else if (this.value == val) {  // set value failed, not changed
        this.postEvent(1, false, src);
      } else {                      // value changed
        this.postEvent(1, null, null);
      }
    },
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
    set value(v) {
      this.Base.setValue(v, this);
    },
    get children() {
      if (this.Base.children != null) {
        return Object.getOwnPropertyNames(this.Base.children);
      } else if ((this.Base.flags & 16) == 16) {
        return [];
      }
      return null;
    },
    get schema() {
      return this.Base.schema;
    },
    get flags() {
      return this.Base.flags;
    },

    getChild: function (name) {
      var c;
      if (this.Base.children != null && (c = this.Base.children[name]) != null) {
        return c.createProjection();
      }
      return null;
    },
    createProjection: function () { return this.Base.createProjection(); },
    dispose: function () {
      var pos = this.Base.projections.indexOf(this);
      if (pos >= 0) {
        this.Base.projections.splice(pos, 1);
      }
      console.log(this.path + "-[" + this.Base.projections.length + "]");
    },
  },
}
