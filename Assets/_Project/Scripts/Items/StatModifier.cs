using UnityEngine;

namespace PathfinderTactics.Items
{
    public enum ModifierType
    {
        Item,
        Status,
        Circumstance,
        Untyped,
    }

    public enum StatType
    {
        ArmorClass,
        AttackBonus,
        Speed,
        Perception,
        Strength,
        Dexterity,
        Constitution,
        Intelligence,
        Wisdom,
        Charisma,
        FortitudeSave,
        ReflexSave,
        WillSave,
        MaxHP,
    }

    [System.Serializable]
    public struct StatModifier
    {
        public StatType statType;
        public int value;
        public ModifierType modifierType;
    }
}
