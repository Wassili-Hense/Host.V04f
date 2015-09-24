function ApiV04Ctor() {
  window.API = {};
  API.socket = io((window.location.protocol == "https:" ? "wss://" : "ws://") + window.location.host
      , { "path": "/api/v04", "transports": ['websocket'] });
  API.socket.on('connect', function () { document.title = window.location.host; });
  API.socket.on('disconnect', function () { document.title = "OFFLINE" });
}
function SelectTopic(cb, left, top) {
  var rez_path = null;
  var UpdateNodeId = function (inst, node) {
    inst.set_id(node, node.parent + "/" + node.text);
    node.data = null;
    for (var i = 0; i < node.children.length; i++) {
      UpdateNodeId(inst, inst.get_node(node.children[i]));
    }
  }
  var tree_cont = $(
    '<div style="position: fixed; top: 1em; left: 50%; min-width: 20%; border: 2px solid #E0E0E0; padding: 5px; min-height: 20%; background:white;" >'
    + '<div id="SelectTopicTree" />'
    + '<div style=" margin:3px -5px 2.1em; border-top: #E0E0E0 solid 2px;"><pre id="SelectTopicValue" style="margin:5px;"></pre></div>'
    + '<div style="position:absolute; bottom:0; left: 0px; right: 0px; padding: 0.3em 0; border-top: #E0E0E0 solid 2px;"><button id="SelectTopicOk" style="margin:0 7%; width:36%; heihgt:1em;">Ok</button>'
    + '<button id="SelectTopicCancel" style="margin:0 7%; width:36%; heihgt:1em">Cancel</button></div>'
    + '</div>');
  if (typeof(left) == "number") {
    tree_cont.css("left", left);
  }
  if (typeof (top) == "number") {
    tree_cont.css("top", top);
  }
  tree_cont.find("#SelectTopicOk").click(function () {
    tree_cont.remove();
    if (cb) {
      cb(rez_path);
    }
  });
  tree_cont.find("#SelectTopicCancel").click(function () {
    tree_cont.remove();
    if (cb) {
      cb(null);
    }
  });
  tree_cont.find("#SelectTopicTree").jstree({
    "core": {
      "data": function (node, cb) {
        var path = node.id === "#" ? "/" : node.id;
        API.socket.emit(9, path, node.data == null ? 3 : 2, function (arr) {  // arr [ [path, flags, icon url], ... ]
          var rez = [];
          for (var i = 0; i < arr.length; i++) {
            if (arr[i][0] == path) {
              var inst= $.jstree.reference(node);
              node.data = arr[i][1];
              var at_icon = arr[i][2] == null ? null : "/dt_icons/"+arr[i][2]+".png";
              inst.set_icon(node, at_icon);
            } else {
              var item = {};
              item.id = arr[i][0];  // path
              var idx = item.id.lastIndexOf("/");
              if (idx == 0) {
                item.text = item.id.substr(1);
              } else {
                item.text = item.id.substr(idx + 1);
              }
              item.children = (arr[i][1] & 16) == 16;
              item.data = arr[i][1];
              item.icon = arr[i][2] == null ? null : "/dt_icons/" + arr[i][2] + ".png";
              rez.push(item);
            }
          }
          cb(rez);
        });
      },
      "check_callback": true
    },
    "contextmenu": {
      "items": {
        "cut": {
          "separator_before": false,
          "separator_after": false,
          "label": "Cut",
          "_disabled": function (data) { return ($.jstree.reference(data.reference).get_node(data.reference).data & 8) != 8; },
          "action": function (data) {
            var inst = $.jstree.reference(data.reference),
                obj = inst.get_node(data.reference);
            if (inst.is_selected(obj)) {
              inst.cut(inst.get_top_selected());
            }
            else {
              inst.cut(obj);
            }
          }
        },
        "copy": {
          "separator_before": false,
          "icon": false,
          "separator_after": false,
          "label": "Copy",
          "action": function (data) {
            var inst = $.jstree.reference(data.reference),
                obj = inst.get_node(data.reference);
            if (inst.is_selected(obj)) {
              inst.copy(inst.get_top_selected());
            }
            else {
              inst.copy(obj);
            }
          }
        },
        "paste": {
          "separator_before": false,
          "separator_after": true,
          "icon": false,
          "_disabled": function (data) {
            var inst = $.jstree.reference(data.reference);
            return !(inst.can_paste() && ((inst.get_node(data.reference).data & 2) == 2));
          },
          "label": "Paste",
          "action": function (data) {
            var inst = $.jstree.reference(data.reference),
                obj = inst.get_node(data.reference);
            inst.paste(obj);
          }
        },
        "create": {
          "separator_before": false,
          "separator_after": false,
          "_disabled": function (data) { return ($.jstree.reference(data.reference).get_node(data.reference).data & 2) != 2; },
          "label": "Create",
          "action": function (data) {
            var inst = $.jstree.reference(data.reference),
                obj = inst.get_node(data.reference);
            inst.create_node(obj, { 'id': obj.id + "/" + Math.random().toString(36).slice(2), 'text': "" }, "last", function (new_node) {
              setTimeout(function () { inst.edit(new_node); }, 0);
            });
          }
        },
        "remove": {
          "separator_before": false,
          "icon": false,
          "separator_after": false,
          "_disabled": function (data) { return ($.jstree.reference(data.reference).get_node(data.reference).data & 8) != 8; },
          "label": "Delete",
          "action": function (data) {
            var inst = $.jstree.reference(data.reference),
                obj = inst.get_node(data.reference);
            if (inst.is_selected(obj)) {
              inst.delete_node(inst.get_selected());
            }
            else {
              inst.delete_node(obj);
            }
          }
        },
        "rename": {
          "separator_before": false,
          "separator_after": false,
          "_disabled": function (data) { return ($.jstree.reference(data.reference).get_node(data.reference).data & 6) != 6; },
          "label": "Rename",
          "action": function (data) {
            var inst = $.jstree.reference(data.reference),
                obj = inst.get_node(data.reference);
            inst.edit(obj);
          }
        },
      }
    },
    "plugins": ["contextmenu", "sort", "state"]
  })
  .on("create_node.jstree", function (e, data) {
    if (data.node.text == "") {
      return;
    }
    var inst = $.jstree.reference(data.reference);
    var nid = data.parent + "/" + data.node.text;
    API.socket.emit(8, nid, function (succes) {
      if (succes === true) {
        inst.set_id(data.node, nid);
      } else {
        inst.delete_node(data.node);
      }
    });
  })
  .on("rename_node.jstree", function (e, data) {
    var inst = data.instance;
    if (data.old == "") {
      if (data.text == "") {
        inst.delete_node(data.node);
      } else {
        var nid = data.node.parent + "/" + data.text;
        API.socket.emit(8, nid, function (succes) {
          if (succes === true) {
            inst.set_id(data.node, nid);
            setTimeout(function () { inst.refresh_node(data.node); }, 120);
          } else {
            inst.delete_node(data.node);
          }
        });
      }
    } else {
      UpdateNodeId(inst, data.node);
      API.socket.emit(12, data.node.parent + "/" + data.old, data.node.parent, data.text);
      setTimeout(function () { inst.refresh_node(data.node); }, 120);
    }
  })
  .on("delete_node.jstree", function (e, data) {
    API.socket.emit(10, data.node.id);
  })
  .on("copy_node.jstree", function (e, data) {
    var inst = data.instance;
    UpdateNodeId(inst, data.node);
    API.socket.emit(11, data.original.id, data.node.parent);
    setTimeout(function () { inst.refresh_node(data.node); }, 120);
  })
  .on("move_node.jstree", function (e, data) {
    var inst = data.instance;
    UpdateNodeId(inst, data.node);
    API.socket.emit(12, data.old_parent + "/" + data.node.text, data.node.parent);
    setTimeout(function () { inst.refresh_node(data.node); }, 120);
  })
  .on("changed.jstree", function (e, data) {
    var arr = data.selected;
    if (arr.length > 0) {
      rez_path = arr[0];
    } else {
      rez_path = null;
    }
    if (rez_path == null) {
      tree_cont.find("#SelectTopicValue").html("null");
    } else {
      API.socket.emit(13, rez_path, function (val) {
        tree_cont.find("#SelectTopicValue").html(val==null?"":JSON.stringify(val, null, 2));
      });
    }
  });
  $('body').append(tree_cont);
}
ApiV04Ctor();