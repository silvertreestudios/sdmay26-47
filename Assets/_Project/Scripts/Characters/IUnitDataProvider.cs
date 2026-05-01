using System.Collections.Generic;
using TacticsGame.Core;
using TacticsGame.Data;
using TacticsGame.Data.TacticsRuleset;

namespace TacticsGame.Characters
{
    public interface IUnitDataProvider
    {
        // Core Identity
        string GetUnitName();
        UnityEngine.Sprite GetPortraitIcon();
        int GetLevel();
        UnitSize GetSize();
        List<string> GetTraits();
        string GetHeritageID();

        // Roleplay Details
        string GetIdentity();
        string GetPronouns();
        string GetDeity();
        List<string> GetEdicts();
        List<string> GetAnathema();
        int GetAge();

        // Ledgers (Event Sourcing)
        List<ChoiceRecord> GetLedger();
        List<SpellSelection> GetSpellLedger();
        Dictionary<string, string> GetEquippedFeats();

        // Core Attributes (Raw modifiers, use Calculator for final)
        int GetStrength();
        int GetDexterity();
        int GetConstitution();
        int GetIntelligence();
        int GetWisdom();
        int GetCharisma();

        // Derived Combat Stats
        int GetMaxHP(TacticsRulesetDatabase db);
        int GetSpeed();
        int GetMaxFocusPoints();
        int GetClassDC();
        int GetSavingThrow(SavingThrowType save);
        int GetPerceptionModifier();
        int GetSpellAttackModifier();
        int GetSpellDC();
        int GetMaxBulk();

        // Proficiencies
        Proficiency GetSkillProficiency(SkillType skill);
        Proficiency GetLoreProficiency(string loreName);
        List<string> GetKnownLanguages();

        // Senses & Special Abilities
        List<SenseType> GetSenses();
        int GetStealth();
        bool HasAllAroundVision();
        bool HasDenyAdvantage();

        // Defenses and Traits
        RWIProfile GetRWIProfile();
    }
}
