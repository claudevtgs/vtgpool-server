using NUnit.Framework;
using UnityEngine;
using VTG.Pool.Core;
using VTG.Pool.Simulation;

namespace VTG.Pool.Tests
{
    public sealed class BallCollisionModelTests
    {
        private const float R = PoolConstants.BallRadius;

        private static BallCollisionParameters Parameters(float friction = 0.06f) =>
            new BallCollisionParameters { Restitution = 0.93f, Friction = friction, Radius = R };

        [Test]
        public void HeadOn_TransfersAlmostAllSpeed()
        {
            Vector3 va = new Vector3(0f, 0f, 2f), wa = Vector3.zero, vb = Vector3.zero, wb = Vector3.zero;
            Assert.IsTrue(BallCollisionModel.Resolve(ref va, ref wa, ref vb, ref wb, Vector3.zero, new Vector3(0f, 0f, 2f * R), Parameters(), out float impulse));
            Assert.AreEqual(2f * (1f - 0.93f) / 2f, va.z, 1e-5f, "Cue ball keeps (1-e)/2 of its speed");
            Assert.AreEqual(2f * (1f + 0.93f) / 2f, vb.z, 1e-5f);
            Assert.AreEqual(1.93f, impulse, 1e-5f);
        }

        [Test]
        public void Separating_IsIgnored()
        {
            Vector3 va = new Vector3(0f, 0f, -1f), wa = Vector3.zero, vb = Vector3.zero, wb = Vector3.zero;
            Assert.IsFalse(BallCollisionModel.Resolve(ref va, ref wa, ref vb, ref wb, Vector3.zero, new Vector3(0f, 0f, 2f * R), Parameters(), out _));
        }

        [Test]
        public void HalfBallCut_ObjectBallLeavesAlongLineOfCentres_WithSmallThrow()
        {
            // Contact normal at 30 degrees to the aim line.
            Vector3 normal = new Vector3(Mathf.Sin(30f * Mathf.Deg2Rad), 0f, Mathf.Cos(30f * Mathf.Deg2Rad));
            Vector3 va = new Vector3(0f, 0f, 2f), wa = Vector3.zero, vb = Vector3.zero, wb = Vector3.zero;
            BallCollisionModel.Resolve(ref va, ref wa, ref vb, ref wb, Vector3.zero, normal * 2f * R, Parameters(0f), out _);
            Assert.AreEqual(0f, Vector3.Angle(vb, normal), 0.01f, "Frictionless: object ball moves along the line of centres");

            Vector3 va2 = new Vector3(0f, 0f, 2f), wa2 = Vector3.zero, vb2 = Vector3.zero, wb2 = Vector3.zero;
            BallCollisionModel.Resolve(ref va2, ref wa2, ref vb2, ref wb2, Vector3.zero, normal * 2f * R, Parameters(), out _);
            float throwAngle = Vector3.Angle(vb2, normal);
            Assert.Greater(throwAngle, 0.5f, "Friction throws the object ball off the line of centres");
            Assert.Less(throwAngle, 5f, "Collision-induced throw is a few degrees at most");
            Assert.Greater(wb2.magnitude, 0f, "Friction transfers spin to the object ball");
        }

        [Test]
        public void TimeOfImpact_IsExact()
        {
            Assert.IsTrue(BallCollisionModel.TimeOfImpact(Vector3.zero, new Vector3(0f, 0f, 1f), new Vector3(0f, 0f, 0.5f), Vector3.zero, 2f * R, 1f, out float t));
            Assert.AreEqual(0.5f - 2f * R, t, 1e-5f);
            Assert.IsFalse(BallCollisionModel.TimeOfImpact(Vector3.zero, new Vector3(0f, 0f, 1f), new Vector3(0f, 0f, 0.5f), Vector3.zero, 2f * R, 0.1f, out _), "Beyond max time");
            Assert.IsFalse(BallCollisionModel.TimeOfImpact(Vector3.zero, new Vector3(1f, 0f, 0f), new Vector3(0f, 0f, 0.5f), Vector3.zero, 2f * R, 1f, out _), "Miss");
        }

        [Test]
        public void TouchingAndApproaching_ImpactsImmediately()
        {
            Assert.IsTrue(BallCollisionModel.TimeOfImpact(Vector3.zero, new Vector3(0f, 0f, 1f), new Vector3(0f, 0f, 2f * R), Vector3.zero, 2f * R, 0.01f, out float t));
            Assert.AreEqual(0f, t);
        }
    }
}
