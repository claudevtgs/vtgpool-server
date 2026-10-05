using System;
using System.Collections.Generic;
using UnityEngine;

namespace VTG.Pool.Simulation.Testing
{
    public enum PresetAimMode
    {
        /// <summary>Absolute yaw in degrees (0 = toward the foot rail, +90 = toward +X).</summary>
        Angle,

        /// <summary>Aim the cue ball at a table point.</summary>
        AtPoint,

        /// <summary>Aim at an object ball; cutFraction offsets the aim line by cutFraction * 2R (0.5 = half-ball hit).</summary>
        AtBall,

        /// <summary>Aim at the ghost-ball position that sends the object ball toward aimPoint.</summary>
        PotToPoint
    }

    public enum PresetRack
    {
        None,
        EightBallTriangle,
        NineBallDiamond
    }

    [Serializable]
    public struct BallPlacement
    {
        public int ballId;

        [Tooltip("Table coordinates (x across, y along the long axis), metres from the centre.")]
        public Vector2 position;

        public BallPlacement(int id, Vector2 tablePosition)
        {
            ballId = id;
            position = tablePosition;
        }
    }

    /// <summary>A repeatable physics test setup and shot.</summary>
    [Serializable]
    public sealed class PhysicsTestPreset
    {
        public string name = "Preset";
        public Vector2 cueBall = new Vector2(0f, -0.635f);
        public PresetRack rack = PresetRack.None;
        public List<BallPlacement> objectBalls = new List<BallPlacement>();
        public PresetAimMode aimMode = PresetAimMode.AtBall;
        public float aimAngle;
        public Vector2 aimPoint;
        public int aimBallId = 1;
        [Range(-1f, 1f)] public float cutFraction;
        [Range(0f, 1f)] public float power = 0.4f;
        public Vector2 tipOffset;
        public bool autoShoot = true;
        [TextArea] public string expectation = string.Empty;
    }

    /// <summary>List of physics presets used by 03_PhysicsTest (MASTER_PROMPT section 15).</summary>
    [CreateAssetMenu(menuName = "VTG Pool/Physics Test Preset Library", fileName = "PhysicsTestPresets")]
    public sealed class PhysicsTestPresetLibrary : ScriptableObject
    {
        public List<PhysicsTestPreset> presets = new List<PhysicsTestPreset>();

        /// <summary>Default presets for a 9 ft table (playing area 2.54 x 1.27 m).</summary>
        public static List<PhysicsTestPreset> CreateDefaults()
        {
            return new List<PhysicsTestPreset>
            {
                new PhysicsTestPreset
                {
                    name = "Straight Shot", cueBall = new Vector2(0f, -0.635f),
                    objectBalls = { new BallPlacement(1, new Vector2(0f, 0.2f)) },
                    aimMode = PresetAimMode.AtBall, aimBallId = 1, power = 0.35f,
                    expectation = "Full hit. Cue ball has started rolling, so it follows slowly after contact."
                },
                new PhysicsTestPreset
                {
                    name = "Stop Shot", cueBall = new Vector2(0.104f, 0.585f),
                    objectBalls = { new BallPlacement(1, new Vector2(0.35f, 0.9f)) },
                    aimMode = PresetAimMode.AtBall, aimBallId = 1, power = 0.45f, tipOffset = new Vector2(0f, -0.25f),
                    expectation = "Straight-in shot to the corner: backspin wears off at contact, cue ball stops on the spot, object ball drops."
                },
                new PhysicsTestPreset
                {
                    name = "Follow Shot", cueBall = new Vector2(0f, -0.55f),
                    objectBalls = { new BallPlacement(1, new Vector2(0f, 0.2f)) },
                    aimMode = PresetAimMode.AtBall, aimBallId = 1, power = 0.4f, tipOffset = new Vector2(0f, 0.8f),
                    expectation = "Topspin: cue ball pauses at contact, then accelerates forward after the object ball."
                },
                new PhysicsTestPreset
                {
                    name = "Draw Shot", cueBall = new Vector2(0f, -0.35f),
                    objectBalls = { new BallPlacement(1, new Vector2(0f, 0.1f)) },
                    aimMode = PresetAimMode.AtBall, aimBallId = 1, power = 0.5f, tipOffset = new Vector2(0f, -0.9f),
                    expectation = "Backspin survives to contact: cue ball stops then draws back toward the shooter."
                },
                new PhysicsTestPreset
                {
                    name = "Left English", cueBall = new Vector2(0.2f, -0.6f),
                    aimMode = PresetAimMode.AtPoint, aimPoint = new Vector2(0.635f, -0.3f), power = 0.4f, tipOffset = new Vector2(-0.8f, 0f),
                    expectation = "Running english: the ball rebounds left off the right rail, so left english widens the rebound (compare with Rail Bounce)."
                },
                new PhysicsTestPreset
                {
                    name = "Right English", cueBall = new Vector2(0.2f, -0.6f),
                    aimMode = PresetAimMode.AtPoint, aimPoint = new Vector2(0.635f, -0.3f), power = 0.4f, tipOffset = new Vector2(0.8f, 0f),
                    expectation = "Reverse english: right english narrows the rebound off the right rail (compare with Rail Bounce)."
                },
                new PhysicsTestPreset
                {
                    name = "Rail Bounce", cueBall = new Vector2(0.2f, -0.6f),
                    aimMode = PresetAimMode.AtPoint, aimPoint = new Vector2(0.635f, -0.3f), power = 0.4f,
                    expectation = "Centre ball bank: rebound slightly steeper than mirror reflection (cushion friction)."
                },
                new PhysicsTestPreset
                {
                    name = "Ball Collision", cueBall = new Vector2(-0.25f, -0.4f),
                    objectBalls = { new BallPlacement(3, new Vector2(0f, 0.3f)) },
                    aimMode = PresetAimMode.AtBall, aimBallId = 3, cutFraction = 0.5f, power = 0.4f,
                    expectation = "Half-ball hit (30 deg cut). Stunned cue ball leaves ~90 deg to the object ball, then bends forward."
                },
                new PhysicsTestPreset
                {
                    name = "Break Shot", cueBall = new Vector2(0.15f, -0.635f), rack = PresetRack.EightBallTriangle,
                    aimMode = PresetAimMode.AtBall, aimBallId = 1, power = 1f, tipOffset = new Vector2(0f, -0.15f),
                    expectation = "Full-power break: no tunnelling, no rack overlap, balls settle in finite time."
                },
                new PhysicsTestPreset
                {
                    name = "Pocket Jaw Test", cueBall = new Vector2(0.2f, 0.45f),
                    objectBalls = { new BallPlacement(1, new Vector2(0.45f, 0.95f)) },
                    aimMode = PresetAimMode.PotToPoint, aimBallId = 1, aimPoint = new Vector2(0.62f, 1.27f), power = 0.45f,
                    expectation = "Object ball is sent at the corner pocket slightly off-centre: it contacts the jaw and drops (or rattles)."
                }
            };
        }
    }
}
