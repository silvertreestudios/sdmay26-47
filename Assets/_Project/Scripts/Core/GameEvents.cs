using System;
using System.Collections.Generic;
using PathfinderTactics.Characters;

namespace PathfinderTactics.Core
{
    /// <summary>
    /// Static Event Bus to decouple gameplay systems from the UI.
    /// This prevents UI scripts from needing direct references to managers
    /// and allows for more robust unit-specific updates.
    /// </summary>
    public static class GameEvents
    {
        // Turn Events
        public static event Action<Unit, List<Unit>> OnTurnOrderChanged;

        public static void TriggerTurnOrderChanged(Unit current, List<Unit> order) =>
            OnTurnOrderChanged?.Invoke(current, order);

        // Unit State Events
        public static event Action<Unit, int, int> OnUnitHealthChanged;

        public static void TriggerUnitHealthChanged(Unit unit, int current, int max) =>
            OnUnitHealthChanged?.Invoke(unit, current, max);

        public static event Action<Unit, bool> OnUnitReactionChanged;

        public static void TriggerUnitReactionChanged(Unit unit, bool isAvailable) =>
            OnUnitReactionChanged?.Invoke(unit, isAvailable);

        // Combat Flow
        public static event Action OnCombatStarted;

        public static void TriggerCombatStarted() => OnCombatStarted?.Invoke();
    }
}
