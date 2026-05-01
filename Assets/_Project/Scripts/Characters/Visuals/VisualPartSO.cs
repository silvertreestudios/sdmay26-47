using UnityEngine;

namespace TacticsGame.Characters.Visuals
{
    public enum VisualSlot
    {
        Head,
        Body,
        ArmLeft,
        ArmRight,
        LegLeft,
        LegRight,
        Cape,
        Helmet,
        Eyes,
        Jaw,
        Mask,
    }

    [CreateAssetMenu(fileName = "NewVisualPart", menuName = "TacticsGame/Visuals/Visual Part")]
    public class VisualPartSO : ScriptableObject
    {
        [Header("Identity")]
        public string PartID;
        public string DisplayName;
        public VisualSlot Slot;
        public Sprite Icon;

        [Header("Mesh Data")]
        public bool IsStaticMesh; // True if it attaches to a bone, False if it's an SMR
        public Mesh SharedMesh;
        public Material[] Materials;
    }
}
