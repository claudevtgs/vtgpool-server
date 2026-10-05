using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using VTG.Pool.Balls;
using VTG.Pool.CameraSystem;
using VTG.Pool.Core;
using VTG.Pool.Cue;
using VTG.Pool.Localization;
using VTG.Pool.Match;
using VTG.Pool.Table;
using VTG.Pool.UI;

namespace VTG.Pool.Replay
{
    /// <summary>
    /// Slow-motion replay of pots. Every shot is recorded (ball transforms at ~60 Hz plus pocket events); after a
    /// shot that pocketed a ball (setting: every pot / highlights only / off) the end of the shot is replayed at
    /// 0.4× with visual stand-ins for the balls — the real balls and the physics are not touched — and a pocket
    /// camera on each ball as it drops. Letterbox + REPLAY badge; any key/click skips. Celebrations wait for it
    /// (<see cref="RunAfterReplay"/>).
    /// </summary>
    public sealed class ShotReplay : MonoBehaviour
    {
        public enum Mode
        {
            EveryPot,
            Highlights,
            Off
        }

        private const float SampleInterval = 1f / 60f;
        private const float MaxRecordSeconds = 25f;

        private struct Frame
        {
            public float Time;
            public Vector3[] Positions;
            public Quaternion[] Rotations;
            public bool[] Visible;
        }

        private struct PocketEvent
        {
            public float Time;
            public int Ball;
            public Vector3 Pocket;
        }

        [SerializeField, Range(0.1f, 1f)] private float speed = 0.4f;
        [SerializeField] private float leadIn = 1.6f;
        [SerializeField] private float tail = 0.8f;

        private readonly List<Frame> frames = new List<Frame>(1024);
        private readonly List<PocketEvent> pockets = new List<PocketEvent>(8);
        private readonly List<PoolBall> balls = new List<PoolBall>(16);
        private readonly List<GameObject> proxies = new List<GameObject>(16);
        private readonly List<Renderer> hidden = new List<Renderer>(32);
        private ShotController shot;
        private CameraController cameras;
        private ShotCallouts callouts;
        private TableBuilder table;
        private bool recording;
        private float recordStart;
        private float lastSample;
        private bool playing;
        private float playTime;
        private float playEnd;
        private int pocketCursor;
        private CameraMode previousMode;
        private RectTransform overlay;
        private Vector3 camPosition;
        private Quaternion camRotation;
        private Vector3 camVelocity;
        private bool camInitialized;
        private int lastPocketShown = -1;

        public static ShotReplay Instance { get; private set; }

        /// <summary>A replay is on screen.</summary>
        public static bool IsPlaying => Instance != null && Instance.playing;

        /// <summary>Player setting.</summary>
        public static Mode Setting { get; set; } = Mode.EveryPot;

        /// <summary>Tests switch replays off globally.</summary>
        public static bool ForceDisabled { get; set; }

        public static event Action Finished;

        /// <summary>Runs <paramref name="action"/> once no replay is playing (waits one frame first so a replay
        /// started by the same shot is seen).</summary>
        public static void RunAfterReplay(MonoBehaviour owner, Action action)
        {
            if (owner == null || !owner.isActiveAndEnabled)
            {
                action();
                return;
            }

            owner.StartCoroutine(WaitThenRun(action));
        }

        private static IEnumerator WaitThenRun(Action action)
        {
            yield return null;
            while (IsPlaying)
            {
                yield return null;
            }

            action();
        }

        private void Awake()
        {
            Instance = this;
        }

        private void Start()
        {
            shot = FindAnyObjectByType<ShotController>();
            cameras = FindAnyObjectByType<CameraController>();
            callouts = FindAnyObjectByType<ShotCallouts>();
            table = FindAnyObjectByType<TableBuilder>();
            if (callouts != null)
            {
                callouts.HighlightDetected += HandleHighlight;
            }
        }

        private void OnEnable()
        {
            PoolEvents.ShotStarted += HandleShotStarted;
            PoolEvents.BallsStopped += HandleBallsStopped;
            PoolEvents.BallPocketed += HandleBallPocketed;
        }

        private void OnDisable()
        {
            PoolEvents.ShotStarted -= HandleShotStarted;
            PoolEvents.BallsStopped -= HandleBallsStopped;
            PoolEvents.BallPocketed -= HandleBallPocketed;
            if (callouts != null)
            {
                callouts.HighlightDetected -= HandleHighlight;
            }

            Stop();
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        // ------------------------------------------------------------ recording

        private void HandleShotStarted(ShotRecord record)
        {
            if (playing)
            {
                Stop();
            }

            frames.Clear();
            pockets.Clear();
            balls.Clear();
            foreach (PoolBall ball in BallRegistry.Balls)
            {
                balls.Add(ball);
            }

            recording = true;
            recordStart = Time.time;
            lastSample = -1f;
            Sample();
        }

        private void HandleBallsStopped(ShotRecord record)
        {
            if (recording)
            {
                Sample();
            }

            recording = false;
        }

        private void HandleBallPocketed(PoolBall ball, Pocket pocket)
        {
            if (!recording || pocket == null || ball.IsCueBall)
            {
                return;
            }

            pockets.Add(new PocketEvent { Time = Time.time - recordStart, Ball = balls.IndexOf(ball), Pocket = pocket.Center });
        }

        private void LateUpdate()
        {
            if (recording && Time.time - recordStart < MaxRecordSeconds && Time.time - lastSample >= SampleInterval)
            {
                Sample();
            }

            if (playing)
            {
                UpdatePlayback();
            }
        }

        private void Sample()
        {
            lastSample = Time.time;
            int count = balls.Count;
            var frame = new Frame
            {
                Time = Time.time - recordStart,
                Positions = new Vector3[count],
                Rotations = new Quaternion[count],
                Visible = new bool[count]
            };

            for (int i = 0; i < count; i++)
            {
                PoolBall ball = balls[i];
                if (ball == null)
                {
                    continue;
                }

                frame.Positions[i] = ball.transform.position;
                frame.Rotations[i] = ball.transform.rotation;
                frame.Visible[i] = ball.gameObject.activeInHierarchy;
            }

            frames.Add(frame);
        }

        // ------------------------------------------------------------ deciding

        private void HandleHighlight(ShotHighlight highlight)
        {
            if (ForceDisabled || Setting == Mode.Off || pockets.Count == 0 || frames.Count < 4)
            {
                return;
            }

            bool notable = highlight.MoneyBall > 0 || highlight.Pocketed.Count >= 2 || highlight.Fluke || highlight.Bank
                           || highlight.Kick || highlight.Combo || highlight.WinningShot;
            if (Setting == Mode.Highlights && !notable)
            {
                return;
            }

            Play();
        }

        /// <summary>Plays the recorded shot from shortly before the first pot to just after the last one.</summary>
        public bool Play()
        {
            if (playing || pockets.Count == 0 || frames.Count < 4 || cameras == null)
            {
                return false;
            }

            float first = pockets[0].Time;
            float last = pockets[pockets.Count - 1].Time;
            playTime = Mathf.Max(0f, first - leadIn);
            playEnd = Mathf.Min(frames[frames.Count - 1].Time, last + tail);
            pocketCursor = 0;
            lastPocketShown = -1;
            camInitialized = false;

            ShotCinematics cinematics = cameras.GetComponent<ShotCinematics>();
            if (cinematics != null)
            {
                cinematics.End();
            }

            previousMode = cameras.ActiveMode == CameraMode.Cinematic ? CameraMode.Cue : cameras.ActiveMode;
            CreateProxies();
            BuildOverlay();
            cameras.CinematicOverride = ReplayPose;
            cameras.SetMode(CameraMode.Cinematic);
            if (shot != null)
            {
                shot.PresentationLocked = true;
            }

            playing = true;
            ApplyFrame(playTime);
            return true;
        }

        /// <summary>Ends the replay at once (skip, a remote shot arriving, scene change).</summary>
        public void Stop()
        {
            if (!playing)
            {
                return;
            }

            playing = false;
            foreach (GameObject proxy in proxies)
            {
                if (proxy != null) Destroy(proxy);
            }

            proxies.Clear();
            foreach (Renderer renderer in hidden)
            {
                if (renderer != null) renderer.enabled = true;
            }

            hidden.Clear();
            if (overlay != null)
            {
                Canvas canvas = overlay.GetComponentInParent<Canvas>();
                Destroy(canvas != null ? canvas.gameObject : overlay.gameObject);
                overlay = null;
            }

            if (cameras != null)
            {
                cameras.CinematicOverride = null;
                cameras.SetMode(previousMode);
            }

            if (shot != null)
            {
                shot.PresentationLocked = false;
            }

            Finished?.Invoke();
        }

        private void UpdatePlayback()
        {
            bool skip = (Keyboard.current != null && Keyboard.current.anyKey.wasPressedThisFrame)
                        || (Pointer.current != null && Pointer.current.press.wasPressedThisFrame);
            playTime += Time.unscaledDeltaTime * speed;
            if (skip || playTime >= playEnd)
            {
                Stop();
                return;
            }

            ApplyFrame(playTime);
        }

        private void ApplyFrame(float time)
        {
            int index = FrameIndex(time);
            Frame a = frames[index];
            Frame b = frames[Mathf.Min(index + 1, frames.Count - 1)];
            float t = b.Time > a.Time ? Mathf.Clamp01((time - a.Time) / (b.Time - a.Time)) : 0f;
            for (int i = 0; i < proxies.Count; i++)
            {
                GameObject proxy = proxies[i];
                if (proxy == null)
                {
                    continue;
                }

                proxy.SetActive(a.Visible[i]);
                proxy.transform.SetPositionAndRotation(Vector3.Lerp(a.Positions[i], b.Positions[i], t), Quaternion.Slerp(a.Rotations[i], b.Rotations[i], t));
            }

            while (pocketCursor < pockets.Count - 1 && time > pockets[pocketCursor].Time + 0.35f)
            {
                pocketCursor++;
            }
        }

        private int FrameIndex(float time)
        {
            int low = 0;
            int high = frames.Count - 1;
            while (low < high)
            {
                int mid = (low + high + 1) / 2;
                if (frames[mid].Time <= time) low = mid;
                else high = mid - 1;
            }

            return low;
        }

        private Vector3 ProxyPosition(int ball)
        {
            return ball >= 0 && ball < proxies.Count && proxies[ball] != null ? proxies[ball].transform.position : pockets[pocketCursor].Pocket;
        }

        /// <summary>Pocket cam on the ball being replayed: behind the pocket, low, looking back along its path.</summary>
        private CameraPose ReplayPose()
        {
            PocketEvent target = pockets[pocketCursor];
            Vector3 ball = ProxyPosition(target.Ball);
            Vector3 toBall = ball - target.Pocket;
            toBall.y = 0f;
            if (toBall.sqrMagnitude < 0.0004f || playTime >= target.Time)
            {
                Vector3 center = table != null ? table.SurfaceCenter : Vector3.zero;
                toBall = center - target.Pocket;
                toBall.y = 0f;
            }

            toBall.Normalize();
            Vector3 position = target.Pocket - toBall * 0.34f + Vector3.up * 0.13f;
            Vector3 look = Vector3.Lerp(ball, target.Pocket, 0.35f) + Vector3.up * 0.01f;

            // Once the ball has dropped, crane up and back over the table instead of staring into the pocket.
            float after = Mathf.Clamp01((playTime - target.Time) / 0.7f);
            if (after > 0f)
            {
                // Always crane toward the table side of the pocket (the dropped ball may sit behind the pocket centre).
                Vector3 center = table != null ? table.SurfaceCenter : target.Pocket;
                Vector3 inward = center - target.Pocket;
                inward.y = 0f;
                inward = inward.sqrMagnitude > 1e-6f ? inward.normalized : toBall;
                Vector3 high = target.Pocket + inward * 1.05f + Vector3.up * 0.85f;
                position = Vector3.Lerp(position, high, Mathf.SmoothStep(0f, 1f, after));
                look = Vector3.Lerp(look, Vector3.Lerp(target.Pocket, center, 0.25f), Mathf.SmoothStep(0f, 1f, after));
            }
            Quaternion rotation = Quaternion.LookRotation(look - position, Vector3.up);
            float dt = Time.unscaledDeltaTime;
            if (!camInitialized || lastPocketShown != pocketCursor)
            {
                camPosition = position;
                camRotation = rotation;
                camVelocity = Vector3.zero;
                camInitialized = true;
                lastPocketShown = pocketCursor;
            }
            else
            {
                camPosition = Vector3.SmoothDamp(camPosition, position, ref camVelocity, 0.25f, Mathf.Infinity, dt);
                camRotation = Quaternion.Slerp(camRotation, rotation, 1f - Mathf.Exp(-8f * dt));
            }

            return new CameraPose(camPosition, camRotation, 40f);
        }

        private void CreateProxies()
        {
            proxies.Clear();
            hidden.Clear();
            foreach (PoolBall ball in balls)
            {
                if (ball == null)
                {
                    proxies.Add(null);
                    continue;
                }

                var proxy = new GameObject("Replay_" + ball.name);
                foreach (MeshRenderer renderer in ball.GetComponentsInChildren<MeshRenderer>(true))
                {
                    MeshFilter filter = renderer.GetComponent<MeshFilter>();
                    if (filter == null || filter.sharedMesh == null)
                    {
                        continue;
                    }

                    var visual = new GameObject(renderer.name);
                    visual.transform.SetParent(proxy.transform, false);
                    Transform source = renderer.transform;
                    visual.transform.localPosition = ball.transform.InverseTransformPoint(source.position);
                    visual.transform.localRotation = Quaternion.Inverse(ball.transform.rotation) * source.rotation;
                    visual.transform.localScale = source.lossyScale;
                    visual.AddComponent<MeshFilter>().sharedMesh = filter.sharedMesh;
                    MeshRenderer copy = visual.AddComponent<MeshRenderer>();
                    copy.sharedMaterials = renderer.sharedMaterials;
                    if (renderer.enabled)
                    {
                        renderer.enabled = false;
                        hidden.Add(renderer);
                    }
                }

                proxies.Add(proxy);
            }
        }

        private void BuildOverlay()
        {
            overlay = UiKit.CreateCanvas(transform, "Replay Canvas", 55);
            foreach (Vector2 anchor in new[] { new Vector2(0.5f, 1f), new Vector2(0.5f, 0f) })
            {
                RectTransform bar = UiKit.Rect(overlay, "Letterbox", anchor, Vector2.zero, new Vector2(4000f, 90f), Color.black);
                bar.GetComponent<Image>().raycastTarget = false;
            }

            RectTransform badge = UiKit.Rect(overlay, "Badge", new Vector2(0f, 1f), new Vector2(40f, -20f), new Vector2(260f, 50f));
            RectTransform dot = UiKit.Rect(badge, "Dot", new Vector2(0f, 0.5f), Vector2.zero, new Vector2(22f, 22f), new Color(0.95f, 0.15f, 0.15f));
            dot.GetComponent<Image>().sprite = BallIconFactory.Circle;
            dot.gameObject.AddComponent<ReplayBlink>();
            UiKit.Label(badge, "replay.badge", 30, TextAnchor.MiddleLeft, new Vector2(0f, 0.5f), new Vector2(34f, 0f), new Vector2(220f, 50f), Color.white, FontStyle.Bold);
            UiKit.Label(overlay, "replay.skip", 16, TextAnchor.LowerRight, new Vector2(1f, 0f), new Vector2(-30f, 24f), new Vector2(600f, 40f), new Color(1f, 1f, 1f, 0.5f));
        }
    }
}
