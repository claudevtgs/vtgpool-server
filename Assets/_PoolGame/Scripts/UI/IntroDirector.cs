using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using VTG.Pool.Audio;
using VTG.Pool.Balls;
using VTG.Pool.Core;
using VTG.Pool.Localization;
using VTG.Pool.Table;

namespace VTG.Pool.UI
{
    /// <summary>
    /// Opening sequence in the main-menu scene (once per launch, started by <see cref="Bootstrap"/>):
    /// black → "presents" → the table lamps flicker on → a low dolly across the cloth → a real physics break
    /// (cue ball at ~10.6 m/s) in slow motion from the side → crane up over the spreading rack → title slam with a
    /// flash → hand-over to the menu orbit. Letterboxed; any key, click or tap skips.
    /// </summary>
    public sealed class IntroDirector : MonoBehaviour
    {
        private const float BreakTime = 3.6f;
        private const float TitleTime = 6.3f;
        private const float EndTime = 9.2f;

        /// <summary>Set by Bootstrap: play the intro when the menu opens next.</summary>
        public static bool PlayOnNextMenu { get; set; }

        [SerializeField] private float breakSpeed = 10.6f;
        [SerializeField, Range(0.05f, 1f)] private float slowMotion = 0.22f;
        [SerializeField] private float slowMotionSeconds = 1.1f;

        private Camera view;
        private MenuCameraOrbit orbit;
        private MainMenuController menu;
        private TableBuilder table;
        private AudioManager audioManager;
        private PoolBall cueBall;
        private Vector3 rackApex;
        private readonly List<(Light light, float intensity, float delay)> lamps = new List<(Light, float, float)>();

        private RectTransform canvas;
        private Image black;
        private Image flash;
        private RectTransform barTop;
        private RectTransform barBottom;
        private Text presents;
        private Text title;
        private Text tagline;
        private Text skipHint;

        private float time;
        private bool struck;
        private bool slowed;
        private bool hit;
        private float slowEnd;
        private bool titleShown;
        private bool finished;

        public bool Playing => !finished;

        /// <summary>Starts the intro if Bootstrap asked for it. Returns false when the menu should show at once.</summary>
        public static bool TryStart(MainMenuController menu)
        {
            if (!PlayOnNextMenu)
            {
                return false;
            }

            PlayOnNextMenu = false;
            var go = new GameObject("Intro");
            IntroDirector intro = go.AddComponent<IntroDirector>();
            intro.menu = menu;
            return true;
        }

        private void Start()
        {
            view = Camera.main;
            orbit = view != null ? view.GetComponent<MenuCameraOrbit>() : null;
            if (orbit != null) orbit.enabled = false;
            table = FindAnyObjectByType<TableBuilder>();
            audioManager = FindAnyObjectByType<AudioManager>();
            cueBall = BallRegistry.FindCueBall();
            rackApex = table != null ? table.FootSpot : Vector3.zero;
            foreach (Light light in FindObjectsByType<Light>(FindObjectsInactive.Exclude))
            {
                if (light.type == LightType.Directional && light.intensity <= 0f)
                {
                    continue;
                }

                lamps.Add((light, light.intensity, 1.3f + Random.Range(0f, 0.9f)));
                light.intensity = 0f;
            }

            PoolEvents.BallHit += HandleBallHit;
            BuildOverlay();
        }

        private void OnDestroy()
        {
            PoolEvents.BallHit -= HandleBallHit;
            if (slowed && !finished)
            {
                Time.timeScale = 1f;
            }
        }

        private void BuildOverlay()
        {
            canvas = UiKit.CreateCanvas(transform, "Intro Canvas", 60);
            black = UiKit.Fill(canvas, "Black", Color.black).GetComponent<Image>();
            flash = UiKit.Fill(canvas, "Flash", new Color(1f, 1f, 1f, 0f)).GetComponent<Image>();
            flash.raycastTarget = false;
            barTop = Bar(new Vector2(0.5f, 1f));
            barBottom = Bar(new Vector2(0.5f, 0f));
            presents = UiKit.Label(canvas, "intro.presents", 26, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1400f, 60f), new Color(1f, 1f, 1f, 0f));
            presents.fontStyle = FontStyle.Bold;
            title = UiKit.Label(canvas, "VTG POOL 3D", 150, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0f, 40f), new Vector2(1800f, 200f), new Color(1f, 1f, 1f, 0f), FontStyle.Bold);
            Shadow glow = title.gameObject.AddComponent<Outline>();
            glow.effectColor = new Color(1f, 0.7f, 0.2f, 0.55f);
            glow.effectDistance = new Vector2(3f, -3f);
            tagline = UiKit.Label(canvas, "intro.tagline", 34, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0f, -80f), new Vector2(1600f, 60f), new Color(1f, 0.78f, 0.25f, 0f));
            skipHint = UiKit.Label(canvas, "intro.skip", 18, TextAnchor.LowerRight, new Vector2(1f, 0f), new Vector2(-30f, 30f), new Vector2(700f, 40f), new Color(1f, 1f, 1f, 0.45f));
        }

        private RectTransform Bar(Vector2 anchor)
        {
            RectTransform bar = UiKit.Rect(canvas, "Letterbox", anchor, Vector2.zero, new Vector2(4000f, 130f), Color.black);
            bar.GetComponent<Image>().raycastTarget = false;
            return bar;
        }

        private void HandleBallHit(BallHitInfo info)
        {
            if (!struck || hit || cueBall == null || !info.Involves(cueBall))
            {
                return;
            }

            hit = true;
            Time.timeScale = slowMotion;
            slowed = true;
            slowEnd = Time.unscaledTime + slowMotionSeconds;
        }

        private void Update()
        {
            if (finished)
            {
                return;
            }

            time += Time.unscaledDeltaTime;
            bool skip = (Keyboard.current != null && Keyboard.current.anyKey.wasPressedThisFrame)
                        || (Pointer.current != null && Pointer.current.press.wasPressedThisFrame);
            if ((skip && time > 0.3f) || time >= EndTime)
            {
                Finish();
                return;
            }

            if (slowed && Time.unscaledTime >= slowEnd)
            {
                Time.timeScale = 1f;
                slowed = false;
            }

            // Fades and lights.
            black.color = new Color(0f, 0f, 0f, 1f - Smooth(1.2f, 2.4f));
            presents.color = new Color(1f, 1f, 1f, Smooth(0.3f, 0.9f) * (1f - Smooth(1.6f, 2.1f)));
            foreach ((Light light, float intensity, float delay) in lamps)
            {
                float on = Smooth(delay, delay + 0.35f);
                bool flicker = time > delay && time < delay + 0.35f && Mathf.PerlinNoise(time * 23f, delay * 7f) < 0.45f;
                light.intensity = flicker ? intensity * 0.15f : intensity * on;
            }

            // Title slam.
            if (!titleShown && time >= TitleTime)
            {
                titleShown = true;
                if (audioManager != null) audioManager.PlayCallout();
            }

            float titleIn = Smooth(TitleTime, TitleTime + 0.25f);
            float titleOut = 1f - Smooth(EndTime - 0.8f, EndTime);
            float scale = Mathf.Lerp(1.8f, 1f, titleIn);
            title.rectTransform.localScale = new Vector3(scale, scale, 1f);
            title.color = new Color(1f, 1f, 1f, titleIn * titleOut);
            tagline.color = new Color(1f, 0.78f, 0.25f, Smooth(TitleTime + 0.5f, TitleTime + 1f) * titleOut);
            flash.color = new Color(1f, 1f, 1f, titleShown ? 0.55f * (1f - Smooth(TitleTime, TitleTime + 0.6f)) : 0f);
            float bars = 1f - Smooth(EndTime - 0.8f, EndTime);
            barTop.sizeDelta = new Vector2(4000f, 130f * bars);
            barBottom.sizeDelta = new Vector2(4000f, 130f * bars);

            // The break.
            if (!struck && time >= BreakTime && cueBall != null)
            {
                struck = true;
                Vector3 direction = rackApex - cueBall.Position;
                direction.y = 0f;
                cueBall.ApplyStrike(direction.normalized * breakSpeed, Vector3.zero);
            }
        }

        private void LateUpdate()
        {
            if (finished || view == null || table == null || cueBall == null)
            {
                return;
            }

            Vector3 up = Vector3.up;
            Vector3 along = (rackApex - table.HeadSpot);
            along.y = 0f;
            along = along.sqrMagnitude > 1e-6f ? along.normalized : Vector3.forward;
            Vector3 side = Vector3.Cross(up, along);
            Vector3 cue = cueBall.Position;
            Vector3 position;
            Vector3 look;
            float fov;
            if (time < BreakTime)
            {
                // Low dolly from the corner of the head rail to behind the cue ball.
                float t = Smooth(0.8f, BreakTime);
                Vector3 start = table.HeadSpot - along * 0.6f + side * 0.9f + up * 0.06f;
                Vector3 end = cue - along * 0.7f + up * 0.16f;
                position = Vector3.Lerp(start, end, t);
                look = Vector3.Lerp(cue, rackApex, 0.5f + 0.5f * t);
                fov = Mathf.Lerp(55f, 42f, t);
            }
            else if (!hit || slowed)
            {
                // Chase the cue ball; at impact cut to the low side shot of the rack.
                if (!hit)
                {
                    position = cue - along * 0.55f + up * 0.14f;
                    look = cue + along * 0.6f;
                    fov = 46f;
                }
                else
                {
                    position = rackApex + side * 0.8f - along * 0.25f + up * 0.12f;
                    look = rackApex + along * 0.05f;
                    fov = 34f;
                }
            }
            else
            {
                // Crane up over the spreading rack.
                float t = Smooth(BreakTime + 1.5f, TitleTime + 1.2f);
                Vector3 low = rackApex + side * 0.8f - along * 0.25f + up * 0.12f;
                Vector3 high = table.SurfaceCenter - along * 1.6f + side * 0.6f + up * 1.9f;
                position = Vector3.Lerp(low, high, t);
                look = Vector3.Lerp(rackApex, table.SurfaceCenter, t);
                fov = Mathf.Lerp(34f, 48f, t);
            }

            view.transform.SetPositionAndRotation(position, Quaternion.LookRotation(look - position, up));
            view.fieldOfView = fov;
        }

        /// <summary>Ends the intro (or skips it): lights on, normal time, menu and its camera orbit take over.</summary>
        public void Finish()
        {
            if (finished)
            {
                return;
            }

            finished = true;
            Time.timeScale = 1f;
            foreach ((Light light, float intensity, float _) in lamps)
            {
                if (light != null) light.intensity = intensity;
            }

            if (orbit != null)
            {
                orbit.enabled = true;
                orbit.BlendIn(1.5f);
            }

            if (menu != null)
            {
                menu.ShowAfterIntro();
            }

            Destroy(gameObject);
        }

        private float Smooth(float from, float to) => Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(from, to, time));
    }
}
