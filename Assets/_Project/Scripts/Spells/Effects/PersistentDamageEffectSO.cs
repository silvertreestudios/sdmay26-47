using TacticsGame.Characters;
using TacticsGame.Data.TacticsRuleset;
using UnityEngine;

namespace TacticsGame.Spells.Effects
{
    /// <summary>
    /// Resolution Phase: Applies persistent damage to the target.
    /// Handles PF2e persistent damage rules (d6s, recovery checks, etc).
    /// </summary>
    [CreateAssetMenu(menuName = "TacticsRuleset/Spell Effects/Persistent Damage")]
    public class PersistentDamageEffectSO : SpellEffectSO
    {
        [Header("Damage Configuration")]
        public DamageType Type = DamageType.Acid;
        public int DiceCount = 1;
        public int DiceFaces = 6;
        public int FlatDamage = 0;

        public bool OnlyOnFailure = true;

        private void OnEnable()
        {
            Phase = SpellEffectPhase.Resolution;
        }

        public override void Apply(SpellCastContext context)
        {
            foreach (Unit target in context.AffectedUnits)
            {
                if (OnlyOnFailure)
                {
                    if (context.RollResults.TryGetValue(target, out var result))
                    {
                        if (result.Degree > Core.Degree.Failure)
                            continue;
                    }
                }

                UnitConditions conditions = target.GetComponent<UnitConditions>();
                if (conditions != null)
                {
                    conditions.ApplyPersistentDamage(
                        Type,
                        DiceCount,
                        DiceFaces,
                        FlatDamage,
                        context.Caster
                    );
                    Debug.Log(
                        $"<color=orange>[PERSISTENT]</color> {target.name} is now taking persistent {Type} damage."
                    );
                }
            }
        }
    }
}
