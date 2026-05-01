using System;
using System.Collections.Generic;
using TacticsGame.Core;
using UnityEngine;

namespace TacticsGame.Characters
{
    /// <summary>
    /// Manages the detection states (Observed, Hidden, Concealed, etc.)
    /// between this unit and all other observers.
    /// </summary>
    public class UnitStealth : MonoBehaviour
    {
        private const bool STEALTH_DEBUG = true;

        private Dictionary<Unit, DetectionState> detectionByObserver =
            new Dictionary<Unit, DetectionState>();

        private Unit unit;
        private UnitConditions conditions;

        public event Action<Unit, DetectionState, DetectionState> OnDetectionStateChanged;

        private bool lastInvisible;

        private void Awake()
        {
            unit = GetComponent<Unit>();
            conditions = GetComponent<UnitConditions>();
        }

        private void OnEnable()
        {
            if (conditions != null)
            {
                lastInvisible = conditions.HasCondition(ConditionType.Invisible);
                conditions.OnConditionsChanged += HandleConditionsChanged;
            }
        }

        private void OnDisable()
        {
            if (conditions != null)
            {
                conditions.OnConditionsChanged -= HandleConditionsChanged;
            }
        }

        private void HandleConditionsChanged()
        {
            if (conditions == null)
                return;

            bool invisibleNow = conditions.HasCondition(ConditionType.Invisible);
            if (invisibleNow == lastInvisible)
                return;

            lastInvisible = invisibleNow;

            // When Invisible is gained/lost, adjust only explicitly tracked observers.
            // For observers not yet in the dictionary, GetDetectionState() provides defaults.
            if (invisibleNow)
            {
                var observers = new List<Unit>(detectionByObserver.Keys);
                foreach (Unit observer in observers)
                {
                    // Approximation (no "HasDetectedLocation" system yet):
                    // If the observer previously had you Observed, you become Hidden when invisible.
                    DetectionState current = GetDetectionState(observer);
                    if (current == DetectionState.Observed)
                        SetDetectionState(observer, DetectionState.Hidden);
                }
            }
            else
            {
                var observers = new List<Unit>(detectionByObserver.Keys);
                foreach (Unit observer in observers)
                {
                    if (
                        detectionByObserver.TryGetValue(observer, out DetectionState current)
                        && current == DetectionState.Hidden
                    )
                    {
                        SetDetectionState(observer, DetectionState.Observed);
                    }
                }
            }
        }

        public void SetDetectionState(Unit observer, DetectionState state)
        {
            DetectionState current = GetDetectionState(observer);
            if (current == state)
                return; // MUST early-return if no change

            if (STEALTH_DEBUG)
            {
                string actorName = unit != null ? unit.name : "(actor null)";
                string observerName = observer != null ? observer.name : "(observer null)";
                Debug.Log(
                    $"<color=teal>[STEALTH]</color> SetDetectionState actor={actorName} observer={observerName} {current} => {state}"
                );
            }

            detectionByObserver[observer] = state;
            OnDetectionStateChanged?.Invoke(observer, current, state);
        }

        public DetectionState GetDetectionState(Unit observer)
        {
            if (detectionByObserver.TryGetValue(observer, out var state))
                return state;

            // Invisible units default to Hidden (approximation without HasDetectedLocation).
            if (conditions == null)
                conditions = GetComponent<UnitConditions>();

            if (conditions != null && conditions.HasCondition(ConditionType.Invisible))
                return DetectionState.Hidden;

            return DetectionState.Observed;
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

        public bool RequiresHiddenFlatCheck(Unit attacker) =>
            GetDetectionState(attacker) == DetectionState.Hidden;

        public bool RequiresConcealedFlatCheck(Unit attacker)
        {
            return conditions != null && conditions.HasCondition(ConditionType.Concealed);
        }

        /// <summary>
        /// Returns the Flat Check DC required to target this unit, based on their detection state.
        /// </summary>
        public int RequiresFlatCheckToTarget(Unit attacker)
        {
            DetectionState state = GetDetectionState(attacker);
            if (state == DetectionState.Hidden)
                return 11;

            if (conditions != null && conditions.HasCondition(ConditionType.Concealed))
                return 5;

            return 0; // Observed requires no flat check
        }

        /// <summary>
        /// Base stealth modifier for this unit's Stealth DC.
        /// </summary>
        public int GetStealthModifier()
        {
            if (unit == null)
                return 0;

            IUnitDataProvider stats = unit.GetStats();
            int dexMod = unit.GetAbilityModifier(AbilityScore.DEX);

            if (stats != null && stats.GetStealth() != 0)
                return stats.GetStealth();

            // Fallback: Dex modifier only.
            return dexMod;
        }
    }
}
