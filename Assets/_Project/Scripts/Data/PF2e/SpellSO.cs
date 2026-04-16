using System.Collections.Generic;
using PathfinderTactics.Characters;
using PathfinderTactics.Combat;
using PathfinderTactics.Spells;
using UnityEngine;

namespace PathfinderTactics.Data.PF2e
{
    [CreateAssetMenu(menuName = "PF2e/Spell")]
    public class SpellSO : GameElementSO
    {
        [Header("Spell Rules")]
        public int Level;
        public List<string> Traditions = new List<string>();
        public ActionCost Cost;
        public string School; // "evocation", "abjuration", etc.
        public List<string> Components = new List<string>(); // "verbal", "somatic", "material"

        [Header("Targeting")]
        public SpellTargetingType Targeting;
        public TargetType Target;
        public int Range; // 0 for touch/none
        public bool RequiresLineOfEffect = true;
        public AreaDefinition Area;
        public bool SpellAttackRoll; // True = uses attack roll, False = uses save DC

        [Header("Combat Mechanics")]
        public SavingThrowType SaveType;
        public DamageType ElementType;
        public bool IsBasicSave;
        public string Duration; // "1 minute", "sustained", etc.
        public bool IsSustained;
        public DiceFormula BaseDamage;

        [Header("Heightening Scaling")]
        public string HeightenRules; // E.g., "+1" or "+2"
        public DiceFormula HeightenDamageScaling; // E.g., +2d6

        [Header("Effect System")]
        /// <summary>
        /// Composable effect pipeline. Executed by SpellEffectResolver in phase order.
        /// Designers build spells by combining effects (Damage, Save, Condition, Area, etc.)
        /// </summary>
        public List<SpellEffectSO> Effects = new List<SpellEffectSO>();

        [Header("AI Metadata")]
        // Used by EnemyAIManager out-of-the-box to check if an enemy CAN cast this usefully
        // e.g. "damage", "heal", "aoe", "buff", "movement", "control", "summon"
        public List<string> AITags = new List<string>();
    }
}
