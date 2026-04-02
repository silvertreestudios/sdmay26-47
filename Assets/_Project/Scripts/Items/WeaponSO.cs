using System.Collections.Generic;
using PathfinderTactics.Characters;
using UnityEngine;

namespace PathfinderTactics.Items
{
    public enum WeaponCategory
    {
        Unarmed,
        Simple,
        Martial,
        Advanced,
    }

    public enum WeaponGroup
    {
        Axe,
        Bomb,
        Bow,
        Brawling,
        Club,
        Dart,
        Flail,
        Hammer,
        Knife,
        Pick,
        Polearm,
        Shield,
        Sling,
        Spear,
        Sword,
    }

    public enum WeaponTrait
    {
        Agile,
        Attached,
        Backstabber,
        Backswing,
        Brutal,
        Cantrip,
        Capacity,
        Cobbled,
        Concealable,
        Concussive,
        DeadlyD6,
        DeadlyD8,
        DeadlyD10,
        DeadlyD12,
        DoubleBarrel,
        FatalD8,
        FatalD10,
        FatalD12,
        Finesse,
        Forceful,
        FreeHand,
        Halfling,
        Heal,
        Injection,
        Jousting,
        Kickback,
        Monk,
        Nonlethal,
        Parry,
        Propulsive,
        Scatter,
        Shove,
        Sweep,
        Tethered,
        Thrown,
        Trip,
        Twin,
        TwoHand,
        Unarmed,
        Versatile,
    }

    public enum HandsRequired
    {
        One,
        Two,
    }

    [System.Serializable]
    public struct Dice
    {
        public int count;
        public int sides;

        public Dice(int count, int sides)
        {
            this.count = count;
            this.sides = sides;
        }
    }

    [CreateAssetMenu(menuName = "PathfinderTactics/Items/Weapon")]
    public class WeaponSO : EquipmentSO
    {
        [Header("Damage")]
        public Dice damageDice = new Dice(1, 8);
        public DamageType damageType = DamageType.Slashing;

        [Header("Weapon Properties")]
        public WeaponCategory category = WeaponCategory.Martial;
        public WeaponGroup group = WeaponGroup.Sword;
        public HandsRequired hands = HandsRequired.One;
        public int weaponAnimType; // 1=1H, 2=2H, 3=Bow

        [Header("Range")]
        [Tooltip("Max reach in feet. Standard melee is 5ft. Reach weapons are 10ft.")]
        public int reachFeet = 5;

        [Tooltip("Range increment for ranged/thrown weapons in feet. 0 means it's strictly melee.")]
        public int rangeIncrementFeet = 0;

        [Header("Traits")]
        public List<WeaponTrait> traits = new List<WeaponTrait>();

        public bool HasTrait(WeaponTrait trait)
        {
            return traits.Contains(trait);
        }

        public bool IsRangedWeapon()
        {
            return rangeIncrementFeet > 0;
        }
    }
}
