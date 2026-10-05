using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using VTG.Pool.Aiming;
using VTG.Pool.Balls;
using VTG.Pool.Core;
using VTG.Pool.Cue;
using VTG.Pool.Match;
using VTG.Pool.Table;

namespace VTG.Pool.Simulation.Testing
{
    /// <summary>
    /// Drives 03_PhysicsTest: lays out repeatable presets, fires their shots and logs the resulting
    /// ShotRecord. F3..F12 run presets, R re-lays the last preset without shooting, P pauses.
    /// Development tool: uses the keyboard directly, not gameplay input.
    /// </summary>
    public sealed class PhysicsTestController : MonoBehaviour
    {
        [SerializeField] private PhysicsTestPresetLibrary library;
        [SerializeField] private TableBuilder table;
        [SerializeField] private ShotController shotController;
        [SerializeField] private AimSystem aimSystem;
        [SerializeField] private CueBallSpinController spinController;
        [SerializeField] private List<PoolBall> balls = new List<PoolBall>(16);
        [SerializeField] private int startPreset;
        [SerializeField] private bool shootStartPreset;
        [SerializeField] private bool showPanel = true;
        [SerializeField, Tooltip("Gap between racked balls (m).")] private float rackGap = 0.0002f;

        private static readonly Key[] PresetKeys = { Key.F3, Key.F4, Key.F5, Key.F6, Key.F7, Key.F8, Key.F9, Key.F10, Key.F11, Key.F12 };

        private readonly Vector3[] rackPositions = new Vector3[15];
        private int lastPreset = -1;
        private bool paused;

        public int LastPresetIndex => lastPreset;

        public IReadOnlyList<PhysicsTestPreset> Presets => library != null ? library.presets : null;

        private void OnEnable()
        {
            PoolEvents.BallsStopped += LogShot;
        }

        private void OnDisable()
        {
            PoolEvents.BallsStopped -= LogShot;
            if (paused)
            {
                Time.timeScale = 1f;
                paused = false;
            }
        }

        private void Start()
        {
            if (library != null && library.presets.Count > 0)
            {
                Run(Mathf.Clamp(startPreset, 0, library.presets.Count - 1), shootStartPreset);
            }
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null || library == null)
            {
                return;
            }

            for (int i = 0; i < PresetKeys.Length && i < library.presets.Count; i++)
            {
                if (keyboard[PresetKeys[i]].wasPressedThisFrame)
                {
                    Run(i, true);
                }
            }

            if (keyboard.rKey.wasPressedThisFrame)
            {
                Run(Mathf.Max(0, lastPreset), false);
            }

            if (keyboard.pKey.wasPressedThisFrame)
            {
                paused = !paused;
                Time.timeScale = paused ? 0f : 1f;
            }
        }

        /// <summary>Lays out a preset and optionally fires its shot on the next physics step.</summary>
        public void Run(int index, bool shoot)
        {
            if (library == null || index < 0 || index >= library.presets.Count)
            {
                return;
            }

            StopAllCoroutines();
            StartCoroutine(RunRoutine(index, shoot));
        }

        private IEnumerator RunRoutine(int index, bool shoot)
        {
            PhysicsTestPreset preset = library.presets[index];
            lastPreset = index;
            LayOut(preset);
            shotController.ResetToAiming();

            Vector3 aim = ComputeAim(preset);
            aimSystem.SetAimDirection(aim);
            spinController.SetTipOffset(preset.tipOffset);

            // Let PhysX settle the new layout for one step so every run starts from the same state.
            yield return new WaitForFixedUpdate();

            if (shoot && preset.autoShoot)
            {
                Debug.Log($"[PhysicsTest] Running '{preset.name}': {preset.expectation}");
                shotController.ExecuteShot(new ShotParameters(aim, preset.power, preset.tipOffset));
            }
        }

        /// <summary>Places all balls for a preset; unused balls are disabled.</summary>
        public void LayOut(PhysicsTestPreset preset)
        {
            float radius = BallRadius();
            if (table.PocketManager != null)
            {
                table.PocketManager.ClearCaptured();
            }

            if (preset.rack == PresetRack.EightBallTriangle)
            {
                RackLayout.GetTrianglePositions(table.FootSpot + Vector3.up * radius, radius, rackGap, rackPositions);
            }
            else if (preset.rack == PresetRack.NineBallDiamond)
            {
                RackLayout.GetDiamondPositions(table.FootSpot + Vector3.up * radius, radius, rackGap, rackPositions);
            }

            for (int i = 0; i < balls.Count; i++)
            {
                PoolBall ball = balls[i];
                if (ball == null)
                {
                    continue;
                }

                if (TryGetPlacement(preset, ball, radius, out Vector3 position))
                {
                    ball.gameObject.SetActive(true);
                    ball.PlaceAt(position);
                }
                else
                {
                    ball.gameObject.SetActive(false);
                }
            }
        }

        private bool TryGetPlacement(PhysicsTestPreset preset, PoolBall ball, float radius, out Vector3 position)
        {
            if (ball.IsCueBall)
            {
                position = table.BallRestPosition(preset.cueBall, radius);
                return true;
            }

            int[] order = preset.rack == PresetRack.EightBallTriangle ? RackLayout.EightBallOrder
                : preset.rack == PresetRack.NineBallDiamond ? RackLayout.NineBallOrder : null;
            if (order != null)
            {
                for (int i = 0; i < order.Length; i++)
                {
                    if (order[i] == ball.BallId)
                    {
                        position = rackPositions[i];
                        return true;
                    }
                }
            }

            for (int i = 0; i < preset.objectBalls.Count; i++)
            {
                if (preset.objectBalls[i].ballId == ball.BallId)
                {
                    position = table.BallRestPosition(preset.objectBalls[i].position, radius);
                    return true;
                }
            }

            position = Vector3.zero;
            return false;
        }

        private Vector3 ComputeAim(PhysicsTestPreset preset)
        {
            float radius = BallRadius();
            Vector3 cue = table.BallRestPosition(preset.cueBall, radius);
            switch (preset.aimMode)
            {
                case PresetAimMode.Angle:
                    return Quaternion.Euler(0f, preset.aimAngle, 0f) * Vector3.forward;
                case PresetAimMode.AtPoint:
                    return GhostBallMath.Flatten(table.BallRestPosition(preset.aimPoint, radius) - cue).normalized;
                case PresetAimMode.AtBall:
                case PresetAimMode.PotToPoint:
                {
                    PoolBall target = FindBall(preset.aimBallId);
                    if (target == null)
                    {
                        return Vector3.forward;
                    }

                    Vector3 objectBall = target.Position;
                    if (preset.aimMode == PresetAimMode.PotToPoint)
                    {
                        Vector3 ghost = GhostBallMath.GhostBallForTarget(objectBall, table.BallRestPosition(preset.aimPoint, radius), radius);
                        return GhostBallMath.Flatten(ghost - cue).normalized;
                    }

                    Vector3 toBall = GhostBallMath.Flatten(objectBall - cue).normalized;
                    Vector3 right = Vector3.Cross(Vector3.up, toBall);
                    Vector3 aimPoint = objectBall + right * (preset.cutFraction * 2f * radius);
                    return GhostBallMath.Flatten(aimPoint - cue).normalized;
                }
                default:
                    return Vector3.forward;
            }
        }

        private PoolBall FindBall(int id)
        {
            for (int i = 0; i < balls.Count; i++)
            {
                if (balls[i] != null && balls[i].BallId == id && balls[i].gameObject.activeInHierarchy)
                {
                    return balls[i];
                }
            }

            return null;
        }

        private float BallRadius()
        {
            PoolBall cue = shotController.CueBall;
            return cue != null ? cue.Radius : PoolConstants.BallRadius;
        }

        private void LogShot(ShotRecord record)
        {
            string presetName = lastPreset >= 0 && library != null ? library.presets[lastPreset].name : "manual";
            PoolBall cue = shotController.CueBall;
            Vector3 cueEnd = cue != null ? cue.Position : Vector3.zero;
            Debug.Log($"[PhysicsTest] '{presetName}' finished. {record}. Cue ball rest: {cueEnd:F3}");
        }

        private void OnGUI()
        {
            if (!showPanel || library == null || !Debug.isDebugBuild)
            {
                return;
            }

            GUILayout.BeginArea(new Rect(10f, 10f, 200f, 66f + library.presets.Count * 26f), GUI.skin.box);
            GUILayout.Label(paused ? "Physics presets (PAUSED)" : "Physics presets");
            for (int i = 0; i < library.presets.Count; i++)
            {
                string label = (i < 10 ? $"F{i + 3}  " : string.Empty) + library.presets[i].name;
                if (GUILayout.Button(label))
                {
                    Run(i, true);
                }
            }

            if (GUILayout.Button("Main menu"))
            {
                Time.timeScale = 1f;
                UnityEngine.SceneManagement.SceneManager.LoadScene("01_MainMenu");
            }

            GUILayout.EndArea();
        }
    }
}
