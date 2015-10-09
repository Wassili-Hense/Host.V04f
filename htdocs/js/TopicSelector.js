"use strict";
var PropertyGrid = React.createClass({
  displayName: "PropertyGrid",
  getInitialState: function () {
    return { value: "value" };
  },
  render: function () {
    return React.DOM.div(null,
      React.DOM.table({ style: { border: "1px solid black", borderCollapse: "collapse" } },
        React.DOM.tr({ style: { border: "1px dotted red" } }, React.DOM.td(null, "name"), React.DOM.td(null, React.DOM.img({ src: "/dt_icons/String.png" })), React.DOM.td(null, this.state.value)),
        React.DOM.tr({ style: { border: "1px dotted red" } }, React.DOM.td(null, "name"), React.DOM.td(null, React.DOM.img({ src: "/dt_icons/String.png" })), React.DOM.td(null, this.state.value))
      )
    );
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
  },
  onChange: function (event) {
    this.setState({ value: event.target.value });
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
