using System;
using PathfinderTactics.Combat;
using PathfinderTactics.Core;
using PathfinderTactics.Reactions;
using UnityEngine;

namespace PathfinderTactics.Characters
{
    /// <summary>
    /// Component that tracks a Unit's current and maximum HP at runtime.
    /// Initializes max HP from the Unit's stats (via Unit.getTotalHealth()).
    /// Raises an OnHpChanged event whenever current HP changes.
    /// </summary>
    [RequireComponent(typeof(UnitConditions))]
    public class UnitHealth : MonoBehaviour, IDamageable
    {
        public event EventHandler OnDeath;
        public event EventHandler OnHealthChanged;
        public event EventHandler<string> OnStatusMessage;

        [Header("Health Stats")]
        [SerializeField]
        private int baseMaxHealth = 20; // The unit's permanent max HP
        private int currentMaxHealth; // Adjusted max HP (affected by Drained)
        private int currentHealth;

        private UnitConditions unitConditions;
        private Unit thisUnit;
        private UnitVisuals unitVisuals;

        // Route state checks directly to the Condition Manager
        public bool IsUnconscious =>
            unitConditions != null && unitConditions.HasCondition(ConditionType.Unconscious);
        public bool IsDead => unitConditions != null && unitConditions.IsDead();

        private void Awake()
        {
            thisUnit = GetComponent<Unit>();
            unitConditions = GetComponent<UnitConditions>();
            unitVisuals = GetComponentInChildren<UnitVisuals>();
            if (unitConditions == null)
            {
                unitConditions = gameObject.AddComponent<UnitConditions>();
            }

            currentMaxHealth = baseMaxHealth;
            currentHealth = currentMaxHealth;
        }

        private void OnEnable()
        {
            if (unitConditions != null)
            {
                // Subscribe to the Drained hook to recalculate Max HP dynamically
                unitConditions.OnDrainedChanged += HandleDrainedChanged;
            }
        }

        private void OnDisable()
        {
            if (unitConditions != null)
            {
                unitConditions.OnDrainedChanged -= HandleDrainedChanged;
            }
        }

        // Drained Hook
        private void HandleDrainedChanged(int drainedValue)
        {
            int level = 1; // TODO: Pull from UnitStatsSO later
            int hpReduction = level * drainedValue;

            int oldMax = currentMaxHealth;
            currentMaxHealth = baseMaxHealth - hpReduction;

            // PF2e Rule: When your max HP drops from Drained, your current HP drops by the same amount
            if (currentMaxHealth < oldMax)
            {
                int difference = oldMax - currentMaxHealth;
                currentHealth = Mathf.Max(0, currentHealth - difference);
                Debug.Log(
                    $"<color=purple>[DRAINED]</color> {thisUnit.name} lost {difference} Max HP!"
                );

                // If Drained somehow reduced them to 0 HP, trigger dying
                if (currentHealth == 0 && !unitConditions.HasCondition(ConditionType.Dying))
                {
                    unitConditions.ApplyDying();
                }
            }

            OnHealthChanged?.Invoke(this, EventArgs.Empty);
        }

        // Damage and Dying Logic
        public void ApplyDamage(
            Unit source,
            int amount,
            DamageType type,
            bool isCriticalHit = false
        )
        {
            if (IsDead)
                return;

            // Resolve RWI (Immunity -> Weakness -> Resistance) BEFORE Reactions
            int rwiResolvedAmount = RWICalculator.ResolveDamage(amount, type, thisUnit);

            if (rwiResolvedAmount <= 0 && amount > 0)
            {
                // Damage was fully blocked by Immunity or Resistance
                return;
            }

            BeforeDamageEvent damageEvent = new BeforeDamageEvent(
                source,
                thisUnit,
                rwiResolvedAmount,
                isCriticalHit,
                type
            );

            ServiceLocator
                .Get<ReactionManager>()
                .EvaluateEvent(
                    damageEvent,
                    (resolvedEvent) =>
                    {
                        BeforeDamageEvent finalDamageEvent = resolvedEvent as BeforeDamageEvent;

                        if (finalDamageEvent.IsCancelled || finalDamageEvent.DamageAmount <= 0)
                        {
                            Debug.Log(
                                $"<color=green>Damage to {thisUnit.name} was fully mitigated!</color>"
                            );
                            return;
                        }

                        int finalAmount = finalDamageEvent.DamageAmount;

                        // Apply the damage
                        currentHealth -= finalAmount;
                        currentHealth = Mathf.Max(0, currentHealth);
                        OnHealthChanged?.Invoke(this, EventArgs.Empty);

                        // CombatLog: Final Impact
                        CombatLogUtility.LogFinalImpact(
                            thisUnit,
                            finalAmount,
                            currentHealth,
                            currentMaxHealth
                        );

                        if (unitVisuals != null && finalAmount > 0)
                            unitVisuals.TriggerTakeDamage();

                        // Dying Logic
                        if (currentHealth == 0)
                        {
                            int currentDying = unitConditions.GetConditionValue(
                                ConditionType.Dying
                            );

                            if (currentDying == 0)
                            {
                                // Initial Knockout
                                // ApplyDying automatically calculates 1 + Wounded.
                                // If it was a crit, we need to add 1 more to that
                                unitConditions.ApplyDying();
                                if (finalDamageEvent.IsCriticalHit)
                                {
                                    int newDying =
                                        unitConditions.GetConditionValue(ConditionType.Dying) + 1;
                                    unitConditions.ApplyCondition(ConditionType.Dying, newDying);
                                }

                                if (unitVisuals != null)
                                    unitVisuals.SetUnconscious(true);

                                OnStatusMessage?.Invoke(this, "Knocked Out!");
                            }
                            else
                            {
                                // Taking damage while ALREADY dying
                                int increase = finalDamageEvent.IsCriticalHit ? 2 : 1;
                                unitConditions.ApplyCondition(
                                    ConditionType.Dying,
                                    currentDying + increase
                                );
                                OnStatusMessage?.Invoke(this, $"Dying {currentDying + increase}!");
                            }

                            // Check if that damage killed them
                            if (IsDead)
                            {
                                if (unitVisuals != null)
                                    unitVisuals.SetDead(true);

                                OnStatusMessage?.Invoke(this, "DEAD");
                                OnDeath?.Invoke(this, EventArgs.Empty);
                            }
                        }
                    }
                );
        }

        // Healing and waking up
        public void ApplyHealing(int amount)
        {
            if (IsDead || amount <= 0)
                return;

            // PF2e Rule: Any healing immediately ends persistent Bleed damage
            unitConditions.RemovePersistentDamage(DamageType.Bleed);

            currentHealth += amount;
            currentHealth = Mathf.Min(currentHealth, currentMaxHealth);
            OnHealthChanged?.Invoke(this, EventArgs.Empty);

            // Waking up from Dying
            if (unitConditions.HasCondition(ConditionType.Dying))
            {
                unitConditions.RecoverFromDying(); // This automatically adds Wounded and removes Unconscious
                int wounded = unitConditions.GetConditionValue(ConditionType.Wounded);

                if (unitVisuals != null)
                    unitVisuals.SetUnconscious(false);

                OnStatusMessage?.Invoke(this, $"Stabilized & Woke Up! (Wounded {wounded})");
            }
            // Waking up from normal Unconscious (like sleep)
            else if (unitConditions.HasCondition(ConditionType.Unconscious))
            {
                unitConditions.RemoveCondition(ConditionType.Unconscious);

                if (unitVisuals != null)
                    unitVisuals.SetUnconscious(false);

                OnStatusMessage?.Invoke(this, "Woke Up!");
            }
        }

        // Recovery Checks
        public void RollRecoveryCheck()
        {
            if (!IsUnconscious || IsDead || !unitConditions.HasCondition(ConditionType.Dying))
                return;

            int currentDying = unitConditions.GetConditionValue(ConditionType.Dying);
            int d20 = UnityEngine.Random.Range(1, 21);
            int dc = 10 + currentDying; // Default PF2e recovery DC

            Degree result = PF2E_Core.CheckResult(d20, 0, dc);
            Debug.Log($"[Recovery Check] Rolled {d20} vs DC {dc}. Result: {result}");

            int newDying = currentDying;

            switch (result)
            {
                case Degree.CriticalSuccess:
                    newDying -= 2;
                    break;
                case Degree.Success:
                    newDying -= 1;
                    break;
                case Degree.Failure:
                    newDying += 1;
                    break;
                case Degree.CriticalFailure:
                    newDying += 2;
                    break;
            }

            // Stabilizing
            if (newDying <= 0)
            {
                // In PF2e, stabilizing keeps you at 0 HP and Unconscious,
                // but removes Dying and increases Wounded.
                unitConditions.RemoveCondition(ConditionType.Dying);
                int wounded = unitConditions.GetConditionValue(ConditionType.Wounded);
                unitConditions.ApplyCondition(ConditionType.Wounded, wounded + 1);

                OnStatusMessage?.Invoke(this, $"Stabilized! (Wounded {wounded + 1})");
            }
            else
            {
                // Still dying... update the condition (which might trigger IsDead)
                unitConditions.ApplyCondition(ConditionType.Dying, newDying);

                if (IsDead)
                {
                    OnStatusMessage?.Invoke(this, "DEAD");
                    OnDeath?.Invoke(this, EventArgs.Empty);
                }
                else
                {
                    OnStatusMessage?.Invoke(this, $"Dying {newDying}");
                }
            }
        }

        // Getters for UI
        public int GetCurrentHealth() => currentHealth;

        public int GetMaxHealth() => currentMaxHealth;
    }
}
