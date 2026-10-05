using NUnit.Framework;
using UnityEngine;
using VTG.Pool.Core;
using VTG.Pool.Simulation;

namespace VTG.Pool.Tests
{
    public sealed class CushionResponseModelTests
    {
        private const float R = PoolConstants.BallRadius;

        // Far cushion (+Z rail); its normal points back into the table.
        private static readonly Vector3 Normal = Vector3.back;

        private static CushionParameters Parameters(float friction = 0.2f)
        {
            return new CushionParameters
            {
                Restitution = 0.85f,
                RestitutionSpeedLoss = 0.025f,
                MinRestitution = 0.6f,
                Friction = friction,
                Radius = R,
                ContactHeight = 0.0365f - R
            };
        }

        [Test]
        public void NotApproaching_IsIgnored()
        {
            Vector3 v = new Vector3(0f, 0f, -1f);
            Vector3 w = Vector3.zero;
            Assert.IsFalse(CushionResponseModel.Resolve(ref v, ref w, Normal, Parameters(), out _));
        }

        [Test]
        public void HeadOn_ReboundsWithSpeedDependentRestitution()
        {
            Vector3 v = new Vector3(0f, 0f, 1f);
            Vector3 w = Vector3.zero;
            Assert.IsTrue(CushionResponseModel.Resolve(ref v, ref w, Normal, Parameters(0f), out float impact));
            Assert.AreEqual(1f, impact, 1e-5f);
            Assert.AreEqual(-(0.85f - 0.025f), v.z, 1e-4f);
            Assert.AreEqual(0f, v.x, 1e-5f);
            Assert.AreEqual(0f, v.y, 1e-6f, "Vertical bounce is suppressed");
        }

        [Test]
        public void HardHit_HasLowerRestitutionThanSoftHit()
        {
            CushionParameters p = Parameters(0f);
            Assert.Greater(CushionResponseModel.EffectiveRestitution(p, 0.5f), CushionResponseModel.EffectiveRestitution(p, 5f));
            Assert.GreaterOrEqual(CushionResponseModel.EffectiveRestitution(p, 50f), p.MinRestitution);
        }

        [Test]
        public void AngledNoSpin_LosesTangentialSpeedFromFriction()
        {
            // 45 degrees, stun (no spin): friction reduces the tangential component.
            Vector3 v = new Vector3(1f, 0f, 1f);
            Vector3 w = Vector3.zero;
            CushionResponseModel.Resolve(ref v, ref w, Normal, Parameters(), out _);
            Assert.Less(v.z, 0f);
            Assert.Less(v.x, 1f);
            Assert.Greater(v.x, 0.5f);
        }

        [Test]
        public void SideSpin_ChangesExitAngle()
        {
            // Ball travelling straight into the far rail. Right english = negative w.y (counter-clockwise
            // from above in Unity's left-handed frame) and must rebound toward +X (shooter's right).
            Vector3 vRight = new Vector3(0f, 0f, 1.5f);
            Vector3 wRight = new Vector3(0f, -40f, 0f);
            CushionResponseModel.Resolve(ref vRight, ref wRight, Normal, Parameters(), out _);

            Vector3 vLeft = new Vector3(0f, 0f, 1.5f);
            Vector3 wLeft = new Vector3(0f, 40f, 0f);
            CushionResponseModel.Resolve(ref vLeft, ref wLeft, Normal, Parameters(), out _);

            Assert.Greater(vRight.x, 0.05f, "Right english should throw the rebound to the right");
            Assert.Less(vLeft.x, -0.05f, "Left english should throw the rebound to the left");
            Assert.Less(Mathf.Abs(wRight.y), 40f, "Cushion friction consumes some side spin");
        }

        [Test]
        public void RunningEnglish_LengthensRebound()
        {
            // Ball heading right into the far rail at 45 degrees; running (right) english vs none.
            Vector3 vPlain = new Vector3(1f, 0f, 1f);
            Vector3 wPlain = Vector3.zero;
            CushionResponseModel.Resolve(ref vPlain, ref wPlain, Normal, Parameters(), out _);

            Vector3 vRunning = new Vector3(1f, 0f, 1f);
            Vector3 wRunning = new Vector3(0f, -60f, 0f);
            CushionResponseModel.Resolve(ref vRunning, ref wRunning, Normal, Parameters(), out _);

            float anglePlain = Mathf.Atan2(vPlain.x, -vPlain.z);
            float angleRunning = Mathf.Atan2(vRunning.x, -vRunning.z);
            Assert.Greater(angleRunning, anglePlain, "Running english widens the rebound angle");
        }
    }
}
