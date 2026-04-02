using UnityEngine;

namespace PathfinderTactics.Items
{
    [CreateAssetMenu(menuName = "PathfinderTactics/Items/Shield")]
    public class ShieldSO : EquipmentSO
    {
        [Header("Shield Defense")]
        public int acBonus = 2;
        public int hardness = 3;
        public int hp = 12;
        public int brokenThreshold = 6;

        [Tooltip("If checked, this shield can be used to perform Shield Block reactions.")]
        public bool blockable = true;
    }
}
