using System.Collections.Generic;
using System.Linq;
using TacticsGame.Characters;
using TacticsGame.Core;
using TacticsGame.Data.TacticsCore;
using TacticsGame.Data.TacticsRuleset;
using SenseType = TacticsGame.Core.SenseType;
using SkillType = TacticsGame.Core.SkillType;

namespace TacticsGame.Data
{
    public static class TacticsRulesetRuleCalculator
    {
        public static int GetMaxHP(IUnitDataProvider data, TacticsRulesetDatabase db = null)
        {
            db = GetFallbackDatabase(db);
            if (db == null)
                return 10;
            int ancestryHP = 0;
            int classHP = 0;

            foreach (var choice in data.GetLedger())
            {
                if (choice.IsInvalid)
                    continue;

                if (choice.Type == ChoiceType.Ancestry)
                {
                    var ancestry = db.GetCoreAncestry(choice.SelectedValue);
                    if (ancestry != null)
                        ancestryHP = ancestry.HitPoints;
                }
                else if (choice.Type == ChoiceType.Class)
                {
                    var tacticsClass = db.GetCoreClass(choice.SelectedValue);
                    if (tacticsClass != null)
                        classHP = tacticsClass.HitPointsPerLevel;
                }
            }

            // Fallbacks for direct unit data (if not using ledger-based creator)
            if (ancestryHP == 0)
                ancestryHP = 8;
            if (classHP == 0)
                classHP = 8;

            int conMod = GetAttributeModifier(data, AbilityScore.CON);
            int level = data.GetLevel();

            // Total HP = Ancestry HP + (Class HP + CON Mod) * Level
            return ancestryHP + (classHP + conMod) * level;
        }

        public static int GetAttributeModifier(IUnitDataProvider data, AbilityScore score)
        {
            int mod = 0;
            foreach (var choice in data.GetLedger())
            {
                if (!choice.IsInvalid)
                {
                    if (
                        choice.Type == ChoiceType.AttributeBoost
                        && choice.SelectedValue == score.ToString()
                    )
                        mod++;
                    else if (
                        choice.Type == ChoiceType.AttributeFlaw
                        && choice.SelectedValue == score.ToString()
                    )
                        mod--;
                }
            }
            // Enforce +4 limit at level 1
            if (data.GetLevel() == 1 && mod > 4)
                mod = 4;
            return mod;
        }

        public static Proficiency GetSkillProficiency(IUnitDataProvider data, SkillType skill)
        {
            Proficiency maxProf = Proficiency.Untrained;
            foreach (var choice in data.GetLedger())
            {
                if (
                    !choice.IsInvalid
                    && choice.Type == ChoiceType.SkillIncrease
                    && choice.SelectedValue == skill.ToString()
                )
                {
                    // Increase proficiency logic
                    if (maxProf == Proficiency.Untrained)
                        maxProf = Proficiency.Trained;
                    else if (maxProf == Proficiency.Trained)
                        maxProf = Proficiency.Expert;
                    else if (maxProf == Proficiency.Expert)
                        maxProf = Proficiency.Master;
                    else if (maxProf == Proficiency.Master)
                        maxProf = Proficiency.Legendary;
                }
            }
            return maxProf;
        }

        public static Proficiency GetLoreProficiency(IUnitDataProvider data, string loreName)
        {
            Proficiency maxProf = Proficiency.Untrained;
            foreach (var choice in data.GetLedger())
            {
                if (
                    !choice.IsInvalid
                    && choice.Type == ChoiceType.SkillIncrease
                    && choice.SelectedValue == loreName
                )
                {
                    if (maxProf == Proficiency.Untrained)
                        maxProf = Proficiency.Trained;
                    else if (maxProf == Proficiency.Trained)
                        maxProf = Proficiency.Expert;
                    else if (maxProf == Proficiency.Expert)
                        maxProf = Proficiency.Master;
                    else if (maxProf == Proficiency.Master)
                        maxProf = Proficiency.Legendary;
                }
            }
            return maxProf;
        }

        public static int GetMaxFocusPoints(IUnitDataProvider data)
        {
            int focusPoints = 0;
            foreach (var spell in data.GetSpellLedger())
            {
                if (spell.Tradition == SpellTradition.Focus)
                    focusPoints++;
            }
            return System.Math.Min(focusPoints, 3);
        }

        public static int GetClassDC(IUnitDataProvider data, TacticsRulesetDatabase db = null)
        {
            db = GetFallbackDatabase(db);
            if (db == null)
                return 10;
            Proficiency prof = Proficiency.Untrained;
            AbilityScore bestAbility = AbilityScore.STR; // Fallback

            foreach (var choice in data.GetLedger())
            {
                if (choice.IsInvalid)
                    continue;
                if (choice.Type == ChoiceType.Class)
                {
                    var tacticsClass = db.GetCoreClass(choice.SelectedValue);
                    if (tacticsClass != null)
                    {
                        prof = (Proficiency)tacticsClass.ClassDifficulty;
                        // Pick the highest mod among key attributes
                        int bestMod = -5;
                        foreach (var keyAttr in tacticsClass.KeyAttributes)
                        {
                            AbilityScore score = MapAttribute(keyAttr);
                            int mod = GetAttributeModifier(data, score);
                            if (mod > bestMod)
                            {
                                bestMod = mod;
                                bestAbility = score;
                            }
                        }
                    }
                }
            }

            return 10
                + TacticsRuleset_Core.CalculateModifier(
                    data.GetLevel(),
                    prof,
                    GetAttributeModifier(data, bestAbility)
                );
        }

        public static int GetSavingThrow(
            IUnitDataProvider data,
            SavingThrowType save,
            TacticsRulesetDatabase db = null
        )
        {
            db = GetFallbackDatabase(db);
            if (db == null)
                return 0;
            Proficiency prof = Proficiency.Untrained;
            AbilityScore ability = AbilityScore.None;

            switch (save)
            {
                case SavingThrowType.Fortitude:
                    ability = AbilityScore.CON;
                    break;
                case SavingThrowType.Reflex:
                    ability = AbilityScore.DEX;
                    break;
                case SavingThrowType.Will:
                    ability = AbilityScore.WIS;
                    break;
            }

            foreach (var choice in data.GetLedger())
            {
                if (choice.IsInvalid)
                    continue;
                if (choice.Type == ChoiceType.Class)
                {
                    var tacticsClass = db.GetCoreClass(choice.SelectedValue);
                    if (tacticsClass != null)
                    {
                        // Map SavingThrowType to DefenseSaveType int for lookup
                        var entry = tacticsClass.SavingThrows.Find(s =>
                            (int)s.Save == ((int)save - 1)
                        );
                        if (entry != null)
                            prof = (Proficiency)entry.Rank;
                    }
                }
            }

            return TacticsRuleset_Core.CalculateModifier(
                data.GetLevel(),
                prof,
                GetAttributeModifier(data, ability)
            );
        }

        public static int GetPerceptionModifier(
            IUnitDataProvider data,
            TacticsRulesetDatabase db = null
        )
        {
            db = GetFallbackDatabase(db);
            if (db == null)
                return 0;
            Proficiency prof = Proficiency.Untrained;
            foreach (var choice in data.GetLedger())
            {
                if (choice.IsInvalid)
                    continue;
                if (choice.Type == ChoiceType.Class)
                {
                    var tacticsClass = db.GetCoreClass(choice.SelectedValue);
                    if (tacticsClass != null)
                        prof = (Proficiency)tacticsClass.Perception;
                }
            }
            return TacticsRuleset_Core.CalculateModifier(
                data.GetLevel(),
                prof,
                GetAttributeModifier(data, AbilityScore.WIS)
            );
        }

        public static int GetSpellAttackModifier(
            IUnitDataProvider data,
            TacticsRulesetDatabase db = null
        )
        {
            db = GetFallbackDatabase(db);
            if (db == null)
                return 0;
            Proficiency prof = Proficiency.Untrained;
            AbilityScore castingAbility = AbilityScore.INT; // Default

            foreach (var choice in data.GetLedger())
            {
                if (choice.IsInvalid)
                    continue;
                if (choice.Type == ChoiceType.Class)
                {
                    var tacticsClass = db.GetCoreClass(choice.SelectedValue);
                    if (tacticsClass != null)
                    {
                        prof = (Proficiency)tacticsClass.Spellcasting;
                        // Use the highest of INT/WIS/CHA for spellcasting
                        int intMod = GetAttributeModifier(data, AbilityScore.INT);
                        int wisMod = GetAttributeModifier(data, AbilityScore.WIS);
                        int chaMod = GetAttributeModifier(data, AbilityScore.CHA);
                        if (wisMod >= intMod && wisMod >= chaMod)
                            castingAbility = AbilityScore.WIS;
                        else if (chaMod >= intMod && chaMod >= wisMod)
                            castingAbility = AbilityScore.CHA;
                    }
                }
            }
            return TacticsRuleset_Core.CalculateModifier(
                data.GetLevel(),
                prof,
                GetAttributeModifier(data, castingAbility)
            );
        }

        public static int GetSpellDC(IUnitDataProvider data, TacticsRulesetDatabase db = null)
        {
            return 10 + GetSpellAttackModifier(data, db);
        }

        public static int GetMaxBulk(IUnitDataProvider data)
        {
            return 5 + GetAttributeModifier(data, AbilityScore.STR);
        }

        public static int GetSpeed(IUnitDataProvider data, TacticsRulesetDatabase db = null)
        {
            db = GetFallbackDatabase(db);
            if (db == null)
                return 25;
            int baseSpeed = 25;
            foreach (var choice in data.GetLedger())
            {
                if (choice.IsInvalid)
                    continue;

                if (choice.Type == ChoiceType.Ancestry)
                {
                    var ancestry = db.GetCoreAncestry(choice.SelectedValue);
                    if (ancestry != null)
                        baseSpeed = ancestry.Speed;
                }
            }
            return baseSpeed;
        }

        public static UnitSize GetSize(IUnitDataProvider data, TacticsRulesetDatabase db = null)
        {
            db = GetFallbackDatabase(db);
            if (db == null)
                return UnitSize.Medium;
            UnitSize size = UnitSize.Medium;
            foreach (var choice in data.GetLedger())
            {
                if (choice.IsInvalid)
                    continue;

                if (choice.Type == ChoiceType.Ancestry)
                {
                    var ancestry = db.GetCoreAncestry(choice.SelectedValue);
                    if (ancestry != null)
                    {
                        // Map CreatureSize to UnitSize
                        switch (ancestry.Size)
                        {
                            case TacticsGame.Data.TacticsCore.CreatureSize.Tiny:
                                size = UnitSize.Tiny;
                                break;
                            case TacticsGame.Data.TacticsCore.CreatureSize.Small:
                                size = UnitSize.Small;
                                break;
                            case TacticsGame.Data.TacticsCore.CreatureSize.Medium:
                                size = UnitSize.Medium;
                                break;
                            case TacticsGame.Data.TacticsCore.CreatureSize.Large:
                                size = UnitSize.Large;
                                break;
                            case TacticsGame.Data.TacticsCore.CreatureSize.Huge:
                                size = UnitSize.Huge;
                                break;
                            case TacticsGame.Data.TacticsCore.CreatureSize.Gargantuan:
                                size = UnitSize.Gargantuan;
                                break;
                        }
                    }
                }
            }
            return size;
        }

        public static List<SenseType> GetSenses(
            IUnitDataProvider data,
            TacticsRulesetDatabase db = null
        )
        {
            db = GetFallbackDatabase(db);
            if (db == null)
                return new List<SenseType>();
            List<SenseType> senses = new List<SenseType>();
            foreach (var choice in data.GetLedger())
            {
                if (choice.IsInvalid)
                    continue;

                if (choice.Type == ChoiceType.Ancestry)
                {
                    var ancestry = db.GetCoreAncestry(choice.SelectedValue);
                    if (ancestry != null)
                    {
                        // Map TacticsCore.SenseType to Core.SenseType
                        switch (ancestry.Sense)
                        {
                            case TacticsGame.Data.TacticsCore.SenseType.LowLight:
                                senses.Add(SenseType.LowLightVision);
                                break;
                            case TacticsGame.Data.TacticsCore.SenseType.Darkvision:
                                senses.Add(SenseType.Darkvision);
                                break;
                        }
                    }
                }
            }
            return senses;
        }

        public static List<string> GetTraits(
            IUnitDataProvider data,
            TacticsRulesetDatabase db = null
        )
        {
            db = GetFallbackDatabase(db);
            if (db == null)
                return new List<string>();
            List<string> traits = new List<string>();
            foreach (var choice in data.GetLedger())
            {
                if (choice.IsInvalid)
                    continue;

                if (choice.Type == ChoiceType.Ancestry)
                {
                    var ancestry = db.GetCoreAncestry(choice.SelectedValue);
                    if (ancestry != null)
                        traits.AddRange(ancestry.Traits);
                }
            }
            return traits;
        }

        public static int GetStealth(IUnitDataProvider data, TacticsRulesetDatabase db = null)
        {
            db = GetFallbackDatabase(db);
            if (db == null)
                return 0;
            return GetAttributeModifier(data, AbilityScore.DEX)
                + (int)GetSkillProficiency(data, SkillType.Stealth)
                + (
                    GetSkillProficiency(data, SkillType.Stealth) == Proficiency.Untrained
                        ? 0
                        : data.GetLevel()
                );
        }

        private static TacticsRulesetDatabase GetFallbackDatabase(TacticsRulesetDatabase provided)
        {
            if (provided != null)
                return provided;
            if (ServiceLocator.TryGet<CompendiumRegistry>(out var registry))
                return registry.MasterDatabase;
            return null;
        }

        private static AbilityScore MapAttribute(TacticsGame.Data.TacticsCore.AttributeType type)
        {
            switch (type)
            {
                case TacticsGame.Data.TacticsCore.AttributeType.Strength:
                    return AbilityScore.STR;
                case TacticsGame.Data.TacticsCore.AttributeType.Dexterity:
                    return AbilityScore.DEX;
                case TacticsGame.Data.TacticsCore.AttributeType.Constitution:
                    return AbilityScore.CON;
                case TacticsGame.Data.TacticsCore.AttributeType.Intelligence:
                    return AbilityScore.INT;
                case TacticsGame.Data.TacticsCore.AttributeType.Wisdom:
                    return AbilityScore.WIS;
                case TacticsGame.Data.TacticsCore.AttributeType.Charisma:
                    return AbilityScore.CHA;
                default:
                    return AbilityScore.None;
            }
        }
    }
}
