using System.Collections.Generic;
using PathfinderTactics.Actions;
using PathfinderTactics.Characters;
using PathfinderTactics.Combat;
using PathfinderTactics.Core;
using UnityEngine;

namespace PathfinderTactics.Grid
{
    public class MoveRangeVisualizer : MonoBehaviour
    {
        [Header("Outer Outline (Base)")]
        [SerializeField]
        private float outerWidth = 0.25f;

        [SerializeField]
        private Material outerMaterial;

        [Header("Inner Outline (Core)")]
        [SerializeField]
        private float innerWidth = 0.08f;

        [SerializeField]
        private Material innerMaterial;

        [Header("Action Range Outline")]
        [SerializeField]
        private Material actionOuterMaterial;

        [SerializeField]
        private Material actionInnerMaterial;

        [Header("Smoothing Settings")]
        [SerializeField]
        private float cornerRadius = 0.4f;

        [SerializeField]
        [Range(2, 10)]
        private int cornerResolution = 6;

        [Header("Targeting Tiles (Fallback)")]
        [SerializeField]
        private GameObject attackRangeTilePrefab; // Soft Red (Bounds)

        [SerializeField]
        private GameObject attackTargetTilePrefab; // Bright Red (Valid Targets)

        private List<GameObject> activeVisuals = new List<GameObject>();
        private Transform visualParent;

        private void Start()
        {
            ServiceLocator.Get<UnitActionSystem>().OnSelectedUnitChanged +=
                UnitActionSystem_OnStateChanged;

            if (ServiceLocator.TryGet<PhaseManager>(out var phaseManager))
            {
                phaseManager.OnPhaseChanged += PhaseManager_OnPhaseChanged;
            }

            visualParent = new GameObject("ActionRangeVisuals").transform;

            // Create a default material if none assigned
            if (outerMaterial == null)
            {
                outerMaterial = new Material(Shader.Find("Sprites/Default"));
            }
            if (innerMaterial == null)
            {
                innerMaterial = new Material(Shader.Find("Sprites/Default"));
            }
            if (actionOuterMaterial == null)
            {
                actionOuterMaterial = new Material(Shader.Find("Sprites/Default"))
                {
                    color = new Color(1f, 0f, 0f, 0.5f),
                };
            }
            if (actionInnerMaterial == null)
            {
                actionInnerMaterial = new Material(Shader.Find("Sprites/Default"))
                {
                    color = new Color(1f, 0.2f, 0.2f, 1f),
                };
            }

            UpdateVisuals();
        }

        private void OnDestroy()
        {
            if (ServiceLocator.TryGet<UnitActionSystem>(out var unitActionSystem))
            {
                unitActionSystem.OnSelectedUnitChanged -= UnitActionSystem_OnStateChanged;
            }
            if (ServiceLocator.TryGet<PhaseManager>(out var phaseManager))
            {
                phaseManager.OnPhaseChanged += PhaseManager_OnPhaseChanged;
            }
        }

        private void PhaseManager_OnPhaseChanged(object sender, GamePhase newPhase)
        {
            UpdateVisuals();
        }

        private void UnitActionSystem_OnStateChanged(object sender, System.EventArgs e)
        {
            UpdateVisuals();
        }

        private void UpdateVisuals()
        {
            ClearVisuals();

            Unit selectedUnit = ServiceLocator.Get<UnitActionSystem>().SelectedUnit;
            if (selectedUnit == null)
                return;

            GamePhase currentPhase = ServiceLocator.Get<PhaseManager>().CurrentPhase;

            // If moving or observing, Show Blue Outline
            if (
                currentPhase == GamePhase.FreeMovement
                || currentPhase == GamePhase.ActionSelection
                || currentPhase == GamePhase.EagleEye
            )
            {
                ShowMoveRangeOutline(selectedUnit);
            }
            // If targeting, Show Red Tiles
            else if (currentPhase == GamePhase.ActionTargeting)
            {
                ShowActionRange();
            }
        }

        private void ShowMoveRangeOutline(Unit unit)
        {
            List<Vector3Int> positions = null;
            if (ServiceLocator.TryGet<UnitActionSystem>(out var uas) && uas != null)
            {
                positions = uas.GetValidMovePositions();
            }

            if (positions == null)
            {
                int maxMoveCost = unit.GetMaxMoveCost();
                positions = Pathfinding.GetReachablePositions(
                    unit.CurrentLayeredPosition,
                    maxMoveCost
                );
            }

            DrawOutlines(positions, outerMaterial, innerMaterial);
        }

        private void DrawOutlines(List<Vector3Int> positions, Material outMat, Material inMat)
        {
            if (positions == null || positions.Count == 0)
                return;

            // Group by Elevation (Y)
            Dictionary<int, HashSet<Vector3Int>> layeredPositions =
                new Dictionary<int, HashSet<Vector3Int>>();

            foreach (var pos in positions)
            {
                if (!layeredPositions.ContainsKey(pos.y))
                    layeredPositions[pos.y] = new HashSet<Vector3Int>();
                layeredPositions[pos.y].Add(pos);
            }

            GridSystem grid = ServiceLocator.Get<GridSystem>();
            float S = grid.CellSize;
            float H = 0.05f;

            foreach (var entry in layeredPositions)
            {
                HashSet<Vector3Int> layerSet = entry.Value;
                Dictionary<Vector3, List<Vector3>> adjacency =
                    new Dictionary<Vector3, List<Vector3>>();

                foreach (Vector3Int pos in layerSet)
                {
                    Vector3 center = grid.GetWorldPosition(pos);
                    center.y += H;

                    // Check neighbors to build undirected graph of boundary edges
                    var dirs = new Vector3Int[]
                    {
                        new Vector3Int(0, 0, 1), // N
                        new Vector3Int(0, 0, -1), // S
                        new Vector3Int(1, 0, 0), // E
                        new Vector3Int(-1, 0, 0), // W
                    };

                    foreach (var dir in dirs)
                    {
                        if (!layerSet.Contains(pos + dir))
                        {
                            Vector3 p1,
                                p2;
                            if (dir.z != 0) // N or S
                            {
                                p1 = center + new Vector3(-0.5f * S, 0, 0.5f * S * dir.z);
                                p2 = center + new Vector3(0.5f * S, 0, 0.5f * S * dir.z);
                            }
                            else // E or W
                            {
                                p1 = center + new Vector3(0.5f * S * dir.x, 0, -0.5f * S);
                                p2 = center + new Vector3(0.5f * S * dir.x, 0, 0.5f * S);
                            }
                            AddEdgeToGraph(adjacency, p1, p2);
                        }
                    }
                }

                // Trace continuous loops from the graph
                List<List<Vector3>> loops = TraceLoops(adjacency);
                foreach (var loop in loops)
                {
                    GenerateDualOutline(loop, layerSet, entry.Key, outMat, inMat);
                }
            }
        }

        private void AddEdgeToGraph(Dictionary<Vector3, List<Vector3>> adj, Vector3 p1, Vector3 p2)
        {
            if (!adj.ContainsKey(p1))
                adj[p1] = new List<Vector3>();
            if (!adj.ContainsKey(p2))
                adj[p2] = new List<Vector3>();

            // Small tolerance for grid precision issues
            bool alreadyHasP2 = false;
            foreach (var existing in adj[p1])
            {
                if (Vector3.Distance(existing, p2) < 0.01f)
                {
                    alreadyHasP2 = true;
                    break;
                }
            }
            if (!alreadyHasP2)
                adj[p1].Add(p2);

            bool alreadyHasP1 = false;
            foreach (var existing in adj[p2])
            {
                if (Vector3.Distance(existing, p1) < 0.01f)
                {
                    alreadyHasP1 = true;
                    break;
                }
            }
            if (!alreadyHasP1)
                adj[p2].Add(p1);
        }

        private List<List<Vector3>> TraceLoops(Dictionary<Vector3, List<Vector3>> adj)
        {
            List<List<Vector3>> loops = new List<List<Vector3>>();
            HashSet<Vector3> visited = new HashSet<Vector3>();

            foreach (var start in adj.Keys)
            {
                if (visited.Contains(start))
                    continue;

                List<Vector3> loop = new List<Vector3>();
                Vector3 current = start;
                Vector3 prev = Vector3.zero;

                while (current != Vector3.zero)
                {
                    loop.Add(current);
                    visited.Add(current);

                    Vector3 next = Vector3.zero;
                    if (adj.ContainsKey(current))
                    {
                        foreach (var neighbor in adj[current])
                        {
                            if (neighbor == prev)
                                continue;
                            if (neighbor == start && loop.Count > 2)
                            {
                                // Loop closed!
                                loops.Add(loop);
                                current = Vector3.zero;
                                break;
                            }
                            if (!visited.Contains(neighbor))
                            {
                                next = neighbor;
                                break;
                            }
                        }
                    }

                    if (current != Vector3.zero)
                    {
                        if (next == Vector3.zero)
                            break; // Dead end
                        prev = current;
                        current = next;
                    }
                }
            }
            return loops;
        }

        private void GenerateDualOutline(
            List<Vector3> points,
            HashSet<Vector3Int> layerSet,
            int layerY,
            Material outMat,
            Material inMat
        )
        {
            if (points.Count < 3)
                return;

            GridSystem grid = ServiceLocator.Get<GridSystem>();

            // Generate the base "Smooth Path"
            List<Vector3> smoothBase = GenerateCurvedPath(points);

            // Determine which direction the computed normal faces
            // by probing the actual grid. Take a point on the boundary,
            // offset it slightly in the normal direction, and check if
            // the resulting grid cell is walkable.
            Vector3 probePoint = (points[0] + points[1]) * 0.5f;
            Vector3 probeTangent = (points[1] - points[0]).normalized;
            Vector3 probeNormal = new Vector3(-probeTangent.z, 0, probeTangent.x);
            Vector3 probeSample = probePoint + probeNormal * (grid.CellSize * 0.4f);

            GridPosition probeGP = grid.GetGridPosition(probeSample);
            Vector3Int probeLayered = new Vector3Int(probeGP.x, layerY, probeGP.z);
            bool normalPointsTowardTiles = layerSet.Contains(probeLayered);

            // If the normal points toward tiles, we need to flip the offsets
            // so that the "Outer" material always faces the void (non-walkable area)
            float sign = normalPointsTowardTiles ? -1f : 1f;

            // Generate Concentric Offset Paths
            List<Vector3> outerPath = GenerateOffsetPath(smoothBase, (outerWidth / 2f) * sign);
            List<Vector3> innerPath = GenerateOffsetPath(smoothBase, (-innerWidth / 2f) * sign);

            // Render
            CreateLineRenderer("OuterOutline", outerPath, outerWidth, outMat, 0.04f);
            CreateLineRenderer("InnerOutline", innerPath, innerWidth, inMat, 0.06f);
        }

        private List<Vector3> GenerateCurvedPath(List<Vector3> points)
        {
            List<Vector3> smoothPoints = new List<Vector3>();
            int n = points.Count;
            GridSystem grid = ServiceLocator.Get<GridSystem>();
            float actualRadius = Mathf.Min(cornerRadius, grid.CellSize * 0.45f);

            for (int i = 0; i < n; i++)
            {
                Vector3 prev = points[(i + n - 1) % n];
                Vector3 curr = points[i];
                Vector3 next = points[(i + 1) % n];

                Vector3 toPrev = (prev - curr).normalized;
                Vector3 toNext = (next - curr).normalized;

                if (Vector3.Dot(toPrev, toNext) < -0.999f)
                {
                    smoothPoints.Add(curr);
                    continue;
                }

                Vector3 startPoint = curr + toPrev * actualRadius;
                Vector3 endPoint = curr + toNext * actualRadius;

                for (int j = 0; j <= cornerResolution; j++)
                {
                    float t = (float)j / cornerResolution;
                    Vector3 p = Vector3.Lerp(
                        Vector3.Lerp(startPoint, curr, t),
                        Vector3.Lerp(curr, endPoint, t),
                        t
                    );
                    smoothPoints.Add(p);
                }
            }
            return smoothPoints;
        }

        private List<Vector3> GenerateOffsetPath(List<Vector3> points, float offset)
        {
            List<Vector3> offsetPoints = new List<Vector3>();
            int n = points.Count;

            for (int i = 0; i < n; i++)
            {
                Vector3 prev = points[(i + n - 1) % n];
                Vector3 curr = points[i];
                Vector3 next = points[(i + 1) % n];

                Vector3 tangentPrev = (curr - prev).normalized;
                Vector3 tangentNext = (next - curr).normalized;

                // 2D Normal
                Vector3 n1 = new Vector3(-tangentPrev.z, 0, tangentPrev.x);
                Vector3 n2 = new Vector3(-tangentNext.z, 0, tangentNext.x);
                Vector3 avgNormal = (n1 + n2).normalized;

                // Handle miter joint (sharpened corner)
                float dot = Vector3.Dot(n1, n2);
                float miterScale = 1.0f;
                if (dot > -0.99f)
                {
                    // Scale offset based on the angle to keep parallel lines
                    float angle = Vector3.Angle(n1, n2);
                    miterScale = 1.0f / Mathf.Cos(angle * 0.5f * Mathf.Deg2Rad);
                }

                // Prevent extreme miters on very sharp angles
                miterScale = Mathf.Min(miterScale, 2.0f);

                offsetPoints.Add(curr + avgNormal * offset * miterScale);
            }

            return offsetPoints;
        }

        private void CreateLineRenderer(
            string name,
            List<Vector3> points,
            float width,
            Material material,
            float yOffset
        )
        {
            GameObject obj = new GameObject(name);
            obj.transform.SetParent(visualParent);

            // Apply height offset
            Vector3[] offsetPoints = new Vector3[points.Count];
            for (int i = 0; i < points.Count; i++)
            {
                offsetPoints[i] = points[i] + new Vector3(0, yOffset, 0);
            }

            LineRenderer lr = obj.AddComponent<LineRenderer>();
            lr.startWidth = width;
            lr.endWidth = width;
            lr.positionCount = offsetPoints.Length;
            lr.SetPositions(offsetPoints);
            lr.loop = true;
            lr.material = material;
            lr.startColor = Color.white;
            lr.endColor = Color.white;
            lr.useWorldSpace = true;
            lr.alignment = LineAlignment.TransformZ;
            lr.sortingOrder = Mathf.RoundToInt(yOffset * 100);

            obj.transform.rotation = Quaternion.Euler(90, 0, 0);
            activeVisuals.Add(obj);
        }

        private void ShowActionRange()
        {
            BaseAction selectedAction = ServiceLocator.Get<UnitActionSystem>().GetSelectedAction();
            if (selectedAction == null)
                return;

            List<Vector3Int> layeredPositions = selectedAction.GetActionRangeGridPositions();
            DrawOutlines(layeredPositions, actionOuterMaterial, actionInnerMaterial);

            List<Vector3Int> validTargets = selectedAction.GetValidActionGridPositions();

            // Spawn bright red for tiles containing enemies
            SpawnTiles(
                validTargets,
                attackTargetTilePrefab,
                ServiceLocator.Get<UnitActionSystem>().SelectedUnit
            );
        }

        private void SpawnTiles(List<Vector3Int> positions, GameObject prefab, Unit referenceUnit)
        {
            if (prefab == null)
                return;

            GridSystem grid = ServiceLocator.Get<GridSystem>();
            float tileScale = grid.CellSize;

            foreach (Vector3Int pos in positions)
            {
                Vector3 worldPos = grid.GetWorldPosition(pos);
                Vector3 visualPos = worldPos + new Vector3(0, 0.02f, 0);

                GameObject tile = Instantiate(
                    prefab,
                    visualPos,
                    Quaternion.Euler(0, 0, 0),
                    visualParent
                );
                tile.transform.rotation = Quaternion.Euler(90, 0, 0);

                DisableColliders(tile);
                tile.transform.localScale = new Vector3(tileScale, tileScale, tileScale);
                activeVisuals.Add(tile);
            }
        }

        private void ClearVisuals()
        {
            foreach (GameObject visual in activeVisuals)
            {
                Destroy(visual);
            }
            activeVisuals.Clear();
        }

        private static void DisableColliders(GameObject root)
        {
            Collider[] colliders = root.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < colliders.Length; i++)
            {
                colliders[i].enabled = false;
            }
        }
    }
}
