using System;
using System.Collections.Generic;
using PathfinderTactics.Characters;
using PathfinderTactics.Core;
using PathfinderTactics.Grid;
using UnityEngine;

namespace PathfinderTactics.Actions
{
    public class MeleeAction : BaseAction
    {
        private Unit targetUnit;
        private float stateTimer;
        private State state;

        private enum State
        {
            Swinging,
            Cooloff,
        }

        public override string GetActionName() => "Strike";

        // TODO: These stats should change based on the weapon equipped. For now we can just hardcode them.
        [Header("Weapon Stats")]
        [SerializeField]
        private int maxRange = 1; // 1 tile = 5ft reach.

        [SerializeField]
        private bool isAgileWeapon = false;

        // Defines the boundaries the cursor can move in
        public override List<GridPosition> GetActionRangeGridPositions()
        {
            List<GridPosition> rangePositions = new List<GridPosition>();
            GridPosition unitGridPos = unit.CurrentGridPosition;

            for (int x = -maxRange; x <= maxRange; x++)
            {
                for (int z = -maxRange; z <= maxRange; z++)
                {
                    GridPosition testPos = new GridPosition(unitGridPos.x + x, unitGridPos.z + z);

                    if (!GridSystem.Instance.IsValidGridPosition(testPos))
                        continue;

                    // TODO: fix distance calculation. For now this works fine when range is 1 tile.
                    int distance = Mathf.Max(Mathf.Abs(x), Mathf.Abs(z));
                    if (distance <= maxRange)
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
            targetUnit = GridSystem.Instance.GetUnitAt(gridPosition);

            if (targetUnit == null)
            {
                Debug.LogError("No unit found at target position!");
                onActionComplete?.Invoke();
                return;
            }

            this.onActionComplete = onActionComplete;
            state = State.Swinging;
            stateTimer = 0.7f;
            isActive = true;
        }


        private void PerformStrikeLogic()
        {
            var stats = unit.GetStats();
            if (stats == null || targetUnit == null) return;

            // Senses and stealth check.
            var targetConditions = targetUnit.GetComponent<UnitConditions>();
            if (targetConditions != null)
            {
                int flatCheckDC = targetConditions.RequiresFlatCheckToTarget(unit);
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

            // Base math
            int level = 1;
            int strengthMod = (stats.strength - 10) / 2;
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
                AbilityScore.STR,
                strengthMod,
                level,
                weaponProf,
                AttackType.Melee,
                mapPenalty
            );

            // what the bonus WOULD be without conditions
            int rawBonus = PF2E_Core.CalculateModifier(level, weaponProf, strengthMod) + mapPenalty;
            int appliedPenalty = attackBonus - rawBonus;

            string atkDebug =
                $"<color=yellow>[ATTACK MATH]</color> {unit.name} | Raw Math: {rawBonus - mapPenalty} | MAP: {mapPenalty}";
            if (appliedPenalty < 0)
                atkDebug += $" | <color=red>Condition Penalties: {appliedPenalty}</color>";
            atkDebug += $" ==> <b>Final Attack Bonus: +{attackBonus}</b>";
            Debug.Log(atkDebug);

            // Defense modifier
            int baseAC = targetUnit.getArmorClass(AttackType.Melee);

            int rawTargetAC = 15;
            int appliedACMod = baseAC - rawTargetAC;

            string defDebug =
                $"<color=yellow>[DEFENSE MATH]</color> {targetUnit.name} | Base AC: {rawTargetAC}";
            if (appliedACMod != 0)
                defDebug +=
                    $" | <color=red>Condition AC Modifiers: {(appliedACMod > 0 ? "+" : "")}{appliedACMod}</color>";
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
                int weaponDice = UnityEngine.Random.Range(1, 9);
                int damage = weaponDice + strengthMod;

                // ENFEEBLED DAMAGE DEBUFF
                // Enfeebled reduces strength-based damage rolls too!
                var myConditions = unit.GetComponent<UnitConditions>();
                if (myConditions != null && myConditions.HasCondition(ConditionType.Enfeebled))
                {
                    int enfValue = myConditions.GetConditionValue(ConditionType.Enfeebled);
                    damage -= enfValue;
                    if (damage < 1) damage = 1; // Minimum 1 damage on a hit
                    Debug.Log(
                        $"<color=orange>[CONDITION]</color> Enfeebled {enfValue} reduced damage roll!"
                    );
                }

                if (result == Degree.CriticalSuccess)
                {
                    damage *= 2;
                    Debug.Log("<b><color=red>CRITICAL HIT!</color></b>");
                }

                var targetHealth = targetUnit.GetComponent<UnitHealth>();
                if (targetHealth != null)
                {
                    targetHealth.ApplyDamage(unit, damage, result == Degree.CriticalSuccess);
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
            var myConditions = unit.GetComponent<UnitConditions>();
            if (myConditions == null)
                return;

            foreach (Unit enemy in GridSystem.Instance.GetAllEnemies(unit.GetFaction()))
            {
                myConditions.SetDetectionState(enemy, DetectionState.Observed);
            }
        }

        public override bool IsValidActionGridPosition(GridPosition gridPosition)
        {
            return GetValidActionGridPositions().Contains(gridPosition);
        }

        public override List<GridPosition> GetValidActionGridPositions()
        {
            List<GridPosition> validGridPositionList = new List<GridPosition>();
            GridPosition unitGridPosition = unit.CurrentGridPosition;

            // Debug.Log($"<color=yellow>--- STARTING TARGET SEARCH (Range: {maxRange}) ---</color>");

            for (int x = -maxRange; x <= maxRange; x++)
            {
                for (int z = -maxRange; z <= maxRange; z++)
                {
                    GridPosition testGridPosition = new GridPosition(
                        unitGridPosition.x + x,
                        unitGridPosition.z + z
                    );

                    // Grid Boundary Check
                    if (!GridSystem.Instance.IsValidGridPosition(testGridPosition))
                        continue;

                    // Self Check
                    if (unitGridPosition == testGridPosition)
                        continue;

                    // Chebyshev Distance Check
                    int distance = Mathf.Max(Mathf.Abs(x), Mathf.Abs(z));
                    if (distance > maxRange)
                        continue;

                    // Unit Check
                    Unit targetUnit = GridSystem.Instance.GetUnitAt(testGridPosition);
                    if (targetUnit == null)
                        continue; // Empty tile

                    // Faction Check
                    if (targetUnit.GetFaction() == unit.GetFaction())
                    {
                        // Debug.Log($"Tile {testGridPosition} rejected: Friendly unit in the way.");
                        continue;
                    }

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
