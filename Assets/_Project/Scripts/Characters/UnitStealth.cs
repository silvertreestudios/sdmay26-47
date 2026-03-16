using System;
using System.Collections.Generic;
using UnityEngine;

namespace PathfinderTactics.Characters
{
    /// <summary>
    /// Manages the detection states (Observed, Hidden, Concealed, etc.)
    /// between this unit and all other observers.
    /// </summary>
    public class UnitStealth : MonoBehaviour
    {
        private Dictionary<Unit, DetectionState> detectionByObserver =
            new Dictionary<Unit, DetectionState>();

        public void SetDetectionState(Unit observer, DetectionState state)
        {
            detectionByObserver[observer] = state;
        }

        public DetectionState GetDetectionState(Unit observer)
        {
            return detectionByObserver.TryGetValue(observer, out var state)
                ? state
                : DetectionState.Observed; // Default state is observed
        }

        /// <summary>
        /// True if the attacker can target this unit without an Area of Effect.
        /// (Cannot directly target Undetected or Unnoticed units without guessing a square).
        /// </summary>
        public bool CanBeDirectlyTargeted(Unit attacker)
        {
            DetectionState state = GetDetectionState(attacker);
            return state != DetectionState.Undetected && state != DetectionState.Unnoticed;
        }

        /// <summary>
        /// Returns the Flat Check DC required to target this unit, based on their detection state.
        /// </summary>
        public int RequiresFlatCheckToTarget(Unit attacker)
        {
            DetectionState state = GetDetectionState(attacker);
            if (state == DetectionState.Concealed)
                return 5;
            if (state == DetectionState.Hidden)
                return 11;
            return 0; // Observed requires no flat check
        }
    }
}
