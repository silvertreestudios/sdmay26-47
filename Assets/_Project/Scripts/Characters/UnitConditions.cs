using System;
using System.Collections.Generic;
using UnityEngine;

namespace PathfinderTactics.Characters
{
    public class UnitConditions : MonoBehaviour
    {
        // Data Structures
        private Dictionary<ConditionType, ActiveCondition> activeConditions =
            new Dictionary<ConditionType, ActiveCondition>();
        public IReadOnlyDictionary<ConditionType, ActiveCondition> ActiveConditions =>
            activeConditions;

        private List<PersistentDamageInstance> persistentDamages =
            new List<PersistentDamageInstance>();

        private readonly HashSet<ConditionType> binaryConditions = new HashSet<ConditionType>
        {
            ConditionType.OffGuard,
            ConditionType.Prone,
            ConditionType.Grabbed,
            ConditionType.Restrained,
            ConditionType.Immobilized,
            ConditionType.Unconscious,
            ConditionType.Blinded,
            ConditionType.Deafened,
            ConditionType.Invisible,
            ConditionType.Fatigued,
        };

        public event Action OnConditionsChanged;
        public event Action<int> OnDrainedChanged;

        private Unit unit;

        private void Awake()
        {
            unit = GetComponent<Unit>();
        }

        // Condition Logic
        public void ApplyCondition(
            ConditionType type,
            int value = 1,
            Unit source = null,
            ActionTag quickenedRestriction = ActionTag.None
        )
        {
            if (binaryConditions.Contains(type))
                value = 1;
            if (value < 1)
                value = 1;

            if (activeConditions.TryGetValue(type, out var condition))
            {
                if (value > condition.Value)
                {
                    condition.Value = value;
                    condition.Source = source;
                    if (type == ConditionType.Quickened)
                        condition.QuickenedRestriction = quickenedRestriction;
                }
            }
            else
            {
                activeConditions.Add(
                    type,
                    new ActiveCondition(type, value, source, quickenedRestriction)
                );
            }

            if (type == ConditionType.Drained)
                OnDrainedChanged?.Invoke(value);
            OnConditionsChanged?.Invoke();
        }

        public void ReduceCondition(ConditionType type, int amount)
        {
            if (activeConditions.TryGetValue(type, out var condition))
            {
                if (binaryConditions.Contains(type))
                {
                    RemoveCondition(type);
                    return;
                }

                condition.Value -= amount;

                if (condition.Value <= 0)
                {
                    RemoveCondition(type);
                }
                else
                {
                    if (type == ConditionType.Drained)
                        OnDrainedChanged?.Invoke(condition.Value);
                    OnConditionsChanged?.Invoke();
                }
            }
        }

        public void RemoveCondition(ConditionType type)
        {
            if (activeConditions.Remove(type))
            {
                if (type == ConditionType.Drained)
                    OnDrainedChanged?.Invoke(0);
                OnConditionsChanged?.Invoke();
            }
        }

        public int GetConditionValue(ConditionType type)
        {
            return activeConditions.TryGetValue(type, out var condition) ? condition.Value : 0;
        }

        // Condition heirchy and helper methods

        public bool HasCondition(ConditionType type)
        {
            // PF2e Condition Hierarchy
            switch (type)
            {
                case ConditionType.Immobilized:
                    return activeConditions.ContainsKey(ConditionType.Immobilized)
                        || activeConditions.ContainsKey(ConditionType.Grabbed)
                        || activeConditions.ContainsKey(ConditionType.Restrained)
                        || activeConditions.ContainsKey(ConditionType.Unconscious); // Unconscious things can't move

                case ConditionType.Grabbed:
                    return activeConditions.ContainsKey(ConditionType.Grabbed)
                        || activeConditions.ContainsKey(ConditionType.Restrained);

                case ConditionType.Blinded:
                    return activeConditions.ContainsKey(ConditionType.Blinded)
                        || activeConditions.ContainsKey(ConditionType.Unconscious);

                case ConditionType.Deafened:
                    return activeConditions.ContainsKey(ConditionType.Deafened)
                        || activeConditions.ContainsKey(ConditionType.Unconscious);

                case ConditionType.Prone:
                    return activeConditions.ContainsKey(ConditionType.Prone)
                        || activeConditions.ContainsKey(ConditionType.Unconscious); // You fall prone when KO'd

                case ConditionType.OffGuard:
                    return IsOffGuard();

                default:
                    return activeConditions.ContainsKey(type);
            }
        }

        public bool IsOffGuard()
        {
            return activeConditions.ContainsKey(ConditionType.OffGuard)
                || activeConditions.ContainsKey(ConditionType.Prone)
                || activeConditions.ContainsKey(ConditionType.Grabbed)
                || activeConditions.ContainsKey(ConditionType.Restrained)
                || activeConditions.ContainsKey(ConditionType.Unconscious)
                || activeConditions.ContainsKey(ConditionType.Blinded)
                || activeConditions.ContainsKey(ConditionType.Invisible);
        }

        public bool CanMove()
        {
            return !HasCondition(ConditionType.Immobilized);
        }

        /// <summary>
        /// General capability check for whether the unit can take actions at all.
        /// Returns false if Dead, Unconscious, Paralyzed, or Petrified.
        /// </summary>
        public bool CanAct
        {
            get
            {
                return !IsDead()
                    && !HasCondition(ConditionType.Unconscious)
                    && !HasCondition(ConditionType.Stunned); // Stunned represents losing actions
                // TODO: Add Paralyzed/Petrified when implemented
            }
        }

        /// <summary>
        /// Capability check for whether the unit can contribute to flanking/threat.
        /// Returns false if unable to act or under specific melee-restricting effects.
        /// </summary>
        public bool CanMakeMeleeAttacks
        {
            get
            {
                // In PF2e, you can't flank if you can't act or are under effects preventing attacks.
                return CanAct && !HasCondition(ConditionType.Restrained);
            }
        }

        // Dying, wounded, and unconscious.

        public void ApplyDying()
        {
            int currentWounded = GetConditionValue(ConditionType.Wounded);
            int newDyingValue = 1 + currentWounded;
            int doomedValue = GetConditionValue(ConditionType.Doomed);
            int maxDying = 4 - doomedValue;

            ApplyCondition(ConditionType.Dying, newDyingValue);
            ApplyCondition(ConditionType.Unconscious); // Force Unconscious

            if (GetConditionValue(ConditionType.Dying) >= maxDying)
            {
                Debug.Log($"{unit.name} has died!");
            }
        }

        public void RecoverFromDying()
        {
            if (HasCondition(ConditionType.Dying))
            {
                RemoveCondition(ConditionType.Dying);
                RemoveCondition(ConditionType.Unconscious); // Wake up

                int currentWounded = GetConditionValue(ConditionType.Wounded);
                ApplyCondition(ConditionType.Wounded, currentWounded + 1); // Increase Wounded
            }
        }

        public bool IsDead()
        {
            int doomedValue = GetConditionValue(ConditionType.Doomed);
            int maxDying = 4 - doomedValue;
            return GetConditionValue(ConditionType.Dying) >= maxDying;
        }

        // Detection states and targeting

        // Persistent Damage

        public void ApplyPersistentDamage(
            DamageType type,
            int diceCount,
            int diceFaces,
            int flatDamage = 0,
            Unit source = null
        )
        {
            var existing = persistentDamages.Find(p => p.Type == type);
            if (existing != null)
            {
                int existingExpected =
                    (existing.DiceCount * existing.DiceFaces) + existing.FlatDamage;
                int newExpected = (diceCount * diceFaces) + flatDamage;

                if (newExpected > existingExpected)
                {
                    existing.DiceCount = diceCount;
                    existing.DiceFaces = diceFaces;
                    existing.FlatDamage = flatDamage;
                    existing.Source = source;
                }
            }
            else
            {
                persistentDamages.Add(
                    new PersistentDamageInstance
                    {
                        Type = type,
                        DiceCount = diceCount,
                        DiceFaces = diceFaces,
                        FlatDamage = flatDamage,
                        Source = source,
                    }
                );
            }
        }

        public void RemovePersistentDamage(DamageType type)
        {
            persistentDamages.RemoveAll(p => p.Type == type);
        }

        // Turn lifecycle

        public int HandleTurnStart(out ActionTag extraActionRestriction)
        {
            int actionModifier = 0;
            extraActionRestriction = ActionTag.None;

            if (activeConditions.TryGetValue(ConditionType.Quickened, out var quickened))
            {
                actionModifier += 1;
                extraActionRestriction = quickened.QuickenedRestriction;
            }

            if (HasCondition(ConditionType.Stunned))
            {
                int stunnedValue = GetConditionValue(ConditionType.Stunned);
                int availableActions = 3 + actionModifier;
                int actionsLost = Mathf.Min(stunnedValue, availableActions);

                actionModifier -= actionsLost;
                ReduceCondition(ConditionType.Stunned, actionsLost);
            }
            else if (HasCondition(ConditionType.Slowed))
            {
                actionModifier -= GetConditionValue(ConditionType.Slowed);
            }

            return actionModifier;
        }

        public void HandleTurnEnd()
        {
            // Natural Decay
            if (activeConditions.ContainsKey(ConditionType.Frightened))
            {
                ReduceCondition(ConditionType.Frightened, 1);
            }

            // Persistent Damage
            for (int i = persistentDamages.Count - 1; i >= 0; i--)
            {
                var pd = persistentDamages[i];
                int dmg = pd.RollDamage();

                Debug.Log(
                    $"<color=orange>[Persistent]</color> {unit.name} takes {dmg} {pd.Type} damage!"
                );
                unit.GetComponent<IDamageable>()?.ApplyDamage(pd.Source, dmg, pd.Type, false);

                // Flat Check for recovery (DC 15 per PF2e Rules)
                int flatCheck = UnityEngine.Random.Range(1, 21);
                if (flatCheck >= 15)
                {
                    Debug.Log(
                        $"<color=green>Passed DC 15 flat check ({flatCheck}).</color> Recovered from {pd.Type} damage."
                    );
                    persistentDamages.RemoveAt(i);
                }
                else
                {
                    Debug.Log(
                        $"<color=red>Failed flat check ({flatCheck}).</color> {pd.Type} damage remains."
                    );
                }
            }
        }
    }
}
