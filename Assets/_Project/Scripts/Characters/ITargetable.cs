using PathfinderTactics.Core;
using UnityEngine;

namespace PathfinderTactics.Characters
{
    /// <summary>
    /// Represents an entity that can be targeted by attacks, spells, or effects.
    /// Used to decouple targeting logic from concrete implementations like Unit.
    /// </summary>
    public interface ITargetable
    {
        Faction GetFaction();

        /// <summary>
        /// Calculates the Armor Class (AC) of this entity against a specific attack type.
        /// </summary>
        int GetArmorClass(Unit attacker = null, AttackType incomingAttackType = AttackType.Melee);

        /// <summary>
        /// Returns true if this entity is an enemy to the specified unit.
        /// </summary>
        bool IsEnemy(Unit otherUnit);

        Transform Transform { get; }
    }
}
