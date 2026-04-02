using System;
using System.Collections.Generic;
using UnityEngine;

namespace PathfinderTactics.Data.PF2e
{
    [CreateAssetMenu(menuName = "PF2e/Class")]
    public class ClassSO : GameElementSO
    {
        [Header("Class Stats")]
        public int HP;
        public List<AbilityType> KeyAbilities = new List<AbilityType>();
        public int ClassDC; // Some classes use Class DC for abilities

        [Header("Proficiency Ranks")]
        public int Perception;
        public int Spellcasting;

        [Header("Saving Throws (Ranks)")]
        public int Fortitude;
        public int Reflex;
        public int Will;

        [Header("Attack Proficiencies (Ranks)")]
        public int SimpleWeapons;
        public int MartialWeapons;
        public int AdvancedWeapons;
        public int UnarmedAttacks;

        [Header("Defense Proficiencies (Ranks)")]
        public int Unarmored;
        public int LightArmor;
        public int MediumArmor;
        public int HeavyArmor;

        [Header("Skills")]
        public List<string> TrainedSkills = new List<string>();
        public int AdditionalSkillCount;

        [Header("Feat Progression")]
        public List<int> AncestryFeatLevels = new List<int>();
        public List<int> ClassFeatLevels = new List<int>();
        public List<int> SkillFeatLevels = new List<int>();
        public List<int> GeneralFeatLevels = new List<int>();
        public List<int> SkillIncreaseLevels = new List<int>();

        [Header("Granted Features")]
        public List<ClassFeatureEntry> GrantedFeatures = new List<ClassFeatureEntry>();
    }

    [Serializable]
    public struct ClassFeatureEntry
    {
        public string Name;
        public int Level;
        public string UUID; // Foundry Compendium UUID for cross-referencing
    }
}
