using System.Collections.Generic;
using UnityEngine;

namespace TacticsGame.Data.TacticsCore
{
    [CreateAssetMenu(menuName = "Tactics Core/Data/Class")]
    public class TacticsClassSO : TacticsGameElementSO
    {
        [Header("Base Statistics")]
        public int HitPointsPerLevel;
        public List<AttributeType> KeyAttributes = new List<AttributeType>();

        [Header("Initial Proficiencies")]
        public ProficiencyRank Perception;
        public ProficiencyRank ClassDifficulty;
        public ProficiencyRank SpellDifficulty;
        public ProficiencyRank Spellcasting;
        public List<SaveProficiencyEntry> SavingThrows = new List<SaveProficiencyEntry>();
        public List<ArmorProficiencyEntry> ArmorProficiencies = new List<ArmorProficiencyEntry>();
        public List<WeaponProficiencyEntry> WeaponProficiencies =
            new List<WeaponProficiencyEntry>();
        public List<SkillTrainingEntry> TrainedSkills = new List<SkillTrainingEntry>();
        public int AdditionalSkillCount;

        [Header("Progression")]
        public List<int> AncestryFeatureLevels = new List<int>();
        public List<int> ClassFeatureLevels = new List<int>();
        public List<int> SkillFeatureLevels = new List<int>();
        public List<int> GeneralFeatureLevels = new List<int>();
        public List<int> SkillIncreaseLevels = new List<int>();

        [Header("Features")]
        public bool HasSpellcasting;
        public int StartingFocusPoints;
        public List<FeatureGrant> LevelOneFeatures = new List<FeatureGrant>();
        public List<FeatureGrant> GrantedFeatures = new List<FeatureGrant>();
    }
}
