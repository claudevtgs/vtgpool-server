'use strict';
// VTG Pool 3D room server: WebSocket relay with 6-character room codes. No dependencies (Node >= 18).
//
// Rooms hold 2-4 players: singles (each for themselves; 3-4 players play 9-ball rotation) or doubles (2v2, seats
// alternate teams: 0 & 2 vs 1 & 3). A full room starts by itself; the host may start a free-for-all early.
//
// Client -> server (JSON text frames):
//   {"t":"create","name":"An","mode":0,"players":2,"teams":false}  create a room (mode: 0 = 8-ball, 1 = 9-ball)
//   {"t":"join","code":"K7P2QX","name":"Binh"}                       join a room
//   {"t":"start"}                                                    host: start with the players present
//   {"t":"relay","d":"<string>"}                                     forward a game message to everyone else
//   {"t":"leave"}   {"t":"ping"}
// Server -> client:
//   {"t":"created","code":..,"seat":0,..}   {"t":"joined","code":..,"seat":n,..}
//   {"t":"peer_left","seat":n,"name":..,"hostSeat":n,"closed":bool}  (match goes on while 2+ remain)
//   client -> server {"t":"watch","code":..,"name":..}  (spectator) -> {"t":"watching",..}, {"t":"start","seat":-1,"role":"spectator",..}
//   client -> server {"t":"sync"} (spectator scene ready) -> host gets relay {"type":"sync"} from -1
//   {"t":"audience","count":n,"names":[..]}
//   {"t":"lobby","names":[..],"capacity":4,"teams":true,"mode":1}
//   {"t":"start","seat":n,"role":"host"|"guest","names":[..],"host":..,"guest":..,"mode":0,"teams":false,"players":n}
//   {"t":"relay","d":"<string>","from":seat}   {"t":"peer_left","seat":n,"name":..}
//   {"t":"error","m":"room_not_found"|"room_full"|"not_enough_players"|"bad_request"}   {"t":"pong"}
//
// The game itself is host-authoritative; this server only pairs players and forwards messages.

const http = require('http');
const crypto = require('crypto');
const fs = require('fs');
const path = require('path');

const PORT = parseInt(process.env.PORT || argValue('--port') || '8765', 10);
// Cloud hosts (Render, Koyeb, ...) route traffic from outside the container: listen on all interfaces there.
const HOST = process.env.HOST || argValue('--host') || (process.env.RENDER || process.env.KOYEB_APP_NAME ? '0.0.0.0' : '127.0.0.1');
const PATH = process.env.WS_PATH || argValue('--path') || '/ws';
const MAX_MESSAGE = 64 * 1024;
const ROOM_IDLE_MS = 30 * 60 * 1000;
const CODE_ALPHABET = 'ABCDEFGHJKLMNPQRSTUVWXYZ23456789'; // no 0/O/1/I
// Optional: also serve the WebGL build (e.g. --static ../Builds/WebGL) so page and rooms share one address.
const STATIC_DIR = process.env.STATIC_DIR || argValue('--static');
const MIME = {
  '.html': 'text/html; charset=utf-8', '.js': 'application/javascript', '.css': 'text/css', '.json': 'application/json',
  '.png': 'image/png', '.ico': 'image/x-icon', '.wasm': 'application/wasm', '.data': 'application/octet-stream',
  '.unityweb': 'application/octet-stream', '.gz': 'application/octet-stream', '.br': 'application/octet-stream'
};

const rooms = new Map(); // code -> { host, guest, mode, touched }

function argValue(name) {
  const i = process.argv.indexOf(name);
  return i >= 0 ? process.argv[i + 1] : undefined;
}

function log(...args) {
  console.log(new Date().toISOString(), ...args);
}

// ---------------------------------------------------------------- WebSocket framing (RFC 6455, text only)

function acceptKey(key) {
  return crypto.createHash('sha1').update(key + '258EAFA5-E914-47DA-95CA-C5AB0DC85B11').digest('base64');
}

function encodeFrame(opcode, payload) {
  const length = payload.length;
  let header;
  if (length < 126) {
    header = Buffer.from([0x80 | opcode, length]);
  } else if (length < 65536) {
    header = Buffer.alloc(4);
    header[0] = 0x80 | opcode;
    header[1] = 126;
    header.writeUInt16BE(length, 2);
  } else {
    header = Buffer.alloc(10);
    header[0] = 0x80 | opcode;
    header[1] = 127;
    header.writeBigUInt64BE(BigInt(length), 2);
  }
  return Buffer.concat([header, payload]);
}

class Connection {
  constructor(socket) {
    this.socket = socket;
    this.buffer = Buffer.alloc(0);
    this.fragments = [];
    this.room = null;
    this.name = 'Player';
    this.alive = true;
    this.closed = false;
    socket.setNoDelay(true);
    socket.on('data', (chunk) => this.onData(chunk));
    socket.on('close', () => this.onClose());
    socket.on('error', () => this.onClose());
  }

  send(object) {
    if (this.closed) return;
    this.socket.write(encodeFrame(0x1, Buffer.from(JSON.stringify(object), 'utf8')));
  }

  close(code = 1000) {
    if (this.closed) return;
    const payload = Buffer.alloc(2);
    payload.writeUInt16BE(code, 0);
    try { this.socket.write(encodeFrame(0x8, payload)); } catch (e) { /* ignore */ }
    this.socket.end();
    this.onClose();
  }

  onData(chunk) {
    this.buffer = Buffer.concat([this.buffer, chunk]);
    while (this.buffer.length >= 2) {
      const first = this.buffer[0];
      const second = this.buffer[1];
      const fin = (first & 0x80) !== 0;
      const opcode = first & 0x0f;
      const masked = (second & 0x80) !== 0;
      let length = second & 0x7f;
      let offset = 2;
      if (length === 126) {
        if (this.buffer.length < 4) return;
        length = this.buffer.readUInt16BE(2);
        offset = 4;
      } else if (length === 127) {
        if (this.buffer.length < 10) return;
        length = Number(this.buffer.readBigUInt64BE(2));
        offset = 10;
      }
      if (length > MAX_MESSAGE) { this.close(1009); return; }
      if (!masked) { this.close(1002); return; } // clients must mask
      if (this.buffer.length < offset + 4 + length) return;
      const mask = this.buffer.subarray(offset, offset + 4);
      const payload = Buffer.from(this.buffer.subarray(offset + 4, offset + 4 + length));
      for (let i = 0; i < payload.length; i++) payload[i] ^= mask[i & 3];
      this.buffer = this.buffer.subarray(offset + 4 + length);

      if (opcode === 0x8) { this.close(); return; }
      if (opcode === 0x9) { this.socket.write(encodeFrame(0xA, payload)); continue; }
      if (opcode === 0xA) { this.alive = true; continue; }
      if (opcode === 0x1 || opcode === 0x0) {
        this.fragments.push(payload);
        if (!fin) continue;
        const text = Buffer.concat(this.fragments).toString('utf8');
        this.fragments = [];
        this.alive = true;
        this.onMessage(text);
      }
    }
  }

  onMessage(text) {
    let message;
    try { message = JSON.parse(text); } catch (e) { this.send({ t: 'error', m: 'bad_request' }); return; }
    switch (message.t) {
      case 'ping': this.send({ t: 'pong' }); break;
      case 'create': createRoom(this, message); break;
      case 'join': joinRoom(this, message); break;
      case 'relay': relay(this, message); break;
      case 'leave': leaveRoom(this); break;
      case 'watch': watchRoom(this, message); break;
      case 'sync': requestSync(this); break;
      case 'start': requestStart(this); break;
      default: this.send({ t: 'error', m: 'bad_request' });
    }
  }

  onClose() {
    if (this.closed) return;
    this.closed = true;
    leaveRoom(this);
  }
}

// ---------------------------------------------------------------- rooms

function cleanName(name) {
  const text = typeof name === 'string' ? name.trim() : '';
  return (text || 'Player').slice(0, 24);
}

function newCode() {
  for (;;) {
    let code = '';
    const bytes = crypto.randomBytes(6);
    for (let i = 0; i < 6; i++) code += CODE_ALPHABET[bytes[i] % CODE_ALPHABET.length];
    if (!rooms.has(code)) return code;
  }
}

function seatsTaken(room) {
  return room.seats.filter((c) => c).length;
}

function lobbyMessage(room) {
  return { t: 'lobby', code: room.code, names: room.seats.map((c) => (c ? c.name : '')), capacity: room.capacity, teams: room.teams, mode: room.mode,
    spectators: room.spectators.length };
}

function broadcast(room, object, except) {
  for (const seat of room.seats) {
    if (seat && seat !== except) seat.send(object);
  }

  for (const watcher of room.spectators) {
    if (watcher !== except) watcher.send(object);
  }
}

const MAX_SPECTATORS = 16;

function audienceMessage(room) {
  return { t: 'audience', count: room.spectators.length, names: room.spectators.map((c) => c.name) };
}

function startMessageFor(room, seat) {
  const names = room.seats.map((c) => (c ? c.name : ''));
  return { t: 'start', code: room.code, seat, role: seat === 0 ? 'host' : seat < 0 ? 'spectator' : 'guest', names, host: names[0], guest: names[1] || '',
    mode: room.mode, teams: room.teams, players: names.length, hostSeat: room.hostSeat || 0 };
}

// A spectator's scene is ready: ask the host for the table (it answers with a state only spectators apply).
function requestSync(connection) {
  const room = connection.room ? rooms.get(connection.room) : null;
  if (!room || !connection.spectator || !room.started) return;
  const host = room.seats[room.hostSeat || 0];
  if (host) host.send({ t: 'relay', d: '{"type":"sync"}', from: -1 });
}

// Spectators watch a room (lobby or match) without a seat; they never relay game messages.
function watchRoom(connection, message) {
  leaveRoom(connection);
  connection.name = cleanName(message.name);
  const code = String(message.code || '').trim().toUpperCase();
  const room = rooms.get(code);
  if (!room) return connection.send({ t: 'error', m: 'room_not_found' });
  if (room.spectators.length >= MAX_SPECTATORS) return connection.send({ t: 'error', m: 'audience_full' });
  room.spectators.push(connection);
  connection.room = code;
  connection.spectator = true;
  room.touched = Date.now();
  connection.send({ t: 'watching', code, names: room.seats.map((c) => (c ? c.name : '')), capacity: room.capacity, teams: room.teams,
    mode: room.mode, started: room.started });
  if (room.started) connection.send(startMessageFor(room, -1));

  broadcast(room, audienceMessage(room));
  log('spectator joined', code, connection.name, room.spectators.length);
}

function createRoom(connection, message) {
  leaveRoom(connection);
  connection.name = cleanName(message.name);
  const code = newCode();
  const capacity = Math.min(4, Math.max(2, parseInt(message.players, 10) || 2));
  const teams = message.teams === true && capacity === 4;
  // 8-ball has two groups: three- or four-player free-for-all is 9-ball only.
  const mode = message.mode === 1 || (capacity > 2 && !teams) ? 1 : 0;
  const room = { code, mode, capacity, teams, seats: new Array(capacity).fill(null), spectators: [], started: false, touched: Date.now() };
  room.seats[0] = connection;
  rooms.set(code, room);
  connection.room = code;
  connection.send({ t: 'created', code, seat: 0, capacity, teams, mode });
  connection.send(lobbyMessage(room));
  log('room created', code, 'by', connection.name, `${capacity} seats${teams ? ' (teams)' : ''}`);
}

function joinRoom(connection, message) {
  const code = typeof message.code === 'string' ? message.code.trim().toUpperCase() : '';
  const room = rooms.get(code);
  if (!room) { connection.send({ t: 'error', m: 'room_not_found' }); return; }
  if (room.seats.includes(connection)) return;
  const free = room.seats.indexOf(null);
  if (room.started || free < 0) { connection.send({ t: 'error', m: 'room_full' }); return; }
  leaveRoom(connection);
  connection.name = cleanName(message.name);
  connection.room = code;
  room.seats[free] = connection;
  room.touched = Date.now();
  connection.send({ t: 'joined', code, seat: free, capacity: room.capacity, teams: room.teams, mode: room.mode });
  broadcast(room, lobbyMessage(room));
  if (seatsTaken(room) === room.capacity) startMatch(room);
}

/** Host may start early (free-for-all with 2+ players); a full room starts by itself. */
function requestStart(connection) {
  const room = connection.room ? rooms.get(connection.room) : null;
  if (!room || room.started || room.seats[0] !== connection) return;
  const players = seatsTaken(room);
  if (players < 2 || (room.teams && players < 4)) { connection.send({ t: 'error', m: 'not_enough_players' }); return; }
  startMatch(room);
}

function startMatch(room) {
  // Close gaps left by players who left the lobby: seats become 0..n-1 in join order.
  room.seats = room.seats.filter((c) => c);
  room.capacity = room.seats.length;
  room.started = true;
  room.hostSeat = 0;
  room.touched = Date.now();
  const names = room.seats.map((c) => c.name);
  room.seats.forEach((c, seat) => {
    c.send({ t: 'start', code: room.code, seat, role: seat === 0 ? 'host' : 'guest', names, host: names[0], guest: names[1] || '',
      mode: room.mode, teams: room.teams, players: names.length });
  });
  for (const watcher of room.spectators) watcher.send(startMessageFor(room, -1));
  log('room started', room.code, names.join(' / '), room.teams ? '(teams)' : '');
}

function relay(connection, message) {
  const room = connection.room ? rooms.get(connection.room) : null;
  if (!room || typeof message.d !== 'string' || connection.spectator) return;
  room.touched = Date.now();
  const from = room.seats.indexOf(connection);
  broadcast(room, { t: 'relay', d: message.d, from }, connection);
}

function leaveRoom(connection) {
  const code = connection.room;
  if (!code) return;
  connection.room = null;
  const room = rooms.get(code);
  if (!room) return;
  if (connection.spectator) {
    connection.spectator = false;
    room.spectators = room.spectators.filter((c) => c !== connection);
    broadcast(room, audienceMessage(room));
    return;
  }

  const seat = room.seats.indexOf(connection);
  if (seat < 0) return;
  room.seats[seat] = null;
  const remaining = seatsTaken(room);
  if (room.started) {
    // The match goes on while two or more players remain; the lowest seat left takes over as host (rule authority).
    if (seat === room.hostSeat) room.hostSeat = room.seats.findIndex((c) => c);
    const closed = remaining < 2;
    for (const other of room.seats) {
      if (!other) continue;
      if (closed) other.room = null;
      other.send({ t: 'peer_left', seat, name: connection.name, hostSeat: room.hostSeat, closed });
    }

    if (!closed) {
      for (const watcher of room.spectators) watcher.send({ t: 'peer_left', seat, name: connection.name, hostSeat: room.hostSeat, closed: false });
    }

    if (closed) {
      for (const watcher of room.spectators) {
        watcher.room = null;
        watcher.spectator = false;
        watcher.send({ t: 'peer_left', seat, name: connection.name, hostSeat: -1, closed: true });
      }

      rooms.delete(code);
      log('room closed', code);
    } else {
      log('player left', code, connection.name, 'host seat', room.hostSeat);
    }
    return;
  }

  if (seat === 0 || remaining === 0) {
    // The lobby cannot go on without its host.
    rooms.delete(code);
    for (const watcher of room.spectators) {
      watcher.room = null;
      watcher.spectator = false;
      watcher.send({ t: 'peer_left', seat, name: connection.name, hostSeat: -1, closed: true });
    }

    for (const other of room.seats) {
      if (other) {
        other.room = null;
        other.send({ t: 'peer_left', seat, name: connection.name, hostSeat: -1, closed: true });
      }
    }

    log('room closed', code);
    return;
  }

  broadcast(room, lobbyMessage(room));
}

// ---------------------------------------------------------------- server

const server = http.createServer((req, res) => {
  if (req.url === '/health' || req.url === PATH + '/health') {
    res.writeHead(200, { 'Content-Type': 'application/json' });
    res.end(JSON.stringify({ ok: true, rooms: rooms.size }));
    return;
  }
  if (STATIC_DIR && serveStatic(req, res)) return;
  res.writeHead(426, { 'Content-Type': 'text/plain' });
  res.end('WebSocket endpoint');
});

function serveStatic(req, res) {
  const root = path.resolve(STATIC_DIR);
  let urlPath;
  try { urlPath = decodeURIComponent((req.url || '/').split('?')[0]); } catch (e) { return false; }
  if (urlPath.endsWith('/')) urlPath += 'index.html';
  const file = path.resolve(root, '.' + urlPath);
  if (!file.startsWith(root + path.sep) && file !== root) return false; // no path traversal
  let stat;
  try { stat = fs.statSync(file); } catch (e) { return false; }
  if (!stat.isFile()) return false;
  res.writeHead(200, {
    'Content-Type': MIME[path.extname(file).toLowerCase()] || 'application/octet-stream',
    'Content-Length': stat.size,
    'Last-Modified': stat.mtime.toUTCString(),
    'Cache-Control': 'no-cache'
  });
  fs.createReadStream(file).pipe(res);
  return true;
}

server.on('upgrade', (req, socket) => {
  const url = (req.url || '').split('?')[0];
  const key = req.headers['sec-websocket-key'];
  if (url !== PATH || !key || (req.headers.upgrade || '').toLowerCase() !== 'websocket') {
    socket.end('HTTP/1.1 400 Bad Request\r\n\r\n');
    return;
  }
  socket.write('HTTP/1.1 101 Switching Protocols\r\nUpgrade: websocket\r\nConnection: Upgrade\r\n' +
    `Sec-WebSocket-Accept: ${acceptKey(key)}\r\n\r\n`);
  const connection = new Connection(socket);
  connections.add(connection);
  socket.on('close', () => connections.delete(connection));
});

const connections = new Set();

// Keep-alive: ping every 25 s (also keeps nginx from timing out), drop silent clients; expire idle rooms.
setInterval(() => {
  for (const connection of connections) {
    if (!connection.alive) { connection.close(1001); continue; }
    connection.alive = false;
    try { connection.socket.write(encodeFrame(0x9, Buffer.alloc(0))); } catch (e) { /* ignore */ }
  }
  const now = Date.now();
  for (const [code, room] of rooms) {
    if (now - room.touched > ROOM_IDLE_MS) {
      for (const seat of room.seats) if (seat) seat.close(1001);
      rooms.delete(code);
    }
  }
}, 25000).unref();

server.listen(PORT, HOST, () => log(`VTG Pool room server on ws://${HOST}:${PORT}${PATH}`));

process.on('SIGTERM', () => server.close(() => process.exit(0)));
