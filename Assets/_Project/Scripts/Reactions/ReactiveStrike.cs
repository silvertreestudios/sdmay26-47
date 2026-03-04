using System;
using PathfinderTactics.Characters;
using PathfinderTactics.Grid;
using UnityEngine;

namespace PathfinderTactics.Reactions
{
    public class ReactiveStrike : BaseReaction
    {
        [SerializeField]
        private int reach = 1;

        public override int GetPriority() => 50; // Standard attack priority

        public override bool CanTrigger(GameEvent gameEvent)
        {
            // Reactive Strike triggers when an enemy LEAVES a threatened square
            if (gameEvent is BeforeMoveEvent moveEvent)
            {
                // Is it an enemy?
                if (!unit.IsEnemy(moveEvent.SourceUnit))
                    return false;

                // Were they inside our reach before they moved?
                int dist = Mathf.Max(
                    Mathf.Abs(moveEvent.FromPos.x - unit.CurrentGridPosition.x),
                    Mathf.Abs(moveEvent.FromPos.z - unit.CurrentGridPosition.z)
                );

                return dist <= reach;
            }

            // TODO: Add RangedAttackEvent and ManipulateEvent triggers here
            return false;
        }

        public override void Execute(ReactionIntent intent, Action onReactionComplete)
        {
            Unit target = intent.TriggeringEvent.SourceUnit;
            Debug.Log(
                $"<color=orange>[REACTION]</color> {unit.name} takes a REACTIVE STRIKE against {target.name}!"
            );

            // PF2e Combat Math
            // TODO: replace these with PF2E_Core references later
            int d20 = UnityEngine.Random.Range(1, 21);
            int attackBonus = 7;
            int ac = target.getArmorClass();

            if (d20 + attackBonus >= ac)
            {
                int damage = UnityEngine.Random.Range(1, 9) + 4; // Longsword + Str
                Debug.Log($"<color=red>HIT!</color> Dealt {damage} damage.");

                UnitHealth targetHealth = target.GetComponent<UnitHealth>();
                if (targetHealth != null)
                {
                    // TODO: Add critical hit logic later
                    targetHealth.ApplyDamage(unit, damage, false);
                    // PF2e Rule: If the reaction kills them, they don't finish moving!
                    if (targetHealth.IsDead || targetHealth.IsUnconscious)
                    {
                        Debug.Log($"{target.name} was struck down while moving!");
                        intent.TriggeringEvent.IsCancelled = true;
                    }
                }
            }
            else
            {
                Debug.Log("<color=grey>MISSED!</color>");
            }

            // Complete the reaction and tell the Manager to process the next one
            onReactionComplete?.Invoke();
        }
    }
}
