using UnityEngine;

namespace TacticsGame.Characters
{
    [System.Serializable]
    public class ActiveCondition
    {
        public ConditionType Type;
        public int Value;
        public Unit Source;
        public ActionTag QuickenedRestriction; // Tells the Action System what is allowed

        public ActiveCondition(
            ConditionType type,
            int value,
            Unit source,
            ActionTag restriction = ActionTag.None
        )
        {
            Type = type;
            Value = value;
            Source = source;
            QuickenedRestriction = restriction;
        }
    }

    [System.Serializable]
    public class PersistentDamageInstance
    {
        public DamageType Type;
        public int DiceFaces; // e.g., 6 for d6
        public int DiceCount; // e.g., 2 for 2d6
        public int FlatDamage;
        public int RecoveryDC = 15; // Default PF2e flat check
        public Unit Source;

        public int RollDamage()
        {
            int total = FlatDamage;
            for (int i = 0; i < DiceCount; i++)
            {
                total += Random.Range(1, DiceFaces + 1);
            }
            return total;
        }
    }
}
