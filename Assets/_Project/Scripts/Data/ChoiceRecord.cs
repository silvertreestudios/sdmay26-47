using System;
using TacticsGame.Core;

namespace TacticsGame.Data
{
    [Serializable]
    public class ChoiceRecord
    {
        public ChoiceType Type; // Enum: AttributeBoost, AttributeFlaw, Feat, SkillIncrease, Language, etc.
        public string SourceID; // The origin of the choice (e.g., "human_ancestry", "level_1_background")
        public int Level; // The character level this choice was made (e.g., 1)
        public string SelectedValue; // The ID of the selection (e.g., "Strength", "feat_nimble_dodge", "Acrobatics")
        public bool IsInvalid; // Used for UI soft-validation
    }
}
