using System.Collections.Generic;
using TacticsGame.Actions;
using TacticsGame.Characters;
using TacticsGame.Combat;
using TacticsGame.Core;
using UnityEngine;

namespace TacticsGame.Grid
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

        [Header("3D Line Settings")]
        [SerializeField]
        private float lineThickness = 0.05f;

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
            ServiceLocator.Get<UnitActionSystem>().OnValidPositionsChanged +=
                HandleValidPositionsChanged;

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
                phaseManager.OnPhaseChanged -= PhaseManager_OnPhaseChanged;
            }

            if (ServiceLocator.TryGet<UnitActionSystem>(out var uas))
            {
                uas.OnValidPositionsChanged -= HandleValidPositionsChanged;
            }
        }

        private void HandleValidPositionsChanged(object sender, System.EventArgs e)
        {
            UpdateVisuals();
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
                || currentPhase == GamePhase.Busy
            )
            {
                // Debug.Log(
                //     $"[MOVEMENT RANGE WAYPOINTS DEBUG] Phase {currentPhase} matches movement. Showing Range Outline."
                // );
                ShowMoveRangeOutline(selectedUnit);
            }
            // If targeting, Show Red Tiles
            else if (currentPhase == GamePhase.ActionTargeting)
            {
                ShowActionRange();
            }
            else
            {
                // Debug.Log(
                //     $"[MOVEMENT RANGE WAYPOINTS DEBUG] Phase {currentPhase} NOT movement. Hiding Range Outline."
                // );
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
                // Debug.Log(
                //     $"[MOVEMENT RANGE WAYPOINTS DEBUG] UAS positions was NULL for {unit.name}. Falling back to full range calc. Start: {unit.CurrentLayeredPosition}, Max: {maxMoveCost}"
                // );
                positions = Pathfinding.GetReachablePositions(
                    unit.CurrentLayeredPosition,
                    maxMoveCost
                );
            }
            else
            {
                // Debug.Log(
                //     $"[MOVEMENT RANGE WAYPOINTS DEBUG] Drawing Range with {positions.Count} positions provided by UAS."
                // );
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
                // Debug.Log(
                //     $"[MOVEMENT RANGE WAYPOINTS DEBUG] Layer Y={entry.Key}: Found {layerSet.Count} tiles, resulting in {loops.Count} loops."
                // );
                foreach (var loop in loops)
                {
                    GenerateDualOutline(loop, layerSet, entry.Key, outMat, inMat);
                }
            }
        }

        private Vector3 RoundVector(Vector3 v)
        {
            return new Vector3(
                Mathf.Round(v.x * 1000f) / 1000f,
                Mathf.Round(v.y * 1000f) / 1000f,
                Mathf.Round(v.z * 1000f) / 1000f
            );
        }

        private void AddEdgeToGraph(
            Dictionary<Vector3, List<Vector3>> adj,
            Vector3 rawP1,
            Vector3 rawP2
        )
        {
            Vector3 p1 = RoundVector(rawP1);
            Vector3 p2 = RoundVector(rawP2);

            if (!adj.ContainsKey(p1))
                adj[p1] = new List<Vector3>();
            if (!adj.ContainsKey(p2))
                adj[p2] = new List<Vector3>();

            bool alreadyHasP2 = false;
            foreach (var existing in adj[p1])
            {
                if (Vector3.Distance(existing, p2) < 0.001f)
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
                if (Vector3.Distance(existing, p1) < 0.001f)
                {
                    alreadyHasP1 = true;
                    break;
                }
            }
            if (!alreadyHasP1)
                adj[p2].Add(p1);
        }

        private (Vector3, Vector3) SortEdge(Vector3 v1, Vector3 v2)
        {
            // Robust sorting for ValueTuple edge keys
            if (
                v1.x < v2.x
                || (v1.x == v2.x && v1.y < v2.y)
                || (v1.x == v2.x && v1.y == v2.y && v1.z < v2.z)
            )
                return (v1, v2);
            return (v2, v1);
        }

        private List<List<Vector3>> TraceLoops(Dictionary<Vector3, List<Vector3>> adj)
        {
            List<List<Vector3>> loops = new List<List<Vector3>>();
            HashSet<(Vector3, Vector3)> visitedEdges = new HashSet<(Vector3, Vector3)>();

            // We iterate through all nodes and all their edges to find untraced cycles
            foreach (var startNode in adj.Keys)
            {
                foreach (var firstNeighbor in adj[startNode])
                {
                    var firstEdge = SortEdge(startNode, firstNeighbor);
                    if (visitedEdges.Contains(firstEdge))
                        continue;

                    // Start a new loop trace from this unused edge
                    List<Vector3> loop = new List<Vector3>();
                    Vector3 current = firstNeighbor;
                    Vector3 prev = startNode;
                    loop.Add(startNode);
                    visitedEdges.Add(firstEdge);

                    bool foundClosure = false;
                    const int MAX_ITER = 5000;
                    int iter = 0;

                    while (iter++ < MAX_ITER)
                    {
                        if (Vector3.Distance(current, startNode) < 0.001f)
                        {
                            foundClosure = true;
                            break;
                        }

                        loop.Add(current);
                        Vector3? nextNode = null;

                        if (adj.ContainsKey(current))
                        {
                            foreach (var neighbor in adj[current])
                            {
                                if (Vector3.Distance(neighbor, prev) < 0.001f)
                                    continue;

                                var edge = SortEdge(current, neighbor);
                                if (visitedEdges.Contains(edge))
                                    continue;

                                nextNode = neighbor;
                                visitedEdges.Add(edge);
                                break;
                            }
                        }

                        if (nextNode.HasValue)
                        {
                            prev = current;
                            current = nextNode.Value;
                        }
                        else
                        {
                            // Stuck at a dead end (shouldn't happen in a valid boundary graph)
                            // Debug.LogWarning($"[MOVEMENT RANGE WAYPOINTS DEBUG] Trace hit dead-end at {current} while starting from {startNode}");
                            break;
                        }
                    }

                    if (foundClosure && loop.Count >= 3)
                    {
                        loops.Add(loop);
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
            CreateExtrudedLineMesh(
                "OuterOutline",
                outerPath,
                outerWidth,
                lineThickness,
                outMat,
                0.04f,
                4
            );
            CreateExtrudedLineMesh(
                "InnerOutline",
                innerPath,
                innerWidth,
                lineThickness,
                inMat,
                0.04f,
                6
            );
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

        private void CreateExtrudedLineMesh(
            string name,
            List<Vector3> points,
            float width,
            float height,
            Material material,
            float yBaseOffset,
            int sortOrder
        )
        {
            if (points.Count < 3)
                return;

            GameObject obj = new GameObject(name);
            obj.transform.SetParent(visualParent);

            MeshRenderer mr = obj.AddComponent<MeshRenderer>();
            mr.material = material;
            mr.sortingOrder = sortOrder;

            MeshFilter mf = obj.AddComponent<MeshFilter>();
            Mesh mesh = new Mesh();
            mesh.name = name + "Mesh";

            int n = points.Count;
            Vector3[] bottomOuter = new Vector3[n];
            Vector3[] bottomInner = new Vector3[n];
            Vector3[] topOuter = new Vector3[n];
            Vector3[] topInner = new Vector3[n];

            for (int i = 0; i < n; i++)
            {
                Vector3 prev = points[(i + n - 1) % n];
                Vector3 curr = points[i];
                Vector3 next = points[(i + 1) % n];

                Vector3 tangentPrev = (curr - prev).normalized;
                Vector3 tangentNext = (next - curr).normalized;

                Vector3 n1 = new Vector3(-tangentPrev.z, 0, tangentPrev.x);
                Vector3 n2 = new Vector3(-tangentNext.z, 0, tangentNext.x);
                Vector3 avgNormal = (n1 + n2).normalized;

                float dot = Vector3.Dot(n1, n2);
                float miterScale = 1.0f;
                if (dot > -0.99f)
                {
                    float angle = Vector3.Angle(n1, n2);
                    miterScale = 1.0f / Mathf.Cos(angle * 0.5f * Mathf.Deg2Rad);
                }
                miterScale = Mathf.Min(miterScale, 2.0f);

                Vector3 rightOffset = avgNormal * (width * 0.5f * miterScale);
                Vector3 basePos = curr + new Vector3(0, yBaseOffset, 0);

                bottomOuter[i] = basePos + rightOffset;
                bottomInner[i] = basePos - rightOffset;
                topOuter[i] = bottomOuter[i] + new Vector3(0, height, 0);
                topInner[i] = bottomInner[i] + new Vector3(0, height, 0);
            }

            List<Vector3> verts = new List<Vector3>();
            List<int> tris = new List<int>();
            List<Vector2> uvs = new List<Vector2>();

            void AddQuad(Vector3 v0, Vector3 v1, Vector3 v2, Vector3 v3)
            {
                int startIndex = verts.Count;
                verts.Add(v0);
                verts.Add(v1);
                verts.Add(v2);
                verts.Add(v3);
                uvs.Add(new Vector2(0, 0));
                uvs.Add(new Vector2(1, 0));
                uvs.Add(new Vector2(1, 1));
                uvs.Add(new Vector2(0, 1));

                // Front face
                tris.Add(startIndex);
                tris.Add(startIndex + 1);
                tris.Add(startIndex + 2);

                tris.Add(startIndex);
                tris.Add(startIndex + 2);
                tris.Add(startIndex + 3);
            }

            for (int i = 0; i < n; i++)
            {
                int ni = (i + 1) % n;

                // Top Face (Faces Up)
                AddQuad(topOuter[i], topInner[i], topInner[ni], topOuter[ni]);

                // Bottom Face (Faces Down)
                AddQuad(bottomInner[i], bottomOuter[i], bottomOuter[ni], bottomInner[ni]);

                // Outer Face
                AddQuad(bottomOuter[i], topOuter[i], topOuter[ni], bottomOuter[ni]);

                // Inner Face
                AddQuad(topInner[i], bottomInner[i], bottomInner[ni], topInner[ni]);
            }

            mesh.SetVertices(verts);
            mesh.SetTriangles(tris, 0);
            mesh.SetUVs(0, uvs);

            Color32[] colors = new Color32[verts.Count];
            for (int i = 0; i < colors.Length; i++)
                colors[i] = new Color32(255, 255, 255, 255);
            mesh.colors32 = colors;

            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            mf.mesh = mesh;
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
