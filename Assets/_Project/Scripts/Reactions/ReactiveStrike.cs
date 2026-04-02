using System;
using PathfinderTactics.Characters;
using PathfinderTactics.Core;
using PathfinderTactics.Grid;
using UnityEngine;

namespace PathfinderTactics.Reactions
{
    public class ReactiveStrike : BaseReaction
    {
        [SerializeField]
        private int reach = 1;

        public override int GetPriority() => 50;

        public override bool CanTrigger(GameEvent gameEvent)
        {
            if (gameEvent is BeforeMoveEvent moveEvent)
            {
                if (moveEvent.IsStep)
                {
                    Debug.Log(
                        $"{unit.name} ignores {moveEvent.SourceUnit.name} because they Stepped!"
                    );
                    return false;
                }

                if (!unit.IsEnemy(moveEvent.SourceUnit))
                    return false;

                int dist = PF2E_Core.GetPF2eDistance3D(
                    moveEvent.StartLayeredPos,
                    unit.CurrentLayeredPosition
                );

                return dist <= reach;
            }

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
            int ac = target.GetArmorClass();

            if (d20 + attackBonus >= ac)
            {
                int damage = UnityEngine.Random.Range(1, 9) + 4; // Longsword + Str
                Debug.Log($"<color=red>HIT!</color> Dealt {damage} damage.");

                IDamageable targetHealth = target.GetComponent<IDamageable>();
                if (targetHealth != null)
                {
                    // TODO: Add critical hit logic later
                    targetHealth.ApplyDamage(unit, damage, DamageType.Slashing, false);
                    // PF2e Rule: If the reaction kills them, they don't finish moving!
                    if (targetHealth.IsDead)
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
