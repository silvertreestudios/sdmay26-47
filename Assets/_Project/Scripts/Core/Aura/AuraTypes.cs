using System;
using TacticsGame.Characters;
using TacticsGame.Core;
using UnityEngine;

namespace TacticsGame.Core
{
    public enum AuraTargetAlignment
    {
        Allies,
        Enemies,
        All,
        SelfExcluded,
    }

    public enum AuraTriggerType
    {
        OnEnter,
        OnExit,
        OnStartTurn,
        Both, // Enter and Exit
    }

    public enum AuraEffectType
    {
        ApplyCondition,
        DealDamage,
    }

    [Serializable]
    public struct AuraDamageData
    {
        public DamageType damageType;
        public int diceCount;
        public int diceSides;
        public int flatBonus;
    }

    [Serializable]
    public struct AuraConditionData
    {
        public ConditionType conditionType;
        public int value;
    }
}
