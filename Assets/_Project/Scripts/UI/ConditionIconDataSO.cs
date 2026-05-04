using System;
using System.Collections.Generic;
using PathfinderTactics.Characters;
using UnityEngine;

namespace PathfinderTactics.UI
{
    [CreateAssetMenu(
        fileName = "ConditionIconData",
        menuName = "PathfinderTactics/UI/ConditionIconData"
    )]
    public class ConditionIconDataSO : ScriptableObject
    {
        [Serializable]
        public struct ConditionIconMapping
        {
            public ConditionType conditionType;
            public Sprite icon;
        }

        [SerializeField]
        private List<ConditionIconMapping> mappings = new List<ConditionIconMapping>();

        private Dictionary<ConditionType, Sprite> iconCache;

        public Sprite GetIcon(ConditionType type)
        {
            if (iconCache == null)
            {
                iconCache = new Dictionary<ConditionType, Sprite>();
                foreach (var mapping in mappings)
                {
                    if (!iconCache.ContainsKey(mapping.conditionType))
                    {
                        iconCache.Add(mapping.conditionType, mapping.icon);
                    }
                }
            }

            return iconCache.TryGetValue(type, out var sprite) ? sprite : null;
        }
    }
}
