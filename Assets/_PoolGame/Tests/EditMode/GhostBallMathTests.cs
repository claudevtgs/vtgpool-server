using NUnit.Framework;
using UnityEngine;
using VTG.Pool.Aiming;
using VTG.Pool.Core;

namespace VTG.Pool.Tests
{
    public sealed class GhostBallMathTests
    {
        private const float R = PoolConstants.BallRadius;

        [Test]
        public void FullHit_GhostIsTwoRadiiBeforeObjectBall()
        {
            Vector3 cue = Vector3.zero;
            Vector3 target = new Vector3(0f, 0f, 1f);
            Assert.IsTrue(GhostBallMath.TryGetGhostBall(cue, Vector3.forward, target, R, out Vector3 ghost));
            Assert.AreEqual(1f - 2f * R, ghost.z, 1e-5f);
            Assert.AreEqual(0f, ghost.x, 1e-6f);
        }

        [Test]
        public void HalfBallHit_CutsThirtyDegrees()
        {
            // Aim line passes R to the side of the object ball centre.
            Vector3 cue = Vector3.zero;
            Vector3 target = new Vector3(R, 0f, 1f);
            Assert.IsTrue(GhostBallMath.TryGetGhostBall(cue, Vector3.forward, target, R, out Vector3 ghost));
            Vector3 objectDirection = GhostBallMath.ObjectBallDirection(ghost, target);
            Assert.AreEqual(30f, GhostBallMath.CutAngleDegrees(Vector3.forward, objectDirection), 0.01f);
            Assert.AreEqual(2f * R, Vector3.Distance(ghost, target), 1e-5f);
        }

        [Test]
        public void Miss_ReturnsFalse()
        {
            Assert.IsFalse(GhostBallMath.TryGetGhostBall(Vector3.zero, Vector3.forward, new Vector3(3f * R, 0f, 1f), R, out _));
            Assert.IsFalse(GhostBallMath.TryGetGhostBall(Vector3.zero, Vector3.back, new Vector3(0f, 0f, 1f), R, out _));
        }

        [Test]
        public void TangentLine_IsPerpendicularToObjectBallLine()
        {
            Vector3 objectDirection = GhostBallMath.ObjectBallDirection(new Vector3(-R, 0f, 1f - 1.7f * R), new Vector3(0f, 0f, 1f));
            Vector3 tangent = GhostBallMath.CueBallTangentDirection(Vector3.forward, objectDirection);
            Assert.AreEqual(0f, Vector3.Dot(tangent, objectDirection), 1e-5f);
            Assert.AreEqual(Vector3.zero, GhostBallMath.CueBallTangentDirection(Vector3.forward, Vector3.forward));
        }

        [Test]
        public void GhostForTarget_SendsObjectBallAtTarget()
        {
            Vector3 objectBall = new Vector3(0.2f, 0f, 0.5f);
            Vector3 pocket = new Vector3(0.6f, 0f, 1.2f);
            Vector3 ghost = GhostBallMath.GhostBallForTarget(objectBall, pocket, R);
            Vector3 departure = GhostBallMath.ObjectBallDirection(ghost, objectBall);
            Assert.Greater(Vector3.Dot(departure, (pocket - objectBall).normalized), 0.99999f);
        }

        [Test]
        public void RaySphere_DetectsObstruction()
        {
            Assert.IsTrue(GhostBallMath.RaySphere(Vector3.zero, Vector3.forward, new Vector3(0.01f, 0f, 1f), 0.05f, out float distance));
            Assert.AreEqual(1f - Mathf.Sqrt(0.05f * 0.05f - 0.01f * 0.01f), distance, 1e-4f);
            Assert.IsFalse(GhostBallMath.RaySphere(Vector3.zero, Vector3.forward, new Vector3(0.2f, 0f, 1f), 0.05f, out _));
        }
    }
}
