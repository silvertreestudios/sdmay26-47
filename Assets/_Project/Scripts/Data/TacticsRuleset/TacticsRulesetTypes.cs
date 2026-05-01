using System;
using UnityEngine;

namespace TacticsGame.Data.TacticsRuleset
{
    public enum ActionCost
    {
        Free,
        Reaction,
        One,
        Two,
        Three,
        Variable,
    }

    [Serializable]
    public struct DiceFormula
    {
        public int DiceCount;
        public int DiceSize;
        public int Bonus;

        public DiceFormula(int count, int size, int bonus = 0)
        {
            DiceCount = count;
            DiceSize = size;
            Bonus = bonus;
        }

        public override string ToString()
        {
            if (Bonus > 0)
                return $"{DiceCount}d{DiceSize} + {Bonus}";
            else if (Bonus < 0)
                return $"{DiceCount}d{DiceSize} - {Mathf.Abs(Bonus)}";
            else
                return $"{DiceCount}d{DiceSize}";
        }
    }

    public enum AreaShape
    {
        None,
        Burst,
        Cone,
        Line,
        Emanation,
    }

    [Serializable]
    public struct AreaDefinition
    {
        public AreaShape Shape;
        public int Radius;
    }

    public enum TargetType
    {
        Self,
        Ally,
        Enemy,
        Creature,
        Object,
        Tile,
        Area,
    }

    public enum SavingThrowType
    {
        None,
        Fortitude,
        Reflex,
        Will,
    }

    public enum AbilityType
    {
        Free, // Free boost - player chooses
        Str,
        Dex,
        Con,
        Int,
        Wis,
        Cha,
    }

    public enum CreatureSize
    {
        Tiny,
        Small,
        Medium,
        Large,
        Huge,
        Gargantuan,
    }
}
