using System;
using System.Collections.Generic;
using TacticsGame.Characters;
using TacticsGame.Core;
using TacticsGame.Data.TacticsRuleset;
using TacticsGame.UI.CharacterCreator;
using UnityEngine;

namespace TacticsGame.Data
{
    [Serializable]
    public class CharacterDataPayload : IUnitDataProvider, ISerializationCallbackReceiver
    {
        // Core Identity
        public string Name;
        public int Level = 1;
        public Sprite PortraitIcon;
        public Color ArmorColor = Color.white;

        // Roleplay Details
        public string Identity;
        public string Pronouns;
        public string Deity;
        public List<string> Edicts = new List<string>();
        public List<string> Anathema = new List<string>();
        public int Age;

        // Core Selection IDs
        public string AncestryID;
        public string HeritageID;
        public string BackgroundID;
        public string ClassID;
        public string SubclassID;
        public string MainHandWeaponID;
        public string OffHandEquipmentID;
        public string ArmorID;

        // Character creation selections that need explicit UI validation.
        public string ClassKeyAttribute;
        public List<string> AncestryBoosts = new List<string>();
        public List<string> AncestryFlaws = new List<string>();
        public List<string> BackgroundBoosts = new List<string>();
        public List<string> FreeBoosts = new List<string>();
        public List<string> TrainedSkills = new List<string>();
        public List<string> Languages = new List<string>();
        public List<string> FeatureIDs = new List<string>();
        public List<string> SpellIDs = new List<string>();

        // Event Sourcing Ledgers
        public List<ChoiceRecord> Ledger = new List<ChoiceRecord>();
        public List<SpellSelection> SpellLedger = new List<SpellSelection>();

        // Visual Parts Dictionary
        [NonSerialized]
        public Dictionary<string, string> VisualPartIDs = new Dictionary<string, string>();

        // Parallel lists for JSON serialization (Dictionary Trap Fix)
        [SerializeField]
        private List<string> visualKeys = new List<string>();

        [SerializeField]
        private List<string> visualValues = new List<string>();

        public void OnBeforeSerialize()
        {
            visualKeys.Clear();
            visualValues.Clear();
            foreach (var kvp in VisualPartIDs)
            {
                visualKeys.Add(kvp.Key);
                visualValues.Add(kvp.Value);
            }
        }

        public void OnAfterDeserialize()
        {
            VisualPartIDs = new Dictionary<string, string>();
            for (int i = 0; i < Math.Min(visualKeys.Count, visualValues.Count); i++)
            {
                VisualPartIDs[visualKeys[i]] = visualValues[i];
            }
        }

        // IUnitDataProvider Implementation
        public string GetUnitName() => Name;

        public Sprite GetPortraitIcon() => PortraitIcon;

        public int GetLevel() => Level;

        // Pass-throughs for rule derivation
        public List<ChoiceRecord> GetLedger() => Ledger;

        public List<SpellSelection> GetSpellLedger() => SpellLedger;

        public Dictionary<string, string> GetEquippedFeats()
        {
            var dict = new Dictionary<string, string>();
            foreach (var choice in Ledger)
            {
                if (choice.Type == ChoiceType.Feat && !choice.IsInvalid)
                {
                    dict[choice.SourceID] = choice.SelectedValue;
                }
            }
            return dict;
        }

        public string GetHeritageID() => HeritageID;

        public string GetIdentity() => Identity;

        public string GetPronouns() => Pronouns;

        public string GetDeity() => Deity;

        public List<string> GetEdicts() => Edicts;

        public List<string> GetAnathema() => Anathema;

        public int GetAge() => Age;

        // Raw Attribute getters routed to calculator
        public int GetStrength() =>
            TacticsRulesetRuleCalculator.GetAttributeModifier(this, AbilityScore.STR);

        public int GetDexterity() =>
            TacticsRulesetRuleCalculator.GetAttributeModifier(this, AbilityScore.DEX);

        public int GetConstitution() =>
            TacticsRulesetRuleCalculator.GetAttributeModifier(this, AbilityScore.CON);

        public int GetIntelligence() =>
            TacticsRulesetRuleCalculator.GetAttributeModifier(this, AbilityScore.INT);

        public int GetWisdom() =>
            TacticsRulesetRuleCalculator.GetAttributeModifier(this, AbilityScore.WIS);

        public int GetCharisma() =>
            TacticsRulesetRuleCalculator.GetAttributeModifier(this, AbilityScore.CHA);

        // Methods routed to TacticsRulesetRuleCalculator
        public int GetMaxHP(TacticsRulesetDatabase db) =>
            TacticsRulesetRuleCalculator.GetMaxHP(this, db);

        public int GetSpeed() => TacticsRulesetRuleCalculator.GetSpeed(this);

        public int GetMaxFocusPoints() => TacticsRulesetRuleCalculator.GetMaxFocusPoints(this);

        public int GetClassDC() => TacticsRulesetRuleCalculator.GetClassDC(this);

        public int GetSavingThrow(SavingThrowType save) =>
            TacticsRulesetRuleCalculator.GetSavingThrow(this, save);

        public int GetPerceptionModifier() =>
            TacticsRulesetRuleCalculator.GetPerceptionModifier(this);

        public int GetSpellAttackModifier() =>
            TacticsRulesetRuleCalculator.GetSpellAttackModifier(this);

        public int GetSpellDC() => TacticsRulesetRuleCalculator.GetSpellDC(this);

        public int GetMaxBulk() => TacticsRulesetRuleCalculator.GetMaxBulk(this);

        public Proficiency GetSkillProficiency(SkillType skill) =>
            TacticsRulesetRuleCalculator.GetSkillProficiency(this, skill);

        public Proficiency GetLoreProficiency(string loreName) =>
            TacticsRulesetRuleCalculator.GetLoreProficiency(this, loreName);

        public List<string> GetKnownLanguages()
        {
            var langs = new List<string>();
            foreach (var choice in Ledger)
                if (choice.Type == ChoiceType.Language && !choice.IsInvalid)
                    langs.Add(choice.SelectedValue);
            return langs;
        }

        public UnitSize GetSize() => TacticsRulesetRuleCalculator.GetSize(this);

        public List<SenseType> GetSenses() => TacticsRulesetRuleCalculator.GetSenses(this);

        public List<string> GetTraits() => TacticsRulesetRuleCalculator.GetTraits(this);

        public int GetStealth() => TacticsRulesetRuleCalculator.GetStealth(this);

        public bool HasAllAroundVision() => false;

        public bool HasDenyAdvantage() => false;

        public RWIProfile GetRWIProfile() => new RWIProfile();

        public void ClearDependentData(CreatorState state)
        {
            // Placeholder for state invalidation logic
        }
    }
}
