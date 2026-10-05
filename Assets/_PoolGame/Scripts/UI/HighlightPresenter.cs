using UnityEngine;
using UnityEngine.UI;
using VTG.Pool.Audio;
using VTG.Pool.Localization;
using VTG.Pool.Match;
using VTG.Pool.Replay;

namespace VTG.Pool.UI
{
    /// <summary>
    /// Screen effects for notable shots (after any replay):
    /// - lucky shot ("bida rùa"): a turtle crawls across the screen, a wobbling "RÙA!" and a teasing line, low "wah";
    /// - multi-ball pot: a big "x2 / x3" with a burst ring and a short screen shake;
    /// - the deciding 8 / 9 is handled by the victory title (<see cref="PoolHud"/>);
    /// - foul / miss stamps, plus gags: the cue ball's ghost floating up after a scratch, a tumbleweed and crickets
    ///   when nothing was hit, sweat drops and a "boing" when a ball hangs on the lip, a rain cloud and a sad
    ///   trombone after three misses in a row, and "TOANG!" when the shooter loses the rack.
    /// </summary>
    public sealed class HighlightPresenter : MonoBehaviour
    {
        private static readonly string[] TrollKeys = { "troll.1", "troll.2", "troll.3", "troll.4", "troll.5" };

        private RectTransform root;
        private ShotCallouts callouts;
        private AudioManager audioManager;
        private RectTransform shakeTarget;

        private RectTransform turtle;
        private CanvasGroup trollGroup;
        private Text trollTitle;
        private Text trollLine;
        private float trollTime = -1f;

        private CanvasGroup comboGroup;
        private Text comboText;
        private RectTransform comboRing;
        private float comboTime = -1f;
        private float shake;

        private Image vignette;
        private CanvasGroup stampGroup;
        private Text stampTitle;
        private Text stampLine;
        private float stampTime = -1f;
        private bool stampIsFoul;

        public enum Gag
        {
            None,
            Ghost,
            Tumbleweed,
            Sweat,
            RainCloud,
            Toang
        }

        private CanvasGroup gagGroup;
        private RectTransform gagImage;
        private RectTransform gagImage2;
        private Text gagTitle;
        private Text gagLine;
        private float gagTime = -1f;
        private Gag gag;
        private readonly System.Collections.Generic.Dictionary<int, int> missStreak = new System.Collections.Generic.Dictionary<int, int>();

        public bool GagShowing => gagTime >= 0f;

        public Gag CurrentGag => gagTime >= 0f ? gag : Gag.None;

        public bool StampShowing => stampTime >= 0f;

        public bool LastStampWasFoul => stampIsFoul;

        public bool TrollShowing => trollTime >= 0f;

        public bool ComboShowing => comboTime >= 0f;

        public static HighlightPresenter Create(RectTransform parent, ShotCallouts shotCallouts, AudioManager audio, RectTransform shakeRoot)
        {
            RectTransform rect = UiKit.Rect(parent, "Highlights", new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            HighlightPresenter presenter = rect.gameObject.AddComponent<HighlightPresenter>();
            presenter.Build(rect, shotCallouts, audio, shakeRoot);
            return presenter;
        }

        private void Build(RectTransform rect, ShotCallouts shotCallouts, AudioManager audio, RectTransform shakeRoot)
        {
            root = rect;
            callouts = shotCallouts;
            audioManager = audio;
            shakeTarget = shakeRoot;

            // Troll.
            RectTransform troll = UiKit.Rect(root, "Troll", new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            troll.anchorMin = Vector2.zero;
            troll.anchorMax = Vector2.one;
            trollGroup = troll.gameObject.AddComponent<CanvasGroup>();
            trollGroup.alpha = 0f;
            trollGroup.blocksRaycasts = false;
            turtle = UiKit.Rect(troll, "Turtle", new Vector2(0f, 0f), new Vector2(-300f, 110f), new Vector2(256f, 160f), Color.white);
            Image turtleImage = turtle.GetComponent<Image>();
            turtleImage.sprite = FunSprites.Turtle;
            turtleImage.raycastTarget = false;
            trollTitle = UiKit.Label(troll, string.Empty, 130, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0f, 90f), new Vector2(1400f, 170f),
                new Color(0.55f, 1f, 0.35f), FontStyle.Bold);
            Outline titleOutline = trollTitle.gameObject.AddComponent<Outline>();
            titleOutline.effectColor = new Color(0.05f, 0.25f, 0.05f, 0.9f);
            titleOutline.effectDistance = new Vector2(5f, -5f);
            trollLine = UiKit.Label(troll, string.Empty, 40, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0f, -20f), new Vector2(1500f, 60f), Color.white, FontStyle.Bold);
            trollLine.gameObject.AddComponent<Shadow>().effectDistance = new Vector2(3f, -3f);

            // Multi-ball.
            RectTransform combo = UiKit.Rect(root, "Combo", new Vector2(0.5f, 0.5f), new Vector2(0f, 60f), new Vector2(600f, 300f));
            comboGroup = combo.gameObject.AddComponent<CanvasGroup>();
            comboGroup.alpha = 0f;
            comboGroup.blocksRaycasts = false;
            comboRing = UiKit.Rect(combo, "Ring", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(300f, 300f), new Color(1f, 0.85f, 0.3f, 0.8f));
            Image ring = comboRing.GetComponent<Image>();
            ring.sprite = FunSprites.Rays;
            ring.raycastTarget = false;
            comboText = UiKit.Label(combo, string.Empty, 170, TextAnchor.MiddleCenter, Vector2.one * 0.5f, Vector2.zero, new Vector2(600f, 220f), new Color(1f, 0.85f, 0.3f), FontStyle.Bold);
            Outline comboOutline = comboText.gameObject.AddComponent<Outline>();
            comboOutline.effectColor = new Color(0.5f, 0.15f, 0f, 0.9f);
            comboOutline.effectDistance = new Vector2(5f, -5f);

            // Foul / miss.
            RectTransform vignetteRect = UiKit.Fill(root, "FoulVignette", new Color(1f, 0.1f, 0.1f, 0f));
            vignetteRect.offsetMin = Vector2.zero; // the glow must sit on the screen edges, not beyond them
            vignetteRect.offsetMax = Vector2.zero;
            vignette = vignetteRect.GetComponent<Image>();
            vignette.sprite = FunSprites.Vignette;
            vignette.raycastTarget = false;
            RectTransform stamp = UiKit.Rect(root, "Stamp", new Vector2(0.5f, 0.5f), new Vector2(0f, 80f), new Vector2(1400f, 260f));
            stampGroup = stamp.gameObject.AddComponent<CanvasGroup>();
            stampGroup.alpha = 0f;
            stampGroup.blocksRaycasts = false;
            stampTitle = UiKit.Label(stamp, string.Empty, 120, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0f, 40f), new Vector2(1400f, 160f), Color.white, FontStyle.Bold);
            Outline stampOutline = stampTitle.gameObject.AddComponent<Outline>();
            stampOutline.effectColor = new Color(0.2f, 0f, 0f, 0.9f);
            stampOutline.effectDistance = new Vector2(5f, -5f);
            stampLine = UiKit.Label(stamp, string.Empty, 36, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0f, -60f), new Vector2(1400f, 60f), Color.white, FontStyle.Bold);
            stampLine.gameObject.AddComponent<Shadow>().effectDistance = new Vector2(3f, -3f);

            // Gags (above the stamp).
            RectTransform gagRoot = UiKit.Rect(root, "Gag", new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            gagRoot.anchorMin = Vector2.zero;
            gagRoot.anchorMax = Vector2.one;
            gagGroup = gagRoot.gameObject.AddComponent<CanvasGroup>();
            gagGroup.alpha = 0f;
            gagGroup.blocksRaycasts = false;
            gagImage = UiKit.Rect(gagRoot, "GagImage", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(160f, 220f), Color.white);
            gagImage.GetComponent<Image>().raycastTarget = false;
            gagImage2 = UiKit.Rect(gagRoot, "GagImage2", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(64f, 96f), Color.white);
            gagImage2.GetComponent<Image>().raycastTarget = false;
            gagTitle = UiKit.Label(gagRoot, string.Empty, 72, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0f, -240f), new Vector2(1600f, 100f), Color.white, FontStyle.Bold);
            Outline gagOutline = gagTitle.gameObject.AddComponent<Outline>();
            gagOutline.effectColor = new Color(0f, 0f, 0f, 0.85f);
            gagOutline.effectDistance = new Vector2(4f, -4f);
            gagLine = UiKit.Label(gagRoot, string.Empty, 32, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0f, -302f), new Vector2(1600f, 50f), Color.white, FontStyle.Bold);
            gagLine.gameObject.AddComponent<Shadow>().effectDistance = new Vector2(3f, -3f);

            if (callouts != null)
            {
                callouts.HighlightDetected += HandleHighlight;
            }
        }

        /// <summary>Plays a gag (also used by tests and screenshots).</summary>
        public void ShowGag(Gag kind, int count = 0)
        {
            gag = kind;
            gagTime = 0f;
            Image image = gagImage.GetComponent<Image>();
            Image image2 = gagImage2.GetComponent<Image>();
            image2.enabled = false;
            image.enabled = true;
            gagImage.localRotation = Quaternion.identity;
            gagImage.localScale = Vector3.one;
            gagTitle.rectTransform.localRotation = Quaternion.identity;
            gagTitle.rectTransform.localScale = Vector3.one;
            switch (kind)
            {
                case Gag.Ghost:
                    image.sprite = FunSprites.Ghost;
                    gagImage.sizeDelta = new Vector2(160f, 220f);
                    gagTitle.text = Loc.T("gag.ghost");
                    gagTitle.color = new Color(0.85f, 0.92f, 1f);
                    gagLine.text = Loc.T("gag.ghost.line");
                    Play(ProceduralSfx.Ghost, 0.6f);
                    break;
                case Gag.Tumbleweed:
                    image.sprite = FunSprites.Tumbleweed;
                    gagImage.sizeDelta = new Vector2(150f, 150f);
                    gagTitle.text = Loc.T("gag.crickets");
                    gagTitle.color = new Color(0.85f, 0.75f, 0.55f);
                    gagLine.text = Loc.T("gag.crickets.line");
                    Play(ProceduralSfx.Crickets, 0.55f);
                    break;
                case Gag.Sweat:
                    image.sprite = FunSprites.Sweat;
                    image2.sprite = FunSprites.Sweat;
                    image2.enabled = true;
                    gagImage.sizeDelta = new Vector2(64f, 96f);
                    gagTitle.text = Loc.T("gag.lip");
                    gagTitle.color = new Color(0.6f, 0.85f, 1f);
                    gagLine.text = Loc.T("gag.lip.line");
                    Play(ProceduralSfx.Boing, 0.6f);
                    break;
                case Gag.RainCloud:
                    image.sprite = FunSprites.RainCloud;
                    gagImage.sizeDelta = new Vector2(230f, 170f);
                    gagTitle.text = Loc.T("gag.streak", count);
                    gagTitle.color = new Color(0.7f, 0.78f, 0.9f);
                    gagLine.text = Loc.T("gag.streak.line");
                    Play(ProceduralSfx.SadTrombone, 0.7f);
                    break;
                case Gag.Toang:
                    image.sprite = FunSprites.RainCloud;
                    gagImage.sizeDelta = new Vector2(230f, 170f);
                    gagTitle.text = Loc.T("gag.toang");
                    gagTitle.color = new Color(1f, 0.4f, 0.3f);
                    gagLine.text = Loc.T("gag.toang.line");
                    shake = 0.35f;
                    Play(ProceduralSfx.SadTrombone, 0.75f);
                    break;
            }
        }

        private void Play(AudioClip clip, float volume)
        {
            if (audioManager != null) audioManager.PlayClip(clip, volume);
        }

        /// <summary>Misses and fouls in a row by this shooter (a pot or a continued turn resets it).</summary>
        private int CountStreak(ShotHighlight highlight)
        {
            bool bad = highlight.Missed || (highlight.Foul && !highlight.GameOver);
            missStreak.TryGetValue(highlight.Shooter, out int streak);
            streak = bad ? streak + 1 : 0;
            missStreak[highlight.Shooter] = streak;
            return streak;
        }

        /// <summary>Foul: red screen edge, a slammed "FOUL!" stamp naming the foul, buzzer.</summary>
        public void ShowFoul(Rules.FoulType foul)
        {
            stampIsFoul = true;
            stampTime = 0f;
            stampTitle.text = Loc.T("fx.foul");
            stampTitle.color = new Color(1f, 0.25f, 0.2f);
            stampLine.text = DescribeFoul(foul);
            shake = 0.25f;
            if (audioManager != null) audioManager.PlayFoul();
        }

        /// <summary>Miss: a grey "MISS" drops in and wobbles, soft "aww".</summary>
        public void ShowMiss()
        {
            stampIsFoul = false;
            stampTime = 0f;
            stampTitle.text = Loc.T("fx.miss");
            stampTitle.color = new Color(0.82f, 0.85f, 0.9f);
            stampLine.text = Loc.T(MissKeys[Random.Range(0, MissKeys.Length)]);
            if (audioManager != null) audioManager.PlayMiss();
        }

        private static readonly string[] MissKeys = { "miss.1", "miss.2", "miss.3", "miss.4" };

        private static string DescribeFoul(Rules.FoulType foul)
        {
            switch (foul)
            {
                case Rules.FoulType.Scratch: return Loc.T("fx.foul.scratch");
                case Rules.FoulType.CueBallOffTable:
                case Rules.FoulType.ObjectBallOffTable: return Loc.T("fx.foul.offtable");
                case Rules.FoulType.NoContact: return Loc.T("fx.foul.nocontact");
                case Rules.FoulType.WrongBallFirst: return Loc.T("fx.foul.wrongball");
                case Rules.FoulType.NoRailAfterContact: return Loc.T("fx.foul.norail");
                case Rules.FoulType.IllegalBreak: return Loc.T("fx.foul.break");
                default: return string.Empty;
            }
        }

        private void OnDestroy()
        {
            if (callouts != null)
            {
                callouts.HighlightDetected -= HandleHighlight;
            }
        }

        private void HandleHighlight(ShotHighlight highlight)
        {
            int streak = CountStreak(highlight);
            if (highlight.LosingShot)
            {
                ShotReplay.RunAfterReplay(this, () => ShowGag(Gag.Toang));
                return;
            }

            if (highlight.Foul && !highlight.GameOver)
            {
                Rules.FoulType foul = highlight.FoulType;
                Gag extra = foul == Rules.FoulType.Scratch ? Gag.Ghost
                    : foul == Rules.FoulType.NoContact ? Gag.Tumbleweed
                    : streak >= 3 ? Gag.RainCloud : Gag.None;
                ShotReplay.RunAfterReplay(this, () =>
                {
                    ShowFoul(foul);
                    if (extra != Gag.None) ShowGag(extra, streak);
                });
                return;
            }

            if (highlight.Missed)
            {
                bool lip = highlight.NearMiss;
                ShotReplay.RunAfterReplay(this, () =>
                {
                    ShowMiss();
                    if (streak >= 3) ShowGag(Gag.RainCloud, streak);
                    else if (lip) ShowGag(Gag.Sweat);
                });
                return;
            }

            if (highlight.Fluke)
            {
                ShotReplay.RunAfterReplay(this, ShowTroll);
            }
            else if (highlight.Pocketed.Count >= 2 && !highlight.WasBreak && !highlight.Foul)
            {
                int count = highlight.Pocketed.Count;
                ShotReplay.RunAfterReplay(this, () => ShowCombo(count));
            }
        }

        /// <summary>Shows the lucky-shot troll (also used by tests).</summary>
        public void ShowTroll()
        {
            trollTime = 0f;
            trollTitle.text = Loc.T("troll.title");
            trollLine.text = Loc.T(TrollKeys[Random.Range(0, TrollKeys.Length)]);
            if (audioManager != null) audioManager.PlayTroll();
        }

        public void ShowCombo(int count)
        {
            comboTime = 0f;
            comboText.text = "x" + count;
            Color color = count >= 3 ? new Color(1f, 0.45f, 0.9f) : new Color(1f, 0.85f, 0.3f);
            comboText.color = color;
            comboRing.GetComponent<Image>().color = new Color(color.r, color.g, color.b, 0.8f);
            shake = 0.35f;
            if (audioManager != null) audioManager.PlayFanfare(count >= 3 ? 1.45f : 1.25f);
        }

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;
            if (trollTime >= 0f)
            {
                trollTime += dt;
                float t = trollTime;
                trollGroup.alpha = Mathf.Clamp01(t / 0.2f) * (1f - Mathf.Clamp01((t - 3.2f) / 0.5f));
                float width = root.rect.width;
                turtle.anchoredPosition = new Vector2(Mathf.Lerp(-300f, width + 300f, t / 3.7f), 110f + Mathf.Abs(Mathf.Sin(t * 9f)) * 10f);
                turtle.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(t * 9f) * 4f);
                float pop = t < 0.3f ? Mathf.Lerp(0.3f, 1.15f, t / 0.3f) : 1f + 0.05f * Mathf.Sin(t * 12f);
                trollTitle.rectTransform.localScale = new Vector3(pop, pop, 1f);
                trollTitle.rectTransform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(t * 7f) * 8f);
                if (t > 3.7f)
                {
                    trollTime = -1f;
                    trollGroup.alpha = 0f;
                }
            }

            if (comboTime >= 0f)
            {
                comboTime += dt;
                float t = comboTime;
                comboGroup.alpha = Mathf.Clamp01(t / 0.1f) * (1f - Mathf.Clamp01((t - 1.6f) / 0.4f));
                float pop = t < 0.18f ? Mathf.Lerp(2.6f, 0.9f, t / 0.18f) : Mathf.Lerp(0.9f, 1f, Mathf.Clamp01((t - 0.18f) / 0.12f));
                comboText.rectTransform.localScale = new Vector3(pop, pop, 1f);
                float ringScale = 0.6f + t * 1.4f;
                comboRing.localScale = new Vector3(ringScale, ringScale, 1f);
                comboRing.localRotation = Quaternion.Euler(0f, 0f, t * 60f);
                if (t > 2f)
                {
                    comboTime = -1f;
                    comboGroup.alpha = 0f;
                }
            }

            if (stampTime >= 0f)
            {
                stampTime += dt;
                float t = stampTime;
                float life = stampIsFoul ? 1.8f : 1.4f;
                stampGroup.alpha = Mathf.Clamp01(t / 0.08f) * (1f - Mathf.Clamp01((t - (life - 0.4f)) / 0.4f));
                if (stampIsFoul)
                {
                    // Slam in (big → overshoot → settle), tilted like a rubber stamp; red edges pulse twice.
                    float slam = t < 0.12f ? Mathf.Lerp(2.4f, 0.9f, t / 0.12f) : Mathf.Lerp(0.9f, 1f, Mathf.Clamp01((t - 0.12f) / 0.1f));
                    stampTitle.rectTransform.localScale = new Vector3(slam, slam, 1f);
                    stampTitle.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -8f);
                    float pulse = Mathf.Clamp01(1f - t / 0.9f) * (0.6f + 0.4f * Mathf.Abs(Mathf.Sin(t * 10f)));
                    vignette.color = new Color(1f, 0.1f, 0.1f, 0.75f * pulse);
                }
                else
                {
                    // Drop in from above and wobble.
                    float drop = Mathf.Clamp01(t / 0.25f);
                    stampTitle.rectTransform.anchoredPosition = new Vector2(0f, Mathf.Lerp(260f, 40f, 1f - (1f - drop) * (1f - drop)));
                    stampTitle.rectTransform.localScale = Vector3.one * 0.75f;
                    stampTitle.rectTransform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(t * 8f) * 5f * (1f - drop * 0.5f));
                }

                if (t > life)
                {
                    stampTime = -1f;
                    stampGroup.alpha = 0f;
                    vignette.color = new Color(1f, 0.1f, 0.1f, 0f);
                    stampTitle.rectTransform.anchoredPosition = new Vector2(0f, 40f);
                }
            }

            if (gagTime >= 0f)
            {
                gagTime += dt;
                UpdateGag(gagTime);
            }

            if (shakeTarget != null)
            {
                if (shake > 0f)
                {
                    shake -= dt;
                    float amount = 14f * Mathf.Clamp01(shake / 0.35f);
                    shakeTarget.anchoredPosition = new Vector2(Random.Range(-amount, amount), Random.Range(-amount, amount));
                }
                else if (shakeTarget.anchoredPosition != Vector2.zero)
                {
                    shakeTarget.anchoredPosition = Vector2.zero;
                }
            }
        }

        private void UpdateGag(float t)
        {
            float life = gag == Gag.RainCloud || gag == Gag.Toang ? 3.2f : gag == Gag.Tumbleweed ? 3.4f : 2.6f;
            gagGroup.alpha = Mathf.Clamp01(t / 0.2f) * (1f - Mathf.Clamp01((t - (life - 0.5f)) / 0.5f));
            float width = root.rect.width;
            float height = root.rect.height;
            switch (gag)
            {
                case Gag.Ghost:
                {
                    // Rises from the bottom, swaying, getting a little smaller.
                    float rise = Mathf.Clamp01(t / life);
                    gagImage.anchoredPosition = new Vector2(Mathf.Sin(t * 3f) * 60f, Mathf.Lerp(-height * 0.35f, height * 0.3f, rise));
                    gagImage.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(t * 3f) * 10f);
                    gagImage.localScale = Vector3.one * Mathf.Lerp(1.1f, 0.7f, rise);
                    break;
                }
                case Gag.Tumbleweed:
                {
                    // Rolls across the bottom with little hops.
                    float x = Mathf.Lerp(-width * 0.5f - 150f, width * 0.5f + 150f, t / life);
                    gagImage.anchoredPosition = new Vector2(x, -height * 0.41f + Mathf.Abs(Mathf.Sin(t * 4.5f)) * 40f);
                    gagImage.localRotation = Quaternion.Euler(0f, 0f, -t * 260f);
                    break;
                }
                case Gag.Sweat:
                {
                    // Two drops slide down beside the title, which trembles.
                    gagImage.anchoredPosition = new Vector2(-440f, -190f - (t % 1.3f) / 1.3f * 120f);
                    gagImage2.anchoredPosition = new Vector2(450f, -200f - ((t + 0.6f) % 1.3f) / 1.3f * 120f);
                    gagTitle.rectTransform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(t * 22f) * 3f * Mathf.Clamp01(1f - t / 1.2f));
                    break;
                }
                default:
                {
                    // Cloud hangs over the title and bobs; "TOANG" slams in.
                    gagImage.anchoredPosition = new Vector2(Mathf.Sin(t * 1.5f) * 30f, -125f + Mathf.Sin(t * 2.2f) * 8f);
                    if (gag == Gag.Toang)
                    {
                        float slam = t < 0.15f ? Mathf.Lerp(2.6f, 0.9f, t / 0.15f) : Mathf.Lerp(0.9f, 1f, Mathf.Clamp01((t - 0.15f) / 0.1f));
                        gagTitle.rectTransform.localScale = new Vector3(slam * 1.4f, slam * 1.4f, 1f);
                        gagTitle.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 6f);
                    }

                    break;
                }
            }

            if (t > life)
            {
                gagTime = -1f;
                gagGroup.alpha = 0f;
            }
        }
    }
}
