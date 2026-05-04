using UnityEngine;

namespace TacticsGame.Objects
{
    public enum DoorState
    {
        Closed,
        Open,
        Locked,
        Stuck,
        Destroyed,
    }

    [CreateAssetMenu(menuName = "TacticsRuleset/Objects/Door Stats")]
    public class DoorStatsSO : ScriptableObject
    {
        public string MaterialName;
        public int Hardness;
        public int MaxHP;
        public int BrokenThreshold;

        // Skill DCs for interacting
        public int PickLockDC = 15;
        public int ForceOpenDC = 15;
    }
}
