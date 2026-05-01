using System;
using TacticsGame.Core;

namespace TacticsGame.Data
{
    [Serializable]
    public class SpellSelection
    {
        public string SpellID;
        public int Rank;
        public SpellTradition Tradition; // Enum: Arcane, Divine, Occult, Primal, Focus, Innate
        public SpellSlotType SlotType; // Enum: Prepared, Repertoire, Cantrip
        public string SourceID; // e.g., "wizard_class", "ancestry_feat"
        public int Level; // The level the spell was learned
    }
}
