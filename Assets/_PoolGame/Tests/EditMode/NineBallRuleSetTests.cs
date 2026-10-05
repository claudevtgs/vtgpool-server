using System.Collections.Generic;
using NUnit.Framework;
using VTG.Pool.Match;
using VTG.Pool.Rules;
using VTG.Pool.Rules.NineBall;

namespace VTG.Pool.Tests
{
    public sealed class NineBallRuleSetTests
    {
        private NineBallRulesConfig config;
        private NineBallRuleSet rules;
        private MatchState state;

        [SetUp]
        public void SetUp()
        {
            config = NineBallRulesConfig.CreateDefault();
            rules = new NineBallRuleSet(config);
            state = new MatchState(GameMode.NineBall, new List<MatchPlayer> { new MatchPlayer(0, "A"), new MatchPlayer(1, "B") });
            rules.BeginRack(state);
        }

        [TearDown]
        public void TearDown() => UnityEngine.Object.DestroyImmediate(config);

        private static ShotRecord Shot(int firstHit, int railsAfter = 1, params int[] pocketed)
        {
            var record = new ShotRecord { FirstObjectBallHit = firstHit, RailContactsAfterFirstHit = railsAfter };
            record.BallsPocketed.AddRange(pocketed);
            return record;
        }

        private void AfterBreak(params int[] removed)
        {
            state.IsBreakShot = false;
            foreach (int ball in removed)
            {
                state.RemoveBall(ball);
            }
        }

        // ---------------------------------------------------------------- break

        [Test]
        public void LegalBreak_WithPot_Continues()
        {
            ShotOutcome outcome = rules.Evaluate(state, Shot(1, 3, 4));
            Assert.IsFalse(outcome.Foul);
            Assert.IsTrue(outcome.TurnContinues);
        }

        [Test]
        public void Break_NotHittingOneFirst_IsFoul()
        {
            ShotRecord shot = Shot(2, 3);
            shot.ObjectBallsHitRail.AddRange(new[] { 1, 2, 3, 4 });
            ShotOutcome outcome = rules.Evaluate(state, shot);
            Assert.AreEqual(FoulType.WrongBallFirst, outcome.FoulType);
            Assert.AreEqual(BallInHandMode.Anywhere, outcome.BallInHand);
        }

        [Test]
        public void Break_TooFewBallsToRail_IsIllegalBreakFoul()
        {
            ShotRecord shot = Shot(1, 1);
            shot.ObjectBallsHitRail.AddRange(new[] { 2, 3 });
            ShotOutcome outcome = rules.Evaluate(state, shot);
            Assert.AreEqual(FoulType.IllegalBreak, outcome.FoulType);
            Assert.AreEqual(BallInHandMode.Anywhere, outcome.BallInHand);
            Assert.IsFalse(outcome.Rerack);
        }

        [Test]
        public void NineOnLegalBreak_Wins()
        {
            ShotOutcome outcome = rules.Evaluate(state, Shot(1, 3, 9));
            Assert.IsTrue(outcome.GameOver);
            Assert.AreEqual(0, outcome.WinnerIndex);
        }

        [Test]
        public void NineOnBreak_WithScratch_IsRespottedFoul_WpaRule()
        {
            config.nineOnFoulLoses = false;
            ShotRecord shot = Shot(1, 3, 9);
            shot.CueBallPocketed = true;
            ShotOutcome outcome = rules.Evaluate(state, shot);
            Assert.IsFalse(outcome.GameOver);
            Assert.IsTrue(outcome.Foul);
            Assert.Contains(9, outcome.RespotBalls);
            Assert.AreEqual(BallInHandMode.Anywhere, outcome.BallInHand, "9-ball: scratch on the break gives ball in hand anywhere");
        }

        [Test]
        public void NineOnBreak_WhenPolicyOff_IsRespottedAndContinues()
        {
            config.nineOnBreakWins = false;
            ShotOutcome outcome = rules.Evaluate(state, Shot(1, 3, 9));
            Assert.IsFalse(outcome.GameOver);
            Assert.Contains(9, outcome.RespotBalls);
            Assert.IsTrue(outcome.TurnContinues);
        }

        // ---------------------------------------------------------------- normal play

        [Test]
        public void LowestBall_IsTheOnlyLegalFirstContact()
        {
            AfterBreak(1, 2);
            Assert.IsTrue(rules.IsLegalFirstContact(state, 3));
            Assert.IsFalse(rules.IsLegalFirstContact(state, 4));
            Assert.IsFalse(rules.IsLegalFirstContact(state, 9));
            Assert.AreEqual("3-ball", rules.DescribeTarget(state));
        }

        [Test]
        public void WrongBallFirst_IsFoul()
        {
            AfterBreak(1);
            ShotOutcome outcome = rules.Evaluate(state, Shot(5, 2));
            Assert.AreEqual(FoulType.WrongBallFirst, outcome.FoulType);
            Assert.AreEqual(BallInHandMode.Anywhere, outcome.BallInHand);
        }

        [Test]
        public void LegalPotOfAnyBall_Continues()
        {
            AfterBreak(1);
            ShotOutcome outcome = rules.Evaluate(state, Shot(2, 0, 6));
            Assert.IsFalse(outcome.Foul);
            Assert.IsTrue(outcome.TurnContinues, "Any ball pocketed on a legal shot keeps the turn");
        }

        [Test]
        public void ComboOnNine_Wins()
        {
            AfterBreak(1, 2);
            ShotOutcome outcome = rules.Evaluate(state, Shot(3, 0, 9));
            Assert.IsTrue(outcome.GameOver);
            Assert.AreEqual(0, outcome.WinnerIndex);
            StringAssert.Contains("combination", outcome.Message);
        }

        [Test]
        public void NineOnFoul_IsRespotted_NotAWin_WpaRule()
        {
            config.nineOnFoulLoses = false;
            AfterBreak(1, 2);
            ShotOutcome outcome = rules.Evaluate(state, Shot(5, 0, 9));
            Assert.IsFalse(outcome.GameOver);
            Assert.IsTrue(outcome.Foul);
            Assert.Contains(9, outcome.RespotBalls);
        }

        [Test]
        public void NineOnFoul_LosesTheRack_HouseRuleDefault()
        {
            Assert.IsTrue(config.nineOnFoulLoses, "House rule is on by default");
            AfterBreak(1, 2);
            ShotRecord shot = Shot(5, 0, 9); // wrong ball first, 9 drops
            ShotOutcome outcome = rules.Evaluate(state, shot);
            Assert.IsTrue(outcome.GameOver);
            Assert.IsTrue(outcome.Foul);
            Assert.AreEqual(state.OpponentIndex, outcome.WinnerIndex, "The fouling player loses");
            CollectionAssert.DoesNotContain(outcome.RespotBalls, 9);

            ShotRecord scratch = Shot(3, 1, 9);
            scratch.CueBallPocketed = true;
            Assert.IsTrue(rules.Evaluate(state, scratch).GameOver, "Legal hit but scratch while the 9 drops also loses");
        }

        [Test]
        public void NineOnBreak_WithScratch_LosesTheRack()
        {
            ShotRecord shot = Shot(1, 3, 9);
            shot.CueBallPocketed = true;
            ShotOutcome outcome = rules.Evaluate(state, shot);
            Assert.IsTrue(outcome.GameOver);
            Assert.AreEqual(state.OpponentIndex, outcome.WinnerIndex);
        }

        [Test]
        public void NoRailAfterContact_IsFoul()
        {
            AfterBreak(1);
            ShotOutcome outcome = rules.Evaluate(state, Shot(2, 0));
            Assert.AreEqual(FoulType.NoRailAfterContact, outcome.FoulType);
        }

        [Test]
        public void ThirdConsecutiveFoul_Loses()
        {
            AfterBreak(1);
            state.CurrentPlayer.ConsecutiveFouls = 2;
            ShotRecord shot = Shot(2, 1);
            shot.CueBallPocketed = true;
            ShotOutcome outcome = rules.Evaluate(state, shot);
            Assert.IsTrue(outcome.GameOver);
            Assert.AreEqual(1, outcome.WinnerIndex);
        }

        [Test]
        public void SecondConsecutiveFoul_Warns()
        {
            AfterBreak(1);
            state.CurrentPlayer.ConsecutiveFouls = 1;
            ShotOutcome outcome = rules.Evaluate(state, Shot(-1, 0));
            Assert.IsFalse(outcome.GameOver);
            StringAssert.Contains("two fouls", outcome.Message);
        }

        [Test]
        public void ThreeFoulRuleOff_NeverLoses()
        {
            config.threeFoulRule = false;
            AfterBreak(1);
            state.CurrentPlayer.ConsecutiveFouls = 5;
            ShotOutcome outcome = rules.Evaluate(state, Shot(-1, 0));
            Assert.IsFalse(outcome.GameOver);
        }
    }
}
