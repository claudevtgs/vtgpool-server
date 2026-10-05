using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using VTG.Pool.Cue;
using VTG.Pool.Simulation;

namespace VTG.Pool.Tests
{
    /// <summary>Elevated-cue strikes (massé): spin about the aim axis, no jump, and a path that bends toward the struck side.</summary>
    public sealed class MasseTests
    {
        private BallPhysicsProfile ball;
        private CueStrikeProfile cue;

        [SetUp]
        public void SetUp()
        {
            ball = ScriptableObject.CreateInstance<BallPhysicsProfile>();
            cue = ScriptableObject.CreateInstance<CueStrikeProfile>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(ball);
            Object.DestroyImmediate(cue);
        }

        private StrikeResult Strike(float power, Vector2 tip, float elevation)
        {
            var shot = new ShotParameters(Vector3.forward, power, tip, elevation);
            return CueStrikeModel.Compute(shot, cue.EvaluateCueSpeed(power), ball.ballMass, ball.ballRadius, cue.ToSettings());
        }

        /// <summary>Lateral (x) offset of the predicted path end for a shot along +z.</summary>
        private float LateralDrift(StrikeResult strike, out float travel)
        {
            var points = new List<Vector3>();
            CuePathPredictor.Predict(Vector3.zero, strike.LinearVelocity, strike.AngularVelocity, ball.ToClothParameters(9.81f),
                new Vector2(10f, 10f), Matrix4x4.identity, new List<Vector3>(), points, out _);
            Vector3 end = points[points.Count - 1];
            travel = end.z;
            return end.x;
        }

        [Test]
        public void LevelCue_IsUnchangedByTheElevationParameter()
        {
            StrikeResult a = Strike(0.6f, new Vector2(0.3f, -0.4f), 0f);
            var shot = new ShotParameters(Vector3.forward, 0.6f, new Vector2(0.3f, -0.4f));
            StrikeResult b = CueStrikeModel.Compute(shot, cue.EvaluateCueSpeed(0.6f), ball.ballMass, ball.ballRadius, cue.ToSettings());
            Assert.AreEqual(a.LinearVelocity, b.LinearVelocity);
            Assert.AreEqual(a.AngularVelocity, b.AngularVelocity);
            Assert.AreEqual(0f, a.LinearVelocity.y);
        }

        [Test]
        public void ElevatedStrike_StaysOnTheClothAndSpinsAboutTheAimAxis()
        {
            StrikeResult level = Strike(0.6f, new Vector2(0.8f, 0f), 0f);
            StrikeResult masse = Strike(0.6f, new Vector2(0.8f, 0f), 70f);
            Assert.AreEqual(0f, masse.LinearVelocity.y, "The slate absorbs the downward part (no jump)");
            Assert.Less(masse.LinearVelocity.magnitude, level.LinearVelocity.magnitude * 0.5f, "Most of a steep stroke goes into the table");
            Assert.Less(masse.AngularVelocity.z, -10f, "Right-side massé spins about -aim");
            Assert.AreEqual(0f, level.AngularVelocity.z, 1e-3f, "A level cue has no spin about the aim axis");
        }

        [Test]
        public void Masse_CurvesTowardTheStruckSide()
        {
            float right = LateralDrift(Strike(0.7f, new Vector2(0.9f, 0f), 70f), out float travelRight);
            float left = LateralDrift(Strike(0.7f, new Vector2(-0.9f, 0f), 70f), out _);
            float straight = LateralDrift(Strike(0.7f, new Vector2(0.9f, 0f), 0f), out _);
            Assert.Greater(right, 0.1f, $"Right english with a raised cue bends right (drift {right:F3} m over {travelRight:F2} m)");
            Assert.Less(left, -0.1f, "Left english bends left");
            Assert.Less(Mathf.Abs(straight), Mathf.Abs(right) * 0.25f, "Level cue: only squirt, nearly straight");
        }

        [Test]
        public void JumpStroke_LeavesTheClothButMasseStaysDown()
        {
            StrikeResult level = Strike(1f, Vector2.zero, 0f);
            StrikeResult jump = Strike(1f, Vector2.zero, 35f);
            StrikeResult masse = Strike(1f, new Vector2(0.8f, 0f), 72f);
            Assert.AreEqual(0f, level.LinearVelocity.y, "Level cue: no jump");
            Assert.Greater(jump.LinearVelocity.y, 1f, "Elevated stroke drives the ball into the slate and it rebounds");
            Assert.Less(jump.LinearVelocity.y, 2.5f, "Jump height stays realistic (~0.1-0.3 m)");
            Assert.AreEqual(0f, masse.LinearVelocity.y, "A steep massé stroke pins the ball");
        }

        [Test]
        public void Predictor_FlyingBallClearsABallInItsPath()
        {
            StrikeResult jump = Strike(1f, Vector2.zero, 35f);
            var flight = new FlightParameters { RestHeight = 0f, Gravity = 9.81f, LandingRestitution = 0.5f, LandingThreshold = 0.25f };
            var points = new List<Vector3>();
            float apexDistance = jump.LinearVelocity.y / 9.81f * jump.LinearVelocity.z;
            var blocker = new List<Vector3> { new Vector3(0f, 0f, apexDistance) };
            CuePathPredictor.End end = CuePathPredictor.Predict(Vector3.zero, jump.LinearVelocity, jump.AngularVelocity, ball.ToClothParameters(9.81f), flight,
                new Vector2(10f, 10f), Matrix4x4.identity, blocker, points, out _, 1.5f);
            Assert.AreNotEqual(CuePathPredictor.End.Ball, end, "The ball flies over the blocker at the top of its arc");
            Assert.Greater(points.Max(p => p.y), 2f * ball.ballRadius, "The arc rises above a ball");
            Assert.AreEqual(0f, points[points.Count - 1].y, 1e-4f, "It comes back down to the cloth");
        }

        [Test]
        public void Predictor_StopsAtABall()
        {
            StrikeResult strike = Strike(0.5f, Vector2.zero, 0f);
            var points = new List<Vector3>();
            var end = CuePathPredictor.Predict(Vector3.zero, strike.LinearVelocity, strike.AngularVelocity, ball.ToClothParameters(9.81f),
                new Vector2(10f, 10f), Matrix4x4.identity, new List<Vector3> { new Vector3(0f, 0f, 0.5f) }, points, out int hit);
            Assert.AreEqual(CuePathPredictor.End.Ball, end);
            Assert.AreEqual(0, hit);
            Assert.AreEqual(0.5f - 2f * ball.ballRadius, points[points.Count - 1].z, 0.02f);
        }
    }
}
