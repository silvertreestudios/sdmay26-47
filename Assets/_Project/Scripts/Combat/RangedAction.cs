using System;
using System.Collections.Generic;
using PathfinderTactics.Characters;
using PathfinderTactics.Core;
using PathfinderTactics.Grid;
using PathfinderTactics.Items;
using UnityEngine;

namespace PathfinderTactics.Actions
{
    public class RangedAction : BaseAction
    {
        private Unit targetUnit;
        private float stateTimer;
        private State state;

        private enum State
        {
            Aiming,
            Shooting,
            Cooloff,
        }

        public override string GetActionName()
        {
            var weapon = GetWeapon();
            string weaponName = weapon != null ? weapon.itemName : "Ranged";
            return $"Ranged Strike — {weaponName}";
        }

        /// <summary>
        /// The specific weapon this ranged action uses.
        /// Set by UnitEquipment.ConfigureStrikeActions(). If null, falls back to equipment.
        /// </summary>
        [HideInInspector]
        public WeaponSO activeWeapon;

        /// <summary>
        /// Returns the weapon this action should use for all calculations.
        /// </summary>
        public WeaponSO GetWeapon()
        {
            if (activeWeapon != null)
                return activeWeapon;
            var equipment = unit.GetComponent<UnitEquipment>();
            if (equipment == null)
                return null;
            var weapon = equipment.GetMainWeapon();
            return (weapon != null && weapon.IsRangedWeapon()) ? weapon : null;
        }

        private int GetMaxRange()
        {
            var weapon = GetWeapon();
            if (weapon != null && weapon.IsRangedWeapon())
            {
                return Mathf.Max(1, weapon.rangeIncrementFeet / 5);
            }
            return 12; // fallback 60ft
        }

        public override void TakeAction(GridPosition gridPosition, Action onActionComplete)
        {
            if (!CanExecuteAction())
            {
                onActionComplete?.Invoke();
                return;
            }
            targetUnit = ServiceLocator.Get<GridSystem>().GetUnitAt(gridPosition);

            if (targetUnit == null)
            {
                onActionComplete?.Invoke();
                return;
            }

            this.onActionComplete = onActionComplete;
            state = State.Aiming;
            stateTimer = 0.5f;
            isActive = true;
        }

        private void Update()
        {
            if (!isActive)
                return;

            stateTimer -= Time.deltaTime;

            switch (state)
            {
                case State.Aiming:
                    if (targetUnit != null)
                    {
                        float rotateSpeed = 10f;
                        Vector3 aimDir = (
                            targetUnit.transform.position - transform.position
                        ).normalized;
                        transform.forward = Vector3.Lerp(
                            transform.forward,
                            aimDir,
                            Time.deltaTime * rotateSpeed
                        );
                    }
                    break;
                case State.Shooting:
                    break;
                case State.Cooloff:
                    break;
            }

            if (stateTimer <= 0f)
            {
                NextState();
            }
        }

        private void NextState()
        {
            switch (state)
            {
                case State.Aiming:
                    state = State.Shooting;
                    stateTimer = 0.1f;
                    // TODO: add arrow projectile here later
                    break;
                case State.Shooting:
                    state = State.Cooloff;
                    stateTimer = 0.5f;
                    PerformShootLogic();
                    break;
                case State.Cooloff:
                    isActive = false;
                    onActionComplete?.Invoke();
                    break;
            }
        }

        private void PerformShootLogic()
        {
            var weapon = GetWeapon();
            if (weapon == null || !weapon.IsRangedWeapon())
            {
                Debug.LogWarning("Tried to shoot without a ranged weapon equipped!");
                return;
            }

            var stats = unit.GetStats();
            if (stats == null)
                return;

            int level = stats.level;
            int dexMod = unit.GetAbilityModifier(AbilityScore.DEX);
            Proficiency weaponProf = Proficiency.Trained;

            int mapPenalty = 0;
            int attacksMade = unit.AttacksThisTurn;
            bool isAgileWeapon = weapon.HasTrait(WeaponTrait.Agile);

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
                mapPenalty
            );

            // Check for Stealth/Vision
            var targetStealth = targetUnit.GetComponent<UnitStealth>();
            if (targetStealth != null)
            {
                int flatCheckDC = targetStealth.RequiresFlatCheckToTarget(unit);
                if (flatCheckDC > 0)
                {
                    int flatRoll = UnityEngine.Random.Range(1, 21);
                    if (flatRoll < flatCheckDC)
                    {
                        Debug.Log(
                            $"<color=grey>Shot missed! Failed DC {flatCheckDC} flat check to see the target (Rolled {flatRoll}).</color>"
                        );
                        unit.IncrementAttacksThisTurn(); // The attack is wasted!

                        // Break your own stealth
                        BreakStealth();
                        return;
                    }
                    Debug.Log(
                        $"<color=green>Passed vision flat check! (Rolled {flatRoll} vs DC {flatCheckDC})</color>"
                    );
                }
            }

            int baseAC = targetUnit.GetArmorClass(AttackType.Ranged);

            int coverBonus = LineOfSightUtility.GetCoverBonus(
                unit.CurrentGridPosition,
                targetUnit.CurrentGridPosition
            );

            if (coverBonus == -1)
            {
                Debug.Log("Shot aborted! Target became completely blocked.");
                return;
            }

            int finalAC = baseAC + coverBonus;

            if (coverBonus > 0)
            {
                Debug.Log(
                    $"<color=cyan>[COVER]</color> Target has {(coverBonus == 2 ? "Standard" : "Lesser")} Cover! AC increased by +{coverBonus} (Base: {baseAC} -> Final: {finalAC})"
                );
            }

            unit.IncrementAttacksThisTurn();

            int d20 = UnityEngine.Random.Range(1, 21);
            Degree result = PF2E_Core.CheckResult(d20, attackBonus, finalAC);

            Debug.Log(
                $"[Shoot] Rolled {d20} + {attackBonus} (MAP: {mapPenalty}) vs AC {finalAC} -> {result}"
            );

            if (result == Degree.Success || result == Degree.CriticalSuccess)
            {
                int weaponDiceRoll = 0;
                for (int i = 0; i < weapon.damageDice.count; i++)
                {
                    weaponDiceRoll += UnityEngine.Random.Range(1, weapon.damageDice.sides + 1);
                }

                // Note: Ranged weapons don't add Strength to damage unless they have the Propulsive trait (half Str) or Thrown trait (full Str).
                // For now, simplifying to just the dice logic.
                int damage = weaponDiceRoll;

                if (result == Degree.CriticalSuccess)
                {
                    damage *= 2;
                    Debug.Log("CRITICAL HIT!");
                }

                var targetHealth = targetUnit.GetComponent<IDamageable>();
                if (targetHealth != null)
                {
                    targetHealth.ApplyDamage(unit, damage, result == Degree.CriticalSuccess);
                    Debug.Log($"Shot dealt {damage} Damage to {targetUnit.name}!");
                }
            }
            else
            {
                Debug.Log("Miss!");
            }

            BreakStealth();
        }

        private void BreakStealth()
        {
            var myStealth = unit.GetComponent<UnitStealth>();
            if (myStealth == null)
                return;

            // Firing a weapon reveals you to all enemies!
            foreach (
                Unit enemy in ServiceLocator.Get<GridSystem>().GetAllEnemies(unit.GetFaction())
            )
            {
                myStealth.SetDetectionState(enemy, DetectionState.Observed);
            }
        }

        public override List<GridPosition> GetActionRangeGridPositions()
        {
            int range = GetMaxRange();
            List<GridPosition> rangePositions = new List<GridPosition>();
            GridPosition unitGridPos = unit.CurrentGridPosition;

            for (int x = -range; x <= range; x++)
            {
                for (int z = -range; z <= range; z++)
                {
                    GridPosition testPos = new GridPosition(unitGridPos.x + x, unitGridPos.z + z);

                    // Keep it on the map
                    if (!ServiceLocator.Get<GridSystem>().IsValidGridPosition(testPos))
                        continue;

                    // Chebyshev distance
                    int distance = Mathf.Max(Mathf.Abs(x), Mathf.Abs(z));
                    if (distance <= range)
                    {
                        rangePositions.Add(testPos);
                    }
                }
            }
            return rangePositions;
        }

        public override List<GridPosition> GetValidActionGridPositions()
        {
            int range = GetMaxRange();
            List<GridPosition> validGridPositionList = new List<GridPosition>();
            GridPosition unitGridPosition = unit.CurrentGridPosition;

            for (int x = -range; x <= range; x++)
            {
                for (int z = -range; z <= range; z++)
                {
                    GridPosition testGridPosition = new GridPosition(
                        unitGridPosition.x + x,
                        unitGridPosition.z + z
                    );

                    if (!ServiceLocator.Get<GridSystem>().IsValidGridPosition(testGridPosition))
                        continue;
                    if (unitGridPosition == testGridPosition)
                        continue;

                    int distance = Mathf.Max(Mathf.Abs(x), Mathf.Abs(z));
                    if (distance > range)
                        continue;

                    Unit targetUnit = ServiceLocator.Get<GridSystem>().GetUnitAt(testGridPosition);
                    if (targetUnit == null)
                        continue;
                    if (targetUnit.GetFaction() == unit.GetFaction())
                        continue;

                    int coverBonus = LineOfSightUtility.GetCoverBonus(
                        unitGridPosition,
                        testGridPosition
                    );
                    if (coverBonus == -1)
                        continue;

                    validGridPositionList.Add(testGridPosition);
                }
            }
            return validGridPositionList;
        }

        public override bool IsValidActionGridPosition(GridPosition gridPosition) =>
            GetValidActionGridPositions().Contains(gridPosition);
    }
}
