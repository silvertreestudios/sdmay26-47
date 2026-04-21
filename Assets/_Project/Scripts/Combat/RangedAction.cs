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
    public class RangedAction : BaseAction
    {
        private const bool STEALTH_DEBUG = true;

        private Unit targetUnit;
        private Unit intendedTargetUnit;
        private Vector3Int intendedTargetTile;

        public override bool IsUnitTargeted => true;

        public override string GetActionName()
        {
            var weapon = GetWeapon();
            string weaponName = weapon != null ? weapon.itemName : "Ranged";
            return $"Ranged Strike - {weaponName}";
        }

        public override DamageType GetPrimaryDamageType()
        {
            var weapon = GetWeapon();
            return weapon != null ? weapon.damageType : DamageType.Untyped;
        }

        [HideInInspector]
        public WeaponSO activeWeapon;

        public WeaponSO GetWeapon()
        {
            if (activeWeapon != null)
                return activeWeapon;
            var equipment = GetComponent<UnitEquipment>();
            if (equipment == null)
                return null;
            var weapon = equipment.GetMainWeapon();
            return (weapon != null && weapon.IsRangedWeapon()) ? weapon : null;
        }

        public float GetDistanceFeet(Vector3Int a, Vector3Int b)
        {
            return Vector3.Distance(new Vector3(a.x, a.y, a.z), new Vector3(b.x, b.y, b.z)) * 5.0f;
        }

        public int GetRangePenalty(
            float distFeet,
            int rangeIncrement,
            out int incrementIndex,
            out bool isInvalid
        )
        {
            if (rangeIncrement <= 0)
            {
                incrementIndex = 1;
                isInvalid = false;
                return 0;
            }

            incrementIndex = Mathf.CeilToInt(distFeet / rangeIncrement);
            if (incrementIndex == 0)
                incrementIndex = 1;

            isInvalid = incrementIndex > 6;
            return Mathf.Max(-10, (incrementIndex - 1) * -2);
        }

        private int GetMaxRange()
        {
            var weapon = GetWeapon();
            if (weapon != null && weapon.IsRangedWeapon())
            {
                return (weapon.rangeIncrementFeet * 6) / 5;
            }
            return 0;
        }

        public override void TakeAction(Vector3Int targetPosition, Action onActionComplete)
        {
            // Validation is now handled by UnitActionSystem before spending AP.

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

                visuals.OnShoot += HandleShoot;
                visuals.OnAnimationEnd += HandleAnimationEnd;
                visuals.TriggerRangedAttack();

                Invoke(nameof(FallbackActionComplete), 2.0f);
            }
            else
            {
                PerformShootLogic();
                ActionComplete();
            }
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

        private void HandleShoot()
        {
            PerformShootLogic();
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
                visuals.OnShoot -= HandleShoot;
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

        private void PerformShootLogic()
        {
            var weapon = GetWeapon();
            if (weapon == null || !weapon.IsRangedWeapon())
                return;

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

            int level = stats.level;
            int dexMod = unit.GetAbilityModifier(AbilityScore.DEX);
            Proficiency weaponProf = Proficiency.Trained;

            float distFeet = GetDistanceFeet(
                unit.CurrentLayeredPosition,
                targetUnit.CurrentLayeredPosition
            );
            int rangePenalty = GetRangePenalty(
                distFeet,
                weapon.rangeIncrementFeet,
                out int incIndex,
                out bool isInvalid
            );

            if (isInvalid)
            {
                Debug.LogWarning(
                    $"<color=red>[RANGE]</color> Target {targetUnit.name} is beyond the 6-increment functional range. Attack aborted."
                );
                unit.IncrementAttacksThisTurn();
                BreakStealth();
                return;
            }

            int attacksMade = unit.AttacksThisTurn;
            bool isAgileWeapon = weapon.HasTrait(WeaponTrait.Agile);
            int mapPenalty = 0;
            if (attacksMade == 1)
                mapPenalty = isAgileWeapon ? -4 : -5;
            else if (attacksMade >= 2)
                mapPenalty = isAgileWeapon ? -8 : -10;

            int attackBonus = PF2E_Core.CalculateAttackRollModifier(
                unit,
                AbilityScore.DEX,
                dexMod,
                level,
                weaponProf,
                AttackType.Ranged,
                mapPenalty + rangePenalty
            );

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
                                $"<color=grey>Shot missed! Failed DC {flatCheckDC} flat check (Rolled {flatRoll}).</color>"
                            );
                            unit.IncrementAttacksThisTurn();
                            BreakStealth();
                            return;
                        }
                    }
                }
            }

            ArmorClassBreakdown acBreakdown = targetUnit.GetArmorClassBreakdown(
                unit,
                AttackType.Ranged
            );
            int baseAC = acBreakdown.totalAC;

            int coverBonus = LineOfSightUtility.GetCoverBonus(
                unit.CurrentLayeredPosition,
                targetUnit.CurrentLayeredPosition
            );

            if (coverBonus == -1)
            {
                Debug.Log("Shot aborted! Target became completely blocked.");
                return;
            }

            CombatLogUtility.LogDefenseStage(targetUnit, acBreakdown, coverBonus);
            int finalAC = baseAC + coverBonus;

            unit.IncrementAttacksThisTurn();
            int d20 = UnityEngine.Random.Range(1, 21);
            CombatLogUtility.LogAttackStage(unit, this, d20, attackBonus, mapPenalty, rangePenalty);

            Degree result = PF2E_Core.CheckResult(
                d20,
                attackBonus + mapPenalty + rangePenalty,
                finalAC
            );
            CombatLogUtility.LogResult(result);

            if (result == Degree.Success || result == Degree.CriticalSuccess)
            {
                int weaponDiceRoll = 0;
                for (int i = 0; i < weapon.damageDice.count; i++)
                {
                    weaponDiceRoll += UnityEngine.Random.Range(1, weapon.damageDice.sides + 1);
                }

                CombatLogUtility.LogDamageStage(
                    targetUnit,
                    weaponDiceRoll,
                    0,
                    weapon.damageType,
                    result == Degree.CriticalSuccess
                );

                int finalDamage = weaponDiceRoll;
                if (result == Degree.CriticalSuccess)
                    finalDamage *= 2;

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

        public override List<Vector3Int> GetActionRangeGridPositions()
        {
            return new List<Vector3Int>();
        }

        public override List<Vector3Int> GetValidActionGridPositions()
        {
            var weapon = GetWeapon();
            if (weapon == null)
                return new List<Vector3Int>();

            List<Vector3Int> validPositions = new List<Vector3Int>();
            Vector3Int attackerPos = Attacker.CurrentLayeredPosition;
            float maxRangeFeet = weapon.rangeIncrementFeet * 6;

            foreach (Unit target in UnitManager.AllUnits)
            {
                if (
                    target == null
                    || target == Attacker
                    || target.GetFaction() == Attacker.GetFaction()
                )
                    continue;

                Vector3Int targetPos = target.CurrentLayeredPosition;

                float distFeet = GetDistanceFeet(attackerPos, targetPos);
                if (distFeet > maxRangeFeet)
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

        public override bool IsValidActionGridPosition(Vector3Int targetPosition) =>
            GetValidActionGridPositions().Contains(targetPosition);
    }
}
