using System;
using PathfinderTactics.Core;
using UnityEngine;

namespace PathfinderTactics.Characters
{
    /// <summary>
    /// Represents an entity that can take damage or be healed.
    /// Used to decouple combat mechanics from concrete implementations like UnitHealth.
    /// </summary>
    public interface IDamageable
    {
        bool IsDead { get; }
        int GetCurrentHealth();
        int GetMaxHealth();

        /// <summary>
        /// Applies damage to this entity.
        /// </summary>
        /// <param name="type">The type of damage being dealt (Piercing, Slashing, Fire, etc.).</param>
        void ApplyDamage(Unit source, int amount, DamageType type, bool isCriticalHit = false);

        /// <summary>
        /// Applies healing to this entity.
        /// </summary>
        /// <param name="amount">The amount to heal.</param>
        void ApplyHealing(int amount);

        event EventHandler OnDeath;
        event EventHandler OnHealthChanged;
        event EventHandler<string> OnStatusMessage;
    }
}
