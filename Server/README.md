# VTG Pool 3D — máy chủ phòng chơi online

Máy chủ WebSocket nhỏ, không cần thư viện ngoài, dùng để ghép phòng bằng mã 6 ký tự và chuyển tin giữa 2–4 người chơi (đơn 1v1, 3–4 người đánh 9 bi xoay vòng, đôi 2v2).
Luật chơi và vật lý chạy trong game: chủ phòng (người tạo phòng) quyết định kết quả, máy chủ chỉ chuyển tiếp.

## Chạy thử trên máy

```bash
node server.js --port 8765          # ws://127.0.0.1:8765/ws
npm test                            # test nhanh: tạo phòng, vào phòng, chuyển tin, thoát
```

Trong game (Editor hoặc bản PC/Android cùng mạng): **CHƠI → TRỰC TUYẾN**, ô *Máy chủ* nhập `ws://<IP máy>:8765/ws`
(khi chạy thử trong mạng LAN thì cần `--host 0.0.0.0`).

## Deploy lên VPS với nginx (Ubuntu/Debian)

1. Cài Node.js 18+ (`sudo apt install nodejs` hoặc NodeSource).
2. Chép thư mục `Server/` lên `/opt/vtgpool-server`.
3. Chạy như dịch vụ:
   ```bash
   sudo cp deploy/vtgpool-server.service /etc/systemd/system/
   sudo systemctl daemon-reload && sudo systemctl enable --now vtgpool-server
   curl http://127.0.0.1:8765/health      # {"ok":true,"rooms":0}
   ```
   Hoặc dùng Docker: `docker build -t vtgpool-server . && docker run -d --restart unless-stopped -p 127.0.0.1:8765:8765 vtgpool-server`.
4. Bản chơi trên trình duyệt: trong Unity chọn **VTG Pool → Build → WebGL (browser)**, chép nội dung `Builds/WebGL/` lên `/var/www/vtgpool`.
5. nginx: sửa tên miền và đường dẫn chứng chỉ trong `deploy/nginx-vtgpool.conf`, rồi:
   ```bash
   sudo cp deploy/nginx-vtgpool.conf /etc/nginx/sites-available/vtgpool
   sudo ln -s ../sites-available/vtgpool /etc/nginx/sites-enabled/
   sudo certbot --nginx -d pool.example.com     # nếu chưa có chứng chỉ HTTPS
   sudo nginx -t && sudo systemctl reload nginx
   ```

Sau đó:
- Trình duyệt: mở `https://pool.example.com` — ô *Máy chủ* tự điền `wss://pool.example.com/ws`.
- Android / PC: ô *Máy chủ* nhập `pool.example.com` (game tự đổi thành `wss://pool.example.com/ws`), địa chỉ được nhớ cho lần sau.

## Cách chơi

1. Người A: **TRỰC TUYẾN** → chọn *Thể thức* (Đơn 1 vs 1 / 3 người / 4 người / Đôi 2 vs 2) → **TẠO PHÒNG** → nhận mã (ví dụ `K7P2QX`, đã chép sẵn vào clipboard trên PC).
2. Gửi mã cho mọi người. Mỗi người: **TRỰC TUYẾN → nhập mã → VÀO PHÒNG**. Phòng chờ hiện danh sách ghế.
3. Đủ người thì trận tự bắt đầu; với 3–4 người (không đội) chủ phòng có thể bấm **BẮT ĐẦU** sớm khi đã có ≥ 2 người. Người tạo phòng phá bi trước.
4. Đánh đôi: ghế 1 & 3 là Đội A, ghế 2 & 4 là Đội B; lượt đánh A1 → B1 → A2 → B2, đồng đội dùng chung nhóm bi. 3–4 người luôn đánh 9 bi.
5. **Khán giả**: nhập mã phòng → **XEM** (tối đa 16 người/phòng). Vào được cả khi trận đang diễn ra; khán giả kéo chuột/vuốt để xoay camera quanh bàn, không điều khiển được trận.
6. Tỉ số các ván trong phòng hiện trên bảng người chơi; mỗi ván xong được lưu vào *Thống kê → Lịch sử online* trên máy của từng người.
7. Hết ván, bấm *Chơi lại*. Ai rời phòng khi đang đấu thì trận kết thúc cho mọi người.

## Giao thức (tóm tắt)

| Hướng | Tin nhắn |
|---|---|
| client → server | `create {name, mode, players 2-4, teams}`, `join {code, name}`, `watch {code, name}` (khán giả), `sync` (khán giả xin bàn), `start` (chủ phòng), `relay {d}`, `leave`, `ping` |
| server → client | `created {code, seat, capacity, teams, mode}`, `joined {seat, ...}`, `lobby {names[], capacity, teams, mode}`, `start {seat, role, names[], host, guest, mode, teams, players}`, `relay {d, from}`, `watching {...}`, `audience {count, names}`, `peer_left {seat, name, hostSeat, closed}`, `error {m}` (`room_not_found`, `room_full`, `not_enough_players`, ...), `pong` |

Nội dung `relay.d` (JSON của game): `ready`, `aim`, `place`, `shot`, `state`, `rematch` — xem `Assets/_PoolGame/Scripts/Network/NetGameMessage.cs`.
Phòng tự xóa sau 30 phút không hoạt động; kết nối im lặng quá ~50 giây bị ngắt.
