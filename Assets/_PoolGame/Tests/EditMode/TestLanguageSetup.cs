using NUnit.Framework;
using VTG.Pool.Localization;

namespace VTG.Pool.Tests
{
    /// <summary>Tests assert English texts: pin the language regardless of the machine's settings or system language.</summary>
    [SetUpFixture]
    public sealed class TestLanguageSetup
    {
        [OneTimeSetUp]
        public void PinEnglish()
        {
            Loc.Override = Language.English;
            // Replays add seconds to every pot; tests that need them switch them back on.
            VTG.Pool.Replay.ShotReplay.ForceDisabled = true;
        }

        [OneTimeTearDown]
        public void Release()
        {
            Loc.Override = null;
            VTG.Pool.Replay.ShotReplay.ForceDisabled = false;
        }
    }
}
