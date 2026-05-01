using TacticsGame.Core;

namespace TacticsGame.Spells
{
    /// <summary>
    /// Roll result carrying the natural die value, computed total, and degree of success.
    /// Enables crit specialization, fortune/misfortune, and reroll hooks.
    /// </summary>
    public class RollResult
    {
        public int NaturalRoll { get; set; }
        public int Total { get; set; }
        public Degree Degree { get; set; }

        public RollResult(int naturalRoll, int total, Degree degree)
        {
            NaturalRoll = naturalRoll;
            Total = total;
            Degree = degree;
        }

        public bool IsNatural20 => NaturalRoll == 20;
        public bool IsNatural1 => NaturalRoll == 1;
    }
}
