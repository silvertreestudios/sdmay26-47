using System;
using TacticsGame.Characters;
using UnityEngine;

namespace TacticsGame.Reactions
{
    // TODO: Update to use sheild and make accurate.
    public class ShieldBlock : BaseReaction
    {
        [Header("Shield Stats")]
        [SerializeField]
        private int shieldHardness = 5;

        // In full PF2e, the shield also has HP and can break, but will stick to Hardness for now

        public override int GetPriority() => 60; // Triggers before damage is locked in

        public override bool CanTrigger(GameEvent gameEvent)
        {
            // Is this a BeforeDamageEvent?
            if (gameEvent is BeforeDamageEvent damageEvent)
            {
                // Are WE the ones being attacked?
                if (damageEvent.TargetUnit != this.unit)
                    return false;

                // Is there damage to reduce?
                if (damageEvent.DamageAmount <= 0)
                    return false;

                // You must have your shield raised.
                // For now, assume it's raised for this prototype if the reaction is available.
                return true;
            }

            return false;
        }

        public override void Execute(ReactionIntent intent, Action onReactionComplete)
        {
            BeforeDamageEvent damageEvent = intent.TriggeringEvent as BeforeDamageEvent;

            int originalDamage = damageEvent.DamageAmount;

            // Apply Hardness reduction
            int newDamage = Mathf.Max(0, originalDamage - shieldHardness);

            // MUTATE THE EVENT PAYLOAD
            damageEvent.DamageAmount = newDamage;

            Debug.Log(
                $"<color=cyan>[SHIELD BLOCK]</color> {unit.name} raised their shield! Reduced damage from {originalDamage} to {newDamage} (Hardness {shieldHardness})."
            );

            // Complete the reaction to let the queue continue
            onReactionComplete?.Invoke();
        }
    }
}
