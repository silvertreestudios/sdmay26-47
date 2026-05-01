using System;
using System.Collections.Generic;

namespace TacticsGame.Data.TacticsCore
{
    public enum AttributeType
    {
        Any,
        Strength,
        Dexterity,
        Constitution,
        Intelligence,
        Wisdom,
        Charisma,
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

    public enum SenseType
    {
        Normal,
        LowLight,
        Darkvision,
        Other,
    }

    public enum ProficiencyRank
    {
        Untrained = 0,
        Trained = 1,
        Expert = 2,
        Master = 3,
        Legendary = 4,
    }

    public enum SkillType
    {
        Custom,
        Acrobatics,
        Arcana,
        Athletics,
        Crafting,
        Deception,
        Diplomacy,
        Intimidation,
        Lore,
        Medicine,
        Nature,
        Occultism,
        Performance,
        Religion,
        Society,
        Stealth,
        Survival,
        Thievery,
    }

    public enum DefenseSaveType
    {
        Fortitude,
        Reflex,
        Will,
    }

    public enum ArmorGroup
    {
        Unarmored,
        Light,
        Medium,
        Heavy,
    }

    public enum WeaponGroup
    {
        Unarmed,
        Simple,
        Martial,
        Advanced,
        Special,
    }

    public enum FeatureCategory
    {
        Unknown,
        AncestryFeature,
        ClassFeature,
        GeneralFeat,
        SkillFeat,
        GrantedItem,
    }

    [Serializable]
    public class AttributeChoiceSet
    {
        public List<AttributeType> Options = new List<AttributeType>();

        public bool IsFreeChoice =>
            Options.Count == 0 || Options.Contains(AttributeType.Any) || Options.Count >= 6;
    }

    [Serializable]
    public class LanguageEntry
    {
        public string Id;
        public string DisplayName;
    }

    [Serializable]
    public class SkillTrainingEntry
    {
        public SkillType Skill;
        public string CustomSkillId;
        public string LoreName;
        public ProficiencyRank Rank = ProficiencyRank.Trained;
    }

    [Serializable]
    public class SaveProficiencyEntry
    {
        public DefenseSaveType Save;
        public ProficiencyRank Rank;
    }

    [Serializable]
    public class ArmorProficiencyEntry
    {
        public ArmorGroup Group;
        public ProficiencyRank Rank;
    }

    [Serializable]
    public class WeaponProficiencyEntry
    {
        public WeaponGroup Group;
        public string CustomGroupName;
        public ProficiencyRank Rank;
    }

    [Serializable]
    public class FeatureGrant
    {
        public string SourceKey;
        public string SourceUuid;
        public string SourceId;
        public string DisplayName;
        public int Level;
        public FeatureDataSO ResolvedFeature;
    }

    [Serializable]
    public class PackReference
    {
        public string SourceUuid;
        public string SourceId;
        public string Slug;
        public string DisplayName;
    }

    [Serializable]
    public class RawRuleEntry
    {
        public string Key;
        public string Path;
        public string Mode;
        public string Value;
        public string Uuid;
        public string Json;
    }

    [Serializable]
    public class ProficiencyUpgrade
    {
        public string Target;
        public ProficiencyRank Rank;
    }
}
