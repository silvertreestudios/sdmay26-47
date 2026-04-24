using System;
using PathfinderTactics.Actions;
using PathfinderTactics.Core;
using UnityEngine;

namespace PathfinderTactics.Characters
{
    public class UnitActionEconomy : MonoBehaviour
    {
        private int actionPointsRemaining;
        private int maxActionPoints;

        public int MaxActionPoints => maxActionPoints;
        public int ActionPointsRemaining => actionPointsRemaining;

        public int AttacksThisTurn { get; private set; } = 0;
        public bool HasReactionAvailable { get; private set; } = true;
        private BaseAction[] baseActionArray;

        private void Awake()
        {
            maxActionPoints = 3;
            actionPointsRemaining = 3;

            // Auto-discover actions
            baseActionArray = GetComponents<BaseAction>();

            // If none found on root, check children
            if (baseActionArray == null || baseActionArray.Length == 0)
            {
                baseActionArray = GetComponentsInChildren<BaseAction>();
            }

            Debug.Log($"[UNIT BOOTUP] {gameObject.name} found {baseActionArray.Length} actions.");
            foreach (var action in baseActionArray)
            {
                Debug.Log($"   -> Action loaded: {action.GetActionName()}");
            }
        }

        public void StartTurn()
        {
            int baseAP = 3;

            var conditions = GetComponent<UnitConditions>();
            if (conditions != null)
            {
                int apModifier = conditions.HandleTurnStart(out ActionTag restriction);
                maxActionPoints = Mathf.Clamp(baseAP + apModifier, 0, 4);
            }
            else
            {
                maxActionPoints = baseAP;
            }

            actionPointsRemaining = maxActionPoints;
            AttacksThisTurn = 0;
            HasReactionAvailable = true;
            Debug.Log(
                $"<color=green>[ECONOMY]</color> {gameObject.name} REACTION RESTORED at start of turn."
            );
            GameEvents.TriggerUnitReactionChanged(GetComponent<Unit>(), true);
            GameEvents.TriggerUnitAPChanged(
                GetComponent<Unit>(),
                actionPointsRemaining,
                maxActionPoints
            );
        }

        public void SpendReaction()
        {
            HasReactionAvailable = false;
            Debug.Log($"<color=red>[ECONOMY]</color> {gameObject.name} REACTION SPENT.");
            GameEvents.TriggerUnitReactionChanged(GetComponent<Unit>(), false);
        }

        public void RestoreReaction()
        {
            HasReactionAvailable = true;
            GameEvents.TriggerUnitReactionChanged(GetComponent<Unit>(), true);
        }

        public void IncrementAttacksThisTurn() => AttacksThisTurn++;

        public void SpendActionPoints(int amount)
        {
            actionPointsRemaining -= amount;
            // Debug.Log(
            //     $"<color=orange>[ECONOMY]</color> {gameObject.name} spent {amount} AP. Remaining: {actionPointsRemaining}"
            // );
            GameEvents.TriggerUnitAPChanged(
                GetComponent<Unit>(),
                actionPointsRemaining,
                maxActionPoints
            );
        }

        public int GetActionPointsRemaining()
        {
            return actionPointsRemaining;
        }

        public BaseAction[] GetBaseActionArray()
        {
            return baseActionArray;
        }

        /// <summary>
        /// Re-scans all BaseAction components on this GameObject.
        /// Called by UnitEquipment after dynamically adding/removing strike actions.
        /// </summary>
        public void RefreshActions()
        {
            baseActionArray = GetComponents<BaseAction>();
            if (baseActionArray == null || baseActionArray.Length == 0)
            {
                baseActionArray = GetComponentsInChildren<BaseAction>();
            }
        }
    }
}
