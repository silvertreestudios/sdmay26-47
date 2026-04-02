using System;

namespace PathfinderTactics.Items
{
    [Serializable]
    public class ItemInstance
    {
        public ItemSO item;
        public int quantity;

        public ItemInstance(ItemSO item, int quantity = 1)
        {
            this.item = item;
            this.quantity = quantity;
        }
    }
}
