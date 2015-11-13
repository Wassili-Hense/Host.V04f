"use strict";
if (typeof (X13) !== "object") {
  var X13 = {};
}
X13.conn={
  _socket: null,
  _root: null,
  _draftRoot: null,
  _draftGroups: null,
  init: function () {
    this._socket = io((window.location.protocol == "https:" ? "wss://" : "ws://") + window.location.host
      , { "path": "/api/v04", "transports": ['websocket'] });
    this._socket.on('connect', X13.conn.handleConnect);
    this._socket.on('disconnect', function () { document.title = "OFFLINE" });
    this._root = Object.create(this.TopicOr, { _conn: { value: this, writable: false, enumerable: false }, name: { value: window.location.host } });
    var Self=this._root;
    this._socket.emit(4, "/", 0, function (arr) { Self.onDataResp(arr); });
  },
  handleConnect: function(){
    document.title = window.location.host;
    X13.conn._draftGroups = [];
    var dr = X13.conn.GetTopic("/etc/draft");
    dr.onChange = X13.conn.handleDraftRoot;
    dr.mask=2;
    X13.conn._draftRoot = dr;
  },
  handleDraftRoot: function (s, e) {
    var i, j, exist, ch;
    var gr = [], gro = X13.conn._draftGroups;
    var cn = s.children;
    for (i = 0; i < cn.length; i++) {
      exist = false;
      for (j = 0; j < gro.length; j++) {
        if (gro[j].name == cn[i]) {
          gr.push(gro[j]);
          gro.splice(j, 1);
          exist = true;
          break;
        }
      }
      if (exist) {
        continue;
      }
      ch = s.getChild(cn[i]);
      gr.push(ch);
      ch.mask = 2;
    }
    X13.conn._draftGroups = gr;
    for (i = gro.length - 1; i >= 0 ; i--) {
      gro[i].dispose();
    }
  },
  /*
  createTopic: function (path, callback) {
    this._socket.emit(8, path, callback);
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
  GetTopic: function (path) {
    var cur = this._root;
    var next;
    if (path != null && typeof(path)=="string") {
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
  GetDraft: function (name) {
    var rez = null;
    var i;
    if (typeof (name) != "string" || name.length == 0) {
      return null;
    }
    if ((i = name.indexOf("/")) >= 0) {
      var n1 = name.substr(0, i - 1);
      var n2=name.substr(i);
      for (i = this._draftGroups.length - 1; i >= 0; i--) {
        if (this._draftGroups[i].name == n1) {
          rez = this._draftGroups[i].getChild(n2);
          if (rez != null) {
            return rez;
          }
          break;
        }
      }
    } else {
      for (i = this._draftGroups.length - 1; i >= 0; i--) {
        rez = this._draftGroups[i].getChild(name);
        if (rez != null) {
          return rez;
        }
      }
    }
    return null;
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
    draft: null,
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
      for (i = 0; this.projections!=null && i < this.projections.length; i++) {
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
          this.draft = arr[i][2];
          if (arr[i].length == 4) {
            if (this.value != arr[i][3]) {
              this.value = arr[i][3];
              mask |= 1;
            }
          } else if (this.draft == null && this.value != null) {
            this.value=null;
            mask |= 1;
          }
          console.log("U "+this.path)
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
          item.draft = arr[i][2];
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
      var i, r;
      for (i = 0; this.projections!=null && i < this.projections.length; i++) {
        r = this.projections[i];
        if (r != null && (r._mask & mask) != 0 && typeof (r.onChange) == "function") {
          if (mode!=null && mode == (r == tpc)) {
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
      } else if(this.value==val) {  // set value failed, not changed
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
    set value(v){
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
    get draft() {
      return this.Base.draft;
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
