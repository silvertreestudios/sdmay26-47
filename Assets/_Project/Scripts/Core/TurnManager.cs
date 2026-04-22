using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using PathfinderTactics.Characters;
using PathfinderTactics.Core;
using UnityEngine;

namespace PathfinderTactics.Core
{
    public class TurnManager : MonoBehaviour
    {
        public event EventHandler OnTurnChanged;
        public event EventHandler<OnTurnOrderedEventArgs> OnCombatStarted;

        public class OnTurnOrderedEventArgs : EventArgs
        {
            public List<Unit> turnOrder;
        }

        private List<Unit> turnOrderList;
        private int currentTurnIndex;
        private bool isCombatActive;
        private int roundCount = 1;

        public int RoundCount => roundCount;

        public Unit CurrentUnit =>
            (isCombatActive && turnOrderList.Count > 0) ? turnOrderList[currentTurnIndex] : null;

        private void Awake()
        {
            ServiceLocator.Register(this);
        }

        private void OnDestroy()
        {
            ServiceLocator.Unregister<TurnManager>();
        }

        private IEnumerator Start()
        {
            // Wait one frame to ensure all UnitGridObject.Start() methods
            // have registered their units on the grid before combat starts.
            yield return null;
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
            GameEvents.TriggerCombatStarted();
            GameEvents.TriggerTurnOrderChanged(turnOrderList[currentTurnIndex], turnOrderList);
            StartTurn(turnOrderList[currentTurnIndex]);
        }

        public void NextTurn()
        {
            // Before moving to the next person, trigger the end-of-turn logic
            // for the person who just finished (Decay Frightened, take Persistent Damage)
            if (CurrentUnit != null)
            {
                var conditions = CurrentUnit.GetComponent<UnitConditions>();
                // Even unconscious/dying units take persistent damage at the end of their turn
                if (conditions != null && !conditions.IsDead())
                {
                    conditions.HandleTurnEnd();
                }
            }

            currentTurnIndex++;

            if (currentTurnIndex >= turnOrderList.Count)
            {
                currentTurnIndex = 0;
                roundCount++;
                // Round Ended
            }

            StartTurn(turnOrderList[currentTurnIndex]);
        }

        private void StartTurn(Unit unit)
        {
            Debug.Log($"<color=cyan>--- Turn Start: {unit.name} ---</color>");

            IDamageable health = unit.GetComponent<IDamageable>();
            UnitConditions conditions = unit.GetComponent<UnitConditions>();

            // Skip dead units entirely
            if (health != null && health.IsDead)
            {
                Debug.Log($"{unit.name} is a corpse. Skipping turn.");
                NextTurn();
                return;
            }

            // Handle unconscious / dying units
            if (
                unit.GetComponent<UnitConditions>()?.HasCondition(ConditionType.Unconscious) == true
            )
            {
                Debug.Log($"{unit.name} is unconscious. Rolling recovery check...");
                unit.GetComponent<UnitHealth>()?.RollRecoveryCheck();

                // Skip the rest of their turn and immediately trigger NextTurn
                NextTurn();
                return;
            }

            // Healthy unit (resetting AP, checking Stunned)
            unit.StartTurn();

            // PF2e Aura / Emanation Refresh
            UnitAuraEmitter[] allEmitters = FindObjectsByType<UnitAuraEmitter>(
                FindObjectsSortMode.None
            );
            foreach (var emitter in allEmitters)
            {
                emitter.ClearOldHistory(roundCount);
                emitter.UpdateAuras(AuraTriggerType.OnStartTurn);
            }

            ServiceLocator.Get<UnitActionSystem>().ForceSelectUnit(unit);
            OnTurnChanged?.Invoke(this, EventArgs.Empty);
            GameEvents.TriggerTurnOrderChanged(unit, turnOrderList);
        }

        public bool IsUnitTurn(Unit unit)
        {
            return isCombatActive && CurrentUnit == unit;
        }

        public bool IsPlayerTurn()
        {
            if (CurrentUnit == null)
                return false;

            if (CurrentUnit.GetFaction() == Faction.Player)
                return true;

            // Allow player control on enemy turns when enabled.
            if (
                ServiceLocator.TryGet<EnemyAIManager>(out var ai)
                && ai != null
                && ai.ControlMode == EnemyAIManager.EnemyControlMode.PlayerControlsEnemy
            )
            {
                return true;
            }

            return false;
        }
    }
}
