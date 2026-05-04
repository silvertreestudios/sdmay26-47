using System;
using System.Collections.Generic;
using TacticsGame.Characters;
using TacticsGame.Core;
using TacticsGame.Grid;
using UnityEngine;

namespace TacticsGame.Spells.Effects
{
    /// <summary>
    /// Manages an effect that stays on the grid for a duration (e.g. Howling Blizzard's snowdrifts).
    /// Handles spawning particles on affected tiles and reverting terrain costs when the duration ends.
    /// </summary>
    public class PersistentGridEffect : MonoBehaviour
    {
        [Header("Visuals")]
        [SerializeField]
        private GameObject tileParticlePrefab;

        [SerializeField]
        private Vector3 particleOffset = new Vector3(0, 0.1f, 0);

        private Unit caster;
        private List<Vector3Int> affectedCells;
        private Dictionary<Vector3Int, TerrainDef> originalTerrains;
        private List<GameObject> spawnedParticles;
        private bool isCleaningUp = false;

        public void Initialize(
            Unit caster,
            List<Vector3Int> cells,
            GameObject particlePrefab = null
        )
        {
            this.caster = caster;
            this.affectedCells = new List<Vector3Int>(cells);
            this.originalTerrains = new Dictionary<Vector3Int, TerrainDef>();
            this.spawnedParticles = new List<GameObject>();

            if (particlePrefab != null)
                this.tileParticlePrefab = particlePrefab;

            GridSystem grid = ServiceLocator.Get<GridSystem>();

            // Store originals and spawn particles
            foreach (var cell in affectedCells)
            {
                GridNode node = grid.GetNode(cell);
                if (node != null)
                {
                    // Store original
                    originalTerrains[cell] = node.Terrain.Clone();

                    // Spawn visuals
                    if (tileParticlePrefab != null)
                    {
                        Vector3 worldPos = grid.GetWorldPosition(cell) + particleOffset;
                        GameObject particles = Instantiate(
                            tileParticlePrefab,
                            worldPos,
                            Quaternion.identity,
                            transform
                        );
                        spawnedParticles.Add(particles);
                    }
                }
            }

            // Listen for turn changes
            var turnManager = ServiceLocator.Get<TurnManager>();
            if (turnManager != null)
            {
                turnManager.OnTurnChanged += TurnManager_OnTurnChanged;
            }
        }

        private void OnDestroy()
        {
            Cleanup();
        }

        private void TurnManager_OnTurnChanged(object sender, EventArgs e)
        {
            var turnManager = ServiceLocator.Get<TurnManager>();

            // Go away at the START of the caster's next turn
            if (turnManager.CurrentUnit == caster)
            {
                Cleanup();
            }
        }

        private void Cleanup()
        {
            if (isCleaningUp)
                return;
            isCleaningUp = true;

            // Unsubscribe
            var turnManager = ServiceLocator.Get<TurnManager>();
            if (turnManager != null)
            {
                turnManager.OnTurnChanged -= TurnManager_OnTurnChanged;
            }

            // Revert Terrain
            GridSystem grid = ServiceLocator.Get<GridSystem>();
            if (grid != null)
            {
                foreach (var kvp in originalTerrains)
                {
                    GridNode node = grid.GetNode(kvp.Key);
                    if (node != null)
                    {
                        node.Terrain = kvp.Value;
                    }
                }
            }

            // Destroy particles (if they don't have auto-destroy)
            foreach (var p in spawnedParticles)
            {
                if (p != null)
                    Destroy(p);
            }

            // Festroy the manager object
            Destroy(gameObject);
        }
    }
}
