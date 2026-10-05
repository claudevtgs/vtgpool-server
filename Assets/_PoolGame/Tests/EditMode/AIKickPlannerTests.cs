using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using VTG.Pool.AI;
using VTG.Pool.Cue;
using VTG.Pool.Match;
using VTG.Pool.Rules;
using VTG.Pool.Rules.NineBall;
using VTG.Pool.Simulation;

namespace VTG.Pool.Tests
{
    public sealed class AIKickPlannerTests
    {
        private AIProfile profile;
        private CueStrikeProfile cue;
        private BallPhysicsProfile physics;
        private NineBallRulesConfig config;
        private NineBallRuleSet rules;
        private MatchState state;
        private List<AIBall> balls;
        private readonly Rect bounds = new Rect(-0.606425f, -1.241425f, 1.21285f, 2.48285f);
        private readonly Vector3 origin = new Vector3(0f, 0f, -0.5f);

        [SetUp] public void SetUp()
        {
            profile = ScriptableObject.CreateInstance<AIProfile>();
            cue = ScriptableObject.CreateInstance<CueStrikeProfile>();
            physics = ScriptableObject.CreateInstance<BallPhysicsProfile>();
            config = NineBallRulesConfig.CreateDefault();
            rules = new NineBallRuleSet(config);
            state = new MatchState(GameMode.NineBall, new[] { new MatchPlayer(0, "A"), new MatchPlayer(1, "B") });
            rules.BeginRack(state);
            state.IsBreakShot = false;
            state.SetBallsOnTable(new[] { 1, 9 });
            balls = new List<AIBall> { new AIBall(1, new Vector3(0f, 0f, 0.5f)), new AIBall(9, Vector3.zero) };
        }

        [TearDown] public void TearDown()
        {
            Object.DestroyImmediate(profile); Object.DestroyImmediate(cue);
            Object.DestroyImmediate(physics); Object.DestroyImmediate(config);
        }

        private bool Plan(AIKickPlanner.RailAvailable rails, out ShotParameters shot, out int ball, out int trials)
            => AIKickPlanner.TryPlan(origin, balls, bounds, state, rules, cue, physics, 0.0365f,
                profile, rails, out shot, out ball, out trials);

        [Test] public void BlockedDirectPath_FindsOneRailLegalContact()
        {
            Assert.IsTrue(Plan((p, n, r) => true, out var shot, out int ball, out int trials));
            Assert.AreEqual(1, ball);
            Assert.Greater(Mathf.Abs(shot.AimDirection.x), 0.1f);
            Assert.Greater(trials, 0);
            Assert.LessOrEqual(trials, 4 * 49);
        }

        [Test] public void MissingRails_PocketMouthCannotBeUsedAsCushion()
            => Assert.IsFalse(Plan((p, n, r) => false, out _, out _, out _));

        [Test] public void EnclosedCueBall_RejectsWrongFirstContact()
        {
            balls.Clear();
            balls.Add(new AIBall(1, new Vector3(0f, 0f, 0.5f)));
            state.SetBallsOnTable(new[] { 1, 2, 3, 4, 5, 6, 7, 8, 9 });
            for (int i = 0; i < 8; i++)
            {
                float a = i * Mathf.PI / 4f;
                balls.Add(new AIBall(i + 2, origin + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * 0.08f));
            }
            Assert.IsFalse(Plan((p, n, r) => true, out _, out _, out _));
        }

        [Test] public void Planning_IsRepeatableAndDoesNotMutatePositions()
        {
            Vector3 before = balls[0].Position;
            Assert.IsTrue(Plan((p, n, r) => true, out var a, out _, out int countA));
            Assert.IsTrue(Plan((p, n, r) => true, out var b, out _, out int countB));
            Assert.AreEqual(a.AimDirection, b.AimDirection);
            Assert.AreEqual(countA, countB);
            Assert.AreEqual(before, balls[0].Position);
            Assert.AreEqual(0, state.ShotsPlayed);
        }

        [Test] public void BreakAndGameOver_DoNotSearch()
        {
            state.IsBreakShot = true;
            Assert.IsFalse(Plan((p, n, r) => true, out _, out _, out int trials));
            Assert.AreEqual(0, trials);
            state.IsBreakShot = false; state.IsGameOver = true;
            Assert.IsFalse(Plan((p, n, r) => true, out _, out _, out trials));
            Assert.AreEqual(0, trials);
        }
    }
}
