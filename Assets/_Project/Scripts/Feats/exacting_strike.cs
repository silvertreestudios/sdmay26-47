using UnityEngine;
using PathfinderTactics.Characters;
using PathfinderTactics.Actions;
using System;
using System.Collections.Generic;
using PathfinderTactics.Core;
using PathfinderTactics.Grid;
using TMPro;

namespace PathfinderTactics.Feats
{
    public class exacting_strike : BaseAction, FeatBase
    {

        public override string GetActionName() => "Exacting Strike";

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

        public override void TakeAction(GridPosition gridPosition, Action onActionComplete)
        {
            Unit targetUnit = GridSystem.Instance.GetUnitAt(gridPosition);

            if (targetUnit == null)
            {
                Debug.LogError("No unit found at target position!");
                onActionComplete?.Invoke();
                return;
            }

            if (Perform(unit, targetUnit))
            {
                Debug.Log("Strike Exacted!");
                onActionComplete?.Invoke();
            }else
            {
                onActionComplete?.Invoke();
                return;
            }

        }


        public bool Perform(Unit parent, Unit target)
        {

            Debug.Log($"Exacting Strike!");
            //Press type feat, so check if we have attacked already
            if (parent.GetActionPointsRemaining() <= 0 || parent.AttacksThisTurn <= 0)
            {
                Debug.Log("Not enough actions.");
                return false;
            }


            Debug.Log("Exacting Strike!");
            // Clear previous rolls
            // Simple attack logic
            int roll = UnityEngine.Random.Range(1, 21);
            int strength = parent.GetStats().strength;
            // Profcienciey is expertise for now (Fighter level 1) expertise = 4 + lvl,
            int proficiency = 5;
            int penalty = -1 * (parent.AttacksThisTurn * 5);
            int attackBonus = strength + proficiency + penalty;
           
            int ac = target.getArmorClass();

            int d20 = UnityEngine.Random.Range(1, 21);
            Degree result = PF2E_Core.CheckResult(roll, attackBonus, ac);

            if (result == Degree.Success || result == Degree.CriticalSuccess)
            {
                int weaponDice = UnityEngine.Random.Range(1, 9);
                int damage = weaponDice + strength;

                parent.IncrementAttacksThisTurn();

                if (result == Degree.CriticalSuccess)
                {
                    damage *= 2;
                    Debug.Log("CRITICAL HIT!");
                }

                // Check health
                var targetHealth = target.GetComponent<UnitHealth>();
                if (targetHealth != null)
                {
                    targetHealth.ApplyDamage(damage);
                    Debug.Log($"Dealt {damage} Damage to {target.name}!");
                }
                else
                {
                    Debug.LogError(
                        $"ERROR: Target '{target.name}' does NOT have a UnitHealth component!"
                    );
                }
            }
            else
            {

                Debug.Log("Miss! Map Does Not Increase!");
            }

            return true;

        }

        public override bool IsValidActionGridPosition(GridPosition gridPosition)
        {
            return GetValidActionGridPositions().Contains(gridPosition);
        }

        public override List<GridPosition> GetValidActionGridPositions()
        {
            List<GridPosition> validGridPositions = new List<GridPosition>();

            // Loop through the bounds we just generated
            foreach (GridPosition rangePos in GetActionRangeGridPositions())
            {
                Unit targetUnit = GridSystem.Instance.GetUnitAt(rangePos);

                if (targetUnit == null)
                    continue;
                if (targetUnit == unit)
                    continue;
                if (!unit.IsEnemy(targetUnit))
                    continue;

                validGridPositions.Add(rangePos);
            }
            return validGridPositions;
        }
    }

}
