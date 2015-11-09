"use strict";
var PropertyGrid = React.createClass({
  displayName: "PropertyGrid",
  getInitialState: function () {
    var nt = servConn.GetTopic(this.props.path);
    nt.onChange = this.topicChanged;
    nt.mask = 1;
    return { topic: nt, items: null };
  },
  componentDidMount: function () {
    this.topicChanged(this.state.topic, 1);
  },
  topicChanged: function (s, e) {
    if ((e & 1) != 1) {
      return;
    }
    var val = this.state.topic.value;
    var arr = [];
    var ct = typeof (val);
    if (ct == "boolean" || ct == "number" || ct == "string") {
      arr.push({ id: "value", name: "value", type: ct, st: 0, level: 0 });
    } else if (ct == "object") {
      if (val == null) {
        arr.push({ id: "value", name: "value", type: "object", level: 0, st: 0, readonly: true });
      } else {
        this.InspectObject(val, arr, "value", 0);
      }
    } else {
      arr.push({ id: "value", name: "value", type: "object", level: 0, st: 0, readonly: true });
    }
    this.setState({ topic: s, items: arr });

  },
  componentWillUnmount: function () {
    this.state.topic.dispose();
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
          arr.push({ id: idn, name: pr[i], type: "object", level: level, st: 0, readonly: true });
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
    var vo = this.state.topic.value;
    var changed = false;
    if (id == "value") {
      if (vo !== value) {
        vo = value;
        changed = true;
      }
    } else {
      var lvls = id.split('.');
      if (lvls.length > 1 && lvls[0] == "value") {
        var val=vo;
        for (var i = 1; i < lvls.length; i++) {
          if (i == lvls.length - 1) {
            if (val[lvls[i]] !== value) {
              val[lvls[i]] = value;
              changed = true;
            }
            break;
          }
          if (!val.hasOwnProperty(lvls[i])) {
            val[lvls[i]] = {};
            changed = true;
          }
          val = val[lvls[i]];
        }
      }
    }
    if (changed) {
      this.state.topic.value = vo;
      console.log(id + "=>" + JSON.stringify(value) + "\n" + JSON.stringify(vo));
    } else {
      console.log(id + "=>" + JSON.stringify(value) + "\t not changed");
    }
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
    var val = this.state.topic.value;
    for (var i = 1; i < lvls.length; i++) {
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
    if (this.state.items != null) {
      for (i = 0; i < this.state.items.length; i++) {
        c = this.state.items[i];
        if (lvl >= 0 && lvl < c.level) {
          continue;
        }
        lvl = -1;
        cmpParam = { id: c.id, value: this.GetValue(c.id), onChange: this.valueChanged };
        if (c.type == "boolean") {
          cmp = PeBool;
        } else if (c.type == "number") {
          cmp = PeNumber;
        } else if (c.type == "string") {
          cmp = PeString;
        } else if (c.type == "topic") {
          cmp = PeTopic;
        } else {
          cmp = PeLabel;
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
    }
    return React.DOM.table({ style: { border: "1px solid black", borderCollapse: "collapse" } },
      React.DOM.caption({ style: { background: "#CCCCCC" } }, this.props.path),
      React.DOM.tbody(null, arr));
  },
});

var PeLabel = React.createClass({
  displayName: 'PeLabel',
  getInitialState: function () {
    var v = this.props.value;
    if (v == null) {
      v = "null";
    } else if (typeof (v) == "object") {
      var d = v["declarer"];
      if (d != null && typeof (d) == "string") {
        v = d;
      } else {
        v = "";
      }
    }
    return { value: v };
  },
  render: function () {
    return React.DOM.label(null, this.state.value);
  }
});

var PeBool = React.createClass({
  displayName: 'PeBool',
  getInitialState: function () {
    return {
      value: this.props.value == true
    }
  },
  render: function () {
    return React.DOM.div(null,
      React.DOM.input({ type: "checkbox", checked: this.state.value, onChange: this._onChange, ref:"cbBool" }));
  },
  _onChange: function (e) {
    this.setState({
      value: this.refs.cbBool.getDOMNode().checked
    }, function(){
      if (this.props.onChange) {
        this.props.onChange(this.props.id, this.state.value);
      }
    });
  }
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
  change: function () {
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
    var sv;
    if (selected) {
      sv = opened[selected]
      opened[selected] = sv==1?3:2;
    } else {
      selected = null;
    }
    return {
      selected: null,
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
    if (this.state.selectedTopic != null) {
      this.state.selectedTopic.dispose();
    }
    var nst = node.state.data.createReflex();
    nst.onChange=function (s, e) {
      This.setState({ selectedValue: s.value });
    };
    nst.mask = 1;
    this.setState({ selectedValue: nst.value, selectedTopic: nst });
  },
  onClickOk: function () {
    if (this.props.callback) {
      this.props.callback(this.state.selected == null ? null : this.state.selected.state.data.path);
    }
  },
  onClickCancel: function () {
    if (this.props.callback) {
      this.props.callback(null);
    }
  },
  componentWillUnmount: function () {
    if (this.state.selectedTopic != null) {
      this.state.selectedTopic.dispose();
    }
    var op = this.state.opened;
    var on = {};
    for (var r in op) {
      if ((op[r] & 1) == 1) {
        on[r] = 1;
      }
    }
    localStorage["TopicSelectorOpened"] = JSON.stringify(on);
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
            React.createElement(TreeNode, { key: 'tn/', parent: null, onTopicSelect: this.onTopicSelect, opened: this.state.opened })
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
    var d;
    if (this.props.parent == null) {
      d = servConn.GetTopic(null);
    } else {
      d=this.props.parent.getChild(this.props.name);
    }
    d.onChange = this.onDataUpdated;
    var s = {
      data: d,
      selected: (this.props.opened[d.path] & 2) == 2,
      opened: (this.props.opened[d.path] & 1) == 1,
    };
    return s;
  },
  componentDidMount: function () {
    if (this.state.selected && this.props.onTopicSelect) {
      this.props.onTopicSelect(this);
    }
    if (this.state.opened) {
      this.state.data.mask = 2;
    }
  },
  componentWillUnmount: function () {
    this.state.data.dispose();
  },
  onTopicSelect: function (ev) {
    if (this.props.onTopicSelect) {
      this.props.onTopicSelect(this);
    }
    ev.preventDefault();
    ev.stopPropagation();
  },
  onChildDisplayToggle: function (ev) {
    var d = this.state.data;
    if ((d.flags & 16) == 16){
      if((d.mask & 2)==0){
        d.mask=2;
      }
      if (!this.state.opened) {  // inverted
        this.props.opened[d.path] = 1;
      } else {
        delete this.props.opened[d.path];
      }
      this.setState({ opened: !this.state.opened });
    }
    ev.preventDefault();
    ev.stopPropagation();
  },
  onDataUpdated : function(s, e){
    if (!this.isMounted()) {
      return;
    }
    this.setState({ data: s });
  },
  render: function () {
    var classes = 'jstree-node ';
    var chdom;
    var d = this.state.data;
    var children = d.children;
    if (children) {
      if (this.state.opened) {
        var arr = [];
        for (var i = 0; i < children.length; i++) {
          arr.push(React.createElement(TreeNode, { key: "tn" + d.path+"/"+children[i], parent: d, name: children[i], onTopicSelect: this.props.onTopicSelect, opened: this.props.opened }));
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
    if (this.props.parent != null) {
      var ns = this.props.parent.children;
      if (ns!=null && ns.length > 0) {
        isLast = ns[ns.length - 1] == d.name;
      }
    }
    if (isLast) {
      classes += " jstree-last";
    }
    var style;
    if (d.dataType != null) {
      style = {
        backgroundImage: 'url(/dt_icons/' + d.dataType + '.png)',
        backgroundSize: 'auto',
        backgroundPosition: '50% 50%'
      };
    } else {
      style = null;
    }
    return (
        React.DOM.li({ role: "treeitem", className: classes, onClick: this.onChildDisplayToggle },
          React.DOM.i({ role: "presentation", className: "jstree-icon jstree-ocl" }),
          React.DOM.a({ className: "jstree-anchor" + (this.state.selected ? " jstree-clicked" : ""), onClick: this.onTopicSelect, 'data-id': d.path },
            React.DOM.i({ role: "presentation", className: "jstree-icon jstree-themeicon", style: style }),
            React.DOM.span(null, d.name)), //{ style: { cursor: "pointer" } }
          chdom
        )
    );
  }
});

var PeTest = React.createClass({
  getInitialState: function () {
    return { path: "/", show: false };
  },
  valueChanged: function (id, value) {
    if (id=="path" && value != null) {
      this.setState({ path: value });
    }
  },
  handleClick: function (event) {
    this.setState({ show: !this.state.show });
  },
  render: function () {
    return React.DOM.div(null,
      React.createElement(PeTopic, { id: "path", value: this.state.path, onChange: this.valueChanged }),
      React.DOM.button({ onClick: this.handleClick }, "Edit"),
      (this.state.show ? React.createElement(PropertyGrid, { path: this.state.path }) : null)
      );
  }
});
