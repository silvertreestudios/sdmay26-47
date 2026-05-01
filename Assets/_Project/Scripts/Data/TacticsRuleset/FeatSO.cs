using System.Collections.Generic;
using UnityEngine;

// using TacticsGame.Characters;

namespace TacticsGame.Data.TacticsRuleset
{
    [CreateAssetMenu(menuName = "TacticsRuleset/Feat")]
    public class FeatSO : GameElementSO
    {
        public int LevelRequirement;
        public List<string> PrerequisiteFeatIds = new List<string>();

        // Hooks for future PassiveModifier system
        // public List<PassiveModifier> GrantedModifiers;
    }
}
