using System;
using System.Collections.Generic;
using UnityEngine;

namespace PathfinderTactics.Data.PF2e
{
    [CreateAssetMenu(menuName = "PF2e/Ancestry")]
    public class AncestrySO : GameElementSO
    {
        [Header("Ancestry Stats")]
        public int HP;
        public int Speed;
        public int Reach;
        public CreatureSize Size;
        public string Vision; // "normal", "darkvision", "low-light vision"

        [Header("Ability Modifiers")]
        public List<AbilityBoostEntry> Boosts = new List<AbilityBoostEntry>();
        public List<AbilityBoostEntry> Flaws = new List<AbilityBoostEntry>();

        [Header("Languages")]
        public List<string> Languages = new List<string>();
        public int AdditionalLanguageCount;

        [Header("Heritages")]
        public List<string> HeritageIds = new List<string>();
    }

    [Serializable]
    public struct AbilityBoostEntry
    {
        /// <summary>
        /// Available ability choices for this boost slot.
        /// If it contains all 6 abilities, it's a "free" boost.
        /// </summary>
        public List<AbilityType> Options;
    }
}
