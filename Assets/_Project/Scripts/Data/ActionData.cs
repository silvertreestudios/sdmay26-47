using UnityEngine;

namespace PathfinderTactics.Data
{
    [CreateAssetMenu(fileName = "NewAction", menuName = "PathfinderTactics/Data/Action Data")]
    public class ActionData : ScriptableObject
    {
        public string actionName;

        [Range(0, 3)]
        public int apCost = 1;

        [Tooltip(
            "The default icon for this action (e.g. for Hide, Seek). For Strikes/Spells, this may be overridden by the Damage Type icon."
        )]
        public Sprite abilityIcon;
    }
}
