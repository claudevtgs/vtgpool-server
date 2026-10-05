using VTG.Pool.Match;

namespace VTG.Pool.AI
{
    public sealed class AIPlayer : MatchPlayer
    {
        public AIPlayer(int index, string name) : base(index, name, PlayerKind.AI) { }
    }
}
