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
        private GridPosition intendedTargetTile;
        private float stateTimer;
        private State state;

        private enum State
        {
            Swinging,
            Cooloff,
        }

        public override string GetActionName()
        {
            var weapon = GetWeapon();
            string weaponName = weapon != null ? weapon.itemName : "Unarmed";
            return $"Melee Strike - {weaponName}";
        }

        /// <summary>
        /// The specific weapon this strike action uses.
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

        // Defines the boundaries the cursor can move in
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

                    if (!ServiceLocator.Get<GridSystem>().IsValidGridPosition(testPos))
                        continue;

                    // TODO: fix distance calculation. For now this works fine when range is 1 tile.
                    int distance = Mathf.Max(Mathf.Abs(x), Mathf.Abs(z));
                    if (distance <= range)
                    {
                        rangePositions.Add(testPos);
                    }
                }
            }
            return rangePositions;
        }

        private void Update()
        {
            if (!isActive)
                return;

            stateTimer -= Time.deltaTime;

            switch (state)
            {
                // TODO: fix
                case State.Swinging:
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
                case State.Swinging:
                    state = State.Cooloff;
                    stateTimer = 0.5f;
                    PerformStrikeLogic();
                    break;
                case State.Cooloff:
                    isActive = false;
                    onActionComplete?.Invoke();
                    break;
            }
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
                Debug.LogError("No unit found at target position!");
                onActionComplete?.Invoke();
                return;
            }

            intendedTargetUnit = targetUnit;
            intendedTargetTile = gridPosition;
            this.onActionComplete = onActionComplete;
            state = State.Swinging;
            stateTimer = 0.7f;
            isActive = true;
        }

        private void PerformStrikeLogic()
        {
            var stats = unit.GetStats();
            if (stats == null || targetUnit == null)
                return;

            // Attacks resolve against what is actually on
            // the selected tile at resolution time (unit may have moved/died/replaced).
            GridSystem grid = ServiceLocator.Get<GridSystem>();
            Unit actualTarget = grid.GetUnitAt(intendedTargetTile);
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

            // Senses and stealth check.
            UnitStealth targetStealth = targetUnit.GetComponent<UnitStealth>();
            if (targetStealth != null)
            {
                DetectionState targetState = targetStealth.GetDetectionState(unit);
                if (STEALTH_DEBUG && unit != null)
                    Debug.Log(
                        $"<color=red>[STEALTH]</color> {unit.name} resolves Melee vs {targetUnit.name}: targetDetectionState={targetState} intendedTile={intendedTargetTile}"
                    );

                // Unnoticed: disallow targeting (exploration-only state).
                if (targetState == DetectionState.Unnoticed)
                {
                    Debug.Log(
                        $"<color=grey>[SENSES]</color> {unit.name} cannot target {targetUnit.name} while it is Unnoticed."
                    );
                    unit.IncrementAttacksThisTurn();
                    BreakStealth();
                    return;
                }

                // Undetected: guess tile mode.
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
                                $"<color=grey>[SENSES]</color> Strike missed! Failed DC {flatCheckDC} flat check to see the target (Rolled {flatRoll})."
                            );
                            unit.IncrementAttacksThisTurn();
                            BreakStealth();
                            return;
                        }
                        Debug.Log(
                            $"<color=green>[SENSES]</color> Passed vision flat check! (Rolled {flatRoll} vs DC {flatCheckDC})"
                        );
                    }
                }
            }

            var weapon = GetWeapon();
            if (weapon == null)
                return;

            bool isAgileWeapon = weapon.HasTrait(WeaponTrait.Agile);
            bool isFinesseWeapon = weapon.HasTrait(WeaponTrait.Finesse);

            // Base math
            int level = stats.level;

            // Finesse weapons can use Dexterity instead of Strength for attack rolls!
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

            // Attack modifier calculation with debug tracking
            int attackBonus = PF2E_Core.CalculateAttackRollModifier(
                unit,
                attackStat,
                attackStatMod,
                level,
                weaponProf,
                AttackType.Melee,
                mapPenalty
            );

            // what the bonus WOULD be without conditions
            int rawBonus =
                PF2E_Core.CalculateModifier(level, weaponProf, attackStatMod) + mapPenalty;
            int appliedPenalty = attackBonus - rawBonus;

            string atkDebug =
                $"<color=yellow>[ATTACK MATH]</color> {unit.name} | Raw Math: {rawBonus - mapPenalty} | MAP: {mapPenalty}";
            if (appliedPenalty < 0)
                atkDebug += $" | <color=red>Condition Penalties: {appliedPenalty}</color>";
            atkDebug += $" ==> <b>Final Attack Bonus: +{attackBonus}</b>";
            Debug.Log(atkDebug);

            // Defense breakdown
            ArmorClassBreakdown acBreakdown = targetUnit.GetArmorClassBreakdown(
                unit,
                AttackType.Melee
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

            // Cover Logic
            int coverBonus = LineOfSightUtility.GetCoverBonus(
                unit.CurrentGridPosition,
                targetUnit.CurrentGridPosition
            );

            if (coverBonus == -1)
            {
                Debug.Log("<color=red>Attack aborted!</color> Target became completely blocked.");
                return;
            }

            int finalAC = baseAC + coverBonus;

            if (coverBonus > 0)
            {
                Debug.Log(
                    $"<color=cyan>[COVER]</color> Target has {(coverBonus == 2 ? "Standard" : "Lesser")} Cover! AC increased by +{coverBonus} (New Final AC: {finalAC})"
                );
            }

            // Roll and resolve
            unit.IncrementAttacksThisTurn();

            int d20 = UnityEngine.Random.Range(1, 21);
            Degree result = PF2E_Core.CheckResult(d20, attackBonus, finalAC);

            Debug.Log(
                $"<b>[STRIKE RESULT]</b> Rolled {d20} + {attackBonus} = {d20 + attackBonus} vs AC {finalAC} -> <color={(result == Degree.Success || result == Degree.CriticalSuccess ? "green" : "red")}>{result}</color>"
            );

            if (result == Degree.Success || result == Degree.CriticalSuccess)
            {
                int weaponDiceRoll = 0;
                for (int i = 0; i < weapon.damageDice.count; i++)
                {
                    weaponDiceRoll += UnityEngine.Random.Range(1, weapon.damageDice.sides + 1);
                }

                // In PF2e, melee damage is ALWAYS strength, even if finesse uses dex for the attack roll (unless it has a specific trait like Thief Racket)
                int damage = weaponDiceRoll + strengthMod;

                // ENFEEBLED DAMAGE DEBUFF
                // Enfeebled reduces strength-based damage rolls too!
                var myConditions = unit.GetComponent<UnitConditions>();
                if (myConditions != null && myConditions.HasCondition(ConditionType.Enfeebled))
                {
                    int enfValue = myConditions.GetConditionValue(ConditionType.Enfeebled);
                    damage -= enfValue;
                    if (damage < 1)
                        damage = 1; // Minimum 1 damage on a hit
                    Debug.Log(
                        $"<color=orange>[CONDITION]</color> Enfeebled {enfValue} reduced damage roll!"
                    );
                }

                if (result == Degree.CriticalSuccess)
                {
                    damage *= 2;
                    Debug.Log("<b><color=red>CRITICAL HIT!</color></b>");
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
                    Debug.Log($"Dealt {damage} Damage to {targetUnit.name}!");
                }
            }
            else
            {
                Debug.Log("Miss!");
            }

            // BREAK STEALTH
            BreakStealth();
        }

        private void BreakStealth()
        {
            // Attacks are noisy and can reveal you based on precise senses.
            StealthResolver.OnNoiseGenerated(unit);
            StealthResolver.BreakStealthAfterAttack(unit);
        }

        public override bool IsValidActionGridPosition(GridPosition gridPosition)
        {
            return GetValidActionGridPositions().Contains(gridPosition);
        }

        public override List<GridPosition> GetValidActionGridPositions()
        {
            int range = GetMaxRange();
            List<GridPosition> validGridPositionList = new List<GridPosition>();
            GridPosition unitGridPosition = unit.CurrentGridPosition;

            // Debug.Log($"<color=yellow>--- STARTING TARGET SEARCH (Range: {range}) ---</color>");

            for (int x = -range; x <= range; x++)
            {
                for (int z = -range; z <= range; z++)
                {
                    GridPosition testGridPosition = new GridPosition(
                        unitGridPosition.x + x,
                        unitGridPosition.z + z
                    );

                    // Grid Boundary Check
                    if (!ServiceLocator.Get<GridSystem>().IsValidGridPosition(testGridPosition))
                        continue;

                    // Self Check
                    if (unitGridPosition == testGridPosition)
                        continue;

                    // Chebyshev Distance Check
                    int distance = Mathf.Max(Mathf.Abs(x), Mathf.Abs(z));
                    if (distance > range)
                        continue;

                    // Unit Check
                    Unit targetUnit = ServiceLocator.Get<GridSystem>().GetUnitAt(testGridPosition);
                    if (targetUnit == null)
                        continue; // Empty tile

                    // Faction Check
                    if (targetUnit.GetFaction() == unit.GetFaction())
                    {
                        // Debug.Log($"Tile {testGridPosition} rejected: Friendly unit in the way.");
                        continue;
                    }

                    // Undetected enemies can still be targeted via guess-tile mode.
                    // Unnoticed (exploration-only) is still not targetable.
                    var targetStealth = targetUnit.GetComponent<UnitStealth>();
                    if (
                        targetStealth != null
                        && targetStealth.GetDetectionState(unit) == DetectionState.Unnoticed
                    )
                        continue;

                    // Line of sight check (cover)
                    // Debug.Log($"Found Enemy at {testGridPosition}! Running Cover check...");

                    int coverBonus = LineOfSightUtility.GetCoverBonus(
                        unitGridPosition,
                        testGridPosition
                    );

                    // Cover / Line of Effect Check
                    if (coverBonus == -1)
                    {
                        // Debug.Log($"<color=red>Tile {testGridPosition} rejected: NO LINE OF EFFECT.</color>");
                        continue;
                    }

                    // Valid target
                    // Debug.Log($"<color=green>Tile {testGridPosition} is a VALID target! (Cover Bonus: +{coverBonus} AC)</color>");
                    validGridPositionList.Add(testGridPosition);
                }
            }

            // Debug.Log($"<color=yellow>--- END TARGET SEARCH. Found {validGridPositionList.Count} valid targets. ---</color>");
            return validGridPositionList;
        }
    }
}
