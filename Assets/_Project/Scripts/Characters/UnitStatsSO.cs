using UnityEngine;

namespace PathfinderTactics.Characters
{
    [CreateAssetMenu(fileName = "NewUnitStats", menuName = "PathfinderTactics/Unit Stats")]
    public class UnitStatsSO : ScriptableObject
    {
        [Header("Identity")]
        public string unitName = "Unit";


        [Header("Ability Modifiers")]
        [Tooltip("Strength: physical power.")]
        public int strength = 0;

        [Tooltip("Dexterity: agility, reflexes, and fine motor control.")]
        public int dexterity = 0;

        [Tooltip("Constitution: health and stamina.")]
        public int constitution = 0;

        [Tooltip("Intelligence: reasoning, memory, and knowledge.")]
        public int intelligence = 0;

        [Tooltip("Wisdom: perception and willpower.")]
        public int wisdom = 0;

        [Tooltip("Charisma: presence and force of personality.")]
        public int charisma = 0;


        [Header("Core Stats (Pathfinder 2e)")]
        [Tooltip("Speed in feet. Standard is 25 or 30 for most humanoids.")]
        public int speedInFeet = 30;

        [Tooltip("Armor Class (AC). Standard is 10 + Dexterity modifier + armor bonus.")]
        public int armorClass => 10 + dexterity; // TODO: add armor bonus

        [Header("Ancestry & Class (Resources)")]
        [Tooltip("Resource path (relative to Resources/) to the ancestry JSON. Example: JSON/ancestries/human")]
        public string ancestryResourcePath = "JSON/ancestries/human";

        [Tooltip("Resource path (relative to Resources/) to the class JSON. Example: JSON/classes/fighter")]
        public string classResourcePath = "JSON/classes/fighter";

        [Tooltip("Character level (minimum 1).")]
        public int level = 1;

        [Header("List of Skills")]
        public int Acrobatics = 0;
        public int Arcana = 0;
        public int Athletics = 0;
        public int Crafting = 0;
        public int Deception = 0;
        public int Diplomacy = 0;
        public int Intimidation = 0;
        public int Lore = 0;
        public int Medicine = 0;
        public int Nature = 0;
        public int Occultism = 0;
        public int Performance = 0;
        public int Religion = 0;
        public int Society = 0;
        public int Stealth = 0;
        public int Survival = 0;
        public int Thievery = 0;
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
                int con = constitution;
                int lvl = Mathf.Max(1, level);
                // Level 1 base
                int total = aHp + cHp + con;
                if (lvl > 1)
                {
                    total += (lvl - 1) * (cHp + con);
                }
                return total;
            }
        }

        private int GetAncestryHp()
        {
            if (string.IsNullOrEmpty(ancestryResourcePath)) return 0;
            var ta = Resources.Load<TextAsset>(ancestryResourcePath);
            if (ta == null) return 0;
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
            if (string.IsNullOrEmpty(classResourcePath)) return 0;
            var ta = Resources.Load<TextAsset>(classResourcePath);
            if (ta == null) return 0;
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

        private void OnValidate()
        {
            if (level < 1) level = 1;
        }

        // TODO: Add many more stats here later (Dying Level, Wounded Level,
        // Resistances, Weaknesses, Immunities, etc.)


    }
}
