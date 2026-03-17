using System.Collections.Generic;
using PathfinderTactics.Core;
using PathfinderTactics.Grid;
using PathfinderTactics.ScriptableObjects;
using UnityEngine;

namespace PathfinderTactics.Characters
{
    /// <summary>
    /// Manages the visual representation of auras for a unit.
    /// highlights tiles within the aura's radius with the specified color.
    /// </summary>
    [RequireComponent(typeof(UnitAuraEmitter))]
    public class UnitAuraVisualizer : MonoBehaviour
    {
        [Header("Prefabs")]
        [SerializeField]
        private GameObject auraTilePrefab;

        private Unit unit;
        private UnitAuraEmitter emitter;
        private GridSystem gridSystem;

        private Dictionary<AuraEffectSO, List<GameObject>> activeAuras =
            new Dictionary<AuraEffectSO, List<GameObject>>();
        private Transform visualParent;
        private GridPosition lastPosition;
        private int lastAuraCount;
        private List<int> lastRadii = new List<int>();

        private void Awake()
        {
            unit = GetComponent<Unit>();
            emitter = GetComponent<UnitAuraEmitter>();
        }

        private void Start()
        {
            gridSystem = ServiceLocator.Get<GridSystem>();
            visualParent = new GameObject($"{unit.name}_AuraVisuals").transform;
            lastPosition = unit.CurrentGridPosition;

            var auras = emitter.GetAuras();
            lastAuraCount = auras?.Count ?? 0;
            UpdateRadiiCache(auras);

            InitializeVisuals();
        }

        private void Update()
        {
            var auras = emitter.GetAuras();
            bool needsRefresh = false;

            // Position change
            GridPosition currentPos = gridSystem.GetGridPosition(unit.transform.position);
            if (currentPos != lastPosition)
            {
                lastPosition = currentPos;
                needsRefresh = true;
            }

            // Aura list count change
            int currentAuraCount = auras?.Count ?? 0;
            if (currentAuraCount != lastAuraCount)
            {
                lastAuraCount = currentAuraCount;
                needsRefresh = true;
            }

            // Aura radius change
            if (!needsRefresh && auras != null)
            {
                for (int i = 0; i < auras.Count; i++)
                {
                    if (i >= lastRadii.Count || auras[i].radiusInTiles != lastRadii[i])
                    {
                        needsRefresh = true;
                        break;
                    }
                }
            }

            if (needsRefresh)
            {
                UpdateRadiiCache(auras);
                RefreshAllAuras();
            }
        }

        private void UpdateRadiiCache(List<AuraEffectSO> auras)
        {
            lastRadii.Clear();
            if (auras == null)
                return;
            foreach (var a in auras)
                lastRadii.Add(a.radiusInTiles);
        }

        private void InitializeVisuals()
        {
            RefreshAllAuras();
        }

        public void RefreshAllAuras()
        {
            ClearAllAuras();

            List<AuraEffectSO> auras = emitter.GetAuras();
            if (auras == null)
                return;

            foreach (var aura in auras)
            {
                CreateAuraVisual(aura);
            }
        }

        private void CreateAuraVisual(AuraEffectSO aura)
        {
            if (auraTilePrefab == null)
            {
                Debug.LogWarning($"[AURA VISUALIZER] Aura tile prefab not assigned on {unit.name}");
                return;
            }

            List<GameObject> tiles = new List<GameObject>();
            float tileScale = gridSystem.CellSize;

            // Get all grid positions within range
            List<GridPosition> positions = GetAuraPositions(aura);

            foreach (var pos in positions)
            {
                Vector3 worldPos = gridSystem.GetWorldPosition(pos);
                // Slight offset to prevent Z-fighting with other grid visuals
                Vector3 visualPos = worldPos + new Vector3(0, 0.012f, 0);

                GameObject tileObj = Instantiate(
                    auraTilePrefab,
                    visualPos,
                    Quaternion.Euler(90, 0, 0),
                    visualParent
                );
                tileObj.transform.localScale = new Vector3(tileScale, tileScale, tileScale);

                // Use AuraTile component for color management
                AuraTile auraTile = tileObj.GetComponent<AuraTile>();
                if (auraTile == null)
                    auraTile = tileObj.AddComponent<AuraTile>();

                Color color = aura.auraColor;
                color.a = 0.25f; // Standard transparency for aura highlights
                auraTile.SetColor(color);

                tiles.Add(tileObj);
            }

            activeAuras[aura] = tiles;
        }

        private List<GridPosition> GetAuraPositions(AuraEffectSO aura)
        {
            List<GridPosition> positions = new List<GridPosition>();
            GridPosition center = lastPosition; // world-position based center
            int radius = aura.radiusInTiles;

            float radiusF = radius + 0.5f;
            float radiusSq = radiusF * radiusF;

            for (int x = -radius; x <= radius; x++)
            {
                for (int z = -radius; z <= radius; z++)
                {
                    // Distance check (Same as Emanation)
                    float distSq = (float)(x * x + z * z);
                    if (distSq <= radiusSq)
                    {
                        GridPosition pos = new GridPosition(center.x + x, center.z + z);
                        if (gridSystem.IsValidGridPosition(pos))
                        {
                            positions.Add(pos);
                        }
                    }
                }
            }
            return positions;
        }

        private void ClearAllAuras()
        {
            foreach (var pair in activeAuras)
            {
                foreach (var tile in pair.Value)
                {
                    Destroy(tile);
                }
            }
            activeAuras.Clear();
        }

        private void OnDestroy()
        {
            if (visualParent != null)
            {
                Destroy(visualParent.gameObject);
            }
        }
    }
}
