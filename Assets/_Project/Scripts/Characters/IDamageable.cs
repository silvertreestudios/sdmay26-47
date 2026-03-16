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
        /// <param name="source">The entity that dealt the damage (can be null for environmental sources).</param>
        /// <param name="amount">The raw damage amount before mitigation.</param>
        /// <param name="isCriticalHit">True if the damage resulted from a critical hit.</param>
        void ApplyDamage(Unit source, int amount, bool isCriticalHit = false);

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
