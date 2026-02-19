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

namespace PathfinderTactics.Characters
{
    [CreateAssetMenu(fileName = "NewUnitStats", menuName = "PathfinderTactics/Unit Stats")]
    public class UnitStatsSO : ScriptableObject
    {
        [Header("Identity")]
        public string unitName = "Unit";

        //All of these will be hardcoded for now this is the basic fighter I built here: https://pathbuilder2e.com/launch.html?build=1381561, where I am getting these from. Should all be read from other places later
        [Header("Ability Modifiers")]
        [Tooltip("Strength: physical power.")]
        public int strength = 4;

        [Tooltip("Dexterity: agility, reflexes, and fine motor control.")]
        public int dexterity = 2;

        [Tooltip("Constitution: health and stamina.")]
        public int constitution = 2;

        [Tooltip("Intelligence: reasoning, memory, and knowledge.")]
        public int intelligence = 0;

        [Tooltip("Wisdom: perception and willpower.")]
        public int wisdom = 1;

        [Tooltip("Charisma: presence and force of personality.")]
        public int charisma = 0;


        [Header("Core Stats (Pathfinder 2e)")]
        [Tooltip("Speed in feet. Standard is 25 or 30 for most humanoids.")]
        public int speedInFeet = 25;
       


        [Tooltip("Armor Class (AC). Standard is 10 + Dexterity modifier + armor bonus.")]
        public int armorClass => GetAC();

        [Header("Ancestry & Class (Resources)")]
        [Tooltip("Resource path (relative to Resources/) to the ancestry JSON. Example: JSON/ancestries/human")]
        public string ancestryResourcePath = "JSON/ancestries/human";

        [Tooltip("Resource path (relative to Resources/) to the class JSON. Example: JSON/classes/fighter")]
        public string classResourcePath = "JSON/classes/fighter";

        public string armorResourcePath = "JSON/eqiupment/scale-mail";


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

        private int GetAC()
        {
            if (string.IsNullOrEmpty(armorResourcePath)) return 0;
            var ta = Resources.Load<TextAsset>(armorResourcePath);
            if (ta == null) return 0;
            try
            {
                var data = JsonUtility.FromJson<ArmorJson>(ta.text);
                if (data != null && data.system != null)
                {
                    int acBonus = data.system.acBonus;
                    int dexCap = data.system.dexCap;
                    int dexBonus = Mathf.Min(dexterity, dexCap);
                    string category = data.system.category.ToLower();
                    // Check category for armor type (heavy, medium, light, unarmored) and apply appropriate profcienecy bonus if needed (TODO: implement proficiency bonuses later)
                    int fighter_lvl1_trained_bonus = 3; // Placeholder for now, should be based on class and level
                    return 10 + acBonus + dexBonus + fighter_lvl1_trained_bonus;
                }
                else
                {
                    return 0;

                }
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
        [System.Serializable]
        private class ArmorJson
        {
            public ArmorSystem system;
        }

        [System.Serializable]
        private class ArmorSystem
        {
            public int acBonus;
            public int dexCap;
            public int checkPenalty;
            public int speedPenalty;
            public int strength;
            public int bulk;
            public string category;
        }
        private void OnValidate()
        {
            if (level < 1) level = 1;
        }

        // TODO: Add many more stats here later (Dying Level, Wounded Level,
        // Resistances, Weaknesses, Immunities, etc.)


    }
}
