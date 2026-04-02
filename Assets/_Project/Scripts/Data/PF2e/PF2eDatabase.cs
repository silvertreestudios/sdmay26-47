using System.Collections.Generic;
using UnityEngine;

namespace PathfinderTactics.Data.PF2e
{
    [CreateAssetMenu(menuName = "PF2e/Database")]
    public class PF2eDatabase : ScriptableObject
    {
        [Header("Compiled Compendium Index")]
        [Tooltip("Contains all imported Pathfinder 2e spells and rituals.")]
        public List<SpellSO> AllSpells = new List<SpellSO>();

        [Tooltip("Contains all imported Pathfinder 2e feats (Class, Ancestry, General, Skill).")]
        public List<FeatSO> AllFeats = new List<FeatSO>();

        [Tooltip(
            "Contains generic items like equipment, basic actions, conditions, and ancestries."
        )]
        public List<GameElementSO> AllItems = new List<GameElementSO>();

        [Header("Character Creation")]
        [Tooltip("All PF2e actions (Stride, Strike, etc.).")]
        public List<ActionSO> AllActions = new List<ActionSO>();

        [Tooltip("All PF2e classes (Fighter, Wizard, etc.).")]
        public List<ClassSO> AllClasses = new List<ClassSO>();

        [Tooltip("All PF2e ancestries (Human, Elf, Dwarf, etc.).")]
        public List<AncestrySO> AllAncestries = new List<AncestrySO>();

        [Tooltip("All PF2e backgrounds (Acolyte, Criminal, etc.).")]
        public List<BackgroundSO> AllBackgrounds = new List<BackgroundSO>();
    }
}
