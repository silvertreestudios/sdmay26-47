using System;
using System.Collections.Generic;
using PathfinderTactics.Characters;
using PathfinderTactics.Core;
using PathfinderTactics.Grid;
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

        public override string GetActionName() => "Shoot";

        [Header("Weapon Stats")]
        [SerializeField]
        private int maxRange = 12; // 60ft range

        [SerializeField]
        private bool isAgileWeapon = false;

        public override void TakeAction(GridPosition gridPosition, Action onActionComplete)
        {
            targetUnit = GridSystem.Instance.GetUnitAt(gridPosition);

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
            var stats = unit.GetStats();
            if (stats == null || targetUnit == null)
                return;

            int level = 1;
            // Ranged uses dexterity
            int dexMod = (stats.dexterity - 10) / 2;
            Proficiency weaponProf = Proficiency.Trained;

            int mapPenalty = 0;
            int attacksMade = unit.AttacksThisTurn;

            if (attacksMade == 1)
                mapPenalty = isAgileWeapon ? -4 : -5;
            else if (attacksMade >= 2)
                mapPenalty = isAgileWeapon ? -8 : -10;

            int attackBonus = PF2E_Core.CalculateModifier(level, weaponProf, dexMod) + mapPenalty;

            // Cover Logic
            int baseAC = targetUnit.getArmorClass();
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
                int damage = UnityEngine.Random.Range(1, 9); // 1d8

                if (result == Degree.CriticalSuccess)
                {
                    damage *= 2;
                    Debug.Log("CRITICAL HIT!");
                }

                var targetHealth = targetUnit.GetComponent<UnitHealth>();
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
        }

        public override List<GridPosition> GetActionRangeGridPositions()
        {
            List<GridPosition> rangePositions = new List<GridPosition>();
            GridPosition unitGridPos = unit.CurrentGridPosition;

            for (int x = -maxRange; x <= maxRange; x++)
            {
                for (int z = -maxRange; z <= maxRange; z++)
                {
                    GridPosition testPos = new GridPosition(unitGridPos.x + x, unitGridPos.z + z);

                    // Keep it on the map
                    if (!GridSystem.Instance.IsValidGridPosition(testPos))
                        continue;

                    // Chebyshev distance
                    int distance = Mathf.Max(Mathf.Abs(x), Mathf.Abs(z));
                    if (distance <= maxRange)
                    {
                        rangePositions.Add(testPos);
                    }
                }
            }
            return rangePositions;
        }

        public override List<GridPosition> GetValidActionGridPositions()
        {
            List<GridPosition> validGridPositionList = new List<GridPosition>();
            GridPosition unitGridPosition = unit.CurrentGridPosition;

            for (int x = -maxRange; x <= maxRange; x++)
            {
                for (int z = -maxRange; z <= maxRange; z++)
                {
                    GridPosition testGridPosition = new GridPosition(
                        unitGridPosition.x + x,
                        unitGridPosition.z + z
                    );

                    if (!GridSystem.Instance.IsValidGridPosition(testGridPosition))
                        continue;
                    if (unitGridPosition == testGridPosition)
                        continue;

                    int distance = Mathf.Max(Mathf.Abs(x), Mathf.Abs(z));
                    if (distance > maxRange)
                        continue;

                    Unit targetUnit = GridSystem.Instance.GetUnitAt(testGridPosition);
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
