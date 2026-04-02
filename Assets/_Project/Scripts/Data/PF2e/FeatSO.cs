using System.Collections.Generic;
using UnityEngine;

// using PathfinderTactics.Characters;

namespace PathfinderTactics.Data.PF2e
{
    [CreateAssetMenu(menuName = "PF2e/Feat")]
    public class FeatSO : GameElementSO
    {
        public int LevelRequirement;
        public List<string> PrerequisiteFeatIds = new List<string>();

        // Hooks for future PassiveModifier system
        // public List<PassiveModifier> GrantedModifiers;
    }
}
