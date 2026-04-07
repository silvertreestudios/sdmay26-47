using System;
using System.Collections.Generic;
using PathfinderTactics.Characters;
using PathfinderTactics.Combat;
using PathfinderTactics.Core;
using PathfinderTactics.Grid;
using PathfinderTactics.Items;
using UnityEngine;

namespace PathfinderTactics.Actions
{
    public class MeleeAction : BaseAction
    {
        private const bool STEALTH_DEBUG = true;

        private Unit targetUnit;
        private Unit intendedTargetUnit;
        private Vector3Int intendedTargetTile;

        public override bool IsUnitTargeted => true;

        public override string GetActionName()
        {
            var weapon = GetWeapon();
            string weaponName = weapon != null ? weapon.itemName : "Unarmed";
            return $"Melee Strike - {weaponName}";
        }

        [HideInInspector]
        public WeaponSO activeWeapon;

        public WeaponSO GetWeapon()
        {
            if (activeWeapon != null)
                return activeWeapon;
            var equipment = GetComponent<UnitEquipment>();
            return equipment != null ? equipment.GetMainWeapon() : null;
        }

        private int GetMaxRange()
        {
            var weapon = GetWeapon();
            if (weapon != null)
            {
                return Mathf.Max(1, weapon.reachFeet / 5);
            }
            return 1;
        }

        public override List<Vector3Int> GetActionRangeGridPositions()
        {
            return new List<Vector3Int>();
        }

        private void Update()
        {
            if (!isActive || targetUnit == null)
                return;

            float rotateSpeed = 10f;
            Vector3 aimDir = (targetUnit.transform.position - transform.position).normalized;
            aimDir.y = 0f;

            if (aimDir.sqrMagnitude > 0.001f)
            {
                transform.forward = Vector3.Lerp(
                    transform.forward,
                    aimDir,
                    Time.deltaTime * rotateSpeed
                );
            }
        }

        private void HandleStrikeConnects()
        {
            PerformStrikeLogic();
        }

        private void HandleAnimationEnd()
        {
            ActionComplete();
        }

        private void ActionComplete()
        {
            if (!isActive)
                return;

            CancelInvoke(nameof(FallbackActionComplete));
            isActive = false;

            var visuals = unit.GetComponentInChildren<UnitVisuals>();
            if (visuals != null)
            {
                visuals.OnStrikeConnects -= HandleStrikeConnects;
                visuals.OnAnimationEnd -= HandleAnimationEnd;
            }
            onActionComplete?.Invoke();
        }

        private void FallbackActionComplete()
        {
            Debug.LogWarning(
                $"<color=orange>[FAILSAFE]</color> The Animator swallowed the ActionComplete event!"
            );
            ActionComplete();
        }

        public override void TakeAction(Vector3Int targetPosition, Action onActionComplete)
        {
            if (!CanExecuteAction())
            {
                onActionComplete?.Invoke();
                return;
            }

            if (
                ServiceLocator.TryGet<TargetLockService>(out var tls)
                && tls.IsActive
                && tls.CurrentTarget != null
            )
            {
                targetUnit = tls.CurrentTarget;
            }
            else
            {
                targetUnit = ServiceLocator.Get<GridSystem>().GetUnitAt(targetPosition);
            }

            if (targetUnit == null)
            {
                onActionComplete?.Invoke();
                return;
            }

            intendedTargetUnit = targetUnit;
            intendedTargetTile = targetPosition;
            this.onActionComplete = onActionComplete;
            isActive = true;

            var visuals = unit.GetComponentInChildren<UnitVisuals>();
            if (visuals != null)
            {
                var weapon = GetWeapon();
                if (weapon != null)
                    visuals.SetWeaponType(weapon.weaponAnimType);

                visuals.OnStrikeConnects += HandleStrikeConnects;
                visuals.OnAnimationEnd += HandleAnimationEnd;
                visuals.TriggerMeleeAttack();

                Invoke(nameof(FallbackActionComplete), 2.0f);
            }
            else
            {
                PerformStrikeLogic();
                ActionComplete();
            }
        }

        private void PerformStrikeLogic()
        {
            var stats = unit.GetStats();
            if (stats == null)
                return;

            if (intendedTargetUnit == null)
            {
                Debug.Log(
                    $"<color=grey>[GUESS]</color> {unit.name} resolves an attack on {intendedTargetTile} but the intended target was destroyed (miss)."
                );
                unit.IncrementAttacksThisTurn();
                BreakStealth();
                return;
            }

            int driftDistance = PF2E_Core.GetChebyshevDistance3D(
                intendedTargetTile,
                intendedTargetUnit.CurrentLayeredPosition
            );

            var targetConditions = intendedTargetUnit.GetComponent<UnitConditions>();
            bool isTargetDead = targetConditions != null && targetConditions.IsDead();

            if (driftDistance > 1 || isTargetDead)
            {
                Debug.Log(
                    $"<color=grey>[GUESS]</color> {unit.name} resolves an attack on {intendedTargetTile} but the intended target is no longer there (miss)."
                );
                unit.IncrementAttacksThisTurn();
                BreakStealth();
                return;
            }

            targetUnit = intendedTargetUnit;

            UnitStealth targetStealth = targetUnit.GetComponent<UnitStealth>();
            if (targetStealth != null)
            {
                DetectionState targetState = targetStealth.GetDetectionState(unit);
                if (targetState == DetectionState.Unnoticed)
                {
                    unit.IncrementAttacksThisTurn();
                    BreakStealth();
                    return;
                }

                if (targetState != DetectionState.Undetected)
                {
                    int flatCheckDC = targetStealth.RequiresFlatCheckToTarget(unit);
                    if (flatCheckDC > 0)
                    {
                        int flatRoll = UnityEngine.Random.Range(1, 21);
                        if (flatRoll < flatCheckDC)
                        {
                            Debug.Log(
                                $"<color=grey>[SENSES]</color> Strike missed! Failed DC {flatCheckDC} flat check (Rolled {flatRoll})."
                            );
                            unit.IncrementAttacksThisTurn();
                            BreakStealth();
                            return;
                        }
                    }
                }
            }

            var weapon = GetWeapon();
            if (weapon == null)
                return;

            bool isAgileWeapon = weapon.HasTrait(WeaponTrait.Agile);
            bool isFinesseWeapon = weapon.HasTrait(WeaponTrait.Finesse);

            int level = stats.level;
            int strengthMod = unit.GetAbilityModifier(AbilityScore.STR);
            int dexMod = unit.GetAbilityModifier(AbilityScore.DEX);
            int attackStatMod = isFinesseWeapon ? Mathf.Max(strengthMod, dexMod) : strengthMod;
            AbilityScore attackStat =
                (isFinesseWeapon && dexMod > strengthMod) ? AbilityScore.DEX : AbilityScore.STR;

            Proficiency weaponProf = Proficiency.Trained;
            int mapPenalty = 0;
            int attacksMade = unit.AttacksThisTurn;

            if (attacksMade == 1)
                mapPenalty = isAgileWeapon ? -4 : -5;
            else if (attacksMade >= 2)
                mapPenalty = isAgileWeapon ? -8 : -10;

            int attackBonus = PF2E_Core.CalculateAttackRollModifier(
                unit,
                attackStat,
                attackStatMod,
                level,
                weaponProf,
                AttackType.Melee,
                mapPenalty
            );

            ArmorClassBreakdown acBreakdown = targetUnit.GetArmorClassBreakdown(
                unit,
                AttackType.Melee
            );
            int baseAC = acBreakdown.totalAC;

            int coverBonus = LineOfSightUtility.GetCoverBonus(
                unit.CurrentLayeredPosition,
                targetUnit.CurrentLayeredPosition
            );

            if (coverBonus == -1)
            {
                Debug.Log("<color=red>Attack aborted!</color> Target became completely blocked.");
                return;
            }

            CombatLogUtility.LogDefenseStage(targetUnit, acBreakdown, coverBonus);
            int finalAC = baseAC + coverBonus;

            unit.IncrementAttacksThisTurn();
            int d20 = UnityEngine.Random.Range(1, 21);

            CombatLogUtility.LogAttackStage(unit, this, d20, attackBonus, 0);
            Degree result = PF2E_Core.CheckResult(d20, attackBonus, finalAC);
            CombatLogUtility.LogResult(result);

            if (result == Degree.Success || result == Degree.CriticalSuccess)
            {
                int weaponDiceRoll = 0;
                for (int i = 0; i < weapon.damageDice.count; i++)
                {
                    weaponDiceRoll += UnityEngine.Random.Range(1, weapon.damageDice.sides + 1);
                }

                int damageModifiers = strengthMod;
                var myConditions = unit.GetComponent<UnitConditions>();
                if (myConditions != null && myConditions.HasCondition(ConditionType.Enfeebled))
                {
                    damageModifiers -= myConditions.GetConditionValue(ConditionType.Enfeebled);
                }

                CombatLogUtility.LogDamageStage(
                    targetUnit,
                    weaponDiceRoll,
                    damageModifiers,
                    weapon.damageType,
                    result == Degree.CriticalSuccess
                );

                int finalDamage = (weaponDiceRoll + damageModifiers);
                if (result == Degree.CriticalSuccess)
                    finalDamage *= 2;
                if (finalDamage < 1)
                    finalDamage = 1;

                var targetHealth = targetUnit.GetComponent<IDamageable>();
                if (targetHealth != null)
                {
                    targetHealth.ApplyDamage(
                        unit,
                        finalDamage,
                        weapon.damageType,
                        result == Degree.CriticalSuccess
                    );
                }
            }
            else
            {
                var targetVisuals = targetUnit.GetComponentInChildren<UnitVisuals>();
                if (targetVisuals != null)
                    targetVisuals.TriggerDodge();
            }

            BreakStealth();
        }

        private void BreakStealth()
        {
            StealthResolver.OnNoiseGenerated(unit);
            StealthResolver.BreakStealthAfterAttack(unit);
        }

        public override bool IsValidActionGridPosition(Vector3Int targetPosition)
        {
            return GetValidActionGridPositions().Contains(targetPosition);
        }

        public override List<Vector3Int> GetValidActionGridPositions()
        {
            List<Vector3Int> validPositions = new List<Vector3Int>();
            Vector3Int attackerPos = Attacker.CurrentLayeredPosition;
            int reach = GetMaxRange();

            foreach (Unit target in UnitManager.AllUnits)
            {
                if (
                    target == null
                    || target == Attacker
                    || target.GetFaction() == Attacker.GetFaction()
                )
                    continue;

                Vector3Int targetPos = target.CurrentLayeredPosition;

                int dist = PF2E_Core.GetChebyshevDistance3D(attackerPos, targetPos);
                if (dist > reach)
                    continue;

                VisibilityResult visResult = LineOfSightUtility.Evaluate(attackerPos, targetPos);
                if (!visResult.HasLineOfSight)
                    continue;

                var targetStealth = target.GetComponent<UnitStealth>();
                if (
                    targetStealth != null
                    && targetStealth.GetDetectionState(Attacker) == DetectionState.Unnoticed
                )
                    continue;

                validPositions.Add(targetPos);
            }

            return validPositions;
        }
    }
}
