'use strict';
// Smoke test: starts the server on a free port and plays through 1v1, 2v2 and early-start rooms.
const { spawn } = require('child_process');
const path = require('path');
const assert = require('assert');

const port = 18000 + Math.floor(Math.random() * 1000);
const server = spawn(process.execPath, [path.join(__dirname, '..', 'server.js'), '--port', String(port)], { stdio: ['ignore', 'pipe', 'inherit'] });

function client() {
  const ws = new WebSocket(`ws://127.0.0.1:${port}/ws`);
  const queue = [];
  const waiters = [];
  ws.onmessage = (event) => {
    const message = JSON.parse(event.data);
    const waiter = waiters.shift();
    if (waiter) waiter(message); else queue.push(message);
  };
  const c = {
    open: () => new Promise((resolve, reject) => { ws.onopen = resolve; ws.onerror = reject; }),
    send: (object) => ws.send(JSON.stringify(object)),
    next: () => queue.length ? Promise.resolve(queue.shift()) : new Promise((resolve) => waiters.push(resolve)),
    close: () => ws.close(),
    /** Next message of a type (skipping others, e.g. lobby updates). */
    async expect(type) {
      for (;;) {
        const m = await c.next();
        if (m.t === type) return m;
      }
    }
  };
  return c;
}

(async () => {
  await new Promise((resolve) => server.stdout.once('data', resolve));

  // ---- 1v1
  const host = client();
  const guest = client();
  await host.open();
  await guest.open();
  host.send({ t: 'create', name: 'An', mode: 1 });
  const created = await host.expect('created');
  assert.match(created.code, /^[A-Z2-9]{6}$/);
  guest.send({ t: 'join', code: 'NOPE00', name: 'Binh' });
  assert.strictEqual((await guest.expect('error')).m, 'room_not_found');
  guest.send({ t: 'join', code: created.code.toLowerCase(), name: 'Binh' });
  const hostStart = await host.expect('start');
  const guestStart = await guest.expect('start');
  assert.strictEqual(hostStart.role, 'host');
  assert.strictEqual(guestStart.seat, 1);
  assert.deepStrictEqual(guestStart.names, ['An', 'Binh']);
  assert.strictEqual(guestStart.mode, 1);
  host.send({ t: 'relay', d: '{"type":"shot"}' });
  const relayed = await guest.expect('relay');
  assert.strictEqual(relayed.d, '{"type":"shot"}');
  assert.strictEqual(relayed.from, 0);
  host.send({ t: 'ping' });
  assert.strictEqual((await host.expect('pong')).t, 'pong');
  guest.close();
  assert.strictEqual((await host.expect('peer_left')).seat, 1);
  host.close();

  // ---- 2v2: lobby updates, auto start when full, team seats, broadcast relay
  const players = [client(), client(), client(), client()];
  for (const p of players) await p.open();
  players[0].send({ t: 'create', name: 'A1', mode: 0, players: 4, teams: true });
  const room = await players[0].expect('created');
  assert.strictEqual(room.capacity, 4);
  assert.strictEqual(room.teams, true);
  players[1].send({ t: 'join', code: room.code, name: 'B1' });
  const lobby = await players[0].expect('lobby');
  assert.strictEqual(lobby.names.length, 4);
  players[0].send({ t: 'start' });
  assert.strictEqual((await players[0].expect('error')).m, 'not_enough_players', 'Doubles need four players');
  players[2].send({ t: 'join', code: room.code, name: 'A2' });
  players[3].send({ t: 'join', code: room.code, name: 'B2' });
  const starts = await Promise.all(players.map((p) => p.expect('start')));
  starts.forEach((s, i) => assert.strictEqual(s.seat, i));
  assert.deepStrictEqual(starts[3].names, ['A1', 'B1', 'A2', 'B2']);
  assert.strictEqual(starts[2].teams, true);
  const fifth = client();
  await fifth.open();
  fifth.send({ t: 'join', code: room.code, name: 'Late' });
  assert.strictEqual((await fifth.expect('error')).m, 'room_full');
  players[2].send({ t: 'relay', d: 'from A2' });
  for (const i of [0, 1, 3]) {
    const m = await players[i].expect('relay');
    assert.strictEqual(m.d, 'from A2');
    assert.strictEqual(m.from, 2);
  }

  // A player leaves: the match goes on for the other three (host unchanged).
  players[3].close();
  for (const i of [0, 1, 2]) {
    const left = await players[i].expect('peer_left');
    assert.strictEqual(left.seat, 3);
    assert.strictEqual(left.closed, false);
    assert.strictEqual(left.hostSeat, 0);
  }

  players[1].send({ t: 'relay', d: 'still here' });
  assert.strictEqual((await players[0].expect('relay')).d, 'still here');
  // The host leaves: seat 1 becomes host.
  players[0].close();
  for (const i of [1, 2]) {
    const left = await players[i].expect('peer_left');
    assert.strictEqual(left.hostSeat, 1, 'Lowest remaining seat hosts');
    assert.strictEqual(left.closed, false);
  }

  // Down to one player: the room closes.
  players[2].close();
  const last = await players[1].expect('peer_left');
  assert.strictEqual(last.closed, true);
  players.forEach((p) => p.close());
  fifth.close();

  // ---- spectators: watch the lobby, get the start, sync request to the host, receive relays, cannot relay
  const sh = client(), sg = client(), fan = client(), late = client();
  for (const p of [sh, sg, fan, late]) await p.open();
  sh.send({ t: 'create', name: 'SH', mode: 0 });
  const sroom = await sh.expect('created');
  fan.send({ t: 'watch', code: sroom.code, name: 'Fan' });
  const watching = await fan.expect('watching');
  assert.strictEqual(watching.started, false);
  assert.strictEqual((await sh.expect('audience')).count, 1);
  sg.send({ t: 'join', code: sroom.code, name: 'SG' });
  const fanStart = await fan.expect('start');
  assert.strictEqual(fanStart.seat, -1);
  assert.strictEqual(fanStart.role, 'spectator');
  assert.deepStrictEqual(fanStart.names, ['SH', 'SG']);
  late.send({ t: 'watch', code: sroom.code, name: 'Late' });
  assert.strictEqual((await late.expect('start')).seat, -1);
  late.send({ t: 'sync' });
  const sync = await sh.expect('relay');
  assert.strictEqual(sync.from, -1);
  assert.strictEqual(JSON.parse(sync.d).type, 'sync');
  sh.send({ t: 'relay', d: 'state' });
  assert.strictEqual((await fan.expect('relay')).d, 'state');
  assert.strictEqual((await late.expect('relay')).d, 'state');
  fan.send({ t: 'relay', d: 'spoof' });
  sg.send({ t: 'relay', d: 'guest' });
  assert.strictEqual((await sh.expect('relay')).d, 'guest', 'Spectator relays are dropped');
  late.close();
  sg.close();
  assert.strictEqual((await fan.expect('peer_left')).closed, true);
  [sh, sg, fan].forEach((p) => p.close());

  // ---- free-for-all: host starts early with three of four seats (9-ball forced)
  const ffa = [client(), client(), client()];
  for (const p of ffa) await p.open();
  ffa[0].send({ t: 'create', name: 'X', mode: 0, players: 4, teams: false });
  const ffaRoom = await ffa[0].expect('created');
  assert.strictEqual(ffaRoom.mode, 1, 'Three or four singles players play 9-ball');
  ffa[1].send({ t: 'join', code: ffaRoom.code, name: 'Y' });
  ffa[2].send({ t: 'join', code: ffaRoom.code, name: 'Z' });
  await ffa[2].expect('joined');
  ffa[0].send({ t: 'start' });
  const ffaStarts = await Promise.all(ffa.map((p) => p.expect('start')));
  assert.strictEqual(ffaStarts[0].players, 3);
  assert.deepStrictEqual(ffaStarts[1].names, ['X', 'Y', 'Z']);
  ffa.forEach((p) => p.close());

  console.log('smoke test passed');
  server.kill();
  process.exit(0);
})().catch((error) => {
  console.error('smoke test FAILED', error);
  server.kill();
  process.exit(1);
});
