using NUnit.Framework;
using VTG.Pool.Localization;
using VTG.Pool.Match;
using VTG.Pool.Rules;
using VTG.Pool.Rules.Practice;

namespace VTG.Pool.Tests
{
    public sealed class LocalizationTests
    {
        [TearDown]
        public void TearDown() => Loc.Override = Language.English;

        [Test]
        public void EveryKeyHasBothLanguages()
        {
            CollectionAssert.IsEmpty(Loc.MissingTranslations());
        }

        [Test]
        public void Vietnamese_TranslatesAndFormats()
        {
            Loc.Override = Language.Vietnamese;
            Assert.AreEqual("CHƠI", Loc.T("menu.play"));
            Assert.AreEqual("LỰC 75%", Loc.T("hud.power", 75));
            Assert.AreEqual("Bi trơn", BallGroups.Describe(BallGroup.Solids));
            Loc.Override = Language.English;
            Assert.AreEqual("PLAY", Loc.T("menu.play"));
            Assert.AreEqual("POWER 75%", Loc.T("hud.power", 75));
        }

        [Test]
        public void UnknownKey_FallsBackToKey()
        {
            Assert.AreEqual("no.such.key", Loc.T("no.such.key"));
            Assert.AreEqual("PLAY", Loc.InEnglish("menu.play"));
        }

        [Test]
        public void SettingValues_Resolve()
        {
            Assert.AreEqual(Language.English, Loc.Resolve(0));
            Assert.AreEqual(Language.Vietnamese, Loc.Resolve(1));
        }

        [Test]
        public void RuleMessages_FollowLanguage()
        {
            var rules = new PracticeRuleSet(GameMode.EightBall);
            var state = new MatchState(GameMode.EightBall, new System.Collections.Generic.List<MatchPlayer> { new MatchPlayer(0, "A"), new MatchPlayer(1, "B") });
            rules.BeginRack(state);
            Loc.Override = Language.Vietnamese;
            Assert.AreEqual("LUYỆN TẬP", rules.DisplayName);
            StringAssert.Contains("Bi cái rơi lỗ", rules.Evaluate(state, new ShotRecord { CueBallPocketed = true, FirstObjectBallHit = 1 }).Message);
        }
    }
}
