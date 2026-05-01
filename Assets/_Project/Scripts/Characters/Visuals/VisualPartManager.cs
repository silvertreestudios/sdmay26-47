using System;
using System.Collections.Generic;
using UnityEngine;

namespace TacticsGame.Characters.Visuals
{
    [Serializable]
    public class VisualSlotBinding
    {
        public VisualSlot slot;

        [Tooltip("Use this for rigged body parts (Body, Arms, Legs).")]
        public SkinnedMeshRenderer smr;

        [Tooltip("Use these for static attachments parented to bones (Helmets, Masks).")]
        public MeshFilter staticMeshFilter;
        public MeshRenderer staticMeshRenderer;
    }

    public class VisualPartManager : MonoBehaviour
    {
        [Header("Rig Slots")]
        [Tooltip("Pre-rigged SkinnedMeshRenderers on the base rig.")]
        [SerializeField]
        private List<VisualSlotBinding> slotBindings = new List<VisualSlotBinding>();

        private MaterialPropertyBlock propBlock;
        private static readonly int ColorProperty = Shader.PropertyToID("_Color"); // Adjust depending on shader

        private void Awake()
        {
            propBlock = new MaterialPropertyBlock();
        }

        public void EquipPart(VisualPartSO part)
        {
            if (part == null)
            {
                Debug.LogWarning("[Char Debug] VisualPartManager.EquipPart called with null part!");
                return;
            }

            Debug.Log(
                $"[Char Debug] Attempting to equip part: {part.name} (Slot: {part.Slot}, IsStatic: {part.IsStaticMesh})"
            );

            var binding = slotBindings.Find(b => b.slot == part.Slot);
            if (binding != null)
            {
                // Clear both so we don't have overlapping SMR and Static meshes
                if (binding.smr != null)
                    binding.smr.sharedMesh = null;
                if (binding.staticMeshFilter != null)
                    binding.staticMeshFilter.sharedMesh = null;

                if (part.IsStaticMesh)
                {
                    if (binding.staticMeshFilter != null && binding.staticMeshRenderer != null)
                    {
                        binding.staticMeshFilter.sharedMesh = part.SharedMesh;
                        binding.staticMeshRenderer.materials = part.Materials;
                        Debug.Log(
                            $"[Char Debug] Successfully equipped Static Mesh to {part.Slot}. SharedMesh: {(part.SharedMesh != null ? part.SharedMesh.name : "NULL")}, Materials Count: {(part.Materials != null ? part.Materials.Length : 0)}"
                        );
                    }
                    else
                    {
                        Debug.LogWarning(
                            $"[Char Debug] VisualPartManager: No Static Mesh Renderer found for slot {part.Slot}"
                        );
                    }
                }
                else
                {
                    if (binding.smr != null)
                    {
                        binding.smr.sharedMesh = part.SharedMesh;
                        binding.smr.materials = part.Materials;
                        Debug.Log(
                            $"[Char Debug] Successfully equipped Skinned Mesh to {part.Slot}. SharedMesh: {(part.SharedMesh != null ? part.SharedMesh.name : "NULL")}, Materials Count: {(part.Materials != null ? part.Materials.Length : 0)}"
                        );
                    }
                    else
                    {
                        Debug.LogWarning(
                            $"[Char Debug] VisualPartManager: No SkinnedMeshRenderer found for slot {part.Slot}"
                        );
                    }
                }
            }
            else
            {
                Debug.LogWarning(
                    $"[Char Debug] No slot binding found in VisualPartManager for slot: {part.Slot}"
                );
            }
        }

        public void SetArmorColor(Color color)
        {
            if (propBlock == null)
                propBlock = new MaterialPropertyBlock();

            // Example: apply color only to Torso, Arms, Legs, Cape, Helmet
            // Adjust exactly which slots are considered "Armor"
            VisualSlot[] armorSlots =
            {
                VisualSlot.Body,
                VisualSlot.ArmLeft,
                VisualSlot.ArmRight,
                VisualSlot.LegLeft,
                VisualSlot.LegRight,
                VisualSlot.Cape,
                VisualSlot.Helmet,
            };

            foreach (var slot in armorSlots)
            {
                var binding = slotBindings.Find(b => b.slot == slot);
                if (binding != null)
                {
                    if (binding.smr != null)
                    {
                        binding.smr.GetPropertyBlock(propBlock);
                        propBlock.SetColor(ColorProperty, color);
                        binding.smr.SetPropertyBlock(propBlock);
                    }
                    else if (binding.staticMeshRenderer != null)
                    {
                        binding.staticMeshRenderer.GetPropertyBlock(propBlock);
                        propBlock.SetColor(ColorProperty, color);
                        binding.staticMeshRenderer.SetPropertyBlock(propBlock);
                    }
                }
            }
        }

        public void ClearSlot(VisualSlot slot)
        {
            var binding = slotBindings.Find(b => b.slot == slot);
            if (binding != null)
            {
                if (binding.smr != null)
                    binding.smr.sharedMesh = null;
                if (binding.staticMeshFilter != null)
                    binding.staticMeshFilter.sharedMesh = null;
            }
        }
    }
}
