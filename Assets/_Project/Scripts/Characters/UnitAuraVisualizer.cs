using System.Collections.Generic;
using TacticsGame.Core;
using TacticsGame.Grid;
using TacticsGame.ScriptableObjects;
using UnityEngine;

namespace TacticsGame.Characters
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

            List<Vector3Int> positions = GetAuraPositions3D(aura);

            foreach (Vector3Int pos in positions)
            {
                Vector3 worldPos = gridSystem.GetWorldPosition(pos);
                Vector3 visualPos = worldPos + new Vector3(0, 0.012f, 0);

                GameObject tileObj = Instantiate(
                    auraTilePrefab,
                    visualPos,
                    Quaternion.Euler(90, 0, 0),
                    visualParent
                );
                tileObj.transform.localScale = new Vector3(tileScale, tileScale, tileScale);

                AuraTile auraTile = tileObj.GetComponent<AuraTile>();
                if (auraTile == null)
                    auraTile = tileObj.AddComponent<AuraTile>();

                Color color = aura.auraColor;
                color.a = 0.25f;
                auraTile.SetColor(color);

                tiles.Add(tileObj);
            }

            activeAuras[aura] = tiles;
        }

        private List<Vector3Int> GetAuraPositions3D(AuraEffectSO aura)
        {
            List<Vector3Int> positions = new List<Vector3Int>();
            // Use the unit's live world position so tiles follow in real-time
            // during free movement, not just after move finalization.
            Vector3Int center = gridSystem.GetLayeredGridPosition(unit.transform.position);
            int radius = aura.radiusInTiles;

            for (int x = -radius; x <= radius; x++)
            {
                for (int z = -radius; z <= radius; z++)
                {
                    Vector2Int colKey = new Vector2Int(center.x + x, center.z + z);
                    List<GridNode> column = gridSystem.GetColumn(colKey);
                    if (column == null || column.Count == 0)
                        continue;

                    foreach (GridNode node in column)
                    {
                        int dist = TacticsRuleset_Core.GetTacticsRulesetDistance3D(
                            center,
                            node.Coordinates
                        );
                        if (dist <= radius)
                            positions.Add(node.Coordinates);
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
