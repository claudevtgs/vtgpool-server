using System.Collections.Generic;

namespace VTG.Pool.Localization
{
    /// <summary>English / Vietnamese strings. Keys are grouped by screen. Placeholders use string.Format ({0}).</summary>
    internal static class LocTables
    {
        public static void Fill(Dictionary<string, string> en, Dictionary<string, string> vi)
        {
            void S(string key, string english, string vietnamese)
            {
                en[key] = english;
                vi[key] = vietnamese;
            }

            // Common
            S("common.start", "START", "BẮT ĐẦU");
            S("common.back", "BACK", "QUAY LẠI");
            S("common.reset", "RESET", "ĐẶT LẠI");
            S("player.default", "Player {0}", "Người chơi {0}");
            S("player.ai", "AI ({0})", "Máy ({0})");

            // Main menu
            S("menu.subtitle", "Realistic 3D billiards", "Bi-a 3D chân thực");
            S("menu.play", "PLAY", "CHƠI");
            S("menu.practice", "PRACTICE", "LUYỆN TẬP");
            S("menu.settings", "SETTINGS", "CÀI ĐẶT");
            S("menu.stats", "STATISTICS", "THỐNG KÊ");
            S("menu.exit", "EXIT", "THOÁT");
            S("menu.game", "Game", "Kiểu chơi");
            S("menu.opponent", "Opponent", "Đối thủ");
            S("menu.ailevel", "AI level", "Trình độ máy");
            S("menu.online", "ONLINE  ·  coming soon", "TRỰC TUYẾN  ·  sắp có");
            S("opt.8ball", "8-Ball", "8 bi");
            S("opt.9ball", "9-Ball", "9 bi");
            S("opt.vsai", "vs AI", "Đấu với máy");
            S("opt.local", "Local 2 players", "2 người cùng máy");
            S("ai.beginner", "Beginner", "Mới chơi");
            S("ai.intermediate", "Intermediate", "Trung bình");
            S("ai.advanced", "Advanced", "Khá");
            S("ai.expert", "Expert", "Chuyên gia");

            // Settings
            S("settings.title", "SETTINGS", "CÀI ĐẶT");
            S("settings.audio", "AUDIO", "ÂM THANH");
            S("settings.master", "Master", "Tổng");
            S("settings.effects", "Effects", "Hiệu ứng");
            S("settings.music", "Music", "Nhạc nền");
            S("settings.ambience", "Ambience", "Âm thanh nền");
            S("settings.graphics", "GRAPHICS", "ĐỒ HỌA");
            S("settings.quality", "Quality", "Chất lượng");
            S("quality.auto", "Auto", "Tự động");
            S("quality.low", "Low", "Thấp");
            S("quality.medium", "Medium", "Trung bình");
            S("quality.high", "High", "Cao");
            S("quality.ultra", "Ultra", "Rất cao");
            S("settings.gameplay", "GAMEPLAY", "LỐI CHƠI");
            S("settings.startcamera", "Start camera", "Camera ban đầu");
            S("camera.cue", "Cue (player view)", "Sau cơ (góc người chơi)");
            S("camera.tactical", "Tactical", "Chiến thuật");
            S("camera.top", "Top", "Từ trên xuống");
            S("settings.assist", "Aim assist", "Hỗ trợ ngắm");
            S("assist.none", "None", "Không");
            S("assist.minimal", "Minimal", "Tối thiểu");
            S("assist.standard", "Standard", "Tiêu chuẩn");
            S("assist.training", "Training", "Tập luyện");
            S("settings.sensitivity", "Aim sensitivity", "Độ nhạy ngắm");
            S("settings.placement", "Ball placement", "Độ nhạy đặt bi");
            S("settings.controls", "CONTROLS", "ĐIỀU KHIỂN");
            S("settings.touch", "Touch controls", "Điều khiển cảm ứng");
            S("touch.auto", "Auto", "Tự động");
            S("touch.off", "Off", "Tắt");
            S("touch.on", "On", "Bật");
            S("settings.hand", "Hand", "Tay thuận");
            S("hand.right", "Right-handed", "Tay phải");
            S("hand.left", "Left-handed", "Tay trái");
            S("settings.language", "Language", "Ngôn ngữ");
            S("language.auto", "Auto (device)", "Tự động (theo máy)");
            S("language.en", "English", "English");
            S("language.vi", "Tiếng Việt", "Tiếng Việt");

            // Statistics
            S("stats.title", "STATISTICS", "THỐNG KÊ");
            S("stats.matches", "Matches played", "Số trận");
            S("stats.wins", "Wins", "Thắng");
            S("stats.winrate", "Win rate", "Tỉ lệ thắng");
            S("stats.streak", "Win streak (best)", "Chuỗi thắng (cao nhất)");
            S("stats.8wins", "8-ball wins", "Thắng 8 bi");
            S("stats.9wins", "9-ball wins", "Thắng 9 bi");
            S("stats.pocketed", "Balls pocketed", "Bi vào lỗ");
            S("stats.breaks", "Break success", "Phá bi có bi vào");
            S("stats.avgshots", "Avg. shots per win", "Số cú TB mỗi trận thắng");
            S("stats.fouls", "Fouls", "Lỗi");

            // Game modes and groups
            S("mode.8ball", "8-BALL", "8 BI");
            S("mode.9ball", "9-BALL", "9 BI");
            S("mode.practice", "PRACTICE", "LUYỆN TẬP");
            S("group.solids", "Solids", "Bi trơn");
            S("group.stripes", "Stripes", "Bi sọc");
            S("group.open", "Open", "Chưa chia nhóm");

            // HUD
            S("hud.ballsontable", "{0} balls on the table", "{0} bi trên bàn");
            S("hud.fouls", "fouls {0}", "lỗi {0}");
            S("hud.inarow", "{0} in a row", "{0} lần liên tiếp");
            S("hud.gameover", "Game over", "Hết ván");
            S("hud.inprogress", "{0} — shot in progress", "{0} — bi đang chạy");
            S("hud.arrange.touch", "Arrange balls — drag a ball with your finger", "Sắp xếp bi — dùng ngón tay kéo bi");
            S("hud.arrange.mouse", "Arrange balls — drag a ball with the mouse", "Sắp xếp bi — dùng chuột kéo bi");
            S("hud.behindhead", " (behind the head string)", " (sau vạch đầu bàn)");
            S("hud.place.touch", "drag it, then tap PLACE", "kéo bi rồi bấm ĐẶT BI");
            S("hud.place.mouse", "move, click / Enter to place", "di chuyển, nhấp chuột / Enter để đặt");
            S("hud.aiplacing", "{0}: placing cue ball{1}", "{0}: đang đặt bi cái{1}");
            S("hud.ballinhand", "{0}: ball in hand{1} — {2}", "{0}: được đặt bi cái{1} — {2}");
            S("hud.bkey", "   (B: move cue ball)", "   (B: di chuyển bi cái)");
            S("hud.aithinking", "{0}: thinking — target: {1}", "{0}: đang tính — mục tiêu: {1}");
            S("hud.toshoot", "{0} to shoot — target: {1}{2}", "Lượt {0} — mục tiêu: {1}{2}");
            S("hud.power", "POWER {0}%", "LỰC {0}%");
            S("hud.stroke.hint", "SPACE + mouse: pull, push", "SPACE + chuột: kéo, đẩy");
            S("hud.stroke.draw", "BACK SWING {0} cm — push the mouse forward to strike", "RÚT CƠ {0} cm — đẩy chuột tới để đánh");
            S("settings.stroke", "Mouse shot", "Đánh bằng chuột");
            S("stroke.mouse", "Stroke (like a real cue)", "Kéo đẩy (như cơ thật)");
            S("stroke.hold", "Hold SPACE to charge", "Giữ SPACE để tăng lực");
            S("settings.cinematic", "Shot camera", "Camera khi đánh");
            S("cinematic.on", "Cinematic", "Điện ảnh");
            S("cinematic.off", "Off", "Tắt");
            S("intro.presents", "VTG GAMES PRESENTS", "VTG GAMES TRÂN TRỌNG GIỚI THIỆU");
            S("intro.skip", "Click or press any key to skip", "Nhấn chuột hoặc phím bất kỳ để bỏ qua");
            S("intro.tagline", "Every shot is real physics", "Mỗi cú đánh là vật lý thật");
            S("hud.wins", "{0} wins!", "{0} thắng!");
            S("hud.paused", "PAUSED", "TẠM DỪNG");
            S("hud.resume", "Resume", "Tiếp tục");
            S("hud.restart", "Restart match", "Chơi lại ván");
            S("hud.settings", "Settings", "Cài đặt");
            S("hud.mainmenu", "Main menu", "Menu chính");
            S("hud.new8local", "8-ball: local 2 players", "8 bi: 2 người");
            S("hud.new9local", "9-ball: local 2 players", "9 bi: 2 người");
            S("hud.new8ai", "8-ball vs AI", "8 bi: đấu với máy");
            S("hud.new9ai", "9-ball vs AI", "9 bi: đấu với máy");
            S("hud.newai", "New AI: {0}", "Máy mới: {0}");
            S("hud.graphics", "Graphics: {0}", "Đồ họa: {0}");
            S("hud.playagain", "Play again", "Chơi lại");
            S("hud.spin.touch", "SPIN (tap / drag)", "XOÁY (chạm / kéo)");
            S("hud.spin.mouse", "SPIN (click / arrows, C = centre)", "XOÁY (nhấp / mũi tên, C = giữa)");
            S("hud.elevation", "CUE\nANGLE", "GÓC\nCƠ");
            S("hud.elevation.keys", "R / F", "R / F");
            S("hud.masse", "MASSÉ", "MASSÉ");
            S("hud.cam.cue", "1 Cue", "1 Sau cơ");
            S("hud.cam.tactical", "2 Tactical", "2 Chiến thuật");
            S("hud.cam.top", "3 Top", "3 Trên cao");
            S("hud.cam.orbit", "4 Orbit", "4 Xoay tự do");

            // Practice
            S("practice.title", "PRACTICE", "LUYỆN TẬP");
            S("practice.stats", "Shots {0}   ·   Pocketed {1}\nScratches {2}", "Cú đánh {0}   ·   Vào lỗ {1}\nBi cái rơi lỗ {2}");
            S("practice.undo", "Undo shot", "Hoàn tác cú đánh");
            S("practice.arrange", "Arrange balls", "Sắp xếp bi");
            S("practice.done", "Done arranging", "Xong");
            S("practice.rack8", "8-Ball rack", "Xếp 8 bi");
            S("practice.rack9", "9-Ball rack", "Xếp 9 bi");
            S("practice.empty", "Empty table", "Bàn trống");
            S("practice.rerack", "Re-rack", "Xếp lại");
            S("practice.hint", "Drag balls on the table. Drop one in a pocket to remove it.", "Kéo bi trên bàn. Thả bi vào lỗ để bỏ ra.");
            S("practice.hinttray", "Drag balls on the table. Drop one in a pocket to remove it. Tap a ball below to put it back.",
                "Kéo bi trên bàn. Thả bi vào lỗ để bỏ ra. Chạm bi bên dưới để đặt lại lên bàn.");

            // Touch controls
            S("touch.pull", "PULL", "KÉO");
            S("touch.release", "release\nto shoot", "thả tay\nđể đánh");
            S("touch.fineaim", "FINE\nAIM", "NGẮM\nTINH");
            S("touch.place", "PLACE", "ĐẶT BI");
            S("touch.movecue", "MOVE CUE BALL", "DI CHUYỂN BI CÁI");

            // Fouls
            S("foul.scratch", "scratch", "bi cái rơi lỗ");
            S("foul.cueofftable", "cue ball off the table", "bi cái văng khỏi bàn");
            S("foul.nocontact", "no ball contacted", "không chạm bi nào");
            S("foul.wrongfirst", "wrong ball first", "chạm sai bi trước");
            S("foul.norail", "no rail after contact", "không có bi chạm băng sau va chạm");
            S("foul.illegalbreak", "illegal break", "phá bi không hợp lệ");
            S("foul.objectofftable", "ball off the table", "bi văng khỏi bàn");
            S("foul.none", "none", "không");

            // 8-ball
            S("rule8.target.break", "Break", "Phá bi");
            S("rule8.target.open", "Open table", "Bàn chưa chia nhóm");
            S("rule8.target.eight", "8-ball", "bi số 8");
            S("rule8.illegalbreak.rerack", "Illegal break by {0}. Re-rack: {1} breaks.", "{0} phá bi không hợp lệ. Xếp lại, {1} phá.");
            S("rule8.illegalbreak.bih", "Illegal break by {0}. {1} has ball in hand.", "{0} phá bi không hợp lệ. {1} được đặt bi cái.");
            S("rule8.eightonbreak", "{0} pocketed the 8 on the break!", "{0} đưa bi 8 vào lỗ ngay khi phá!");
            S("rule8.eightofftable", "{0} drove the 8 off the table. {1} wins.", "{0} đánh bi 8 văng khỏi bàn. {1} thắng.");
            S("rule8.eightearly", "{0} pocketed the 8 early. {1} wins.", "{0} đưa bi 8 vào lỗ quá sớm. {1} thắng.");
            S("rule8.eightfoul", "{0} fouled ({2}) on the 8. {1} wins.", "{0} phạm lỗi ({2}) khi đánh bi 8. {1} thắng.");
            S("rule8.eightwin", "{0} pocketed the 8. {0} wins!", "{0} đưa bi 8 vào lỗ. {0} thắng!");
            S("rule8.foul", "Foul: {0}. {1} has ball in hand {2}.", "Lỗi: {0}. {1} được đặt bi cái {2}.");
            S("rule.where.kitchen", "behind the head string", "sau vạch đầu bàn");
            S("rule.where.anywhere", "anywhere", "ở bất kỳ đâu");
            S("rule8.takesgroup", "{0} takes {1}.", "{0} chọn nhóm {1}.");
            S("rule.continuesbreak", "{0} pocketed on the break and continues.", "{0} có bi vào lỗ khi phá và đánh tiếp.");
            S("rule.continues", "{0} continues.", "{0} đánh tiếp.");
            S("rule.toshoot", "{0} to shoot.", "Đến lượt {0}.");
            S("rule8.eightspotted", "8-ball spotted. ", "Bi 8 được đặt lại. ");

            // 9-ball
            S("rule9.target", "{0}-ball", "bi {0}");
            S("rule9.target.break", "Break ({0} first)", "Phá bi (chạm {0} trước)");
            S("rule9.win", "{0} pocketed the 9{1}. {0} wins!", "{0} đưa bi 9 vào lỗ{1}. {0} thắng!");
            S("rule9.onbreak", " on the break", " ngay khi phá");
            S("rule9.oncombo", " on a combination", " bằng bi dẫn (combination)");
            S("rule9.threefouls", "Third consecutive foul by {0}. {1} wins.", "{0} phạm lỗi 3 lần liên tiếp. {1} thắng.");
            S("rule9.twofouls", " {0} is on two fouls.", " {0} đã phạm 2 lỗi liên tiếp.");
            S("rule9.nineonfoul", "{0} pocketed the 9 on a foul ({1}). {2} wins.", "{0} phạm lỗi ({1}) khi bi 9 rơi. {2} thắng.");
            S("victory.title", "VICTORY", "CHIẾN THẮNG");
            S("replay.badge", "REPLAY", "PHÁT LẠI");
            S("replay.skip", "Click or press any key to skip", "Nhấn chuột hoặc phím bất kỳ để bỏ qua");
            S("settings.replay", "Pot replays", "Phát lại khi ăn bi");
            S("replay.every", "Every pot", "Mọi lần ăn bi");
            S("replay.highlights", "Highlights only", "Chỉ cú đẹp");
            S("replay.off", "Off", "Tắt");
            S("troll.title", "TURTLE!", "RÙA!!!");
            S("fx.foul", "FOUL!", "PHẠM LỖI!");
            S("gag.ghost", "R.I.P. CUE BALL", "BI CÁI ĐI ĐẦU THAI");
            S("gag.ghost.line", "It saw the pocket and couldn't resist", "Thấy lỗ là lao vào, cản không kịp");
            S("gag.crickets", "... cri cri ...", "... cri cri ...");
            S("gag.crickets.line", "Not a single ball was harmed in this shot", "Không một quả bi nào bị thương trong cú đánh này");
            S("gag.lip", "SO CLOSE!!!", "RUNG RINH MIỆNG LỖ!!!");
            S("gag.lip.line", "One more breath and it was in", "Thổi nhẹ cái là vào rồi...");
            S("gag.streak", "{0} IN A ROW", "TRƯỢT {0} PHÁT LIÊN TIẾP");
            S("gag.streak.line", "Maybe chalk the cue? Or your hands?", "Đánh phấn cơ chưa? Hay đánh phấn tay luôn?");
            S("gag.toang", "OOF!", "TOANG!");
            S("gag.toang.line", "That one hurt", "Thôi xong, về chuẩn bị ván sau");
            S("fx.miss", "MISS", "TRƯỢT");
            S("fx.foul.scratch", "The cue ball went down", "Bi cái rơi lỗ");
            S("fx.foul.offtable", "Ball off the table", "Bi văng khỏi bàn");
            S("fx.foul.nocontact", "Didn't touch a single ball", "Không chạm trúng bi nào");
            S("fx.foul.wrongball", "Wrong ball first", "Chạm sai bi trước");
            S("fx.foul.norail", "Nothing reached a rail", "Không có bi nào chạm băng");
            S("fx.foul.break", "Illegal break", "Phá bi không hợp lệ");
            S("miss.1", "So close...", "Suýt nữa thì vào...");
            S("miss.2", "The pocket said no", "Miệng lỗ nói không");
            S("miss.3", "Next time!", "Lần sau nhé!");
            S("miss.4", "It was a safety. Obviously.", "Đánh phòng thủ đó... chắc vậy");
            S("troll.1", "Pure luck, and everyone saw it", "Ăn may thôi, cả bàn đều thấy");
            S("troll.2", "Calculated. Probably.", "Tính hết rồi đó... chắc vậy");
            S("troll.3", "Even the turtle is impressed", "Đến con rùa cũng phải nể");
            S("troll.4", "Aimed at one, potted another", "Nhắm bi này, ăn bi kia");
            S("troll.5", "The table did all the work", "Cái bàn đánh giùm luôn"); 
            S("victory.wins", "{0} wins the rack", "{0} thắng ván");
            S("hud.jump", "JUMP", "NHẢY");
            S("rule9.foul", "Foul: {0}. {1} has ball in hand.{2}", "Lỗi: {0}. {1} được đặt bi cái.{2}");

            // Practice rules
            S("rulep.target.any", "any ball", "bi bất kỳ");
            S("rulep.target.clear", "table clear", "bàn đã sạch");
            S("rulep.scratch", "Scratch — place the cue ball anywhere", "Bi cái rơi lỗ — đặt lại ở bất kỳ đâu");
            S("rulep.cueofftable", "Cue ball off the table — place it anywhere", "Bi cái văng khỏi bàn — đặt lại ở bất kỳ đâu");
            S("rulep.cleared", "Table cleared!", "Đã dọn sạch bàn!");
            S("rulep.pocketedone", "{0}-ball pocketed", "Bi {0} vào lỗ");
            S("rulep.pocketedmany", "{0} balls pocketed", "{0} bi vào lỗ");
            S("rulep.nohit", "No ball hit", "Không chạm bi nào");

            // Shot callouts
            S("callout.golden", "GOLDEN BREAK", "PHÁ BI VÀNG");
            S("callout.breakrun", "BREAK & RUN", "PHÁ VÀ ĂN SẠCH");
            S("callout.kick", "KICK SHOT", "ĐÁNH BĂNG");
            S("callout.combo", "COMBO", "BI DẪN");
            S("callout.bank", "BANK SHOT", "DỘI BĂNG");
            S("callout.triple", "TRIPLE", "BA BI");
            S("callout.double", "DOUBLE", "HAI BI");
            S("callout.powerbreak", "POWER BREAK ×{0}", "PHÁ MẠNH ×{0}");
            S("callout.streak", "STREAK ×{0}", "CHUỖI ×{0}");
            S("callout.masse", "MASSÉ", "MASSÉ");

            // Online
            S("online.title", "ONLINE", "TRỰC TUYẾN");
            S("menu.online.play", "ONLINE", "TRỰC TUYẾN");
            S("online.name", "Your name", "Tên của bạn");
            S("online.server", "Server", "Máy chủ");
            S("online.create", "CREATE ROOM", "TẠO PHÒNG");
            S("online.join", "JOIN", "VÀO PHÒNG");
            S("online.code", "Room code", "Mã phòng");
            S("online.connecting", "Connecting… (a sleeping server can take up to a minute to wake up)", "Đang kết nối… (máy chủ đang ngủ có thể mất tới 1 phút để thức dậy)");
            S("online.created", "Room code: {0}\nSend this code to your friend — waiting for them to join…", "Mã phòng: {0}\nGửi mã này cho bạn bè — đang chờ người vào…");
            S("online.joining", "Joining room {0}…", "Đang vào phòng {0}…");
            S("online.starting", "{0} vs {1} — starting…", "{0} đấu với {1} — bắt đầu…");
            S("online.err.room_not_found", "Room not found. Check the code.", "Không tìm thấy phòng. Kiểm tra lại mã.");
            S("online.err.room_full", "That room is full.", "Phòng đã đủ người.");
            S("online.err.connect_failed", "Cannot reach the server. Check the address and your connection.", "Không kết nối được máy chủ. Kiểm tra địa chỉ và mạng.");
            S("online.err.noserver", "Enter the server address first.", "Hãy nhập địa chỉ máy chủ trước.");
            S("online.err.nocode", "Enter the 6-character room code.", "Hãy nhập mã phòng 6 ký tự.");
            S("online.err.bad_request", "The server rejected the request.", "Máy chủ từ chối yêu cầu.");
            S("online.cancel", "CANCEL", "HỦY");
            S("online.format", "Format", "Thể thức");
            S("online.watch", "WATCH", "XEM");
            S("online.watching", "Watching room {0} — waiting for the match to start…", "Đang xem phòng {0} — chờ trận đấu bắt đầu…");
            S("online.audience", "Spectators: {0}", "Khán giả: {0}");
            S("online.roomclosed", "The match is over — the room was closed.", "Trận đấu đã kết thúc — phòng đã đóng.");
            S("online.err.audience_full", "Too many spectators in that room.", "Phòng này đã đủ khán giả.");
            S("spectator.badge", "WATCHING · room {0} · {1} spectating · drag to look around", "ĐANG XEM · phòng {0} · {1} khán giả · kéo để xoay camera");
            S("hud.racks", "Racks {0}", "Thắng {0} ván");
            S("history.title", "ONLINE MATCHES", "LỊCH SỬ ONLINE");
            S("history.open", "ONLINE HISTORY", "LỊCH SỬ ONLINE");
            S("history.empty", "No online matches yet.", "Chưa có trận online nào.");
            S("history.win", "WIN", "THẮNG");
            S("history.loss", "LOSS", "THUA");
            S("history.watched", "WATCHED", "XEM");
            S("history.forfeit", "(forfeit)", "(bỏ cuộc)");
            S("history.line", "{0}  ·  {1}  ·  {2}  ·  {3}\n   {4} wins  ·  room score {5}  {6}", "{0}  ·  {1}  ·  {2}  ·  {3}\n   {4} thắng  ·  tỉ số phòng {5}  {6}");
            S("history.clear", "CLEAR", "XÓA");
            S("online.playerleft", "{0} left the room — the match goes on", "{0} đã rời phòng — trận đấu tiếp tục");
            S("online.lefttag", "(left)", "(đã rời)");
            S("online.forfeit", "Everyone else left — {0} wins!", "Mọi người đã rời phòng — {0} thắng!");
            S("online.alone", "Everyone else left the room. You win by forfeit.", "Mọi người đã rời phòng. Bạn thắng do đối thủ bỏ cuộc.");
            S("online.err.closed", "The host closed the room.", "Chủ phòng đã đóng phòng.");
            S("online.fmt.1v1", "1 v 1", "Đơn 1 vs 1");
            S("online.fmt.ffa3", "3 players (9-ball)", "3 người (9 bi)");
            S("online.fmt.ffa4", "4 players (9-ball)", "4 người (9 bi)");
            S("online.fmt.2v2", "Doubles 2 v 2", "Đôi 2 vs 2");
            S("online.start", "START", "BẮT ĐẦU");
            S("online.starting.n", "Starting: {0}", "Bắt đầu: {0}");
            S("online.lobby.count", "Players: {0}/{1} — waiting for everyone…", "Người chơi: {0}/{1} — đang chờ mọi người…");
            S("online.seat.empty", "(empty)", "(trống)");
            S("online.seat.host", "[host]", "[chủ phòng]");
            S("online.seat.you", "(you)", "(bạn)");
            S("online.team.a", "Team A", "Đội A");
            S("online.team.b", "Team B", "Đội B");
            S("online.err.not_enough_players", "Need more players to start (doubles needs 4).", "Chưa đủ người để bắt đầu (đánh đôi cần 4 người).");
            S("online.waiting", "Waiting for the host's result…", "Đang chờ kết quả từ chủ phòng…");
            S("online.remoteaiming", "{0} is aiming — target: {1}", "{0} đang ngắm — mục tiêu: {1}");
            S("online.peerleft", "A player left the room.", "Một người chơi đã rời phòng.");
            S("online.disconnected", "Connection to the server was lost.", "Mất kết nối với máy chủ.");

            // Spin / cue angle editor (touch)
            S("spin.title", "SPIN & CUE ANGLE", "XOÁY & GÓC CƠ");
            S("spin.hint", "Tap to place the tip · drag to fine-tune", "Chạm để chọn điểm đánh · kéo để chỉnh tinh");
            S("spin.center", "Centre", "Giữa");
            S("spin.top", "Follow", "Bi lên");
            S("spin.bottom", "Draw", "Bi về");
            S("spin.left", "Left", "Trái");
            S("spin.right", "Right", "Phải");
            S("spin.angle", "Cue angle {0}°", "Góc cơ {0}°");
            S("spin.level", "Level", "Nằm ngang");
            S("spin.done", "DONE", "XONG");
            S("spin.offset", "Tip {0}", "Điểm đánh {0}");
        }
    }
}
