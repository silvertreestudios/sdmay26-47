using System;
using System.Collections.Generic;
using PathfinderTactics.Characters;
using PathfinderTactics.Grid;
using UnityEngine;

namespace PathfinderTactics.Actions
{
    public abstract class BaseAction : MonoBehaviour
    {
        protected Unit unit;
        protected Unit Attacker
        {
            get => unit ??= GetComponent<Unit>();
            set => unit = value;
        }
        protected bool isActive;
        protected Action onActionComplete;

        protected virtual void Awake()
        {
            unit = GetComponent<Unit>();
        }

        /// <summary>
        /// Returns the name to display in the UI (e.g., "Strike").
        /// </summary>
        public abstract string GetActionName();

        /// <summary>
        /// How many actions (1-3) this consumes.
        /// </summary>
        public virtual int GetActionPointsCost()
        {
            return 1;
        }

        public abstract void TakeAction(Vector3Int targetPosition, Action onActionComplete);

        public virtual bool IsValidActionGridPosition(Vector3Int targetPosition)
        {
            return true;
        }

        // Returns a list of all valid 3D layered positions (e.g., all enemies in range)
        public abstract List<Vector3Int> GetValidActionGridPositions();

        public virtual List<Vector3Int> GetActionRangeGridPositions()
        {
            return GetValidActionGridPositions();
        }

        /// <summary>
        /// True for actions that target a specific unit (melee/ranged strikes,
        /// single-target abilities). False for position-based actions (AoE spells).
        /// When true, the TargetLockService handles targeting instead of the grid cursor.
        /// </summary>
        public virtual bool IsUnitTargeted => false;

        /// <summary>
        /// Validates if the unit's current physical/mental state allows actions.
        /// </summary>
        public virtual bool CanExecuteAction()
        {
            var conditions = unit.GetComponent<UnitConditions>();
            if (conditions == null)
                return true;

            // Universal Blockers: Dead or Unconscious units cannot take ANY actions.
            if (conditions.IsDead() || conditions.HasCondition(ConditionType.Unconscious))
            {
                Debug.Log(
                    $"<color=red>Action blocked: {unit.name} is Unconscious or Dead.</color>"
                );
                return false;
            }

            // Stunned blocker (Just in case the UI accidentally lets them click)
            if (conditions.GetConditionValue(ConditionType.Stunned) > 0)
            {
                Debug.Log($"<color=red>Action blocked: {unit.name} is Stunned.</color>");
                return false;
            }

            return true;
        }
    }
}
