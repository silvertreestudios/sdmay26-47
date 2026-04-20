using System;
using System.Collections.Generic;
using PathfinderTactics.Characters;
using UnityEngine;

namespace PathfinderTactics.Data
{
    [Serializable]
    public struct DamageTypeIconMapping
    {
        public DamageType damageType;
        public Sprite icon;
    }

    [CreateAssetMenu(fileName = "UIDataConfig", menuName = "PathfinderTactics/UI/UI Data Config")]
    public class UIDataConfig : ScriptableObject
    {
        [Header("Damage Type Icons")]
        [SerializeField]
        private List<DamageTypeIconMapping> damageTypeIcons = new List<DamageTypeIconMapping>();

        private Dictionary<DamageType, Sprite> iconDictionary;

        private void InitializeDictionary()
        {
            iconDictionary = new Dictionary<DamageType, Sprite>();
            foreach (var mapping in damageTypeIcons)
            {
                if (mapping.icon != null && !iconDictionary.ContainsKey(mapping.damageType))
                {
                    iconDictionary.Add(mapping.damageType, mapping.icon);
                }
            }
        }

        public Sprite GetDamageIcon(DamageType damageType)
        {
            if (iconDictionary == null)
            {
                InitializeDictionary();
            }

            if (iconDictionary.TryGetValue(damageType, out Sprite sprite))
            {
                return sprite;
            }

            return null;
        }
    }
}
