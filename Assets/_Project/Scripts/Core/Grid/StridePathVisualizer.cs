using System.Collections.Generic;
using TacticsGame.Combat;
using TacticsGame.Core;
using TacticsGame.Grid;
using UnityEngine;

namespace TacticsGame.UI
{
    public class StridePathVisualizer : MonoBehaviour
    {
        [Header("Line Material")]
        [SerializeField]
        private Material lineMaterial;

        [Header("Width Settings")]
        [SerializeField]
        private float lineWidth = 0.25f;

        [Header("Elevation Settings")]
        [SerializeField]
        private float yBaseOffset = 0.08f;

        [Header("Smoothing")]
        [SerializeField]
        private float cornerRadius = 0.4f;

        [SerializeField]
        private int cornerResolution = 8;

        [Header("Waypoints")]
        [SerializeField]
        private GameObject waypointPrefab;

        [SerializeField]
        private float nodeScale = 0.4f;

        private List<GameObject> activeNodes = new List<GameObject>();
        private Transform visualParent;
        private LineRenderer pathLine;

        private void Start()
        {
            ServiceLocator.Register(this);
            visualParent = new GameObject("StridePathVisuals").transform;
            visualParent.SetParent(transform);

            pathLine = CreatePathLine("StridePath", lineMaterial, lineWidth, 100, Color.cyan);
        }

        private LineRenderer CreatePathLine(
            string name,
            Material mat,
            float width,
            int sortOrder,
            Color fallbackColor
        )
        {
            GameObject obj = new GameObject(name);
            obj.transform.SetParent(visualParent);

            LineRenderer lr = obj.AddComponent<LineRenderer>();
            if (mat != null)
                lr.material = mat;
            else
            {
                lr.material = new Material(Shader.Find("Sprites/Default"));
                lr.startColor = fallbackColor;
                lr.endColor = fallbackColor;
            }

            lr.useWorldSpace = true;
            lr.alignment = LineAlignment.View;
            lr.startWidth = width;
            lr.endWidth = width;
            lr.sortingOrder = sortOrder;

            lr.numCornerVertices = 8;
            lr.numCapVertices = 5;
            lr.positionCount = 0;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            return lr;
        }

        private void OnDestroy()
        {
            ServiceLocator.Unregister<StridePathVisualizer>();
        }

        private void LateUpdate()
        {
            UpdateVisuals();
        }

        public void UpdateVisuals()
        {
            if (!ServiceLocator.TryGet<UnitActionSystem>(out var uas))
                return;
            if (uas.SelectedUnit == null)
            {
                ClearVisuals();
                return;
            }

            PhaseManager phaseManager = ServiceLocator.Get<PhaseManager>();
            GamePhase phase = phaseManager.CurrentPhase;
            bool isMovingPhase = phase == GamePhase.FreeMovement || phase == GamePhase.EagleEye;

            if (!isMovingPhase || uas.SelectedUnit == null)
            {
                ClearVisuals();
                return;
            }

            var waypoints = uas.MovementWaypoints;
            GridSystem grid = ServiceLocator.Get<GridSystem>();

            // Collect Key Grid Nodes
            List<Vector3Int> gridNodes = new List<Vector3Int>();
            if (waypoints != null && waypoints.Count > 0)
                gridNodes.AddRange(waypoints);
            else
                gridNodes.Add(uas.SelectedUnit.CurrentLayeredPosition);

            Vector3Int currentCell = grid.GetLayeredGridPosition(
                uas.SelectedUnit.transform.position
            );
            if (gridNodes[gridNodes.Count - 1] != currentCell)
                gridNodes.Add(currentCell);

            // Build World Path
            List<Vector3> worldPoints = new List<Vector3>();
            for (int i = 0; i < gridNodes.Count - 1; i++)
            {
                var segment = Pathfinding.FindPath(
                    gridNodes[i],
                    gridNodes[i + 1],
                    uas.SelectedUnit.CurrentLayeredPosition
                );
                if (segment != null)
                {
                    for (int j = 0; j < segment.Count; j++)
                    {
                        if (j == segment.Count - 1 && i < gridNodes.Count - 2)
                            continue;
                        worldPoints.Add(grid.GetWorldPosition(segment[j]));
                    }
                }
            }

            if (worldPoints.Count == 0)
                worldPoints.Add(grid.GetWorldPosition(gridNodes[0]));

            // Skip current grid cell center, connect from previous node directly to model
            if (worldPoints.Count >= 2)
            {
                worldPoints.RemoveAt(worldPoints.Count - 1);
            }

            worldPoints.Add(uas.SelectedUnit.transform.position);

            // Render Nodes
            List<Vector3> nodeWorldPos = new List<Vector3>();
            if (waypoints != null)
            {
                foreach (var wp in waypoints)
                    nodeWorldPos.Add(grid.GetWorldPosition(wp) + Vector3.up * yBaseOffset);
            }
            SyncNodes(nodeWorldPos);

            // Apply to LineRenderer
            if (worldPoints.Count >= 2)
            {
                List<Vector3> smooth = GenerateCurvedPath(worldPoints);
                ApplyToLines(smooth);
            }
            else
            {
                pathLine.positionCount = 0;
            }
        }

        private void ApplyToLines(List<Vector3> points)
        {
            int n = points.Count;
            Vector3[] pos = new Vector3[n];
            for (int i = 0; i < n; i++)
            {
                pos[i] = points[i] + Vector3.up * yBaseOffset;
            }
            pathLine.positionCount = n;
            pathLine.SetPositions(pos);
        }

        private List<Vector3> GenerateCurvedPath(List<Vector3> points)
        {
            if (points.Count < 3)
                return points;
            List<Vector3> smooth = new List<Vector3>();
            smooth.Add(points[0]);
            for (int i = 1; i < points.Count - 1; i++)
            {
                Vector3 prev = points[i - 1];
                Vector3 curr = points[i];
                Vector3 next = points[i + 1];
                Vector3 tPrev = (prev - curr).normalized;
                Vector3 tNext = (next - curr).normalized;
                if (Vector3.Dot(tPrev, tNext) < -0.999f)
                {
                    smooth.Add(curr);
                    continue;
                }
                Vector3 s = curr + tPrev * cornerRadius;
                Vector3 e = curr + tNext * cornerRadius;
                for (int j = 0; j <= cornerResolution; j++)
                {
                    float t = (float)j / cornerResolution;
                    smooth.Add(
                        Vector3.LerpUnclamped(
                            Vector3.LerpUnclamped(s, curr, t),
                            Vector3.LerpUnclamped(curr, e, t),
                            t
                        )
                    );
                }
            }
            smooth.Add(points[points.Count - 1]);
            return smooth;
        }

        private void SyncNodes(List<Vector3> points)
        {
            while (activeNodes.Count < points.Count)
            {
                GameObject node = null;
                if (waypointPrefab != null)
                    node = Instantiate(waypointPrefab, transform);
                else
                {
                    node = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    node.transform.SetParent(transform);
                    node.GetComponent<Renderer>().material.color = Color.white;
                    Destroy(node.GetComponent<Collider>());
                }
                if (waypointPrefab == null)
                {
                    node.transform.localScale = Vector3.one * nodeScale;
                }
                activeNodes.Add(node);
            }
            while (activeNodes.Count > points.Count)
            {
                Destroy(activeNodes[activeNodes.Count - 1]);
                activeNodes.RemoveAt(activeNodes.Count - 1);
            }
            for (int i = 0; i < points.Count; i++)
                activeNodes[i].transform.position = points[i];
        }

        private void ClearVisuals()
        {
            if (pathLine != null)
                pathLine.positionCount = 0;
            foreach (var n in activeNodes)
                Destroy(n);
            activeNodes.Clear();
        }
    }
}
