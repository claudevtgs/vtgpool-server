using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using VTG.Pool.Inputs;
using VTG.Pool.Match;
using VTG.Pool.Rules;
using VTG.Pool.Rules.Practice;
using VTG.Pool.Save;

namespace VTG.Pool.Tests
{
    public sealed class PracticeRuleSetTests
    {
        private static MatchState NewState(PracticeRuleSet rules)
        {
            var state = new MatchState(rules.Mode, new List<MatchPlayer> { new MatchPlayer(0, "A"), new MatchPlayer(1, "B") });
            rules.BeginRack(state);
            return state;
        }

        [Test]
        public void Rack_UsesBallSetOfTheChosenShape()
        {
            Assert.AreEqual(15, NewState(new PracticeRuleSet(GameMode.EightBall)).BallsOnTable.Count);
            Assert.AreEqual(9, NewState(new PracticeRuleSet(GameMode.NineBall)).BallsOnTable.Count);
            Assert.AreEqual(BallInHandMode.Anywhere, new PracticeRuleSet(GameMode.EightBall).OpeningBallInHand);
        }

        [Test]
        public void AnyShot_KeepsTheTableWithoutFoul()
        {
            var rules = new PracticeRuleSet(GameMode.EightBall);
            MatchState state = NewState(rules);
            var shot = new ShotRecord { FirstObjectBallHit = 8 };
            shot.BallsPocketed.Add(8);
            ShotOutcome outcome = rules.Evaluate(state, shot);
            Assert.IsFalse(outcome.Foul);
            Assert.IsTrue(outcome.TurnContinues);
            Assert.IsFalse(outcome.GameOver, "Pocketing the 8 never ends practice");
            Assert.AreEqual(BallInHandMode.None, outcome.BallInHand);
            Assert.IsTrue(rules.IsLegalFirstContact(state, 15));
        }

        [Test]
        public void Scratch_GivesBallInHandAnywhere()
        {
            var rules = new PracticeRuleSet(GameMode.NineBall);
            MatchState state = NewState(rules);
            ShotOutcome outcome = rules.Evaluate(state, new ShotRecord { FirstObjectBallHit = 1, CueBallPocketed = true });
            Assert.IsFalse(outcome.Foul);
            Assert.IsTrue(outcome.TurnContinues);
            Assert.AreEqual(BallInHandMode.Anywhere, outcome.BallInHand);
        }

        [Test]
        public void LastBall_ReportsTableCleared()
        {
            var rules = new PracticeRuleSet(GameMode.EightBall);
            MatchState state = NewState(rules);
            state.SetBallsOnTable(new[] { 5 });
            var shot = new ShotRecord { FirstObjectBallHit = 5 };
            shot.BallsPocketed.Add(5);
            ShotOutcome outcome = rules.Evaluate(state, shot);
            StringAssert.Contains("cleared", outcome.Message);
            Assert.IsFalse(outcome.GameOver);
        }
    }

    public sealed class TouchGesturesTests
    {
        [Test]
        public void TwoFinger_SpreadAndPan()
        {
            TouchGestures.TwoFinger(new Vector2(100f, 100f), new Vector2(200f, 100f), new Vector2(80f, 110f), new Vector2(240f, 110f), out float spread, out Vector2 pan);
            Assert.AreEqual(60f, spread, 1e-4f, "Fingers moved apart by 60 px");
            Assert.AreEqual(10f, pan.x, 1e-4f);
            Assert.AreEqual(10f, pan.y, 1e-4f);
        }

        [Test]
        public void PullPower_IsClampedFraction()
        {
            Assert.AreEqual(0.5f, TouchGestures.PullPower(0f, -100f, 200f), 1e-5f);
            Assert.AreEqual(1f, TouchGestures.PullPower(0f, -500f, 200f), 1e-5f);
            Assert.AreEqual(0f, TouchGestures.PullPower(0f, 50f, 200f), "Pushing up does not charge");
            Assert.AreEqual(0f, TouchGestures.PullPower(0f, -50f, 0f));
        }

        [Test]
        public void SafeArea_BecomesNormalisedAnchors()
        {
            // 2400x1080 landscape phone with a 120 px notch on the left and 60 px gesture bar at the bottom.
            TouchGestures.SafeAreaAnchors(new Rect(120f, 60f, 2280f, 1020f), new Vector2(2400f, 1080f), out Vector2 min, out Vector2 max);
            Assert.AreEqual(0.05f, min.x, 1e-5f);
            Assert.AreEqual(60f / 1080f, min.y, 1e-5f);
            Assert.AreEqual(1f, max.x, 1e-5f);
            Assert.AreEqual(1f, max.y, 1e-5f);
            TouchGestures.SafeAreaAnchors(new Rect(0f, 0f, 10f, 10f), Vector2.zero, out min, out max);
            Assert.AreEqual(Vector2.zero, min);
            Assert.AreEqual(Vector2.one, max);
        }

        [Test]
        public void TouchSettings_DefaultAndSanitised()
        {
            var settings = new GameSettings();
            Assert.AreEqual(-1, settings.touchControls, "Auto by default");
            Assert.IsFalse(settings.leftHanded);
            settings.touchControls = 7;
            settings.Sanitize();
            Assert.AreEqual(1, settings.touchControls);
        }
    }
}
