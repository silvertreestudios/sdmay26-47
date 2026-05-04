using TacticsGame.Core;
using TacticsGame.Grid;
using UnityEngine;

namespace TacticsGame.Spells.Effects
{
    /// <summary>
    /// Resolution Phase: Modifies the terrain of affected cells (e.g. creating difficult terrain).
    /// </summary>
    [CreateAssetMenu(menuName = "TacticsRuleset/Spell Effects/Terrain Modification")]
    public class TerrainModificationEffectSO : SpellEffectSO
    {
        [Header("Modification Configuration")]
        public int MovementCostOverride = 2; // Standard difficult terrain is 2
        public bool AddToExistingCost = false;

        [Header("Duration & Visuals")]
        public bool HasDuration = true;
        public GameObject PersistentEffectPrefab;

        private void OnEnable()
        {
            Phase = SpellEffectPhase.Resolution;
        }

        public override void Apply(SpellCastContext context)
        {
            GridSystem gridSystem = ServiceLocator.Get<GridSystem>();
            if (gridSystem == null)
                return;

            foreach (Vector3Int cell in context.AffectedCells)
            {
                GridNode node = gridSystem.GetNode(cell);
                if (node != null && node.Terrain != null)
                {
                    // Clone to avoid modifying shared scene templates
                    TerrainDef newTerrain = node.Terrain.Clone();

                    if (AddToExistingCost)
                        newTerrain.MovementCost += MovementCostOverride;
                    else
                        newTerrain.MovementCost = MovementCostOverride;

                    node.Terrain = newTerrain;
                }
            }

            // If it has a duration, spawn the manager to handle cleanup
            if (HasDuration && context.AffectedCells.Count > 0)
            {
                GameObject effectObj = new GameObject(
                    $"PersistentTerrain_{context.SpellData.ElementName}"
                );
                PersistentGridEffect persistent = effectObj.AddComponent<PersistentGridEffect>();
                persistent.Initialize(
                    context.Caster,
                    context.AffectedCells,
                    PersistentEffectPrefab
                );
            }

            Debug.Log(
                $"<color=green>[TERRAIN]</color> {context.SpellData.ElementName} modified {context.AffectedCells.Count} cells."
            );
        }
    }
}
