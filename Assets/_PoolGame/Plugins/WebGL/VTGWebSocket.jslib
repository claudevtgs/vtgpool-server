// Browser WebSocket for VTG Pool 3D (WebGLWebSocketTransport). Messages are queued and polled from C#.
var VTGWebSocketLib = {
  $VTGWS: { sockets: {}, next: 1 },

  VTGWS_Connect: function (urlPtr) {
    var url = UTF8ToString(urlPtr);
    var id = VTGWS.next++;
    var entry = { queue: [], socket: null, failed: false };
    VTGWS.sockets[id] = entry;
    try {
      entry.socket = new WebSocket(url);
      entry.socket.onmessage = function (event) { if (typeof event.data === 'string') entry.queue.push(event.data); };
      entry.socket.onerror = function () { entry.failed = true; };
    } catch (e) {
      entry.failed = true;
    }
    return id;
  },

  VTGWS_State: function (id) {
    var entry = VTGWS.sockets[id];
    if (!entry || entry.failed || !entry.socket) return 3;
    return entry.socket.readyState;
  },

  VTGWS_Send: function (id, textPtr) {
    var entry = VTGWS.sockets[id];
    if (entry && entry.socket && entry.socket.readyState === 1) entry.socket.send(UTF8ToString(textPtr));
  },

  VTGWS_Close: function (id) {
    var entry = VTGWS.sockets[id];
    if (!entry) return;
    try { if (entry.socket) entry.socket.close(); } catch (e) {}
    delete VTGWS.sockets[id];
  },

  VTGWS_Next: function (id) {
    var entry = VTGWS.sockets[id];
    if (!entry || entry.queue.length === 0) return null;
    var text = entry.queue.shift();
    var size = lengthBytesUTF8(text) + 1;
    var buffer = _malloc(size);
    stringToUTF8(text, buffer, size);
    return buffer;
  }
};

autoAddDeps(VTGWebSocketLib, '$VTGWS');
mergeInto(LibraryManager.library, VTGWebSocketLib);
