using System;
using UnityEngine;
using VTG.Pool.Localization;
using VTG.Pool.Save;

namespace VTG.Pool.UI
{
    /// <summary>
    /// Settings overlay (audio, graphics, gameplay) used by the main menu and the in-match pause menu.
    /// Changes apply live through <see cref="SaveSystem.SettingsChanged"/>; the file is written on close.
    /// </summary>
    public static class SettingsScreen
    {
        private static readonly string[] GraphicsOptions = { "quality.auto", "quality.low", "quality.medium", "quality.high", "quality.ultra" };
        private static readonly string[] CameraOptions = { "camera.cue", "camera.tactical", "camera.top" };
        private static readonly string[] AssistOptions = { "assist.none", "assist.minimal", "assist.standard", "assist.training" };

        private static readonly string[] TouchOptions = { "touch.auto", "touch.off", "touch.on" };
        private static readonly string[] HandOptions = { "hand.right", "hand.left" };
        private static readonly string[] LanguageOptions = { "language.auto", "language.en", "language.vi" };
        private static readonly string[] StrokeOptions = { "stroke.mouse", "stroke.hold" };
        private static readonly string[] CinematicOptions = { "cinematic.on", "cinematic.off" };
        private static readonly string[] ReplayOptions = { "replay.every", "replay.highlights", "replay.off" };

        /// <summary>Builds the overlay under <paramref name="canvas"/>; <paramref name="onClose"/> runs after saving.</summary>
        public static GameObject Build(RectTransform canvas, Action onClose)
        {
            GameSettings s = SaveSystem.Settings;
            RectTransform dim = UiKit.Fill(canvas, "SettingsOverlay", new Color(0f, 0f, 0f, 0.6f));
            RectTransform box = UiKit.Rect(dim, "Box", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1420f, 800f), UiKit.Panel);
            UiKit.Label(box, "settings.title", 40, TextAnchor.UpperCenter, new Vector2(0.5f, 1f), new Vector2(0f, -26f), new Vector2(760f, 56f), UiKit.Accent, FontStyle.Bold);

            // Two columns so the overlay fits phone screens: audio + graphics | gameplay + controls.
            const float width = 620f;
            const float left = -345f;
            const float right = 345f;
            float y = -100f;
            Section(box, "settings.audio", left, ref y);
            UiKit.Slider(box, "settings.master", new Vector2(left, y), width, s.masterVolume, v => Preview(x => x.masterVolume = v)); y -= 58f;
            UiKit.Slider(box, "settings.effects", new Vector2(left, y), width, s.effectsVolume, v => Preview(x => x.effectsVolume = v)); y -= 58f;
            UiKit.Slider(box, "settings.music", new Vector2(left, y), width, s.musicVolume, v => Preview(x => x.musicVolume = v)); y -= 58f;
            UiKit.Slider(box, "settings.ambience", new Vector2(left, y), width, s.ambienceVolume, v => Preview(x => x.ambienceVolume = v)); y -= 72f;
            Section(box, "settings.graphics", left, ref y);
            UiKit.Selector(box, "settings.quality", new Vector2(left, y), width, GraphicsOptions, s.graphicsTier + 1, i => Preview(x => x.graphicsTier = i - 1)); y -= 58f;
            UiKit.Selector(box, "settings.language", new Vector2(left, y), width, LanguageOptions, s.language + 1, i => Preview(x => x.language = i - 1)); y -= 58f;
            UiKit.Selector(box, "settings.cinematic", new Vector2(left, y), width, CinematicOptions, s.cinematicCamera ? 0 : 1, i => Preview(x => x.cinematicCamera = i == 0)); y -= 58f;
            UiKit.Selector(box, "settings.stroke", new Vector2(left, y), width, StrokeOptions, s.mouseStroke ? 0 : 1, i => Preview(x => x.mouseStroke = i == 0));

            y = -100f;
            Section(box, "settings.gameplay", right, ref y);
            UiKit.Selector(box, "settings.startcamera", new Vector2(right, y), width, CameraOptions, s.defaultCamera, i => Preview(x => x.defaultCamera = i)); y -= 58f;
            UiKit.Selector(box, "settings.assist", new Vector2(right, y), width, AssistOptions, s.aimAssist, i => Preview(x => x.aimAssist = i)); y -= 58f;
            UiKit.Slider(box, "settings.sensitivity", new Vector2(right, y), width, s.aimSensitivity, v => Preview(x => x.aimSensitivity = v), 0.1f, 3f,
                v => "×" + v.ToString("0.00")); y -= 58f;
            UiKit.Slider(box, "settings.placement", new Vector2(right, y), width, s.placementSensitivity, v => Preview(x => x.placementSensitivity = v), 0.1f, 2f,
                v => "×" + v.ToString("0.00")); y -= 72f;
            Section(box, "settings.controls", right, ref y);
            UiKit.Selector(box, "settings.touch", new Vector2(right, y), width, TouchOptions, s.touchControls + 1, i => Preview(x => x.touchControls = i - 1)); y -= 58f;
            UiKit.Selector(box, "settings.hand", new Vector2(right, y), width, HandOptions, s.leftHanded ? 1 : 0, i => Preview(x => x.leftHanded = i == 1)); y -= 58f;
            UiKit.Selector(box, "settings.replay", new Vector2(right, y), width, ReplayOptions, s.replayMode, i => Preview(x => x.replayMode = i));

            GameObject overlay = dim.gameObject;
            UiKit.Button(box, "common.back", new Vector2(0.5f, 0f), new Vector2(0f, 30f), new Vector2(300f, 60f), () =>
            {
                SaveSystem.SaveSettings();
                UnityEngine.Object.Destroy(overlay);
                onClose?.Invoke();
            });
            return overlay;
        }

        private static void Section(RectTransform box, string title, float x, ref float y)
        {
            UiKit.Label(box, title, 18, TextAnchor.MiddleLeft, new Vector2(0.5f, 1f), new Vector2(x, y), new Vector2(620f, 30f), UiKit.Muted, FontStyle.Bold);
            UiKit.Rect(box, "Rule", new Vector2(0.5f, 1f), new Vector2(x, y - 30f), new Vector2(620f, 2f), new Color(1f, 1f, 1f, 0.12f));
            y -= 44f;
        }

        private static void Preview(Action<GameSettings> change) => SaveSystem.UpdateSettings(change, false);
    }

    /// <summary>Lifetime statistics overlay with a reset option.</summary>
    public static class StatsScreen
    {
        public static GameObject Build(RectTransform canvas, Action onClose)
        {
            RectTransform dim = UiKit.Fill(canvas, "StatsOverlay", new Color(0f, 0f, 0f, 0.6f));
            RectTransform box = UiKit.Rect(dim, "Box", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(720f, 830f), UiKit.Panel);
            UiKit.Label(box, "stats.title", 40, TextAnchor.UpperCenter, new Vector2(0.5f, 1f), new Vector2(0f, -26f), new Vector2(660f, 56f), UiKit.Accent, FontStyle.Bold);
            UnityEngine.UI.Text labels = UiKit.Label(box, string.Empty, 24, TextAnchor.UpperLeft, new Vector2(0.5f, 1f), new Vector2(-90f, -110f), new Vector2(380f, 500f), UiKit.Muted);
            UnityEngine.UI.Text values = UiKit.Label(box, string.Empty, 24, TextAnchor.UpperRight, new Vector2(0.5f, 1f), new Vector2(150f, -110f), new Vector2(260f, 500f), UiKit.Text, FontStyle.Bold);
            labels.lineSpacing = 1.35f;
            values.lineSpacing = 1.35f;
            Fill(labels, values);

            GameObject overlay = dim.gameObject;
            UiKit.Button(box, "history.open", new Vector2(0.5f, 0f), new Vector2(0f, 104f), new Vector2(600f, 56f), () =>
            {
                dim.gameObject.SetActive(false);
                HistoryScreen.Build(canvas, () => { if (dim != null) dim.gameObject.SetActive(true); });
            });
            UiKit.Button(box, "common.reset", new Vector2(0.5f, 0f), new Vector2(-160f, 30f), new Vector2(280f, 60f), () =>
            {
                SaveSystem.ResetStats();
                Fill(labels, values);
            });
            UiKit.Button(box, "common.back", new Vector2(0.5f, 0f), new Vector2(160f, 30f), new Vector2(280f, 60f), () =>
            {
                UnityEngine.Object.Destroy(overlay);
                onClose?.Invoke();
            });
            return overlay;
        }

        private static void Fill(UnityEngine.UI.Text labels, UnityEngine.UI.Text values)
        {
            PlayerStats s = SaveSystem.Stats;
            labels.text = string.Join("\n", Loc.T("stats.matches"), Loc.T("stats.wins"), Loc.T("stats.winrate"), Loc.T("stats.streak"),
                Loc.T("stats.8wins"), Loc.T("stats.9wins"), Loc.T("stats.pocketed"), Loc.T("stats.breaks"), Loc.T("stats.avgshots"), Loc.T("stats.fouls"));
            values.text = string.Join("\n", s.matchesPlayed.ToString(), s.wins.ToString(), $"{s.WinRate * 100f:0}%",
                $"{s.currentWinStreak} ({s.bestWinStreak})", s.eightBallWins.ToString(), s.nineBallWins.ToString(), s.ballsPocketed.ToString(),
                $"{s.BreakSuccessRate * 100f:0}% ({s.successfulBreaks}/{s.breaks})", s.AverageShotsPerWin.ToString("0.0"), s.fouls.ToString());
        }
    }

    /// <summary>Finished online racks on this device (newest first): time, room, game, players, winner, room score.</summary>
    public static class HistoryScreen
    {
        public const int Shown = 10;

        public static GameObject Build(RectTransform canvas, Action onClose)
        {
            RectTransform dim = UiKit.Fill(canvas, "HistoryOverlay", new Color(0f, 0f, 0f, 0.6f));
            RectTransform box = UiKit.Rect(dim, "Box", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1180f, 900f), new Color(0.06f, 0.07f, 0.09f, 0.97f));
            UiKit.Label(box, "history.title", 40, TextAnchor.UpperCenter, new Vector2(0.5f, 1f), new Vector2(0f, -26f), new Vector2(1100f, 56f), UiKit.Accent, FontStyle.Bold);
            UnityEngine.UI.Text body = UiKit.Label(box, string.Empty, 21, TextAnchor.UpperLeft, new Vector2(0.5f, 1f), new Vector2(0f, -100f), new Vector2(1100f, 680f), UiKit.Text);
            body.name = "HistoryText";
            body.supportRichText = true;
            body.lineSpacing = 1.15f;
            body.text = Describe();
            GameObject overlay = dim.gameObject;
            UiKit.Button(box, "history.clear", new Vector2(0.5f, 0f), new Vector2(-160f, 30f), new Vector2(280f, 60f), () =>
            {
                SaveSystem.ClearHistory();
                body.text = Describe();
            });
            UiKit.Button(box, "common.back", new Vector2(0.5f, 0f), new Vector2(160f, 30f), new Vector2(280f, 60f), () =>
            {
                UnityEngine.Object.Destroy(overlay);
                onClose?.Invoke();
            });
            return overlay;
        }

        /// <summary>The newest records as text (rich-text colour tags for the result).</summary>
        public static string Describe()
        {
            var matches = SaveSystem.History.matches;
            if (matches.Count == 0)
            {
                return Loc.T("history.empty");
            }

            var text = new System.Text.StringBuilder();
            for (int i = matches.Count - 1, n = 0; i >= 0 && n < Shown; i--, n++)
            {
                OnlineMatchRecord r = matches[i];
                string tag = r.result > 0 ? "<color=#5BE37A>" + Loc.T("history.win") + "</color>"
                    : r.result == 0 ? "<color=#FF6B5B>" + Loc.T("history.loss") + "</color>"
                    : "<color=#9DB4FF>" + Loc.T("history.watched") + "</color>";
                string game = Loc.T(r.mode == 1 ? "opt.9ball" : "opt.8ball");
                string players = r.teams && r.players.Length == 4
                    ? r.players[0] + " & " + r.players[2] + " vs " + r.players[1] + " & " + r.players[3]
                    : string.Join(" vs ", r.players);
                text.Append(tag).Append("  ").Append(Loc.T("history.line", r.time, r.room, game, players, r.winner, r.score,
                    r.forfeit ? Loc.T("history.forfeit") : string.Empty)).Append('\n');
            }

            return text.ToString().TrimEnd('\n');
        }
    }
}
