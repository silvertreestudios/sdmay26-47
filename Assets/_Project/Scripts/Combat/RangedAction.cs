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
        private GridPosition intendedTargetTile;
        private float stateTimer;
        private State state;

        private enum State
        {
            Aiming,
            Shooting,
            Cooloff,
        }

        public override bool IsUnitTargeted => true;

        public override string GetActionName()
        {
            var weapon = GetWeapon();
            string weaponName = weapon != null ? weapon.itemName : "Ranged";
            return $"Ranged Strike - {weaponName}";
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
                targetUnit = ServiceLocator.Get<GridSystem>().GetUnitAt(gridPosition);
            }

            if (targetUnit == null)
            {
                onActionComplete?.Invoke();
                return;
            }

            intendedTargetUnit = targetUnit;
            intendedTargetTile = gridPosition;
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

            // Tile source of truth: resolve against what is actually on the
            // selected tile at resolution time.
            GridSystem grid = ServiceLocator.Get<GridSystem>();
            Unit actualTarget = grid.GetUnitAt(intendedTargetUnit.CurrentLayeredPosition);
            if (actualTarget == null || actualTarget != intendedTargetUnit)
            {
                Debug.Log(
                    $"<color=grey>[GUESS]</color> {unit.name} resolves an attack on {intendedTargetTile} but the intended target is no longer there (miss)."
                );
                unit.IncrementAttacksThisTurn();
                BreakStealth();
                return;
            }

            targetUnit = actualTarget;

            // Check for Stealth/Vision
            UnitStealth targetStealth = targetUnit.GetComponent<UnitStealth>();
            if (targetStealth != null)
            {
                DetectionState targetState = targetStealth.GetDetectionState(unit);
                if (STEALTH_DEBUG && unit != null)
                    Debug.Log(
                        $"<color=red>[STEALTH]</color> {unit.name} resolves Ranged vs {targetUnit.name}: targetDetectionState={targetState} intendedTile={intendedTargetTile}"
                    );

                // Unnoticed: exploration-only state, still not targetable.
                if (targetState == DetectionState.Unnoticed)
                {
                    Debug.Log(
                        $"<color=grey>[SENSES]</color> {unit.name} cannot target {targetUnit.name} while it is Unnoticed."
                    );
                    unit.IncrementAttacksThisTurn();
                    BreakStealth();
                    return;
                }

                // Undetected: guess-tile mode.
                if (targetState == DetectionState.Undetected)
                {
                    // Guess succeeds because we already verified the intended unit
                    // is still on the intended tile at resolution time.
                    // We still must skip the vision flat-check (handled below).
                }

                // Undetected guess mode replaces the vision flat-check entirely.
                if (targetState != DetectionState.Undetected)
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
            }

            // Defense breakdown
            ArmorClassBreakdown acBreakdown = targetUnit.GetArmorClassBreakdown(
                unit,
                AttackType.Ranged
            );
            int baseAC = acBreakdown.totalAC;

            string defDebug =
                $"<color=yellow>[DEFENSE MATH]</color> {targetUnit.name} | Base AC: {acBreakdown.baseAC}";

            if (acBreakdown.statusPenalty != 0)
                defDebug +=
                    $" | <color=red>Status: {acBreakdown.statusPenaltySources} ({acBreakdown.statusPenalty})</color>";

            if (acBreakdown.circumstanceMod != 0)
                defDebug +=
                    $" | <color=orange>Circumstance: {acBreakdown.circumstanceModSources}</color>";

            defDebug += $" ==> <b>Calculated AC: {baseAC}</b>";
            Debug.Log(defDebug);

            int coverBonus = LineOfSightUtility.GetCoverBonus(
                unit.CurrentLayeredPosition,
                targetUnit.CurrentLayeredPosition
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
                    targetHealth.ApplyDamage(
                        unit,
                        damage,
                        weapon.damageType,
                        result == Degree.CriticalSuccess
                    );
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
            // Attacks are noisy and can reveal you based on precise senses.
            StealthResolver.OnNoiseGenerated(unit);
            StealthResolver.BreakStealthAfterAttack(unit);
        }

        public override List<GridPosition> GetActionRangeGridPositions()
        {
            int range = GetMaxRange();
            List<GridPosition> rangePositions = new List<GridPosition>();
            GridSystem grid = ServiceLocator.Get<GridSystem>();
            Vector3Int unitPos = unit.CurrentLayeredPosition;

            for (int x = -range; x <= range; x++)
            {
                for (int z = -range; z <= range; z++)
                {
                    Vector2Int colKey = new Vector2Int(unitPos.x + x, unitPos.z + z);
                    List<GridNode> column = grid.GetColumn(colKey);
                    if (column == null || column.Count == 0)
                        continue;

                    foreach (GridNode node in column)
                    {
                        if (PF2E_Core.GetPF2eDistance3D(unitPos, node.Coordinates) <= range)
                        {
                            rangePositions.Add(new GridPosition(colKey.x, colKey.y));
                            break;
                        }
                    }
                }
            }
            return rangePositions;
        }

        public override List<GridPosition> GetValidActionGridPositions()
        {
            int range = GetMaxRange();
            List<GridPosition> validGridPositionList = new List<GridPosition>();
            HashSet<Vector2Int> addedColumns = new HashSet<Vector2Int>();
            GridSystem grid = ServiceLocator.Get<GridSystem>();
            Vector3Int unitPos = unit.CurrentLayeredPosition;

            for (int x = -range; x <= range; x++)
            {
                for (int z = -range; z <= range; z++)
                {
                    Vector2Int colKey = new Vector2Int(unitPos.x + x, unitPos.z + z);
                    List<GridNode> column = grid.GetColumn(colKey);
                    if (column == null || column.Count == 0)
                        continue;

                    foreach (GridNode node in column)
                    {
                        Vector3Int testPos = node.Coordinates;
                        if (testPos == unitPos)
                            continue;

                        if (PF2E_Core.GetPF2eDistance3D(unitPos, testPos) > range)
                            continue;

                        Unit target = grid.GetUnitAt(testPos);
                        if (target == null)
                            continue;
                        if (target.GetFaction() == unit.GetFaction())
                            continue;

                        var targetStealth = target.GetComponent<UnitStealth>();
                        if (
                            targetStealth != null
                            && targetStealth.GetDetectionState(unit) == DetectionState.Unnoticed
                        )
                            continue;

                        if (!LineOfSightUtility.HasLineOfEffect(unitPos, testPos))
                            continue;

                        if (!LineOfSightUtility.Evaluate(unitPos, testPos).HasLineOfSight)
                            continue;

                        if (addedColumns.Add(colKey))
                            validGridPositionList.Add(new GridPosition(testPos.x, testPos.z));
                    }
                }
            }
            return validGridPositionList;
        }

        public override bool IsValidActionGridPosition(GridPosition gridPosition) =>
            GetValidActionGridPositions().Contains(gridPosition);
    }
}
