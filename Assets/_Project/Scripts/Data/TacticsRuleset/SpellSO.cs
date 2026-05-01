using System;
using System.Collections.Generic;
using TacticsGame.Characters;
using TacticsGame.Combat;
using TacticsGame.Spells;
using UnityEngine;

namespace TacticsGame.Data.TacticsRuleset
{
    public enum SpellDelivery
    {
        Instant,
        Projectile,
    }

    [CreateAssetMenu(menuName = "TacticsRuleset/Spell")]
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

        [Header("Delivery Mechanics")]
        public SpellDelivery DeliveryType = SpellDelivery.Instant;

        [Tooltip("Only used if deliveryType is Projectile.")]
        public float ProjectileSpeed = 15f;

        [Header("Visual Effects (Particles)")]
        [Tooltip("Spawns at the caster's hand when the cast begins.")]
        public GameObject CastVFXPrefab;

        [Tooltip("Spawns and travels from caster to target.")]
        public GameObject ProjectileVFXPrefab;

        [Tooltip("Spawns at the target's location upon impact.")]
        public GameObject HitVFXPrefab;

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
        // Used by EnemyAIManager out-of-the-box to check if an enemy can cast this usefully
        // e.g. "damage", "heal", "aoe", "buff", "movement", "control", "summon"
        public List<string> AITags = new List<string>();
    }
}
