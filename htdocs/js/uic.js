"use strict";
X13.UI = {};

X13.UI.App = React.createClass({
  getInitialState: function () {
    return { path: "/", vt: null};
  },
  componentDidMount: function () {
    var nt = X13.conn.GetView("Topic");
    nt.onChange = this.vtChanged;
    nt.mask = 1;
    if (nt.value != null) {
      this.topicChanged(nt, 1);
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
/*
X13.UI.PropertyGrid = React.createClass({
  displayName: "PropertyGrid",
  getInitialState: function () {
    var nt = X13.conn.GetTopic(this.props.path, 1, this.topicChanged);
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
*/
