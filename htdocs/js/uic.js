"use strict";
X13.UI = {};

X13.UI.App = React.createClass({
  getInitialState: function () {
    return { path: "/", vt: null};
  },
  componentDidMount: function () {
    var nt = X13.GetView("PropertyGrid/Topic");
    nt.onChange = this.vtChanged;
    nt.mask = 1;
    if (nt.value != null) {
      this.vtChanged(nt, 1);
    }
  },
  vtChanged: function (s, e) {
    if ((e & 1) != 1) {
      return;
    }
    this.setState({ "vt": s });
  },
  valueChanged: function (id, value) {
    if (id == "path" && value != null) {
      this.setState({ path: value });
    }
  },
  render: function () {
    /*
    return React.DOM.div(null,
      React.createElement(X13.PGE.Topic, { id: "path", value: this.state.path, onChange: this.valueChanged }),
      React.DOM.button({ onClick: this.handleClick }, "Edit"),
      (this.state.show ? React.createElement(X13.UI.PropertyGrid, { path: this.state.path }) : null)
      );*/
    var tc = this.state.vt!=null?this.state.vt.GetView():null;
    return React.DOM.div(null,
        tc!=null?React.createElement(tc, { id: "path", value: this.state.path, onChange: this.valueChanged }):null
      );
  }
});

X13.UI.TreeNode = React.createClass({
  displayName: "TreeNode",
  getInitialState: function () {
    var d;
    if (this.props.parent == null) {
      d = X13.GetTopic(null);
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

X13.RegisterView("PropertyGrid/Topic", {
  displayName: "PropertyGrid/Topic",
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
