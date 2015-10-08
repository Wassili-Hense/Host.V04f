"use strict";

var TopicSelector = React.createClass({
  displayName: "TopicSelector",
  getInitialState: function () {
    return {
      selected: null,
      root: {
        id: "/",
        name: "ROOT",
        flags: null,
        children: null,
        dataType: null,
      }
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
  onClickOk: function(){
    if (this.props.callback) {
      this.props.callback(this.state.selected == null ? null : this.state.selected.state.id);
    }
  },
  onClickCancel: function(){
    if (this.props.callback) {
      this.props.callback(null);
    }
  },
  render: function () {
    var top = "1em";
    var left = "50%";
    if (typeof (this.props.left) == "number") {
      left = this.props.left;
    }
    if (typeof (this.props.top) == "number") {
      top = this.props.top;
    }

    return (
      React.DOM.div({ style: { position: "fixed", top: top, left: left, minWidth: "20%", border: "2px solid #E0E0E0", padding: "5px", minHeight: "20%", background: "white", } },
        React.DOM.div({ className: "jstree", },
          React.DOM.ul({ className: "jstree-container-ul jstree-children" },
            React.createElement(TreeNode, { key: 'tn/', parent: null, onTopicSelect: this.onTopicSelect, data: this.state.root })
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
/*
    '<div style="position: fixed; top: 1em; left: 50%; min-width: 20%; border: 2px solid #E0E0E0; padding: 5px; min-height: 20%; background:white;" >'
    + '<div id="SelectTopicTree" />'
    + '<div style=" margin:3px -5px 2.1em; border-top: #E0E0E0 solid 2px;"><pre id="SelectTopicValue" style="margin:5px;"></pre></div>'
    + '<div style="position:absolute; bottom:0; left: 0px; right: 0px; padding: 0.3em 0; border-top: #E0E0E0 solid 2px;"><button id="SelectTopicOk" style="margin:0 7%; width:36%; heihgt:1em;">Ok</button>'
    + '<button id="SelectTopicCancel" style="margin:0 7%; width:36%; heihgt:1em">Cancel</button></div>'
    + '</div>');

*/
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
      opened: false,
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
    if (this.state.flags == null) {
      servConn.dir(this.state.id, 3, this.onDataResp);
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
    // jstree-node jstree-open jstree-closed jstree-leaf jstree-last
    var classes = 'jstree-node ';
    var chdom;
    if (this.state.children) {
      if (this.state.opened) {
        var arr = [];
        for (var n in this.state.children) {
          var child = this.state.children[n];
          arr.push(React.createElement(TreeNode, { key: "tn" + child.id, parent: this.props.data, onTopicSelect: this.props.onTopicSelect, data: child }));
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
            //var at_icon = arr[i][2] == null ? null : "/dt_icons/" + arr[i][2] + ".png";
            // style="background-image: url(http://localhost/dt_icons/JSObject.png); background-size: auto; background-position: 50% 50%;"
            React.DOM.i({ role: "presentation", className: "jstree-icon jstree-themeicon", style: style }),
            React.DOM.span(null, this.state.name)), //{ style: { cursor: "pointer" } }
          chdom
        )
    );
  }
});
