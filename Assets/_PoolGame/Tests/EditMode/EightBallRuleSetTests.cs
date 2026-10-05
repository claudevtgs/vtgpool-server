using System.Collections.Generic;
using NUnit.Framework;
using VTG.Pool.Match;
using VTG.Pool.Rules;
using VTG.Pool.Rules.EightBall;

namespace VTG.Pool.Tests
{
    public sealed class EightBallRuleSetTests
    {
        private EightBallRulesConfig config;
        private EightBallRuleSet rules;
        private MatchState state;

        [SetUp]
        public void SetUp()
        {
            config = EightBallRulesConfig.CreateDefault();
            rules = new EightBallRuleSet(config);
            state = new MatchState(GameMode.EightBall, new List<MatchPlayer> { new MatchPlayer(0, "A"), new MatchPlayer(1, "B") });
            rules.BeginRack(state);
        }

        [TearDown]
        public void TearDown() => UnityEngine.Object.DestroyImmediate(config);

        private void AfterBreak(BallGroup shooterGroup)
        {
            state.IsBreakShot = false;
            if (shooterGroup != BallGroup.None)
            {
                state.TableOpen = false;
                state.CurrentPlayer.Group = shooterGroup;
                state.Opponent.Group = BallGroups.Opposite(shooterGroup);
            }
        }

        private static ShotRecord Shot(int firstHit, int railsAfter = 1, params int[] pocketed)
        {
            var record = new ShotRecord { FirstObjectBallHit = firstHit, RailContactsAfterFirstHit = railsAfter };
            record.BallsPocketed.AddRange(pocketed);
            return record;
        }

        private void ClearGroup(BallGroup group)
        {
            for (int n = 1; n <= 15; n++)
            {
                if (BallGroups.GroupOf(n) == group)
                {
                    state.RemoveBall(n);
                }
            }
        }

        // ---------------------------------------------------------------- break

        [Test]
        public void LegalBreak_WithPot_ContinuesAndTableStaysOpen()
        {
            ShotRecord shot = Shot(1, 3, 3);
            shot.ObjectBallsHitRail.AddRange(new[] { 2, 5 });
            ShotOutcome outcome = rules.Evaluate(state, shot);
            Assert.IsFalse(outcome.Foul);
            Assert.IsTrue(outcome.TurnContinues);
            Assert.AreEqual(BallGroup.None, outcome.AssignGroupToShooter, "WPA: table is open after the break");
        }

        [Test]
        public void LegalBreak_FourBallsToRail_NoPot_PassesTurn()
        {
            ShotRecord shot = Shot(1, 5);
            shot.ObjectBallsHitRail.AddRange(new[] { 2, 5, 9, 12 });
            ShotOutcome outcome = rules.Evaluate(state, shot);
            Assert.IsFalse(outcome.Foul);
            Assert.IsFalse(outcome.TurnContinues);
            Assert.IsFalse(outcome.Rerack);
        }

        [Test]
        public void IllegalBreak_Reracks()
        {
            ShotRecord shot = Shot(1, 1);
            shot.ObjectBallsHitRail.AddRange(new[] { 2, 5 });
            ShotOutcome outcome = rules.Evaluate(state, shot);
            Assert.IsTrue(outcome.Foul);
            Assert.AreEqual(FoulType.IllegalBreak, outcome.FoulType);
            Assert.IsTrue(outcome.Rerack);
        }

        [Test]
        public void ScratchOnLegalBreak_BallInHandBehindHeadString()
        {
            ShotRecord shot = Shot(1, 3, 4);
            shot.CueBallPocketed = true;
            ShotOutcome outcome = rules.Evaluate(state, shot);
            Assert.IsTrue(outcome.Foul);
            Assert.AreEqual(FoulType.Scratch, outcome.FoulType);
            Assert.AreEqual(BallInHandMode.BehindHeadString, outcome.BallInHand);
            Assert.IsFalse(outcome.TurnContinues);
        }

        [Test]
        public void EightOnBreak_IsRespottedNotLoss()
        {
            ShotRecord shot = Shot(1, 3, 8);
            ShotOutcome outcome = rules.Evaluate(state, shot);
            Assert.IsFalse(outcome.GameOver);
            Assert.Contains(8, outcome.RespotBalls);
            Assert.IsFalse(outcome.Foul);
        }

        [Test]
        public void EightOnBreak_WinPolicy_Wins()
        {
            config.eightOnBreak = EightOnBreakPolicy.Win;
            ShotOutcome outcome = rules.Evaluate(state, Shot(1, 3, 8));
            Assert.IsTrue(outcome.GameOver);
            Assert.AreEqual(0, outcome.WinnerIndex);
        }

        // ---------------------------------------------------------------- open table

        [Test]
        public void OpenTable_PotAssignsGroupOfFirstPocketedBall()
        {
            AfterBreak(BallGroup.None);
            ShotOutcome outcome = rules.Evaluate(state, Shot(11, 0, 11, 2));
            Assert.IsFalse(outcome.Foul);
            Assert.AreEqual(BallGroup.Stripes, outcome.AssignGroupToShooter);
            Assert.IsTrue(outcome.TurnContinues);
        }

        [Test]
        public void OpenTable_HittingEightFirst_IsFoul()
        {
            AfterBreak(BallGroup.None);
            ShotOutcome outcome = rules.Evaluate(state, Shot(8, 1));
            Assert.IsTrue(outcome.Foul);
            Assert.AreEqual(FoulType.WrongBallFirst, outcome.FoulType);
            Assert.AreEqual(BallInHandMode.Anywhere, outcome.BallInHand);
        }

        [Test]
        public void OpenTable_AnyGroupFirstIsLegal()
        {
            AfterBreak(BallGroup.None);
            Assert.IsTrue(rules.IsLegalFirstContact(state, 3));
            Assert.IsTrue(rules.IsLegalFirstContact(state, 12));
            Assert.IsFalse(rules.IsLegalFirstContact(state, 8));
        }

        // ---------------------------------------------------------------- groups

        [Test]
        public void WrongGroupFirst_IsFoul()
        {
            AfterBreak(BallGroup.Solids);
            ShotOutcome outcome = rules.Evaluate(state, Shot(10, 2));
            Assert.IsTrue(outcome.Foul);
            Assert.AreEqual(FoulType.WrongBallFirst, outcome.FoulType);
        }

        [Test]
        public void OwnGroupPot_Continues()
        {
            AfterBreak(BallGroup.Solids);
            ShotOutcome outcome = rules.Evaluate(state, Shot(3, 0, 3));
            Assert.IsFalse(outcome.Foul);
            Assert.IsTrue(outcome.TurnContinues);
        }

        [Test]
        public void OnlyOpponentBallPotted_TurnPassesWithoutFoul()
        {
            AfterBreak(BallGroup.Solids);
            ShotOutcome outcome = rules.Evaluate(state, Shot(3, 1, 12));
            Assert.IsFalse(outcome.Foul);
            Assert.IsFalse(outcome.TurnContinues);
        }

        [Test]
        public void NoRailAfterContact_IsFoul()
        {
            AfterBreak(BallGroup.Solids);
            ShotOutcome outcome = rules.Evaluate(state, Shot(3, 0));
            Assert.IsTrue(outcome.Foul);
            Assert.AreEqual(FoulType.NoRailAfterContact, outcome.FoulType);
        }

        [Test]
        public void NoContact_IsFoul()
        {
            AfterBreak(BallGroup.Solids);
            ShotOutcome outcome = rules.Evaluate(state, Shot(-1, 2));
            Assert.AreEqual(FoulType.NoContact, outcome.FoulType);
            Assert.AreEqual(BallInHandMode.Anywhere, outcome.BallInHand);
        }

        [Test]
        public void Scratch_IsFoulWithBallInHandAnywhere()
        {
            AfterBreak(BallGroup.Stripes);
            ShotRecord shot = Shot(12, 1, 12);
            shot.CueBallPocketed = true;
            ShotOutcome outcome = rules.Evaluate(state, shot);
            Assert.IsTrue(outcome.Foul);
            Assert.AreEqual(FoulType.Scratch, outcome.FoulType);
            Assert.AreEqual(BallInHandMode.Anywhere, outcome.BallInHand);
            Assert.IsFalse(outcome.TurnContinues, "A pot on a scratch does not keep the turn");
        }

        [Test]
        public void ObjectBallOffTable_IsFoulAndRespotted()
        {
            AfterBreak(BallGroup.Solids);
            ShotRecord shot = Shot(2, 2);
            shot.BallsOffTable.Add(13);
            ShotOutcome outcome = rules.Evaluate(state, shot);
            Assert.AreEqual(FoulType.ObjectBallOffTable, outcome.FoulType);
            Assert.Contains(13, outcome.RespotBalls);
        }

        // ---------------------------------------------------------------- the 8

        [Test]
        public void EarlyEight_Loses()
        {
            AfterBreak(BallGroup.Solids);
            ShotOutcome outcome = rules.Evaluate(state, Shot(2, 1, 2, 8));
            Assert.IsTrue(outcome.GameOver);
            Assert.AreEqual(1, outcome.WinnerIndex, "Opponent wins on an early 8");
        }

        [Test]
        public void LegalEight_Wins()
        {
            AfterBreak(BallGroup.Solids);
            ClearGroup(BallGroup.Solids);
            Assert.AreEqual("8-ball", rules.DescribeTarget(state));
            ShotOutcome outcome = rules.Evaluate(state, Shot(8, 0, 8));
            Assert.IsTrue(outcome.GameOver);
            Assert.AreEqual(0, outcome.WinnerIndex);
        }

        [Test]
        public void EightWithScratch_Loses()
        {
            AfterBreak(BallGroup.Solids);
            ClearGroup(BallGroup.Solids);
            ShotRecord shot = Shot(8, 0, 8);
            shot.CueBallPocketed = true;
            ShotOutcome outcome = rules.Evaluate(state, shot);
            Assert.IsTrue(outcome.GameOver);
            Assert.AreEqual(1, outcome.WinnerIndex);
        }

        [Test]
        public void OnEight_HittingGroupBallFirst_IsFoul()
        {
            AfterBreak(BallGroup.Solids);
            ClearGroup(BallGroup.Solids);
            ShotOutcome outcome = rules.Evaluate(state, Shot(12, 1));
            Assert.IsTrue(outcome.Foul);
            Assert.AreEqual(FoulType.WrongBallFirst, outcome.FoulType);
        }

        [Test]
        public void ClearingLastBallAndEightInSameShot_Loses()
        {
            AfterBreak(BallGroup.Solids);
            ClearGroup(BallGroup.Solids);
            state.AddBall(7);
            ShotOutcome outcome = rules.Evaluate(state, Shot(7, 0, 7, 8));
            Assert.IsTrue(outcome.GameOver);
            Assert.AreEqual(1, outcome.WinnerIndex);
        }
    }
}
