using TacticsGame.Characters;
using TacticsGame.Core;
using UnityEngine;

namespace TacticsGame.ScriptableObjects
{
    [CreateAssetMenu(fileName = "New Aura", menuName = "TacticsGame/Aura Effect")]
    public class AuraEffectSO : ScriptableObject
    {
        [Header("General Settings")]
        public string auraName;
        public int radiusInTiles = 1;
        public Color auraColor = Color.cyan;
        public bool isHarmful = false;

        [Header("Targeting")]
        public AuraTargetAlignment alignment;
        public bool affectSelf = false;

        [Header("Logic")]
        public AuraTriggerType triggerType = AuraTriggerType.OnEnter;
        public bool oncePerTurn = true;

        [Header("Effect")]
        public AuraEffectType effectType;
        public AuraConditionData condition;
        public AuraDamageData damage;

        public bool ShouldAffect(Unit emitter, Unit target)
        {
            if (emitter == target)
                return affectSelf;

            switch (alignment)
            {
                case AuraTargetAlignment.Allies:
                    return emitter.GetFaction() == target.GetFaction();
                case AuraTargetAlignment.Enemies:
                    return emitter.GetFaction() != target.GetFaction();
                case AuraTargetAlignment.All:
                    return true;
                case AuraTargetAlignment.SelfExcluded:
                    return emitter != target;
                default:
                    return false;
            }
        }
    }
}
