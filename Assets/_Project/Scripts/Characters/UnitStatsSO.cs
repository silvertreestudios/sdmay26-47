using System.Collections.Generic;
using TacticsGame.Core;
using UnityEngine;

public class ClassData
{
    public DefenseValues defenses;
}

[System.Serializable]
public class DefenseValues
{
    public int heavy;
    public int light;
    public int medium;
    public int unarmored;
}

namespace TacticsGame.Characters
{
    public enum UnitSize
    {
        Tiny = 0,
        Small = 1,
        Medium = 2,
        Large = 3,
        Huge = 4,
        Gargantuan = 5,
    }

    [System.Serializable]
    public class RWIModifier
    {
        public DamageType Type;
        public int Value;
    }

    [System.Serializable]
    public class RWIProfile
    {
        public List<DamageType> Immunities = new List<DamageType>();
        public List<RWIModifier> Weaknesses = new List<RWIModifier>();
        public List<RWIModifier> Resistances = new List<RWIModifier>();
    }

    [CreateAssetMenu(fileName = "NewUnitStats", menuName = "TacticsGame/Unit Stats")]
    public class UnitStatsSO : ScriptableObject, IUnitDataProvider
    {
        [Header("Identity")]
        public string unitName = "Unit";
        public Sprite portraitIcon;
        public UnitSize unitSize = UnitSize.Medium;

        [Header("Skills & Senses")]
        [Tooltip("Perception modifier for Initiative rolls.")]
        public int perception = 0;

        [Tooltip(
            "Stealth modifier for stealth checks (Dex-based). If left 0, defaults to Dex modifier."
        )]
        public int stealth = 0;

        [Header("PF2e Special Abilities")]
        [Tooltip("Immune to the Off-Guard penalty from flanking.")]
        public bool hasAllAroundVision = false;

        [Tooltip("Immune to flanking from creatures of equal or lower level.")]
        public bool hasDenyAdvantage = false;

        [Header("Ability Scores")]
        [Tooltip("Strength: physical power (e.g. 18).")]
        public int strength = 18;

        [Tooltip("Dexterity: agility and reflexes (e.g. 14).")]
        public int dexterity = 14;

        [Tooltip("Constitution: health and stamina (e.g. 14).")]
        public int constitution = 14;

        [Tooltip("Intelligence: reasoning and knowledge (e.g. 10).")]
        public int intelligence = 10;

        [Tooltip("Wisdom: perception and willpower (e.g. 12).")]
        public int wisdom = 12;

        [Tooltip("Charisma: presence and personality (e.g. 10).")]
        public int charisma = 10;

        [Header("Key Ability")]
        [Tooltip("The primary attribute used for this unit's Class DC and Spell DC.")]
        public AbilityScore keyAbility = AbilityScore.STR;

        [Header("Core Stats (PF2e)")]
        [Tooltip("Speed in feet. Standard is 25 or 30 for most humanoids.")]
        public int baseSpeedInFeet = 30;

        [Header("Ancestry & Class (Resources)")]
        [Tooltip(
            "Resource path (relative to Resources/) to the ancestry JSON. Example: JSON/ancestries/human"
        )]
        public string ancestryResourcePath = "JSON/ancestries/human";

        [Tooltip(
            "Resource path (relative to Resources/) to the class JSON. Example: JSON/classes/fighter"
        )]
        public string classResourcePath = "JSON/classes/fighter";

        [Tooltip("Character level (minimum 1).")]
        public int level = 1;

        /// <summary>
        /// Ancestry HP value read from the ancestry JSON (or 0 if unavailable).
        /// </summary>
        public int AncestryHP => GetAncestryHp();

        /// <summary>
        /// Class HP (per level) read from the class JSON (or 0 if unavailable).
        /// </summary>
        public int ClassHP => GetClassHp();

        /// <summary>
        /// Total maximum HP following PF2e rules:
        /// Level 1: ancestryHp + classHp + constitutionModifier
        /// Each additional level: + (classHp + constitutionModifier)
        /// </summary>
        public int TotalHP
        {
            get
            {
                int aHp = GetAncestryHp();
                int cHp = GetClassHp();
                int conMod = TacticsRuleset_Core.GetAbilityModifier(constitution);
                int lvl = Mathf.Max(1, level);

                // Level 1: Ancestry + Class + ConMod
                int total = aHp + cHp + conMod;

                // Each additional level: Class + ConMod
                if (lvl > 1)
                {
                    total += (lvl - 1) * (cHp + conMod);
                }
                return total;
            }
        }

        private int GetAncestryHp()
        {
            if (string.IsNullOrEmpty(ancestryResourcePath))
                return 0;
            var ta = Resources.Load<TextAsset>(ancestryResourcePath);
            if (ta == null)
                return 0;
            try
            {
                var data = JsonUtility.FromJson<AncestryJson>(ta.text);
                return (data != null && data.system != null) ? data.system.hp : 0;
            }
            catch
            {
                return 0;
            }
        }

        private int GetClassHp()
        {
            if (string.IsNullOrEmpty(classResourcePath))
                return 0;
            var ta = Resources.Load<TextAsset>(classResourcePath);
            if (ta == null)
                return 0;
            try
            {
                var data = JsonUtility.FromJson<ClassJson>(ta.text);
                return (data != null && data.system != null) ? data.system.hp : 0;
            }
            catch
            {
                return 0;
            }
        }

        [System.Serializable]
        private class AncestryJson
        {
            public AncestrySystem system;
        }

        [System.Serializable]
        private class AncestrySystem
        {
            public int hp;
        }

        [System.Serializable]
        private class ClassJson
        {
            public ClassSystem system;
        }

        [System.Serializable]
        private class ClassSystem
        {
            public int hp;
        }

        [Header("Defenses")]
        public RWIProfile rwiProfile = new RWIProfile();

        [Header("Traits")]
        public List<string> traits = new List<string>();

        private void OnValidate()
        {
            if (level < 1)
                level = 1;
        }

        // IUnitDataProvider Implementation
        public string GetUnitName() => unitName;

        public UnityEngine.Sprite GetPortraitIcon() => portraitIcon;

        public UnitSize GetSize() => unitSize;

        public int GetLevel() => level;

        public string GetHeritageID() => "";

        public string GetIdentity() => "";

        public string GetPronouns() => "";

        public string GetDeity() => "";

        public List<string> GetEdicts() => new List<string>();

        public List<string> GetAnathema() => new List<string>();

        public int GetAge() => 0;

        public List<Data.ChoiceRecord> GetLedger() => new List<Data.ChoiceRecord>();

        public List<Data.SpellSelection> GetSpellLedger() => new List<Data.SpellSelection>();

        public Dictionary<string, string> GetEquippedFeats() => new Dictionary<string, string>();

        public int GetMaxHP(Data.TacticsRuleset.TacticsRulesetDatabase db) => TotalHP;

        public int GetSpeed() => baseSpeedInFeet;

        public int GetMaxFocusPoints() => 0;

        public int GetClassDC() => 10 + GetSpellAttackModifier();

        public int GetSavingThrow(Data.TacticsRuleset.SavingThrowType save)
        {
            // Simplified logic for non-player units
            AbilityScore ability;
            switch (save)
            {
                case Data.TacticsRuleset.SavingThrowType.Fortitude:
                    ability = AbilityScore.CON;
                    break;
                case Data.TacticsRuleset.SavingThrowType.Reflex:
                    ability = AbilityScore.DEX;
                    break;
                case Data.TacticsRuleset.SavingThrowType.Will:
                    ability = AbilityScore.WIS;
                    break;
                default:
                    return 0;
            }

            return TacticsRuleset_Core.CalculateModifier(
                level,
                Proficiency.Trained,
                TacticsRuleset_Core.GetAbilityModifier(GetStatValue(ability))
            );
        }

        public int GetPerceptionModifier() => perception;

        public int GetSpellAttackModifier()
        {
            return TacticsRuleset_Core.CalculateModifier(
                level,
                Proficiency.Trained,
                TacticsRuleset_Core.GetAbilityModifier(GetStatValue(keyAbility))
            );
        }

        public int GetSpellDC() => 10 + GetSpellAttackModifier();

        private int GetStatValue(AbilityScore score)
        {
            switch (score)
            {
                case AbilityScore.STR:
                    return strength;
                case AbilityScore.DEX:
                    return dexterity;
                case AbilityScore.CON:
                    return constitution;
                case AbilityScore.INT:
                    return intelligence;
                case AbilityScore.WIS:
                    return wisdom;
                case AbilityScore.CHA:
                    return charisma;
                default:
                    return 10;
            }
        }

        public int GetMaxBulk() => 5 + TacticsRuleset_Core.GetAbilityModifier(strength);

        public Proficiency GetSkillProficiency(SkillType skill) => Proficiency.Untrained;

        public Proficiency GetLoreProficiency(string loreName) => Proficiency.Untrained;

        public List<string> GetKnownLanguages() => new List<string>();

        public List<SenseType> GetSenses() => new List<SenseType>();

        public int GetStealth() => stealth;

        public bool HasAllAroundVision() => hasAllAroundVision;

        public bool HasDenyAdvantage() => hasDenyAdvantage;

        public int GetStrength() => TacticsRuleset_Core.GetAbilityModifier(strength);

        public int GetDexterity() => TacticsRuleset_Core.GetAbilityModifier(dexterity);

        public int GetConstitution() => TacticsRuleset_Core.GetAbilityModifier(constitution);

        public int GetIntelligence() => TacticsRuleset_Core.GetAbilityModifier(intelligence);

        public int GetWisdom() => TacticsRuleset_Core.GetAbilityModifier(wisdom);

        public int GetCharisma() => TacticsRuleset_Core.GetAbilityModifier(charisma);

        public RWIProfile GetRWIProfile() => rwiProfile;

        public List<string> GetTraits() => traits;
    }
}
