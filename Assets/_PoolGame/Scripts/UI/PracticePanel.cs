using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using VTG.Pool.Localization;
using VTG.Pool.Match;

namespace VTG.Pool.UI
{
    /// <summary>
    /// Practice side panel: session counters, Undo shot, Arrange balls (with a tray to put balls back),
    /// rack choice (8-ball / 9-ball / empty) and Re-rack. Visible only while practice is running.
    /// </summary>
    public sealed class PracticePanel : MonoBehaviour
    {
        private const float Width = 300f;
        private const float CollapsedHeight = 330f;
        private const float TrayIcon = 42f;

        private static readonly string[] RackNames = { "practice.rack8", "practice.rack9", "practice.empty" };

        private PracticeSession session;
        private PracticeLayoutEditor editor;
        private RectTransform panel;
        private Text stats;
        private Button undoButton;
        private Button arrangeButton;
        private Text arrangeText;
        private Text rackText;
        private RectTransform tray;
        private Text trayHint;
        private readonly List<Button> trayButtons = new List<Button>(15);
        private bool dirty = true;

        public RectTransform Rect => panel;

        public static PracticePanel Create(RectTransform parent, PracticeSession practice, PracticeLayoutEditor layoutEditor)
        {
            RectTransform rect = UiKit.Rect(parent, "PracticePanel", new Vector2(0f, 1f), new Vector2(24f, -170f), new Vector2(Width, CollapsedHeight), UiKit.Panel);
            PracticePanel component = rect.gameObject.AddComponent<PracticePanel>();
            component.Build(rect, practice, layoutEditor);
            return component;
        }

        private void Build(RectTransform rect, PracticeSession practice, PracticeLayoutEditor layoutEditor)
        {
            panel = rect;
            session = practice;
            editor = layoutEditor;
            const float buttonWidth = Width - 40f;
            UiKit.Label(panel, "practice.title", 26, TextAnchor.UpperLeft, new Vector2(0.5f, 1f), new Vector2(0f, -14f), new Vector2(buttonWidth, 34f), UiKit.Accent, FontStyle.Bold);
            stats = UiKit.Label(panel, string.Empty, 17, TextAnchor.UpperLeft, new Vector2(0.5f, 1f), new Vector2(0f, -50f), new Vector2(buttonWidth, 44f), UiKit.Muted);

            undoButton = UiKit.Button(panel, "practice.undo", new Vector2(0.5f, 1f), new Vector2(0f, -104f), new Vector2(buttonWidth, 48f), () => session.Undo(), 20);
            arrangeButton = UiKit.Button(panel, "practice.arrange", new Vector2(0.5f, 1f), new Vector2(0f, -160f), new Vector2(buttonWidth, 48f),
                () => editor.SetEditing(!editor.IsEditing), 20);
            arrangeText = arrangeButton.GetComponentInChildren<Text>();
            Button rack = UiKit.Button(panel, RackNames[0], new Vector2(0.5f, 1f), new Vector2(0f, -216f), new Vector2(buttonWidth, 48f), null, 20);
            rackText = rack.GetComponentInChildren<Text>();
            rack.onClick.AddListener(() =>
            {
                editor.SetEditing(false);
                session.SetRack((PracticeRack)(((int)session.Rack + 1) % RackNames.Length));
            });
            UiKit.Button(panel, "practice.rerack", new Vector2(0.5f, 1f), new Vector2(0f, -272f), new Vector2(buttonWidth, 48f), () =>
            {
                editor.SetEditing(false);
                session.SetRack(session.Rack);
            }, 20);

            tray = UiKit.Rect(panel, "Tray", new Vector2(0.5f, 1f), new Vector2(0f, -330f), new Vector2(buttonWidth, 190f));
            trayHint = UiKit.Label(tray, "practice.hinttray",
                14, TextAnchor.UpperLeft, new Vector2(0.5f, 1f), Vector2.zero, new Vector2(buttonWidth, 54f), UiKit.Muted);
            for (int number = 1; number <= 15; number++)
            {
                int ball = number;
                RectTransform icon = UiKit.Rect(tray, $"Add{number}", new Vector2(0f, 1f), Vector2.zero, new Vector2(TrayIcon, TrayIcon), Color.white);
                Image image = icon.GetComponent<Image>();
                image.sprite = BallIconFactory.ForBall(number);
                Button button = icon.gameObject.AddComponent<Button>();
                button.onClick.AddListener(() => session.AddBall(ball));
                trayButtons.Add(button);
            }

            session.Changed += MarkDirty;
            editor.EditingChanged += MarkDirty;
            Loc.Changed += MarkDirty;
        }

        private void OnDestroy()
        {
            if (session != null) session.Changed -= MarkDirty;
            if (editor != null) editor.EditingChanged -= MarkDirty;
            Loc.Changed -= MarkDirty;
        }

        private void MarkDirty() => dirty = true;

        /// <summary>Puts the panel on the left (default) or right edge, below the player panels.</summary>
        public void SetSide(bool right)
        {
            float x = right ? 1f : 0f;
            panel.anchorMin = panel.anchorMax = panel.pivot = new Vector2(x, 1f);
            panel.anchoredPosition = new Vector2(right ? -24f : 24f, -170f);
        }

        private void Update()
        {
            bool active = session != null && session.IsActive;
            Graphic background = panel.GetComponent<Graphic>();
            if (background.enabled != active)
            {
                background.enabled = active;
                for (int i = 0; i < panel.childCount; i++)
                {
                    panel.GetChild(i).gameObject.SetActive(active);
                }

                dirty = true;
            }

            if (!active)
            {
                return;
            }

            undoButton.interactable = session.CanUndo;
            arrangeButton.interactable = editor.IsEditing || editor.CanEdit;
            string line = Loc.T("practice.stats", session.Shots, session.BallsPocketed, session.Scratches);
            if (stats.text != line)
            {
                stats.text = line;
            }

            if (dirty)
            {
                Refresh();
            }
        }

        private void Refresh()
        {
            dirty = false;
            rackText.text = Loc.T(RackNames[(int)session.Rack]);
            arrangeText.text = Loc.T(editor.IsEditing ? "practice.done" : "practice.arrange");
            bool editing = editor.IsEditing;
            tray.gameObject.SetActive(editing);
            int shown = 0;
            if (editing)
            {
                List<int> off = session.BallsOffTable();
                for (int i = 0; i < trayButtons.Count; i++)
                {
                    int number = i + 1;
                    bool show = off.Contains(number);
                    trayButtons[i].gameObject.SetActive(show);
                    if (show)
                    {
                        var rect = (RectTransform)trayButtons[i].transform;
                        rect.anchoredPosition = new Vector2((shown % 6) * (TrayIcon + 2f), -58f - (shown / 6) * (TrayIcon + 2f));
                        shown++;
                    }
                }

                trayHint.text = Loc.T(off.Count > 0 ? "practice.hinttray" : "practice.hint");
            }

            int rows = editing ? Mathf.Max(0, (shown + 5) / 6) : 0;
            float height = editing ? CollapsedHeight + 64f + rows * (TrayIcon + 2f) : CollapsedHeight;
            panel.sizeDelta = new Vector2(Width, height);
        }
    }
}
