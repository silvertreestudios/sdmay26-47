using System;
using System.Collections.Generic;
using System.Linq;
using PathfinderTactics.Characters;
using UnityEngine;

namespace PathfinderTactics.Core
{
    public class TurnManager : MonoBehaviour
    {
        public static TurnManager Instance { get; private set; }

        public event EventHandler OnTurnChanged;
        public event EventHandler<OnTurnOrderedEventArgs> OnCombatStarted;

        public class OnTurnOrderedEventArgs : EventArgs
        {
            public List<Unit> turnOrder;
        }

        private List<Unit> turnOrderList;
        private int currentTurnIndex;
        private bool isCombatActive;

        public Unit CurrentUnit =>
            (isCombatActive && turnOrderList.Count > 0) ? turnOrderList[currentTurnIndex] : null;

        private void Awake()
        {
            if (Instance != null)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void Start()
        {
            StartCombat();
        }

        public void StartCombat()
        {
            isCombatActive = true;
            turnOrderList = new List<Unit>();

            // Dictionary to store the rolls so they don't change during sorting
            Dictionary<Unit, int> initiativeRolls = new Dictionary<Unit, int>();

            // Roll for everyone
            foreach (Unit unit in UnitManager.AllUnits)
            {
                turnOrderList.Add(unit);

                // Get Perception (default to 0 if stats missing)
                int perceptionBonus = 0;
                if (unit.GetStats() != null)
                    perceptionBonus = unit.GetStats().perception;

                int roll = UnityEngine.Random.Range(1, 21) + perceptionBonus;
                initiativeRolls[unit] = roll;

                Debug.Log($"{unit.name} Initiative Roll: {roll} (Bonus: {perceptionBonus})");
            }

            // Sort based on the stored rolls (Highest to Lowest)
            turnOrderList.Sort(
                (a, b) =>
                {
                    int rollA = initiativeRolls[a];
                    int rollB = initiativeRolls[b];
                    return rollB.CompareTo(rollA); // Descending
                }
            );

            currentTurnIndex = 0;

            OnCombatStarted?.Invoke(this, new OnTurnOrderedEventArgs { turnOrder = turnOrderList });
            StartTurn(turnOrderList[currentTurnIndex]);
        }

        public void NextTurn()
        {
            currentTurnIndex++;

            if (currentTurnIndex >= turnOrderList.Count)
            {
                currentTurnIndex = 0;
                // Round Ended
            }

            StartTurn(turnOrderList[currentTurnIndex]);
        }

        private void StartTurn(Unit unit)
        {
            Debug.Log($"Turn Start: {unit.name}");

            UnitHealth health = unit.GetComponent<UnitHealth>();

            // Skip dead units entirely
            if (health != null && health.IsDead)
            {
                Debug.Log($"{unit.name} is dead. Skipping turn.");
                NextTurn();
                return;
            }

            // Handle unconscious units
            if (health != null && health.IsUnconscious)
            {
                Debug.Log($"{unit.name} is unconscious. Rolling recovery check...");
                health.RollRecoveryCheck();

                // Wait a moment so the player can read the log before skipping
                // FOr now, just skip instantly.
                // TODO: Add a UI delay here later.
                NextTurn();
                return;
            }

            // If healthy, proceed normally
            unit.StartTurn();
            UnitActionSystem.Instance.ForceSelectUnit(unit);
            OnTurnChanged?.Invoke(this, EventArgs.Empty);
        }

        public bool IsUnitTurn(Unit unit)
        {
            return isCombatActive && CurrentUnit == unit;
        }

        public bool IsPlayerTurn()
        {
            if (CurrentUnit == null)
                return false;

            return CurrentUnit.GetFaction() == Faction.Player;
        }
    }
}
