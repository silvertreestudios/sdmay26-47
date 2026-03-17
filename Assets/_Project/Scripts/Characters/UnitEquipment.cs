using System;
using System.Collections.Generic;
using PathfinderTactics.Actions;
using PathfinderTactics.Items;
using UnityEngine;

namespace PathfinderTactics.Characters
{
    public class UnitEquipment : MonoBehaviour
    {
        [Header("Inventory")]
        [SerializeField]
        private List<ItemInstance> inventory = new List<ItemInstance>();

        [Header("Equipped Items")]
        [SerializeField]
        private WeaponSO mainHand;

        [SerializeField]
        private EquipmentSO offHand; // Can be WeaponSO, ShieldSO, or empty

        [SerializeField]
        private ArmorSO equippedArmor;

        [Header("Fallbacks")]
        [SerializeField]
        [Tooltip("The fallback weapon used if MainHand is empty (e.g., Fist).")]
        private WeaponSO unarmedFallback;

        public event Action OnEquipmentChanged;

        private void Start()
        {
            ConfigureStrikeActions();
        }

        /// <summary>
        /// Equips a weapon to the main hand. Handles 2-hand constraints.
        /// </summary>
        public void EquipMainHand(WeaponSO weapon)
        {
            if (weapon == null)
            {
                mainHand = null;
                NotifyEquipmentChanged();
                return;
            }

            mainHand = weapon;

            // If the weapon requires 2 hands, we MUST unequip the offhand
            if (weapon.hands == HandsRequired.Two)
            {
                if (offHand != null)
                {
                    Debug.Log(
                        $"Unequipped {offHand.itemName} from off-hand because {weapon.itemName} requires 2 hands."
                    );
                    offHand = null;
                }
            }

            NotifyEquipmentChanged();
        }

        /// <summary>
        /// Equips an item to the off hand. Validates against MainHand 2-hand constraints.
        /// </summary>
        public void EquipOffHand(EquipmentSO equipment)
        {
            if (equipment == null)
            {
                offHand = null;
                NotifyEquipmentChanged();
                return;
            }

            // Cannot equip offhand if mainhand is a 2-handed weapon
            if (mainHand != null && mainHand.hands == HandsRequired.Two)
            {
                Debug.LogWarning(
                    $"Cannot equip {equipment.itemName} to off-hand; {mainHand.itemName} requires 2 hands."
                );
                return;
            }

            if (equipment is WeaponSO || equipment is ShieldSO)
            {
                offHand = equipment;
                NotifyEquipmentChanged();
            }
            else
            {
                Debug.LogWarning(
                    "Only Weapons and Shields can be equipped to the OffHand slot in this system."
                );
            }
        }

        public void EquipArmor(ArmorSO armor)
        {
            equippedArmor = armor;
            NotifyEquipmentChanged();
        }

        public void AddToInventory(ItemSO item, int quantity = 1)
        {
            // Simple stacking check
            var existing = inventory.Find(i => i.item == item);
            if (existing != null)
            {
                existing.quantity += quantity;
            }
            else
            {
                inventory.Add(new ItemInstance(item, quantity));
            }
        }

        public void RemoveFromInventory(ItemSO item, int quantity = 1)
        {
            var existing = inventory.Find(i => i.item == item);
            if (existing != null)
            {
                existing.quantity -= quantity;
                if (existing.quantity <= 0)
                {
                    inventory.Remove(existing);
                }
            }
        }

        public WeaponSO GetMainWeapon()
        {
            return mainHand != null ? mainHand : unarmedFallback;
        }

        public EquipmentSO GetOffHand()
        {
            return offHand;
        }

        public ArmorSO GetArmor()
        {
            return equippedArmor;
        }

        private void NotifyEquipmentChanged()
        {
            ConfigureStrikeActions();
            OnEquipmentChanged?.Invoke();
        }

        /// <summary>
        /// Collects all active StatModifiers from currently equipped items.
        /// </summary>
        public List<StatModifier> GetActiveModifiers()
        {
            List<StatModifier> mods = new List<StatModifier>();

            if (mainHand != null)
                mods.AddRange(mainHand.modifiers);
            if (offHand != null)
                mods.AddRange(offHand.modifiers);
            if (equippedArmor != null)
                mods.AddRange(equippedArmor.modifiers);

            return mods;
        }

        //  Dynamic Strike Action Management

        /// <summary>
        /// Removes all existing MeleeAction/RangedAction components and creates
        /// fresh ones based on the currently equipped weapons.
        /// Each equipped weapon that can attack gets its own Strike button.
        /// </summary>
        public void ConfigureStrikeActions()
        {
            // Remove all existing strike action components (DestroyImmediate is
            // required here so GetComponents sees the clean state before we add new ones)
            foreach (var melee in GetComponents<MeleeAction>())
                DestroyImmediate(melee);
            foreach (var ranged in GetComponents<RangedAction>())
                DestroyImmediate(ranged);

            // Collect all weapons that should have strike options
            var weapons = new List<WeaponSO>();

            // Main hand (or unarmed fallback)
            WeaponSO mainWeapon = mainHand != null ? mainHand : unarmedFallback;
            if (mainWeapon != null)
                weapons.Add(mainWeapon);

            // Off-hand weapon (if it's a WeaponSO, not a shield)
            if (offHand is WeaponSO offWeapon && offWeapon != mainWeapon)
                weapons.Add(offWeapon);

            // Create one action per weapon
            foreach (var weapon in weapons)
            {
                // Melee: any weapon with reachFeet > 0
                if (weapon.reachFeet > 0)
                {
                    var melee = gameObject.AddComponent<MeleeAction>();
                    melee.activeWeapon = weapon;
                }

                // Ranged: any weapon with rangeIncrementFeet > 0 (thrown, bows, etc.)
                if (weapon.IsRangedWeapon())
                {
                    var ranged = gameObject.AddComponent<RangedAction>();
                    ranged.activeWeapon = weapon;
                }
            }

            // Refresh the action economy cache so the UI picks up the new components
            var actionEconomy = GetComponent<UnitActionEconomy>();
            if (actionEconomy != null)
                actionEconomy.RefreshActions();

            Debug.Log(
                $"[EQUIPMENT] {gameObject.name}: Configured {weapons.Count} weapon(s) into strike actions."
            );
        }
    }
}
