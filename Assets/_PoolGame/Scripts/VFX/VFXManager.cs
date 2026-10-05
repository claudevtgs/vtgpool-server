using UnityEngine;
using VTG.Pool.Balls;
using VTG.Pool.Core;
using VTG.Pool.Match;
using VTG.Pool.Table;

namespace VTG.Pool.VFX
{
    /// <summary>
    /// Restrained, realistic effects (MASTER_PROMPT section 27): a small chalk puff where the cue tip meets the
    /// cue ball and a faint dust puff when a ball drops into a pocket. Two pre-built particle systems are
    /// re-positioned and emitted on demand (no runtime instantiation).
    /// </summary>
    public sealed class VFXManager : MonoBehaviour
    {
        [SerializeField] private Material particleMaterial;
        [SerializeField] private Color chalkColor = new Color(0.55f, 0.72f, 1f, 0.55f);
        [SerializeField] private Color dustColor = new Color(0.8f, 0.8f, 0.85f, 0.25f);
        [SerializeField, Range(0, 40)] private int chalkParticles = 14;
        [SerializeField, Range(0, 40)] private int dustParticles = 8;
        [SerializeField] private bool effectsEnabled = true;

        private ParticleSystem chalk;
        private ParticleSystem dust;
        private ParticleSystem sparks;
        private ParticleSystem fireworks;
        private ParticleSystem confetti;
        private Pocket lastPocket;
        private Vector3 tableCenter;
        private float celebrateUntil;
        private float nextBurst;

        private static readonly Color[] FireworkColors =
        {
            new Color(1f, 0.78f, 0.25f), new Color(0.35f, 0.75f, 1f), new Color(1f, 0.35f, 0.4f), new Color(0.5f, 1f, 0.55f), new Color(0.95f, 0.6f, 1f)
        };

        /// <summary>A celebration is running.</summary>
        public bool Celebrating => Time.unscaledTime < celebrateUntil;

        public bool EffectsEnabled
        {
            get => effectsEnabled;
            set => effectsEnabled = value;
        }

        private void Awake()
        {
            chalk = CreateSystem("ChalkPuff", chalkColor, 0.0025f, 0.007f, 0.35f, 0.7f, 0.04f, 0.18f, -0.05f);
            dust = CreateSystem("PocketDust", dustColor, 0.006f, 0.016f, 0.5f, 1f, 0.03f, 0.1f, -0.02f);
            sparks = CreateSystem("WinSparks", new Color(1f, 0.82f, 0.35f), 0.008f, 0.022f, 0.6f, 1.3f, 0.6f, 1.6f, 0.9f, 160);
            fireworks = CreateSystem("Fireworks", Color.white, 0.024f, 0.055f, 0.9f, 1.6f, 0.8f, 1.5f, 0.25f, 600);
            confetti = CreateSystem("Confetti", Color.white, 0.016f, 0.032f, 2.5f, 4f, 0.05f, 0.3f, 0.12f, 500);
            ParticleSystem.ShapeModule burst = fireworks.shape;
            burst.shapeType = ParticleSystemShapeType.Sphere;
            burst.radius = 0.02f;
            ParticleSystem.MainModule confettiMain = confetti.main;
            var palette = new Gradient();
            palette.SetKeys(
                new[] { new GradientColorKey(FireworkColors[0], 0f), new GradientColorKey(FireworkColors[1], 0.25f), new GradientColorKey(FireworkColors[2], 0.5f),
                    new GradientColorKey(FireworkColors[3], 0.75f), new GradientColorKey(FireworkColors[4], 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
            confettiMain.startColor = new ParticleSystem.MinMaxGradient(palette) { mode = ParticleSystemGradientMode.RandomColor };
            ParticleSystem.ShapeModule rain = confetti.shape;
            rain.shapeType = ParticleSystemShapeType.Box;
            rain.scale = new Vector3(2.4f, 0.05f, 1.4f);
            ParticleSystem.LimitVelocityOverLifetimeModule floaty = confetti.limitVelocityOverLifetime;
            floaty.drag = 1.2f;
            ParticleSystem.LimitVelocityOverLifetimeModule burstDrag = fireworks.limitVelocityOverLifetime;
            burstDrag.drag = 1.5f;
        }

        private void OnEnable()
        {
            PoolEvents.ShotStarted += HandleShotStarted;
            PoolEvents.BallPocketed += HandlePocketed;
            PoolEvents.GameWon += HandleGameWon;
        }

        private void OnDisable()
        {
            PoolEvents.ShotStarted -= HandleShotStarted;
            PoolEvents.BallPocketed -= HandlePocketed;
            PoolEvents.GameWon -= HandleGameWon;
        }

        private void HandleGameWon(int winnerIndex, string message)
        {
            Replay.ShotReplay.RunAfterReplay(this, CelebrateLastPocket);
        }

        private void CelebrateLastPocket()
        {
            var table = FindAnyObjectByType<VTG.Pool.Table.TableBuilder>();
            Celebrate(lastPocket != null ? lastPocket.Center : (table != null ? table.SurfaceCenter : transform.position),
                table != null ? table.SurfaceCenter : transform.position);
        }

        /// <summary>End-of-rack celebration: golden sparks from the last pocket, fireworks over the table, confetti.</summary>
        public void Celebrate(Vector3 pocketPosition, Vector3 center)
        {
            if (!effectsEnabled || sparks == null)
            {
                return;
            }

            tableCenter = center;
            celebrateUntil = Time.unscaledTime + 3.5f;
            nextBurst = Time.unscaledTime + 0.35f;
            sparks.transform.SetPositionAndRotation(pocketPosition + Vector3.up * 0.02f, Quaternion.LookRotation(Vector3.up));
            sparks.Emit(140);
            confetti.transform.SetPositionAndRotation(center + Vector3.up * 1.4f, Quaternion.LookRotation(Vector3.down));
            confetti.Emit(320);
        }

        private void Update()
        {
            if (fireworks == null || Time.unscaledTime >= celebrateUntil || Time.unscaledTime < nextBurst)
            {
                return;
            }

            // A firework burst every ~0.6 s somewhere above the table.
            nextBurst = Time.unscaledTime + Random.Range(0.45f, 0.75f);
            Vector3 position = tableCenter + new Vector3(Random.Range(-1.1f, 1.1f), Random.Range(1.1f, 1.6f), Random.Range(-0.6f, 0.6f));
            fireworks.transform.position = position;
            var emit = new ParticleSystem.EmitParams { startColor = FireworkColors[Random.Range(0, FireworkColors.Length)], applyShapeToPosition = true };
            fireworks.Emit(emit, 110);
            ParticleSystem.EmitParams falling = new ParticleSystem.EmitParams { applyShapeToPosition = true };
            for (int i = 0; i < 24; i++)
            {
                falling.startColor = FireworkColors[Random.Range(0, FireworkColors.Length)];
                confetti.Emit(falling, 1);
            }
        }

        private int potsThisShot;
        private int moneyBall = -1;

        private void HandleShotStarted(ShotRecord record)
        {
            potsThisShot = 0;
            var turns = FindAnyObjectByType<TurnManager>();
            moneyBall = turns == null || turns.State == null || turns.Rules is Rules.Practice.PracticeRuleSet ? -1
                : turns.State.Mode == Rules.GameMode.NineBall ? 9 : 8;
            if (!effectsEnabled || chalk == null)
            {
                return;
            }

            Vector3 aim = record.AimDirection;
            aim.y = 0f;
            aim = aim.sqrMagnitude > 1e-6f ? aim.normalized : Vector3.forward;
            Vector3 contact = record.CueBallPosition - aim * PoolConstants.BallRadius;
            chalk.transform.SetPositionAndRotation(contact, Quaternion.LookRotation(-aim, Vector3.up));
            int count = Mathf.RoundToInt(chalkParticles * Mathf.Lerp(0.4f, 1f, record.Power));
            chalk.Emit(count);
        }

        private void HandlePocketed(PoolBall ball, Pocket pocket)
        {
            if (!effectsEnabled || dust == null || pocket == null)
            {
                return;
            }

            lastPocket = pocket;
            if (!ball.IsCueBall)
            {
                potsThisShot++;
                bool money = ball.BallId == moneyBall;
                if (money || potsThisShot >= 2)
                {
                    Color color = money ? (moneyBall == 8 ? new Color(1f, 0.78f, 0.25f) : new Color(1f, 0.95f, 0.3f))
                        : potsThisShot >= 3 ? new Color(1f, 0.45f, 0.9f) : new Color(0.45f, 0.85f, 1f);
                    sparks.transform.SetPositionAndRotation(pocket.Center + Vector3.up * 0.02f, Quaternion.LookRotation(Vector3.up));
                    var emit = new ParticleSystem.EmitParams { startColor = color, applyShapeToPosition = true };
                    sparks.Emit(emit, money ? 120 : 60);
                }
            }

            dust.transform.SetPositionAndRotation(pocket.Center + Vector3.up * 0.01f, Quaternion.LookRotation(Vector3.up));
            dust.Emit(dustParticles);
        }

        private ParticleSystem CreateSystem(string systemName, Color color, float minSize, float maxSize, float minLife, float maxLife,
            float minSpeed, float maxSpeed, float gravity, int maxParticles = 64)
        {
            var systemObject = new GameObject(systemName);
            systemObject.transform.SetParent(transform, false);
            systemObject.layer = PoolLayers.AimHelper;
            ParticleSystem system = systemObject.AddComponent<ParticleSystem>();
            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            ParticleSystem.MainModule main = system.main;
            main.playOnAwake = false;
            main.loop = false;
            main.duration = 1f;
            main.maxParticles = maxParticles;
            main.useUnscaledTime = true;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = new ParticleSystem.MinMaxCurve(minLife, maxLife);
            main.startSpeed = new ParticleSystem.MinMaxCurve(minSpeed, maxSpeed);
            main.startSize = new ParticleSystem.MinMaxCurve(minSize, maxSize);
            main.startColor = color;
            main.gravityModifier = gravity;

            ParticleSystem.EmissionModule emission = system.emission;
            emission.enabled = false;

            ParticleSystem.ShapeModule shape = system.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 35f;
            shape.radius = 0.004f;

            ParticleSystem.ColorOverLifetimeModule fade = system.colorOverLifetime;
            fade.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
            fade.color = gradient;

            ParticleSystem.SizeOverLifetimeModule grow = system.sizeOverLifetime;
            grow.enabled = true;
            grow.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.6f, 1f, 1.6f));

            ParticleSystem.LimitVelocityOverLifetimeModule drag = system.limitVelocityOverLifetime;
            drag.enabled = true;
            drag.drag = 3f;

            ParticleSystemRenderer renderer = system.GetComponent<ParticleSystemRenderer>();
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            if (particleMaterial != null)
            {
                renderer.sharedMaterial = particleMaterial;
            }

            return system;
        }
    }
}
