using NUnit.Framework;
using UnityEngine;
using VTG.Pool.Core;
using VTG.Pool.Simulation;

namespace VTG.Pool.Tests
{
    public sealed class ClothFrictionModelTests
    {
        private const float Dt = 1f / 120f;
        private const float R = PoolConstants.BallRadius;

        private static ClothParameters Parameters()
        {
            return new ClothParameters
            {
                SlidingFriction = 0.2f,
                RollingResistance = 0.012f,
                SpinFriction = 0.012f,
                Gravity = 9.81f,
                Radius = R,
                MinLinearSpeed = 0.005f,
                MinAngularSpeed = 0.2f,
                SlipSpeedEpsilon = 0.002f
            };
        }

        /// <summary>Runs until the ball first reaches pure rolling; returns the speed at that moment.</summary>
        private static float SlideUntilRolling(ref Vector3 v, ref Vector3 w, out float distance, out int steps)
        {
            ClothParameters p = Parameters();
            distance = 0f;
            steps = 0;
            while (steps < 100000)
            {
                ClothMotionPhase phase = ClothFrictionModel.Step(ref v, ref w, p, Dt);
                distance += v.z * Dt;
                steps++;
                if (phase != ClothMotionPhase.Sliding)
                {
                    break;
                }
            }

            return v.z;
        }

        [Test]
        public void StunShot_ReachesRollingAtFiveSeventhsOfInitialSpeed()
        {
            Vector3 v = new Vector3(0f, 0f, 2f);
            Vector3 w = Vector3.zero;
            float rollingSpeed = SlideUntilRolling(ref v, ref w, out _, out _);

            // Rolling resistance acts during the last step, so allow a small tolerance.
            Assert.AreEqual(2f * 5f / 7f, rollingSpeed, 0.01f);
            Assert.AreEqual(rollingSpeed / R, w.x, 0.5f, "Pure rolling must satisfy w = v / R");
        }

        [Test]
        public void StunShot_SlidingDistanceMatchesAnalyticResult()
        {
            Vector3 v = new Vector3(0f, 0f, 2f);
            Vector3 w = Vector3.zero;
            SlideUntilRolling(ref v, ref w, out float distance, out _);

            // d = 12 v0^2 / (49 mu g)
            float expected = 12f * 4f / (49f * 0.2f * 9.81f);
            Assert.AreEqual(expected, distance, 0.02f);
        }

        [Test]
        public void NaturalRoll_StaysRollingWithoutSliding()
        {
            Vector3 v = new Vector3(0f, 0f, 1.5f);
            Vector3 w = ClothFrictionModel.RollingAngularVelocity(v, R, 0f);
            ClothMotionPhase phase = ClothFrictionModel.Step(ref v, ref w, Parameters(), Dt);
            Assert.AreEqual(ClothMotionPhase.Rolling, phase);
            Assert.Less(ClothFrictionModel.ContactSlipVelocity(v, w, R).magnitude, 1e-4f);
        }

        [Test]
        public void HeavyBackspin_ReversesDirection()
        {
            // w = -3 v/R (k = 3 > 2.5): final rolling velocity (5 - 2k)/7 v0 is negative.
            Vector3 v = new Vector3(0f, 0f, 1f);
            Vector3 w = new Vector3(-3f * 1f / R, 0f, 0f);
            float rollingSpeed = SlideUntilRolling(ref v, ref w, out _, out _);
            Assert.AreEqual((5f - 6f) / 7f, rollingSpeed, 0.01f);
        }

        [Test]
        public void Topspin_AcceleratesSlowBall()
        {
            Vector3 v = new Vector3(0f, 0f, 0.2f);
            Vector3 w = new Vector3(2f / R, 0f, 0f);
            float rollingSpeed = SlideUntilRolling(ref v, ref w, out _, out _);
            Assert.Greater(rollingSpeed, 0.2f);
            Assert.AreEqual((5f * 0.2f + 2f * 2f) / 7f, rollingSpeed, 0.01f);
        }

        [Test]
        public void RollingBall_StopsInFiniteTimeWithoutDrift()
        {
            Vector3 v = new Vector3(0.3f, 0f, 1f);
            Vector3 w = ClothFrictionModel.RollingAngularVelocity(v, R, 15f);
            ClothParameters p = Parameters();
            ClothMotionPhase phase = ClothMotionPhase.Rolling;
            int steps = 0;
            while (phase != ClothMotionPhase.Stationary && steps < 120 * 60)
            {
                phase = ClothFrictionModel.Step(ref v, ref w, p, Dt);
                steps++;
            }

            Assert.AreEqual(ClothMotionPhase.Stationary, phase, "Ball must come to rest within 60 s");
            Assert.AreEqual(Vector3.zero, new Vector3(v.x, 0f, v.z));
            Assert.AreEqual(Vector3.zero, w);

            // Expected rolling time v / (mu_r g) = 1.044 / 0.1177 ~ 8.9 s (spin may last a little longer).
            Assert.Less(steps * Dt, 12f);
        }

        [Test]
        public void SideSpin_DecaysIndependently()
        {
            Vector3 v = Vector3.zero;
            Vector3 w = new Vector3(0f, 20f, 0f);
            ClothMotionPhase phase = ClothFrictionModel.Step(ref v, ref w, Parameters(), Dt);
            Assert.AreEqual(ClothMotionPhase.Spinning, phase);
            Assert.Less(w.y, 20f);
            Assert.AreEqual(Vector3.zero, v);
        }
    }
}
