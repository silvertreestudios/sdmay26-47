using UnityEngine;

namespace PathfinderTactics.Characters
{
    [CreateAssetMenu(fileName = "NewUnitStats", menuName = "PathfinderTactics/Unit Stats")]
    public class UnitStatsSO : ScriptableObject
    {
        [Header("Identity")]
        public string unitName = "Unit";

        [Header("Core Stats (Pathfinder 2e)")]
        [Tooltip("Speed in feet. Standard is 25 or 30 for most humanoids.")]
        public int speedInFeet = 30;

        // TODO: Add many more stats here later (HP, Dying Level, Wounded Level,
        // Resistances, Weaknesses, Immunities, etc.)
    }
}
