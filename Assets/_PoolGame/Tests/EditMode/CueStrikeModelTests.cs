using NUnit.Framework;
using UnityEngine;
using VTG.Pool.Core;
using VTG.Pool.Cue;
using VTG.Pool.Simulation;

namespace VTG.Pool.Tests
{
    public sealed class CueStrikeModelTests
    {
        private const float M = PoolConstants.BallMass;
        private const float R = PoolConstants.BallRadius;

        private static CueStrikeSettings Settings(float squirt = 0f)
        {
            return new CueStrikeSettings { CueMass = 0.54f, TipRestitution = 0.75f, MaxTipOffsetFraction = 0.5f, SquirtAtMaxOffsetDegrees = squirt };
        }

        private static StrikeResult Strike(Vector2 tip, float squirt = 0f)
        {
            var shot = new ShotParameters(Vector3.forward, 0.5f, tip);
            return CueStrikeModel.Compute(shot, 3f, M, R, Settings(squirt));
        }

        [Test]
        public void CentreHit_HasNoSpin()
        {
            StrikeResult result = Strike(Vector2.zero);
            Assert.Greater(result.LinearVelocity.z, 0f);
            Assert.Less(result.AngularVelocity.magnitude, 1e-4f);
            Assert.AreEqual(1.75f * 3f / (1f + M / 0.54f), result.LinearVelocity.z, 1e-4f);
        }

        [Test]
        public void TopAtTwoFifthsRadius_ProducesNaturalRoll()
        {
            // 0.4 R = 0.8 of the 0.5 R maximum offset.
            StrikeResult result = Strike(new Vector2(0f, 0.8f));
            Vector3 slip = ClothFrictionModel.ContactSlipVelocity(result.LinearVelocity, result.AngularVelocity, R);
            Assert.Less(slip.magnitude, 1e-3f);
        }

        [Test]
        public void BottomHit_ProducesBackspin()
        {
            StrikeResult result = Strike(new Vector2(0f, -1f));
            Assert.Less(result.AngularVelocity.x, 0f, "Backspin about +X opposes forward roll");
        }

        [Test]
        public void TopHit_ProducesTopspin()
        {
            StrikeResult result = Strike(new Vector2(0f, 1f));
            Assert.Greater(result.AngularVelocity.x, result.LinearVelocity.z / R, "Above natural roll -> overspin");
        }

        [Test]
        public void RightEnglish_SpinsCounterClockwiseFromAbove()
        {
            StrikeResult result = Strike(new Vector2(1f, 0f));
            Assert.Less(result.AngularVelocity.y, 0f);
        }

        [Test]
        public void OffCentreHit_IsSlowerThanCentreHit()
        {
            Assert.Less(Strike(new Vector2(0f, -1f)).LinearVelocity.magnitude, Strike(Vector2.zero).LinearVelocity.magnitude);
        }

        [Test]
        public void Squirt_DeflectsAwayFromEnglish()
        {
            StrikeResult right = Strike(new Vector2(1f, 0f), 1.5f);
            Assert.Less(right.LinearVelocity.x, 0f, "Right english squirts the ball left");
            Assert.AreEqual(1.5f, Vector3.Angle(Vector3.forward, right.LinearVelocity), 0.01f);
        }
    
        [Test]
        public void PowerForCueSpeed_InvertsThePowerCurve()
        {
            var profile = UnityEngine.ScriptableObject.CreateInstance<CueStrikeProfile>();
            foreach (float power in new[] { 0.1f, 0.35f, 0.7f, 1f })
            {
                float speed = profile.EvaluateCueSpeed(power);
                Assert.AreEqual(power, profile.PowerForCueSpeed(speed), 2e-3f);
            }

            Assert.AreEqual(1f, profile.PowerForCueSpeed(99f));
            Assert.AreEqual(0f, profile.PowerForCueSpeed(0f));
            UnityEngine.Object.DestroyImmediate(profile);
        }
}
}
