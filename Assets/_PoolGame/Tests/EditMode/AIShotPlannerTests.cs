using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using VTG.Pool.AI;
using VTG.Pool.Core;
using VTG.Pool.Cue;
using VTG.Pool.Match;
using VTG.Pool.Rules;
using VTG.Pool.Rules.NineBall;
using VTG.Pool.Rules.EightBall;

namespace VTG.Pool.Tests
{
    public sealed class AIShotPlannerTests
    {
        private AIProfile profile;
        private NineBallRulesConfig config;
        private IRuleSet rules;
        private MatchState state;
        private readonly List<AIBall> balls = new List<AIBall>();
        private readonly List<AIShotCandidate> candidates = new List<AIShotCandidate>();
        private readonly Vector3[] pockets = { new Vector3(0f, 0f, 1.2f) };
        private static readonly Rect Bounds = new Rect(-0.6f, -1.25f, 1.2f, 2.5f);

        [SetUp]
        public void SetUp()
        {
            profile = ScriptableObject.CreateInstance<AIProfile>();
            config = NineBallRulesConfig.CreateDefault();
            rules = new NineBallRuleSet(config);
            state = new MatchState(GameMode.NineBall, new[] { new MatchPlayer(0, "A"), new MatchPlayer(1, "B") });
            rules.BeginRack(state);
            state.IsBreakShot = false;
            balls.Clear();
            balls.Add(new AIBall(1, new Vector3(0f, 0f, 0.5f)));
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(profile);
            Object.DestroyImmediate(config);
        }

        private void Generate(AIShotPlanner.SurfacePathClear surface = null)
        {
            AIShotPlanner.Generate(Vector3.zero, balls, pockets, PoolConstants.BallRadius,
                Bounds, state, rules, profile, surface ?? ((a, b, r) => true), candidates);
        }

        [Test]
        public void StraightPot_HasCorrectGhostAndNormalizedAim()
        {
            Generate();
            Assert.AreEqual(1, candidates.Count);
            Assert.AreEqual(0.5f - PoolConstants.BallDiameter, candidates[0].GhostPosition.z, 1e-6f);
            Assert.AreEqual(Vector3.forward, candidates[0].AimDirection);
            Assert.AreEqual(0f, candidates[0].CutAngle, 1e-5f);
        }

        [TestCase(0.2f)]
        [TestCase(0.8f)]
        public void BallBlockingEitherLeg_RejectsPot(float z)
        {
            balls.Add(new AIBall(2, new Vector3(0f, 0f, z)));
            Generate();
            Assert.IsEmpty(candidates);
        }

        [Test]
        public void PocketedBlocker_IsIgnored()
        {
            balls.Add(new AIBall(2, new Vector3(0f, 0f, 0.2f)));
            state.RemoveBall(2);
            Generate();
            Assert.AreEqual(1, candidates.Count);
        }

        [Test]
        public void NineBall_DoesNotTargetHigherNumber()
        {
            balls.Clear();
            balls.Add(new AIBall(2, new Vector3(0f, 0f, 0.5f)));
            Generate();
            Assert.IsEmpty(candidates);
            state.RemoveBall(1);
            Generate();
            Assert.AreEqual(2, candidates[0].BallNumber);
        }

        [Test]
        public void EightBall_RespectsAssignedGroupAndLastEight()
        {
            var eightConfig = EightBallRulesConfig.CreateDefault();
            try
            {
                rules = new EightBallRuleSet(eightConfig);
                state = new MatchState(GameMode.EightBall, new[] { new MatchPlayer(0, "A"), new MatchPlayer(1, "B") });
                rules.BeginRack(state);
                state.IsBreakShot = false;
                state.TableOpen = false;
                state.CurrentPlayer.Group = BallGroup.Solids;
                balls[0] = new AIBall(8, new Vector3(0f, 0f, 0.5f));
                Generate();
                Assert.IsEmpty(candidates);
                for (int i = 1; i <= 7; i++) state.RemoveBall(i);
                Generate();
                Assert.AreEqual(8, candidates[0].BallNumber);
            }
            finally { Object.DestroyImmediate(eightConfig); }
        }

        [Test]
        public void SurfaceQuery_RejectsJawOrCushionOnEitherLeg()
        {
            Generate((a, b, r) => false);
            Assert.IsEmpty(candidates);
            Generate((a, b, r) => a.z < 0.1f);
            Assert.IsEmpty(candidates);
        }

        [Test]
        public void ImpossibleCutAndGhostOutsideTable_AreRejected()
        {
            balls[0] = new AIBall(1, new Vector3(0f, 0f, -0.5f));
            Generate();
            Assert.IsEmpty(candidates);
            balls[0] = new AIBall(1, new Vector3(0.8f, 0f, 0.5f));
            Generate();
            Assert.IsEmpty(candidates);
        }

        [Test]
        public void BreakAndGameOver_ClearPreviousCandidates()
        {
            Generate();
            Assert.IsNotEmpty(candidates);
            state.IsBreakShot = true;
            Generate();
            Assert.IsEmpty(candidates);
            state.IsBreakShot = false;
            state.IsGameOver = true;
            Generate();
            Assert.IsEmpty(candidates);
        }

        [Test]
        public void BestCandidate_PrefersShortStraightPot()
        {
            Generate();
            var straight = candidates[0];
            balls[0] = new AIBall(1, new Vector3(0.15f, 0f, 0.5f));
            Generate();
            Assert.IsNotEmpty(candidates);
            Assert.Greater(straight.Score, candidates[0].Score);
            candidates.Add(straight);
            Assert.IsTrue(AIShotPlanner.TrySelectBest(candidates, out var best));
            Assert.AreEqual(straight.Score, best.Score);
            candidates.Clear();
            Assert.IsFalse(AIShotPlanner.TrySelectBest(candidates, out _));
        }

        [TestCase(AIDifficulty.Beginner)]
        [TestCase(AIDifficulty.Intermediate)]
        [TestCase(AIDifficulty.Advanced)]
        [TestCase(AIDifficulty.Expert)]
        public void Error_IsRepeatableBoundedAndDoesNotChangeSpin(AIDifficulty difficulty)
        {
            profile.difficulty = difficulty;
            var a = new System.Random(42);
            var b = new System.Random(42);
            var shot = new ShotParameters(Vector3.forward, 0.5f, new Vector2(0.2f, -0.3f));
            for (int i = 0; i < 100; i++)
            {
                var actual = AIShotPlanner.ApplyExecutionError(shot, profile, a);
                var repeated = AIShotPlanner.ApplyExecutionError(shot, profile, b);
                Assert.AreEqual(actual.AimDirection, repeated.AimDirection);
                Assert.AreEqual(actual.Power, repeated.Power);
                Assert.LessOrEqual(Vector3.Angle(shot.AimDirection, actual.AimDirection), profile.AimErrorDegrees + 0.01f);
                Assert.LessOrEqual(Mathf.Abs(actual.Power - shot.Power), shot.Power * profile.RelativePowerError + 1e-6f);
                Assert.AreEqual(shot.TipOffset, actual.TipOffset);
            }
        }
    }
}
