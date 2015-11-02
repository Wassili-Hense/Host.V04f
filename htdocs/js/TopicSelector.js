"use strict";
var PropertyGrid = React.createClass({
  displayName: "PropertyGrid",
  getInitialState: function () {
    var val = this.props.value;
    var arr = [];
    var ct = typeof (val);
    if (ct == "boolean" || ct == "number" || ct == "string") {
      arr.push({ id: "value", name: "value", type: ct, st: 0, level: 0 });
    } else if (ct == "object") {
      if (val == null) {
        arr.push({ id: "value", name: "value", type: "string", level: 0, st: 2, readonly: true });
      } else {
        //Array.isArray(obj)
        this.InspectObject(val, arr, "value", 0);
      }
    } else {
      arr.push({ id: "value", name: "value", type: "string", level: 0, st: 0, readonly: true });
    }
    return { name: this.props.name, value: val, items: arr };
  },
  InspectObject: function (obj, arr, id, level) {
    var pr = Object.keys(obj);
    for (var i = 0; i < pr.length; i++) {
      var val = obj[pr[i]];
      var ct = typeof (val);
      var idn = id + "." + pr[i];
      if (ct == "boolean" || ct == "number" || ct == "string") {
        arr.push({ id: idn, name: pr[i], type: ct, level: level, st: 0 });
      } else if (ct == "object") {
        if (val == null) {
          arr.push({ id: idn, name: pr[i], type: "string", level: level, st: 0, readonly: true });
        } else {
          arr.push({ id: idn, name: pr[i], type: ct, level: level, st: 2 });
          this.InspectObject(val, arr, idn, level + 1);
        }
      } else {
        arr.push({ id: idn, name: pr[i], type: "string", level: level, st: 0, readonly: true });
      }
    }
  },
  valueChanged: function (id, value) {
    console.log(id + "=>" + JSON.stringify(value));
  },
  ItemClick: function (id) {
    for (var i = 0; i < this.state.items.length; i++) {
      if (this.state.items[i].id == id) {
        if (this.state.items[i].st == 2) {
          this.state.items[i].st = 3;
        } else if (this.state.items[i].st == 3) {
          this.state.items[i].st = 2;
        }
        this.setState({ items: this.state.items });
      }
    }
  },
  GetValue: function (id) {
    var lvls = id.split('.');
    var val = this.state;
    for (var i = 0; i < lvls.length; i++) {
      val = val[lvls[i]];
      if (val == null) {
        break;
      }
    }
    return val;

  },
  render: function () {
    var c, i, cmp, cmpParam, arr = [];
    var lvl = -1;
    for (i = 0; i < this.state.items.length; i++) {
      c = this.state.items[i];
      if (lvl >= 0 && lvl < c.level) {
        continue;
      }
      lvl = -1;
      cmpParam = { id: c.id, value: this.GetValue(c.id), onChange: this.valueChanged };
      if (c.type == "number") {
        cmp = PeNumber;
      } else if (c.type == "string") {
        cmp = PeString;
      } else if (c.type == "topic") {
        cmp = PeTopic;
      } else {
        cmp = PeString;
      }
      var oc = function (self, id) { return function (event) { self.ItemClick(id); } };
      var bPos = c.st == 2 ? "-4px -4px" : (c.st == 3 ? "-36px -4px" : "-164px -68px");
      arr.push(React.DOM.tr({ key: "pG" + c.id, style: { border: "1px dotted black" } },
         React.DOM.td({ style: { paddingLeft: 10 + c.level * 5, paddingRight: 10 } },
           React.DOM.div(null,
             React.DOM.i({ style: { width: 24, height: 24, backgroundImage: 'url(/css/jstree-32px.png)', display: "inline-block", verticalAlign: "top", backgroundPosition: bPos, borderRight: "1px dotted black" }, onClick: oc(this, c.id) }),
             React.DOM.i({ style: { width: 24, height: 24, backgroundImage: 'url(/dt_icons/' + c.type + '.png)', display: "inline-block", verticalAlign: "top", backgroundSize: 'auto', backgroundPosition: '50% 50%', backgroundRepeat: "no-repeat" } }),
             React.DOM.span(null, c.name)),
         React.DOM.td(null, React.createElement(cmp, cmpParam)))));
      if (c.st == 2) {
        lvl = c.level;
      }
    }
    return React.DOM.table({ style: { border: "1px solid black", borderCollapse: "collapse" } },
      React.DOM.caption({ style: {background: "#CCCCCC"} }, this.props.name),
      React.DOM.tbody(null, arr));
  },
});
var PeNumber = React.createClass({
  displayName: 'PeNumber',
  getDefaultProps: function () {
    return {
      step: 1
    };
  },
  parse: function (value) {
    if (value === '') return '';
    if (value) {
      value = parseFloat(value);
      if (isNaN(value)) return '';
    }

    if (typeof this.props.max === 'number' && value > this.props.max) return this.props.max;
    if (typeof this.props.min === 'number' && value < this.props.min) return this.props.min;

    if (this.props.step) {
      var p = (this.props.step.toString().split('.')[1] || []).length;
      if (p) return parseFloat(value.toFixed(p));
    }

    return value;
  },
  getInitialState: function () {
    return {
      value: this.parse(this.props.value)
    }
  },
  render: function () {
    return React.createElement("input", {
      className: this.props.className,
      type: "number",
      step: this.props.step,
      value: this.state.value,
      onKeyUp: this._onKeyUp,
      onKeyDown: this._onKeyDown,
      onChange: this._onChange,
      onBlur: this._onBlur,
      onWheel: this._onWheel,
    });
  },
  componentWillReceiveProps: function (nextProps) {
    this.setState({
      value: this.parse(nextProps.value)
    });
  },
  change: function (value) {
    if (this.props.onChange) {
      this.props.onChange(this.props.id, this.parse(value));
    }
  },
  up: function () {
    var v = this.parse(this.state.value) + this.props.step;
    this.setState({ value: v });
  },
  down: function () {
    var v = this.parse(this.state.value) - this.props.step;
    this.setState({ value: v });
  },
  _onKeyDown: function (e) {
    switch (e.keyCode) {
      case 38:    // KEY_UP
        e.preventDefault();
        this.up();
        break;
      case 40:    // KEY_DOWN
        e.preventDefault();
        this.down();
        break;
    }
  },
  _onKeyUp: function (e) {
    if (e.keyCode === 13) {   // KEY_ENTER
      this.change(this.state.value);
    }
  },
  _onWheel: function (e) {
    if (e.deltaY > 0) {
      e.preventDefault();
      this.down();
    } else if (e.deltaY < 0) {
      e.preventDefault();
      this.up();
    }
  },
  _onBlur: function (e) {
    this.change(this.state.value);
  },
  _onChange: function (e) {
    this.setState({
      value: e.target.value
    });
  }
});

var PeString = React.createClass({
  displayName: "PeString",
  getInitialState: function () {
    return { value: this.props.value };
  },
  onChange: function (event) {
    this.setState({ value: event.target.value });
  },
  change: function() {
    if (this.props.onChange) {
      this.props.onChange(this.props.id, this.state.value);
    }
  },
  render: function () {
    return React.DOM.input({
      type: "text",
      value: this.state.value,
      onChange: this.onChange,
      onKeyUp: this._onKeyUp,
      onBlur: this._onBlur
    });
  },
  _onKeyUp: function (e) {
    if (e.keyCode === 13) {   // KEY_ENTER
      this.change();
    }
  },
  _onBlur: function (e) {
    this.change();
  },

});

var PeTopic = React.createClass({
  displayName: "PeTopic",
  getInitialState: function () {
    return { value: this.props.value, showTree: false, left: 16, top: 16 };
  },
  onClickTree: function (event) {
    event = event || window.event
    var t = event.target || event.srcElement
    this.setState({ showTree: !this.state.showTree, left: t.offsetLeft, top: t.offsetTop });
  },
  onSelect: function (id) {
    var r = { showTree: false };
    if (id) {
      r.value = id;
    }
    this.setState(r);
    if (this.props.onChange) {
      this.props.onChange(this.props.id, id);
    }
  },
  onChange: function (event) {
    this.setState({ value: event.target.value });
    if (this.props.onChange) {
      this.props.onChange(this.props.id, event.target.value);
    }
  },
  _onBlur: function (e) {
    this.change(this.state.value);
  },
  render: function () {
    return React.DOM.div(null,
        React.DOM.input({ type: "text", value: this.state.value, onChange: this.onChange }),
        React.DOM.button({ onClick: this.onClickTree }, "..."),
        this.state.showTree ? React.createElement(TopicSelector, { left: this.state.left, top: this.state.top, callback: this.onSelect, selected: this.state.value }) : null
      );
  },
});

var TopicSelector = React.createClass({
  displayName: "TopicSelector",
  getInitialState: function () {
    var tmp = localStorage["TopicSelectorOpened"];
    var opened = tmp == null ? null : JSON.parse(tmp);
    if (opened == null || typeof opened != 'object' || !opened.hasOwnProperty("/")) {
      opened = { "/": 1 };
    }
    var selected = this.props.selected;
    if (selected) {
      opened[this.props.selected] = 2;
    } else {
      selected = null;
    }
    return {
      selected: null,
      root: {
        id: "/",
        name: "ROOT",
        flags: null,
        children: null,
        dataType: null,
      },
      opened: opened,
    };
  },
  onTopicSelect: function (node) {
    if (this.state.selected && this.state.selected.isMounted()) {
      this.state.selected.setState({ selected: false });
    }
    this.setState({ selected: node });
    node.setState({ selected: true });

    var This = this;
    servConn.GetValue(node.state.id, function (val) {
      This.setState({ selectedValue: val });
    });
  },
  onClickOk: function () {
    if (this.props.callback) {
      this.props.callback(this.state.selected == null ? null : this.state.selected.state.id);
    }
  },
  onClickCancel: function () {
    if (this.props.callback) {
      this.props.callback(null);
    }
  },
  componentWillUnmount: function () {
    localStorage["TopicSelectorOpened"] = JSON.stringify(this.state.opened);
  },
  render: function () {
    var top, left, maxWidth, maxHeight;
    if (typeof (this.props.left) == "number") {
      left = this.props.left;
    } else {
      left = window.innerWidth / 2;
    }
    if (typeof (this.props.top) == "number") {
      top = this.props.top;
    } else {
      top = 16;
    }
    maxWidth = window.innerWidth - left - 16;
    maxHeight = window.innerHeight - top - 16;

    return (
      React.DOM.div({ style: { position: "fixed", top: top, left: left, minWidth: "20em", maxWidth: maxWidth, maxHeight: maxHeight, border: "2px solid #E0E0E0", padding: "5px", minHeight: "12em", background: "white", } },
        React.DOM.div({ className: "jstree", },
          React.DOM.ul({ className: "jstree-container-ul jstree-children" },
            React.createElement(TreeNode, { key: 'tn/', parent: null, onTopicSelect: this.onTopicSelect, opened: this.state.opened, data: this.state.root })
          )
        ),
        React.DOM.div({ style: { margin: "3px -5px 2.1em", borderTop: "#E0E0E0 solid 2px", } },
          React.DOM.pre({ style: { margin: "5px", } }, this.state.selectedValue == null ? null : JSON.stringify(this.state.selectedValue, null, 2))
        ),
        React.DOM.div({ style: { position: "absolute", bottom: 0, left: 0, right: 0, padding: "0.3em 0", borderTop: "#E0E0E0 solid 2px", } },
          React.DOM.button({ style: { margin: "0 7%", width: "36%", heihgt: "1em", }, onClick: this.onClickOk }, "Ok"),
          React.DOM.button({ style: { margin: "0 7%", width: "36%", heihgt: "1em", }, onClick: this.onClickCancel }, "Cancel")
        )
      )
    );
  }
});

var TreeNode = React.createClass({
  displayName: "TreeNode",
  getInitialState: function () {
    var d = this.props.data;
    var s = {
      parent: this.props.parent,
      id: d.id,
      name: d.name,
      flags: d.flags,
      dataType: d.dataType,
      selected: false,
      opened: this.props.opened[d.id] == 1,
    };
    if ((d.flags & 16) == 16) {
      if (d.children != null) {
        s.children = d.children;
      } else {
        s.children = {};
      }
    } else {
      s.children = null;
    }
    return s;
  },
  componentDidMount: function () {
    if (this.props.opened[this.state.id] == 2) {
      delete this.props.opened[this.state.id];
      if (this.props.onTopicSelect) {
        this.props.onTopicSelect(this);
      }
    }
    if (this.state.flags == null) {
      servConn.dir(this.state.id, 3, this.onDataResp);
    } else if (this.state.opened && this.state.children != null && Object.getOwnPropertyNames(this.state.children).length == 0) {
      servConn.dir(this.state.id, 2, this.onDataResp);
    }
  },
  onTopicSelect: function (ev) {
    if (this.props.onTopicSelect) {
      this.props.onTopicSelect(this);
    }
    ev.preventDefault();
    ev.stopPropagation();
  },
  onChildDisplayToggle: function (ev) {
    if (this.state.children) {
      var isFilled = Object.getOwnPropertyNames(this.state.children).length > 0;
      if (!this.state.opened && !isFilled) {
        servConn.dir(this.state.id, 2, this.onDataResp);
      }
      if (!this.state.opened) {  // inverted
        this.props.opened[this.state.id] = 1;
      } else {
        delete this.props.opened[this.state.id];
      }
      this.setState({ opened: !this.state.opened });
    }
    ev.preventDefault();
    ev.stopPropagation();
  },
  onDataResp: function (arr) {
    if (!this.isMounted()) {
      return;
    }
    var rez = this.props.data;
    for (var i = 0; i < arr.length; i++) {
      if (arr[i][0] == this.state.id) {
        rez.flags = arr[i][1];
        rez.dataType = arr[i][2];
      } else {
        var id = arr[i][0];  // path
        var name = id == "/" ? "ROOT" : id.substr(id.lastIndexOf("/") + 1);
        var item;
        if (rez.children == null) {
          rez.children = {};
          item = {};
        } else {
          item = rez.children[name];
          if (item == null) {
            item = {};
          }
        }
        item.id = id;
        item.name = name;
        item.flags = arr[i][1];
        item.dataType = arr[i][2];
        rez.children[name] = item;
      }
    }
    this.setState(rez);
  },
  render: function () {
    var classes = 'jstree-node ';
    var chdom;
    if (this.state.children) {
      if (this.state.opened) {
        var arr = [];
        for (var n in this.state.children) {
          var child = this.state.children[n];
          arr.push(React.createElement(TreeNode, { key: "tn" + child.id, parent: this.props.data, onTopicSelect: this.props.onTopicSelect, opened: this.props.opened, data: child }));
        }
        chdom = React.DOM.ul({ role: "group", className: "jstree-children" }, arr);
        classes += "jstree-open";
      } else {
        chdom = null;
        classes += "jstree-closed";
      }
    } else {
      chdom = null;
      classes += "jstree-leaf";
    }
    var isLast = true;
    if (this.state.parent != null) {
      var ns = Object.getOwnPropertyNames(this.state.parent.children);
      if (ns.length > 0) {
        isLast = this.state.parent.children[ns[ns.length - 1]].id == this.state.id;
      }
    }
    if (isLast) {
      classes += " jstree-last";
    }
    var style;
    if (this.state.dataType != null) {
      style = {
        backgroundImage: 'url(/dt_icons/' + this.state.dataType + '.png)',
        backgroundSize: 'auto',
        backgroundPosition: '50% 50%'
      };
    } else {
      style = null;
    }
    return (
        React.DOM.li({ role: "treeitem", className: classes, onClick: this.onChildDisplayToggle },
          React.DOM.i({ role: "presentation", className: "jstree-icon jstree-ocl" }),
          React.DOM.a({ className: "jstree-anchor" + (this.state.selected ? " jstree-clicked" : ""), onClick: this.onTopicSelect, 'data-id': this.state.id },
            React.DOM.i({ role: "presentation", className: "jstree-icon jstree-themeicon", style: style }),
            React.DOM.span(null, this.state.name)), //{ style: { cursor: "pointer" } }
          chdom
        )
    );
  }
});
var PeTest = React.createClass({
  getInitialState: function () {
    return { path: "/", value: null, show: false };
  },
  valueChanged: function (id, value) {
    var r = {};
    r[id] = value;
    if (id == "path") {
      var This = this;
      servConn.GetValue(value, function (val) {
        This.setState({ value: val });
      });
    }
    this.setState(r);
  },
  handleClick: function (event) {
    this.setState({ show: !this.state.show });
  },
  render: function () {
    return React.DOM.div(null,
      React.createElement(PeTopic, { id: "path", value: this.state.path, onChange: this.valueChanged }),
      React.DOM.button({ onClick: this.handleClick }, "Edit"),
      (this.state.show ? React.createElement(PropertyGrid, { name: this.state.path, value: this.state.value }) : null)
      );
  }
});
