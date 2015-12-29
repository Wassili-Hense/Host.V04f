"use strict";
if (typeof (X13) !== "object") {
  var X13 = {};
}
if (typeof (X13.UI) !== "object") {
  X13.UI = {};
}
if (typeof (X13.PGE) !== "object") {
  X13.PGE = {};
}

X13.UI.PropertyGrid = React.createClass({
  displayName: "PropertyGrid",
  getInitialState: function () {
    var nt = X13.conn.GetTopic(this.props.path);
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
    var r = {};
    var val = this.state.topic.value;
    r.items = [];
    var ct = this.state.topic.schema;
    var schema = this.state.schema;
    if (schema != null) {
      if (schema.name.length > ct.length || ct.substr(ct.length - schema.name) != schema.name) {
        schema.dispose();
        schema = null;
      }
    }
    if (schema == null) {
      schema = X13.conn.GetSchema(ct);
      if (schema != null) {
        schema.onChange = this.schemaChanged;
        schema.mask = 1;
        r.schema = schema;
      }
    }
    if (schema != null && schema.value != null) {
      if (schema.value.enum != null && Array.isArray(schema.value.enum)) {
        ct = "enum";
      } else {
        ct = schema.value.view || schema.value.type || this.state.topic.schema;
      }
    }
    if (ct == "object" && val != null) {
      this.InspectObject(val, r.items, "value", 0, schema != null ? schema.value : null);
    } else {
      r.items.push({ id: "value", name: "", type: (schema != null && schema.value != null) ? schema.value : ct, st: 0, level: 0 });
    }
    this.setState(r);

  },
  schemaChanged: function (s, e) {
    if (e != 1) {
      return;
    }
    this.topicChanged(this.state.topic, 1);  // refill state.arr
  },
  componentWillUnmount: function () {
    this.state.topic.dispose();
  },
  InspectObject: function (obj, arr, id, level, lSchema) {
    var pr = Object.keys(obj);
    for (var i = 0; i < pr.length; i++) {
      if (level == 0 && pr[i] == "$schema") {
        continue;
      }
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
        var val = vo;
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
    var c, ct, i, cmp, cmpParam, arr = [];
    var lvl = -1;
    if (this.state.items != null) {
      for (i = 0; i < this.state.items.length; i++) {
        c = this.state.items[i];
        if (lvl >= 0 && lvl < c.level) {
          continue;
        }
        lvl = -1;
        cmpParam = { id: c.id, value: this.GetValue(c.id), onChange: this.valueChanged };

        if (c.type != null && typeof (c.type) == "object") {
          if (c.type.enum != null && Array.isArray(c.type.enum)) {
            ct = "enum";
          } else {
            ct = c.type.view || c.type.type;
          }
          cmpParam.schema = c.type;
        } else {
          ct = c.type;
        }
        if (X13.PGE.hasOwnProperty(ct)) {
          cmp = X13.PGE[ct];
        } else {
          cmp = X13.PGE.label;
        }
        var oc = function (self, id) { return function (event) { self.ItemClick(id); } };
        var bPos = c.st == 2 ? "-4px -4px" : (c.st == 3 ? "-36px -4px" : "-164px -68px");
        arr.push(React.DOM.tr({ key: "pG" + c.id, style: { border: "1px dotted black" } },
           React.DOM.td({ style: { paddingLeft: 10 + c.level * 5, paddingRight: 10 } },
             React.DOM.div(null,
               React.DOM.i({ style: { width: 24, height: 24, backgroundImage: 'url(/css/jstree-32px.png)', display: "inline-block", verticalAlign: "top", backgroundPosition: bPos, borderRight: "1px dotted black" }, onClick: oc(this, c.id) }),
               React.DOM.i({ style: { width: 24, height: 24, backgroundImage: 'url(/dt_icons/' + ct + '.png)', display: "inline-block", verticalAlign: "top", backgroundSize: 'auto', backgroundPosition: '50% 50%', backgroundRepeat: "no-repeat" } }),
               React.DOM.span(null, c.name)),
           React.DOM.td(null, React.createElement(cmp, cmpParam)))));
        if (c.st == 2) {
          lvl = c.level;
        }
      }
    }
    return React.DOM.table({ style: { border: "1px solid black", borderCollapse: "collapse" } },
      React.DOM.caption({ style: { background: "#DDDDDD", textAlign: "left" } },
        React.DOM.i({ style: { backgroundImage: 'url(/dt_icons/' + this.state.topic.schema + '.png)', width: 16, height: 16, display: "inline-block", backgroundPosition: '50% 50%' } }),
        this.props.path),
      React.DOM.tbody(null, arr));
  },
});
X13.PGE.label = React.createClass({
  displayName: 'PGE.Label',
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

X13.PGE.boolean = React.createClass({
  displayName: 'PGE.boolean',
  getInitialState: function () {
    return {
      value: this.props.value == true
    }
  },
  render: function () {
    return React.DOM.div(null,
      React.DOM.input({ type: "checkbox", checked: this.state.value, onChange: this._onChange, ref: "cbBool" }));
  },
  _onChange: function (e) {
    this.setState({
      value: this.refs.cbBool.getDOMNode().checked
    }, function () {
      if (this.props.onChange) {
        this.props.onChange(this.props.id, this.state.value);
      }
    });
  }
});

X13.PGE.number = React.createClass({
  // props.schema properties: minimum, maximum, multipleOf
  displayName: 'PGE.number',
  getDefaultProps: function () {
    return {
      schema: {
        multipleOf: 1
      }
    };
  },
  parse: function (value) {
    if (value === '') {
      return '';
    }
    if (value) {
      value = parseFloat(value);
      if (isNaN(value)) return '';
      if (this.props.schema != null) {
        if (typeof(this.props.schema.maximum) === "number" && value > this.props.schema.maximum)
          return this.props.schema.maximum;
        if (typeof(this.props.schema.minimum) === "number" && value < this.props.schema.minimum)
          return this.props.schema.minimum;
        if (typeof(this.props.schema.multipleOf) === "number" && this.props.schema.multipleOf > 0) {
          value = this.props.schema.multipleOf * Math.round(value / this.props.schema.multipleOf);
        }
      }
    }
    return value;
  },
  getInitialState: function () {
    var r = {
      value: this.parse(this.props.value),
    };
    if (this.props.schema != null) {
      var s = this.props.schema;
      if (s.multipleOf != null && typeof (s.multipleOf) == "number") {

      }
    }
    return r;
  },
  render: function () {
    var pr={
      className: this.props.className,
      type: "number",
      step: this.props.schema.multipleOf,
      value: this.state.value,
      onKeyUp: this._onKeyUp,
      onKeyDown: this._onKeyDown,
      onChange: this._onChange,
      onBlur: this._onBlur,
      onWheel: this._onWheel,
    };
    if (typeof(this.props.schema.maximum)==='number'){
      pr.max=this.props.schema.maximum;
    }
    if (typeof(this.props.schema.minimum) == 'number') {
      pr.min = this.props.schema.minimum;
    }
    return React.createElement("input", pr);
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
    var v = this.parse(this.state.value) + this.props.schema.multipleOf;
    if (typeof(this.props.schema.maximum)!='number' || v <= this.props.schema.maximum) {
      this.setState({ value: v });
    }
  },
  down: function () {
    var v = this.parse(this.state.value) - this.props.schema.multipleOf;
    if (typeof(this.props.schema.minimum)!= 'number' || v >= this.props.schema.minimum) {
      this.setState({ value: v });
    }
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

X13.PGE.string = React.createClass({
  // props.schema properties: maxLength, minLength, pattern
  displayName: "PGE.string",
  getInitialState: function () {
    var r={ value: this.props.value };
    if (this.props.schema != null && typeof(this.props.schema.pattern) == "string") {
      try{
        r.pattern=new RegExp(this.props.schema.pattern);
      }catch(e){
        Console.log("PGE.string."+this.props.id+".pattern("+this.props.schema.pattern+") ERR:"+e);
      }
    }
    return r;
  },
  onChange: function (event) {
    this.setState({ value: event.target.value });
  },
  change: function () {
    var v = this.state.value;
    if (this.props.onChange && this._check(v)) {
      this.props.onChange(this.props.id, v);
    }
  },
  render: function () {
    var pr = {
      type: "text",
      value: this.state.value,
      onChange: this.onChange,
      onKeyUp: this._onKeyUp,
      onBlur: this._onBlur
    };
    if (this.props.schema != null) {
      if (this.state.pattern != null) {
        pr.pattern = this.state.pattern.toString();
      }
      if (typeof (this.props.schema.maxLength) == "number" && this.props.schema.maxLength > 0) {
        pr.maxLength = this.props.schema.maxLength;
      }
    }
    return React.DOM.input(pr);
  },
  _onKeyUp: function (e) {
    if (e.keyCode === 13) {   // KEY_ENTER
      this.change();
    } else if (e.keyCode == 27) {  // ESC
      this.setState({ value: this.props.value });
    }
  },
  _onBlur: function (e) {
    this.change();
  },
  _check: function (v) {
    if (this.props.schema != null) {
      if (typeof (this.props.schema.maxLength) == "number" && typeof(v) == "string" && v.length > this.props.schema.maxLength) {
        return false;
      }
      if (typeof (this.props.schema.minLength) == "number" && ((typeof(v) != "string" && this.props.schema.minLength>0) || v.length < this.props.schema.minLength)) {
        return false;
      }
      if (this.state.pattern != null && !this.state.pattern.test(v)) {
        return false;
      }
    }
    return true;
  },
});

X13.PGE.Topic = React.createClass({
  displayName: "PGE.topic",
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
      opened[selected] = sv == 1 ? 3 : 2;
    } else {
      selected = null;
    }
    return {
      value: this.props.value,
      showTree: false,
      left: 16,
      top: 16,
      selected: null,
      opened: opened,
    };
  },
  onClickTree: function (event) {
    event = event || window.event
    var t = event.target || event.srcElement
    this.setState({ showTree: !this.state.showTree, left: t.offsetLeft, top: t.offsetTop });
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
  },
  onClickOk: function () {
    this.onSelect(this.state.selected == null ? null : this.state.selected.state.data.path);
  },
  onClickCancel: function () {
    this.onSelect(null);
  },
  componentWillUnmount: function () {
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
    var ts;
    if (this.state.showTree) {
      var maxWidth, maxHeight;
      maxWidth = window.innerWidth - this.state.left - 16;
      maxHeight = window.innerHeight - this.state.top - 16;
      ts = (
        React.DOM.div({
          style: {
            position: "fixed", top: this.state.top, left: this.state.left,
            minWidth: "20em", maxWidth: maxWidth, minHeight: "12em", maxHeight: maxHeight,
            border: "2px solid #E0E0E0", padding: "5px", background: "white",
          }
        },
          React.DOM.div({ className: "jstree", style: { margin: "0 0 2.1em", } },
            React.DOM.ul({ className: "jstree-container-ul jstree-children" },
              React.createElement(X13.UI.TreeNode, { key: 'tn/', parent: null, onTopicSelect: this.onTopicSelect, opened: this.state.opened })
            )
          ),
          React.DOM.div({ style: { position: "absolute", bottom: 0, left: 0, right: 0, padding: "0.3em 0", borderTop: "#E0E0E0 solid 2px", } },
            React.DOM.button({ style: { margin: "0 7%", width: "36%", heihgt: "1em", }, onClick: this.onClickOk }, "Ok"),
            React.DOM.button({ style: { margin: "0 7%", width: "36%", heihgt: "1em", }, onClick: this.onClickCancel }, "Cancel")
          )
        )
      );

    } else {
      ts = null;
    }
    return React.DOM.div(null,
        React.DOM.input({ type: "text", value: this.state.value, onChange: this.onChange }),
        React.DOM.button({ onClick: this.onClickTree }, "..."),
        ts
      );
  },
});

X13.UI.TreeNode = React.createClass({
  displayName: "TreeNode",
  getInitialState: function () {
    var d;
    if (this.props.parent == null) {
      d = X13.conn.GetTopic(null);
    } else {
      d = this.props.parent.getChild(this.props.name);
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
    if ((d.flags & 16) == 16) {
      if ((d.mask & 2) == 0) {
        d.mask = 2;
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
  onDataUpdated: function (s, e) {
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
          arr.push(React.createElement(X13.UI.TreeNode, { key: "tn" + d.path + "/" + children[i], parent: d, name: children[i], onTopicSelect: this.props.onTopicSelect, opened: this.props.opened }));
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
      if (ns != null && ns.length > 0) {
        isLast = ns[ns.length - 1] == d.name;
      }
    }
    if (isLast) {
      classes += " jstree-last";
    }
    var style;
    if (d.schema != null) {
      style = {
        backgroundImage: 'url(/dt_icons/' + d.schema + '.png)',
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
    if (id == "path" && value != null) {
      this.setState({ path: value });
    }
  },
  handleClick: function (event) {
    this.setState({ show: !this.state.show });
  },
  render: function () {
    return React.DOM.div(null,
      React.createElement(X13.PGE.Topic, { id: "path", value: this.state.path, onChange: this.valueChanged }),
      React.DOM.button({ onClick: this.handleClick }, "Edit"),
      (this.state.show ? React.createElement(X13.UI.PropertyGrid, { path: this.state.path }) : null)
      );
  }
});
