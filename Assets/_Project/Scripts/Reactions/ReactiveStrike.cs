using System;
using PathfinderTactics.Characters;
using PathfinderTactics.Core;
using PathfinderTactics.Grid;
using PathfinderTactics.Items;
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
            if (!unit.HasReactionAvailable)
                return false;

            if (gameEvent is BeforeMoveEvent moveEvent)
            {
                if (moveEvent.IsStep)
                {
                    // Debug.Log(
                    //     $"{unit.name} ignores {moveEvent.SourceUnit.name} because they Stepped!"
                    // );
                    return false;
                }

                if (!unit.IsEnemy(moveEvent.SourceUnit))
                    return false;

                // Triggered by leaving a square.
                // Check if the square being left is within our reach.
                int dist = PF2E_Core.GetPF2eDistance3D(
                    moveEvent.StartLayeredPos,
                    unit.CurrentLayeredPosition
                );

                return dist <= reach;
            }

            if (gameEvent is ManipulateEvent manipulateEvent)
            {
                if (!unit.IsEnemy(manipulateEvent.SourceUnit))
                    return false;

                int dist = PF2E_Core.GetPF2eDistance3D(
                    manipulateEvent.SourceUnit.CurrentLayeredPosition,
                    unit.CurrentLayeredPosition
                );
                return dist <= reach;
            }

            if (gameEvent is RangedAttackEvent rangedEvent)
            {
                if (!unit.IsEnemy(rangedEvent.SourceUnit))
                    return false;

                int dist = PF2E_Core.GetPF2eDistance3D(
                    rangedEvent.SourceUnit.CurrentLayeredPosition,
                    unit.CurrentLayeredPosition
                );
                return dist <= reach;
            }

            if (gameEvent is MoveActionEvent moveActionEvent)
            {
                if (!unit.IsEnemy(moveActionEvent.SourceUnit))
                    return false;

                int dist = PF2E_Core.GetPF2eDistance3D(
                    moveActionEvent.SourceUnit.CurrentLayeredPosition,
                    unit.CurrentLayeredPosition
                );
                return dist <= reach;
            }

            return false;
        }

        public override void Execute(ReactionIntent intent, Action onReactionComplete)
        {
            Unit target = intent.TriggeringEvent.SourceUnit;
            if (target == null)
            {
                onReactionComplete?.Invoke();
                return;
            }

            Debug.Log(
                $"<color=orange>[REACTION]</color> {unit.name} takes a REACTIVE STRIKE against {target.name}!"
            );

            // PF2e Combat Math
            WeaponSO weapon = null;
            var equipment = unit.GetComponent<UnitEquipment>();
            if (equipment != null)
                weapon = equipment.GetMainWeapon();

            int level = unit.Level;
            int strengthMod = unit.GetAbilityModifier(AbilityScore.STR);
            int dexMod = unit.GetAbilityModifier(AbilityScore.DEX);

            bool isFinesse = weapon != null && weapon.HasTrait(WeaponTrait.Finesse);
            int attackStatMod = isFinesse ? Mathf.Max(strengthMod, dexMod) : strengthMod;
            AbilityScore attackStat =
                (isFinesse && dexMod > strengthMod) ? AbilityScore.DEX : AbilityScore.STR;

            Proficiency weaponProf = Proficiency.Trained; // Assume trained for now

            int attackBonus = PF2E_Core.CalculateAttackRollModifier(
                unit,
                attackStat,
                attackStatMod,
                level,
                weaponProf,
                AttackType.Melee,
                0 // Reactive strikes do not suffer MAP
            );

            int d20 = UnityEngine.Random.Range(1, 21);
            int targetAC = target.GetArmorClass(unit, AttackType.Melee);
            Degree degree = PF2E_Core.CheckResult(d20, attackBonus, targetAC);

            if (degree >= Degree.Success)
            {
                int weaponDiceRoll = 0;
                if (weapon != null)
                {
                    for (int i = 0; i < weapon.damageDice.count; i++)
                    {
                        weaponDiceRoll += UnityEngine.Random.Range(1, weapon.damageDice.sides + 1);
                    }
                }
                else
                {
                    // Fallback unarmed
                    weaponDiceRoll = UnityEngine.Random.Range(1, 5); // 1d4
                }

                int damageModifiers = strengthMod;
                var myConditions = unit.GetComponent<UnitConditions>();
                if (myConditions != null && myConditions.HasCondition(ConditionType.Enfeebled))
                {
                    damageModifiers -= myConditions.GetConditionValue(ConditionType.Enfeebled);
                }

                int damage = weaponDiceRoll + damageModifiers;
                if (damage < 1)
                    damage = 1;

                bool isCrit = (degree == Degree.CriticalSuccess);

                if (isCrit)
                    damage *= 2;

                Debug.Log(
                    $"<color={(isCrit ? "orange" : "red")}>{(isCrit ? "CRITICAL HIT!" : "HIT!")}</color> Dealt {damage} damage."
                );

                DamageType damageType = weapon != null ? weapon.damageType : DamageType.Bludgeoning;

                IDamageable targetHealth = target.GetComponent<IDamageable>();
                if (targetHealth != null)
                {
                    targetHealth.ApplyDamage(unit, damage, damageType, isCrit);

                    // PF2e Rule: If the reaction kills them, the action is disrupted regardless.
                    if (targetHealth.IsDead)
                    {
                        Debug.Log($"{target.name} was struck down during their action!");
                        intent.TriggeringEvent.IsCancelled = true;
                    }
                    // PF2e Rule: Reactive Strike only disrupts manipulate actions on a critical hit.
                    // It does not disrupt movement or ranged attacks by default.
                    else if (isCrit && intent.TriggeringEvent is ManipulateEvent)
                    {
                        Debug.Log(
                            $"<color=orange>[REACTION]</color> {target.name}'s action is DISRUPTED by a critical Reactive Strike!"
                        );
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
