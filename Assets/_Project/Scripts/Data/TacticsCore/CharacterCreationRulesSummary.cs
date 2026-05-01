using System;
using System.Collections.Generic;
using System.Linq;
using TacticsGame.Data.TacticsRuleset;
using TacticsGame.Items;

namespace TacticsGame.Data.TacticsCore
{
    public class CharacterCreationRulesSummary
    {
        public readonly Dictionary<AttributeType, int> AttributeModifiers =
            new Dictionary<AttributeType, int>();
        public readonly Dictionary<SkillType, ProficiencyRank> SkillProficiencies =
            new Dictionary<SkillType, ProficiencyRank>();
        public readonly Dictionary<DefenseSaveType, ProficiencyRank> SavingThrows =
            new Dictionary<DefenseSaveType, ProficiencyRank>();
        public readonly Dictionary<ArmorGroup, ProficiencyRank> ArmorProficiencies =
            new Dictionary<ArmorGroup, ProficiencyRank>();
        public readonly Dictionary<WeaponGroup, ProficiencyRank> WeaponProficiencies =
            new Dictionary<WeaponGroup, ProficiencyRank>();
        public readonly List<string> Traits = new List<string>();
        public readonly List<string> Languages = new List<string>();
        public readonly List<string> Features = new List<string>();

        public int HitPoints;
        public int Speed;
        public int ClassDC;
        public int FocusPoints;
        public int PerceptionModifier;
        public int SpellAttackModifier;
        public int SpellDC;
        public CreatureSize Size = CreatureSize.Medium;
        public SenseType Sense = SenseType.Normal;

        public int GetAttributeModifier(AttributeType attribute) =>
            AttributeModifiers.TryGetValue(attribute, out int value) ? value : 0;

        public ProficiencyRank GetSkillProficiency(SkillType skill) =>
            SkillProficiencies.TryGetValue(skill, out ProficiencyRank rank)
                ? rank
                : ProficiencyRank.Untrained;

        public ProficiencyRank GetSaveProficiency(DefenseSaveType save) =>
            SavingThrows.TryGetValue(save, out ProficiencyRank rank)
                ? rank
                : ProficiencyRank.Untrained;
    }

    public static class CharacterCreationRules
    {
        private static readonly AttributeType[] CoreAttributes =
        {
            AttributeType.Strength,
            AttributeType.Dexterity,
            AttributeType.Constitution,
            AttributeType.Intelligence,
            AttributeType.Wisdom,
            AttributeType.Charisma,
        };

        public static CharacterCreationRulesSummary BuildSummary(
            CharacterDataPayload payload,
            TacticsRulesetDatabase database
        )
        {
            var summary = new CharacterCreationRulesSummary();
            foreach (AttributeType attribute in CoreAttributes)
                summary.AttributeModifiers[attribute] = 0;

            if (payload == null)
                return summary;

            AncestryDataSO ancestry = database?.GetCoreAncestry(payload.AncestryID);
            HeritageDataSO heritage = database?.GetCoreHeritage(payload.HeritageID);
            BackgroundDataSO background = database?.GetCoreBackground(payload.BackgroundID);
            TacticsClassSO characterClass = database?.GetCoreClass(payload.ClassID);

            ApplyAncestry(summary, ancestry);
            ApplyHeritage(summary, heritage);
            ApplyBackground(summary, background);
            ApplyClass(summary, characterClass);

            ApplyBoosts(summary, payload.AncestryBoosts, 1);
            ApplyBoosts(summary, payload.AncestryFlaws, -1);
            ApplyBoosts(summary, payload.BackgroundBoosts, 1);
            ApplyBoosts(summary, payload.FreeBoosts, 1);
            ApplyBoost(summary, ParseAttribute(payload.ClassKeyAttribute), 1);

            foreach (string skill in payload.TrainedSkills)
                ApplySkill(summary, ParseSkill(skill), ProficiencyRank.Trained);

            foreach (string language in payload.Languages)
                AddUnique(summary.Languages, language);

            foreach (string feature in payload.FeatureIDs)
                AddUnique(summary.Features, feature);

            ApplyEquipment(summary, payload, database);
            FinalizeLevelOneStats(summary, payload.Level);
            return summary;
        }

        public static AttributeType ParseAttribute(string value)
        {
            if (Enum.TryParse(value, true, out AttributeType attribute))
                return attribute;

            switch (value)
            {
                case "STR":
                    return AttributeType.Strength;
                case "DEX":
                    return AttributeType.Dexterity;
                case "CON":
                    return AttributeType.Constitution;
                case "INT":
                    return AttributeType.Intelligence;
                case "WIS":
                    return AttributeType.Wisdom;
                case "CHA":
                    return AttributeType.Charisma;
                default:
                    return AttributeType.Any;
            }
        }

        public static SkillType ParseSkill(string value)
        {
            if (Enum.TryParse(value, true, out SkillType skill))
                return skill;
            return SkillType.Custom;
        }

        private static void ApplyAncestry(
            CharacterCreationRulesSummary summary,
            AncestryDataSO ancestry
        )
        {
            if (ancestry == null)
                return;

            summary.HitPoints += ancestry.HitPoints;
            summary.Speed = ancestry.Speed;
            summary.Size = ancestry.Size;
            summary.Sense = ancestry.Sense;
            AddRangeUnique(summary.Traits, ancestry.Traits);
            AddRangeUnique(
                summary.Languages,
                ancestry.StartingLanguages.Select(language => language.DisplayName)
            );
            AddRangeUnique(
                summary.Features,
                ancestry.GrantedFeatures.Select(feature => feature.DisplayName)
            );
        }

        private static void ApplyHeritage(
            CharacterCreationRulesSummary summary,
            HeritageDataSO heritage
        )
        {
            if (heritage == null)
                return;

            AddRangeUnique(summary.Traits, heritage.Traits);
            AddRangeUnique(
                summary.Features,
                heritage.GrantedFeatures.Select(feature => feature.DisplayName)
            );
        }

        private static void ApplyBackground(
            CharacterCreationRulesSummary summary,
            BackgroundDataSO background
        )
        {
            if (background == null)
                return;

            AddRangeUnique(summary.Traits, background.Traits);
            foreach (SkillTrainingEntry skill in background.TrainedSkills)
                ApplySkill(summary, skill.Skill, skill.Rank);
            foreach (string lore in background.LoreSkills)
                AddUnique(summary.Features, $"{lore} Lore");
            AddRangeUnique(
                summary.Features,
                background.GrantedFeats.Select(feature => feature.DisplayName)
            );
        }

        private static void ApplyClass(
            CharacterCreationRulesSummary summary,
            TacticsClassSO characterClass
        )
        {
            if (characterClass == null)
                return;

            summary.HitPoints += characterClass.HitPointsPerLevel;
            summary.FocusPoints += characterClass.StartingFocusPoints;
            AddRangeUnique(summary.Traits, characterClass.Traits);
            AddRangeUnique(
                summary.Features,
                characterClass.LevelOneFeatures.Select(feature => feature.DisplayName)
            );
            AddRangeUnique(
                summary.Features,
                characterClass
                    .GrantedFeatures.Where(feature => feature.Level <= 1)
                    .Select(feature => feature.DisplayName)
            );

            foreach (SkillTrainingEntry skill in characterClass.TrainedSkills)
                ApplySkill(summary, skill.Skill, skill.Rank);
            foreach (SaveProficiencyEntry save in characterClass.SavingThrows)
                summary.SavingThrows[save.Save] = save.Rank;
            foreach (ArmorProficiencyEntry armor in characterClass.ArmorProficiencies)
                summary.ArmorProficiencies[armor.Group] = armor.Rank;
            foreach (WeaponProficiencyEntry weapon in characterClass.WeaponProficiencies)
                summary.WeaponProficiencies[weapon.Group] = weapon.Rank;

            summary.ClassDC = 10 + ToNumericRank(characterClass.ClassDifficulty);
            summary.SpellAttackModifier = ToNumericRank(characterClass.Spellcasting);
            summary.SpellDC = 10 + ToNumericRank(characterClass.SpellDifficulty);
        }

        private static void ApplyEquipment(
            CharacterCreationRulesSummary summary,
            CharacterDataPayload payload,
            TacticsRulesetDatabase database
        )
        {
            ArmorSO armor = database?.AllArmor.FirstOrDefault(item =>
                item != null && MatchesItemId(item, payload.ArmorID)
            );
            if (armor != null)
                summary.Speed += armor.speedPenaltyFeet;
        }

        private static void FinalizeLevelOneStats(CharacterCreationRulesSummary summary, int level)
        {
            int conMod = summary.GetAttributeModifier(AttributeType.Constitution);
            summary.HitPoints += Math.Max(1, level) * conMod;
            summary.HitPoints = Math.Max(1, summary.HitPoints);
            summary.FocusPoints = Math.Min(3, Math.Max(0, summary.FocusPoints));

            ProficiencyRank perception = ProficiencyRank.Untrained;
            summary.PerceptionModifier =
                summary.GetAttributeModifier(AttributeType.Wisdom) + ToNumericRank(perception);
        }

        private static void ApplyBoosts(
            CharacterCreationRulesSummary summary,
            IEnumerable<string> values,
            int delta
        )
        {
            if (values == null)
                return;

            foreach (string value in values)
                ApplyBoost(summary, ParseAttribute(value), delta);
        }

        private static void ApplyBoost(
            CharacterCreationRulesSummary summary,
            AttributeType attribute,
            int delta
        )
        {
            if (
                attribute == AttributeType.Any
                || !summary.AttributeModifiers.ContainsKey(attribute)
            )
                return;

            summary.AttributeModifiers[attribute] += delta;
            if (summary.AttributeModifiers[attribute] > 4)
                summary.AttributeModifiers[attribute] = 4;
        }

        private static void ApplySkill(
            CharacterCreationRulesSummary summary,
            SkillType skill,
            ProficiencyRank rank
        )
        {
            if (skill == SkillType.Custom)
                return;

            if (
                !summary.SkillProficiencies.TryGetValue(skill, out ProficiencyRank current)
                || rank > current
            )
                summary.SkillProficiencies[skill] = rank;
        }

        private static int ToNumericRank(ProficiencyRank rank)
        {
            switch (rank)
            {
                case ProficiencyRank.Trained:
                    return 3;
                case ProficiencyRank.Expert:
                    return 5;
                case ProficiencyRank.Master:
                    return 7;
                case ProficiencyRank.Legendary:
                    return 9;
                default:
                    return 0;
            }
        }

        private static bool MatchesItemId(ItemSO item, string id)
        {
            if (item == null || string.IsNullOrEmpty(id))
                return false;

            return item.name == id || item.itemName == id;
        }

        private static void AddRangeUnique(List<string> target, IEnumerable<string> values)
        {
            if (target == null || values == null)
                return;

            foreach (string value in values)
                AddUnique(target, value);
        }

        private static void AddUnique(List<string> target, string value)
        {
            if (string.IsNullOrWhiteSpace(value) || target.Contains(value))
                return;

            target.Add(value);
        }
    }
}
