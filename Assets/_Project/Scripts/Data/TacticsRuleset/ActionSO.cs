using UnityEngine;

namespace TacticsGame.Data.TacticsRuleset
{
    [CreateAssetMenu(menuName = "TacticsRuleset/Action")]
    public class ActionSO : GameElementSO
    {
        [Header("Action Rules")]
        public string ActionType; // "action", "passive", "free", "reaction"
        public int ActionCount; // 1, 2, or 3
        public string Category; // "basic", "skill", "class", "exploration", etc.

        [Header("Action Conditions")]
        [TextArea(2, 4)]
        public string Trigger;

        [TextArea(2, 4)]
        public string Requirements;
        public string Frequency;
    }
}
