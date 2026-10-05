using NUnit.Framework;
using UnityEngine;
using VTG.Pool.AI;
using VTG.Pool.Core;
using VTG.Pool.Cue;
using VTG.Pool.Simulation;

namespace VTG.Pool.Tests
{
    public sealed class AIShotPredictionTests
    {
        private CueStrikeProfile cue;
        private BallPhysicsProfile physics;
        [SetUp] public void SetUp()
        {
            cue = ScriptableObject.CreateInstance<CueStrikeProfile>();
            physics = ScriptableObject.CreateInstance<BallPhysicsProfile>();
        }
        [TearDown] public void TearDown() { Object.DestroyImmediate(cue); Object.DestroyImmediate(physics); }

        private float Power(float cueDistance, float objectDistance)
        {
            Vector3 ghost = Vector3.forward * cueDistance;
            var candidate = new AIShotCandidate(1, 0, ghost, Vector3.forward, cueDistance, objectDistance, 0f, 0f);
            Assert.IsTrue(AIShotPrediction.TryEstimate(candidate, ghost + Vector3.forward * PoolConstants.BallDiameter,
                cue, physics, 0.4f, out var shot, out var velocity, out var angular));
            Assert.Greater(shot.Power, 0f);
            Assert.LessOrEqual(shot.Power, 1f);
            Assert.IsFalse(float.IsNaN(velocity.x) || float.IsNaN(angular.x));
            return shot.Power;
        }

        [Test] public void LongerObjectPath_NeedsMorePower() => Assert.Greater(Power(0.4f, 1.5f), Power(0.4f, 0.2f));
        [Test] public void LongerCuePath_NeedsMorePower() => Assert.Greater(Power(1.8f, 0.5f), Power(0.2f, 0.5f));
        [Test] public void SlowCloth_NeedsMorePower()
        {
            float before = Power(0.4f, 1f);
            physics.rollingResistance *= 2f;
            Assert.Greater(Power(0.4f, 1f), before);
        }
        [Test] public void WeakCue_RejectsUnreachablePot()
        {
            cue.maxCueSpeed = 0.15f;
            var candidate = new AIShotCandidate(1, 0, Vector3.forward, Vector3.forward, 1f, 1f, 0f, 0f);
            Assert.IsFalse(AIShotPrediction.TryEstimate(candidate, Vector3.forward * (1f + PoolConstants.BallDiameter),
                cue, physics, 0.4f, out _, out _, out _));
        }
    }
}
