using System;
using PathfinderTactics.Actions;
using UnityEngine;

namespace PathfinderTactics.Characters
{
    public class UnitActionEconomy : MonoBehaviour
    {
        private int actionPointsRemaining;
        public int AttacksThisTurn { get; private set; } = 0;
        public bool HasReactionAvailable { get; private set; } = true;
        private BaseAction[] baseActionArray;

        private void Awake()
        {
            // Auto-discover actions
            baseActionArray = GetComponents<BaseAction>();

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
                actionPointsRemaining = Mathf.Clamp(baseAP + apModifier, 0, 4);
            }
            else
            {
                actionPointsRemaining = baseAP;
            }

            AttacksThisTurn = 0;
            HasReactionAvailable = true;
        }

        public void SpendReaction() => HasReactionAvailable = false;

        public void RestoreReaction() => HasReactionAvailable = true;

        public void IncrementAttacksThisTurn() => AttacksThisTurn++;

        public void SpendActionPoints(int amount)
        {
            actionPointsRemaining -= amount;
        }

        public int GetActionPointsRemaining()
        {
            return actionPointsRemaining;
        }

        public BaseAction[] GetBaseActionArray()
        {
            return baseActionArray;
        }
    }
}
