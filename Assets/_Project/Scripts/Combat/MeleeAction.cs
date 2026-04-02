using System;
using System.Collections.Generic;
using PathfinderTactics.Characters;
using PathfinderTactics.Core;
using PathfinderTactics.Grid;
using UnityEngine;
using TMPro;

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
            // Check stats
            var stats = unit.GetStats();
            if (stats == null)
            {
                Debug.LogError(
                    $"ERROR: Unit '{unit.name}' is missing its UnitStatsSO! Assign it in the Inspector."
                );
                return;
            }

            // Check target
            if (targetUnit == null)
            {
                Debug.LogError("ERROR: Target Unit is null in PerformStrikeLogic.");
                return;
            }

            // Get Stats
            // TODO: Move Proficiency to UnitStatsSO later
            int level = 1;
            int strengthMod = (stats.strength - 10) / 2;
            Proficiency weaponProf = Proficiency.Trained;

            // calculate MAP (Multiple Attack Penalty)
            int mapPenalty = 0;
            int attacksMade = unit.AttacksThisTurn;

            if (attacksMade == 1)
                mapPenalty = isAgileWeapon ? -4 : -5;
            else if (attacksMade >= 2)
                mapPenalty = isAgileWeapon ? -8 : -10;

            // Apply to bonus
            int attackBonus =
                PF2E_Core.CalculateModifier(level, weaponProf, strengthMod) + mapPenalty;
            int ac = targetUnit.getArmorClass();

            // Increment attack count for MAP
            unit.IncrementAttacksThisTurn();

            int d20 = UnityEngine.Random.Range(1, 21);
            Degree result = PF2E_Core.CheckResult(d20, attackBonus, ac);

            Debug.Log(
                $"[Strike] Rolled {d20} + {attackBonus} (MAP: {mapPenalty}) vs AC {ac} -> {result}"
            );
            Transform Log = GameObject.Find("LogLayoutGroup").transform;
            GameObject prefab = Resources.Load<GameObject>("LogChatBox");
            GameObject textIcon = Instantiate(prefab, Log);

            textIcon.GetComponent<TextMeshProUGUI>().text
                = $"[Strike] Rolled {d20} + {attackBonus} (MAP: {mapPenalty}) vs AC {ac} -> {result}";

            // Apply Damage
            if (result == Degree.Success || result == Degree.CriticalSuccess)
            {
                int weaponDice = UnityEngine.Random.Range(1, 9);
                int damage = weaponDice + strengthMod;

                if (result == Degree.CriticalSuccess)
                {
                    damage *= 2;
                    Debug.Log("CRITICAL HIT!");
                }

                // Check health
                var targetHealth = targetUnit.GetComponent<UnitHealth>();
                if (targetHealth != null)
                {
                    targetHealth.ApplyDamage(damage);
                    Debug.Log($"Dealt {damage} Damage to {targetUnit.name}!");

                    Transform Log1 = GameObject.Find("LogLayoutGroup").transform;
                    GameObject prefab1 = Resources.Load<GameObject>("LogChatBox");
                    GameObject textIcon1 = Instantiate(prefab1, Log1);

                    textIcon1.GetComponent<TextMeshProUGUI>().text
                        = $"Dealt {damage} Damage to {targetUnit.name}!";
                }
                else
                {
                    Debug.LogError(
                        $"ERROR: Target '{targetUnit.name}' does NOT have a UnitHealth component!"
                    );
                }
            }
            else
            {
                Debug.Log("Miss!");
            }
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
