using UnityEngine;

namespace TacticsGame.Items
{
    public abstract class ItemSO : ScriptableObject
    {
        [Header("Item Details")]
        public string itemName;

        [TextArea(3, 10)]
        public string description;
        public Sprite icon;
        public GameObject prefab; // Visual model for the item

        [Header("Item Properties")]
        public int level = 0;
        public int priceInCopper = 0; // Standardize price as copper for math
        public string bulk = "L"; // Empty, L, 1, 2, etc. (Can be made an enum later)
    }
}
