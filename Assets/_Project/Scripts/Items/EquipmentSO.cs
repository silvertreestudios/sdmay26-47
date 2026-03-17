using System.Collections.Generic;
using UnityEngine;

namespace PathfinderTactics.Items
{
    public abstract class EquipmentSO : ItemSO
    {
        [Header("Stat Modifiers")]
        [Tooltip("Any passive stat boosts this equipment grants while equipped.")]
        public List<StatModifier> modifiers = new List<StatModifier>();
    }
}
