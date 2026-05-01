using UnityEngine;

namespace TacticsGame.Items
{
    public class WeaponGrip : MonoBehaviour
    {
        [Header("Grip Offsets")]
        [Tooltip("The positional offset relative to the bone it attaches to.")]
        public Vector3 positionalOffset;

        [Tooltip("The rotational offset relative to the bone it attaches to.")]
        public Vector3 rotationalOffset;
    }
}
