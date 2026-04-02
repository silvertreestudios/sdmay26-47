using UnityEngine;

namespace PathfinderTactics.Items
{
    public enum ArmorCategory
    {
        Unarmored,
        Light,
        Medium,
        Heavy,
    }

    [CreateAssetMenu(menuName = "PathfinderTactics/Items/Armor")]
    public class ArmorSO : EquipmentSO
    {
        [Header("Armor Properties")]
        public ArmorCategory category;
        public int acBonus = 1;
        public int dexCap = 3;

        [Header("Penalties (Require Strength to mitigate)")]
        [Tooltip(
            "Strength requirement to ignore check/speed penalties (e.g., 14 means 14 Strength)."
        )]
        public int strengthRequirement = 10;
        public int checkPenalty = 0; // Negative value like -1
        public int speedPenaltyFeet = 0; // Negative value like -5
    }
}
