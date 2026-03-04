using System;
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
    public class UnitHealth : MonoBehaviour
    {
        public event EventHandler OnDeath;
        public event EventHandler OnHealthChanged;
        public event EventHandler<string> OnStatusMessage; // For popping up "Dying 1!" text later

        [SerializeField]
        private int maxHealth = 20;
        private int currentHealth;

        // PF2E Conditions
        public int DyingValue { get; private set; } = 0;
        public int WoundedValue { get; private set; } = 0;
        public bool IsUnconscious => currentHealth <= 0;
        public bool IsDead { get; private set; } = false;

        private void Awake()
        {
            currentHealth = maxHealth;
        }

        public void ApplyDamage(Unit source, int amount, bool isCriticalHit = false)
        {
            if (IsDead)
                return;

            Unit thisUnit = GetComponent<Unit>();

            // Package the intended damage
            BeforeDamageEvent damageEvent = new BeforeDamageEvent(
                source,
                thisUnit,
                amount,
                isCriticalHit
            );

            // Send it through the Reaction Manager
            ReactionManager.Instance.EvaluateEvent(
                damageEvent,
                (resolvedEvent) =>
                {
                    BeforeDamageEvent finalDamageEvent = resolvedEvent as BeforeDamageEvent;

                    // Did a reaction completely cancel the attack (e.g., a total miss reaction)
                    // or reduce the damage to 0 or below?
                    if (finalDamageEvent.IsCancelled || finalDamageEvent.DamageAmount <= 0)
                    {
                        Debug.Log(
                            $"<color=green>Damage to {thisUnit.name} was fully mitigated!</color>"
                        );
                        return;
                    }

                    // Actually apply the modified damage to the HP pool
                    currentHealth -= finalDamageEvent.DamageAmount;
                    currentHealth = Mathf.Max(0, currentHealth);
                    OnHealthChanged?.Invoke(this, EventArgs.Empty);

                    Debug.Log(
                        $"<color=red>[HEALTH]</color> {thisUnit.name} took {finalDamageEvent.DamageAmount} final damage. HP: {currentHealth}/{maxHealth}"
                    );

                    // Handle PF2e Dying rules
                    if (currentHealth == 0 && DyingValue == 0)
                    {
                        // Drop to 0 HP logic
                        int initialDying = 1 + WoundedValue;
                        if (finalDamageEvent.IsCriticalHit)
                            initialDying += 1; // Crits cause Dying 2
                        SetDying(initialDying);
                    }
                    else if (currentHealth == 0 && DyingValue > 0)
                    {
                        // Taking damage while already dying increases dying value
                        SetDying(DyingValue + (finalDamageEvent.IsCriticalHit ? 2 : 1));
                    }

                    // TODO: Fire the AfterDamageEvent if we add Spiky Armor/Retaliation
                    // ReactionManager.Instance.EvaluateEvent(new AfterDamageEvent(source, thisUnit, finalDamageEvent.DamageAmount), (_) => {});
                }
            );
        }

        public void ApplyHealing(int amount)
        {
            if (IsDead)
                return;

            if (IsUnconscious && amount > 0)
            {
                // Waking up resets Dying and increases Wounded
                DyingValue = 0;
                WoundedValue++;
                OnStatusMessage?.Invoke(this, $"Woke Up! Wounded {WoundedValue}");
            }

            currentHealth += amount;
            currentHealth = Mathf.Min(currentHealth, maxHealth);
            OnHealthChanged?.Invoke(this, EventArgs.Empty);
        }

        private void SetDying(int newValue)
        {
            DyingValue = newValue;

            if (DyingValue >= 4) // Standard death threshold
            {
                IsDead = true;
                OnStatusMessage?.Invoke(this, "DEAD");
                OnDeath?.Invoke(this, EventArgs.Empty);
                // TODO: Play death animations, disable colliders, etc.
            }
            else
            {
                OnStatusMessage?.Invoke(this, $"Dying {DyingValue}");
            }
        }

        public void RollRecoveryCheck()
        {
            if (!IsUnconscious || IsDead)
                return;

            int d20 = UnityEngine.Random.Range(1, 21);
            int dc = 10 + DyingValue; // Recovery DC is usually 10 + current dying value (unless forced by an effect)

            // Calculate degrees of success
            Degree result = PF2E_Core.CheckResult(d20, 0, dc);

            Debug.Log($"[Recovery Check] Rolled {d20} vs DC {dc}. Result: {result}");

            switch (result)
            {
                case Degree.CriticalSuccess:
                    SetDying(DyingValue - 2);
                    break;
                case Degree.Success:
                    SetDying(DyingValue - 1);
                    break;
                case Degree.Failure:
                    SetDying(DyingValue + 1);
                    break;
                case Degree.CriticalFailure:
                    SetDying(DyingValue + 2);
                    break;
            }

            // If dying drops below 1, they stabilize (but remain at 0 HP / unconscious)
            if (DyingValue <= 0 && !IsDead)
            {
                DyingValue = 0;
                WoundedValue++;
                OnStatusMessage?.Invoke(this, $"Stabilized! Wounded {WoundedValue}");
            }
        }

        // Getters for UI
        public int GetCurrentHealth() => currentHealth;

        public int GetMaxHealth() => maxHealth;
    }
}
