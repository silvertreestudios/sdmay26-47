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
            if (mainHand != null)
                SpawnWeaponModel(mainHand, 1);
            if (offHand != null)
                SpawnWeaponModel(offHand, 2);
            ConfigureStrikeActions();
        }

        private GameObject mainHandInstance;
        private GameObject offHandInstance;

        /// <summary>
        /// Method to equip a weapon, requested by character creator pipeline.
        /// handSlot: 1 = Main Hand (Right), 2 = Off Hand (Left).
        /// </summary>
        public void EquipWeapon(WeaponSO weapon, int handSlot = 1)
        {
            if (handSlot == 1)
                EquipMainHand(weapon);
            else
                EquipOffHand(weapon);
        }

        /// <summary>
        /// Equips a weapon to the main hand. Handles 2-hand constraints and visual spawning.
        /// </summary>
        public void EquipMainHand(WeaponSO weapon)
        {
            mainHand = weapon;

            // If the weapon requires 2 hands, we should unequip the offhand
            if (weapon != null && weapon.hands == HandsRequired.Two)
            {
                if (offHand != null)
                {
                    Debug.Log(
                        $"Unequipped {offHand.itemName} from off-hand because {weapon.itemName} requires 2 hands."
                    );
                    offHand = null;
                    DespawnVisualModel(2);
                }
            }

            SpawnWeaponModel(weapon, 1);
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
                DespawnVisualModel(2);
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
                SpawnWeaponModel(equipment, 2);
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

        private void DespawnVisualModel(int handSlot)
        {
            if (handSlot == 1 && mainHandInstance != null)
            {
                Destroy(mainHandInstance);
                mainHandInstance = null;
            }
            else if (handSlot == 2 && offHandInstance != null)
            {
                Destroy(offHandInstance);
                offHandInstance = null;
            }
        }

        private void SpawnWeaponModel(EquipmentSO equipment, int handSlot)
        {
            DespawnVisualModel(handSlot);

            if (equipment == null || equipment.prefab == null)
                return;

            Animator animator = GetComponentInChildren<Animator>();
            if (animator == null || !animator.isHuman)
            {
                Debug.LogWarning(
                    $"Cannot spawn {equipment.itemName} visually: No Humanoid Animator found on {gameObject.name}."
                );
                return;
            }

            Transform handBone = animator.GetBoneTransform(
                handSlot == 1 ? HumanBodyBones.RightHand : HumanBodyBones.LeftHand
            );
            if (handBone == null)
            {
                Debug.LogWarning(
                    $"Cannot spawn {equipment.itemName} visually: Humanoid Animator missing hand bone."
                );
                return;
            }

            GameObject instance = Instantiate(equipment.prefab, handBone);

            WeaponGrip grip = instance.GetComponent<WeaponGrip>();
            if (grip != null)
            {
                instance.transform.localPosition = grip.positionalOffset;
                instance.transform.localEulerAngles = grip.rotationalOffset;
            }
            else
            {
                instance.transform.localPosition = Vector3.zero;
                instance.transform.localRotation = Quaternion.identity;
            }

            if (handSlot == 1)
                mainHandInstance = instance;
            else
                offHandInstance = instance;

            if (equipment is WeaponSO weaponData)
            {
                animator.SetInteger("WeaponType", weaponData.weaponAnimType);
            }
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
        /// Updates the MeleeAction/RangedAction components based on the currently equipped weapons.
        /// Uses a component pooling pattern rather than destroying at runtime.
        /// </summary>
        public void ConfigureStrikeActions()
        {
            // Get existing components instead of destroying them
            var meleeActions = GetComponents<MeleeAction>();
            var rangedActions = GetComponents<RangedAction>();

            // Disable them all first
            foreach (var melee in meleeActions)
                melee.enabled = false;
            foreach (var ranged in rangedActions)
                ranged.enabled = false;

            // Figure out what weapons we actually have
            var weapons = new List<WeaponSO>();
            WeaponSO mainWeapon = mainHand != null ? mainHand : unarmedFallback;
            if (mainWeapon != null)
                weapons.Add(mainWeapon);
            if (offHand is WeaponSO offWeapon && offWeapon != mainWeapon)
                weapons.Add(offWeapon);

            // Update or Add components as needed
            int meleeIndex = 0;
            int rangedIndex = 0;

            foreach (var weapon in weapons)
            {
                if (weapon.reachFeet > 0)
                {
                    // Reuse existing or add new
                    MeleeAction melee =
                        meleeIndex < meleeActions.Length
                            ? meleeActions[meleeIndex]
                            : gameObject.AddComponent<MeleeAction>();

                    melee.activeWeapon = weapon;
                    melee.enabled = true;
                    meleeIndex++;
                }

                if (weapon.IsRangedWeapon())
                {
                    RangedAction ranged =
                        rangedIndex < rangedActions.Length
                            ? rangedActions[rangedIndex]
                            : gameObject.AddComponent<RangedAction>();

                    ranged.activeWeapon = weapon;
                    ranged.enabled = true;
                    rangedIndex++;
                }
            }

            // Refresh the action economy cache so the UI picks up the updated components
            var actionEconomy = GetComponent<UnitActionEconomy>();
            if (actionEconomy != null)
                actionEconomy.RefreshActions();

            Debug.Log(
                $"[EQUIPMENT] {gameObject.name}: Configured {weapons.Count} weapon(s) into strike actions."
            );
        }
    }
}
