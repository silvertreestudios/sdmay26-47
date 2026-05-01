using System.Collections.Generic;
using TacticsGame.Actions;
using TacticsGame.Characters;
using TacticsGame.Core;
using TacticsGame.Grid;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace TacticsGame.Core.TacticalDebug
{
    public class TacticalRangeDebugger : MonoBehaviour
    {
        [Header("Settings")]
        [SerializeField]
        private KeyCode toggleKey = KeyCode.F3;

        [SerializeField]
        private bool isVisible = false;

        [Header("Line Settings")]
        [Tooltip("Thickness of the debug line (Works in Unity Editor only).")]
        [SerializeField]
        private float lineThickness = 25f;

        [Header("Colors")]
        [SerializeField]
        private Color clearColor = new Color(0f, 1f, 0f, 0.8f); // Green

        [SerializeField]
        private Color standardCoverColor = new Color(1f, 1f, 0f, 0.8f); // Yellow

        [SerializeField]
        private Color greaterCoverColor = new Color(1f, 0.5f, 0f, 0.8f); // Orange

        [SerializeField]
        private Color blockedColor = new Color(1f, 0f, 0f, 0.8f); // Red

        [SerializeField]
        private Color outOfRangeColor = new Color(0.5f, 0.5f, 0.5f, 0.5f); // Gray

        private BaseAction currentAction;

        private struct DebugSegment
        {
            public Vector3 Origin;
            public Vector3 Target;
            public Color RayColor;
        }

        private struct TargetCap
        {
            public Vector3 Position;
            public Color CapColor;
        }

        private List<DebugSegment> currentSegments = new List<DebugSegment>();
        private List<TargetCap> targetCaps = new List<TargetCap>();

        private void Start()
        {
            ServiceLocator.Register(this);
        }

        private void OnDestroy()
        {
            ServiceLocator.Unregister<TacticalRangeDebugger>();
        }

        private void Update()
        {
            if (Input.GetKeyDown(toggleKey))
            {
                isVisible = !isVisible;
                UnityEngine.Debug.Log(
                    $"<color=cyan>[DEBUG]</color> Tactical Range Visualization: {(isVisible ? "<color=green>ENABLED</color>" : "<color=red>DISABLED</color>")}"
                );
            }

            UnitActionSystem uas = ServiceLocator.TryGet<UnitActionSystem>(out var system)
                ? system
                : null;
            if (uas != null)
            {
                currentAction = uas.GetSelectedAction();
            }

            if (isVisible)
            {
                RefreshData();
            }
        }

        public void RefreshData()
        {
            currentSegments.Clear();
            targetCaps.Clear();

            if (currentAction == null || !currentAction.IsUnitTargeted)
                return;

            Unit attacker = currentAction.GetComponent<Unit>();
            if (attacker == null)
                return;

            GridSystem grid = ServiceLocator.Get<GridSystem>();
            if (grid == null)
                return;

            Vector3Int attackerPos = attacker.CurrentLayeredPosition;
            float halfHeight = grid.VerticalCellSize * 0.5f;

            float maxRangeFeet = 0;
            bool isMelee = currentAction is MeleeAction;
            bool isRanged = currentAction is RangedAction;

            if (isMelee)
            {
                var melee = (MeleeAction)currentAction;
                maxRangeFeet = melee.GetWeapon()?.reachFeet ?? 5f;
            }
            else if (isRanged)
            {
                var ranged = (RangedAction)currentAction;
                maxRangeFeet = (ranged.GetWeapon()?.rangeIncrementFeet ?? 0) * 6f;
            }

            foreach (Unit enemy in UnitManager.AllUnits)
            {
                if (
                    enemy == null
                    || enemy == attacker
                    || enemy.GetFaction() == attacker.GetFaction()
                )
                    continue;

                Vector3Int targetPos = enemy.CurrentLayeredPosition;

                // Get the exact Bresenham path to evaluate it progressively
                List<Vector3Int> line = LineOfSightUtility.Get3DBresenhamLine(
                    attackerPos,
                    targetPos
                );
                if (line == null || line.Count == 0)
                    continue;

                Vector3 previousWorld =
                    grid.GetWorldPosition(line[0]) + new Vector3(0, halfHeight, 0);

                CoverType worstCoverSoFar = CoverType.None;
                bool isBlocked = false;
                Color finalTerminalColor = clearColor;

                for (int i = 1; i < line.Count; i++)
                {
                    Vector3Int currentVoxel = line[i];
                    Vector3 currentWorld =
                        grid.GetWorldPosition(currentVoxel) + new Vector3(0, halfHeight, 0);

                    bool outOfRange = false;
                    if (isMelee)
                    {
                        int distTiles = TacticsRuleset_Core.GetChebyshevDistance3D(
                            attackerPos,
                            currentVoxel
                        );
                        outOfRange = (distTiles * 5) > maxRangeFeet;
                    }
                    else if (isRanged)
                    {
                        float distFeet = ((RangedAction)currentAction).GetDistanceFeet(
                            attackerPos,
                            currentVoxel
                        );
                        outOfRange = distFeet > maxRangeFeet;
                    }

                    if (!isBlocked)
                    {
                        VisibilityResult visResult = LineOfSightUtility.Evaluate(
                            attackerPos,
                            currentVoxel
                        );
                        if (!visResult.HasLineOfSight)
                        {
                            isBlocked = true;
                        }
                        else if (visResult.Cover > worstCoverSoFar)
                        {
                            worstCoverSoFar = visResult.Cover;
                        }
                    }

                    Color segmentColor;
                    if (outOfRange)
                        segmentColor = outOfRangeColor;
                    else if (isBlocked)
                        segmentColor = blockedColor;
                    else if (worstCoverSoFar == CoverType.Greater)
                        segmentColor = greaterCoverColor;
                    else if (worstCoverSoFar == CoverType.Standard)
                        segmentColor = standardCoverColor;
                    else
                        segmentColor = clearColor;

                    currentSegments.Add(
                        new DebugSegment
                        {
                            Origin = previousWorld,
                            Target = currentWorld,
                            RayColor = segmentColor,
                        }
                    );

                    previousWorld = currentWorld;
                    if (i == line.Count - 1)
                        finalTerminalColor = segmentColor;
                }

                targetCaps.Add(
                    new TargetCap { Position = previousWorld, CapColor = finalTerminalColor }
                );
            }
        }

        private void OnDrawGizmos()
        {
            if (!isVisible)
                return;

            foreach (var seg in currentSegments)
            {
#if UNITY_EDITOR
                Handles.color = seg.RayColor;
                Handles.DrawAAPolyLine(lineThickness, seg.Origin, seg.Target);
#else
                Gizmos.color = seg.RayColor;
                Gizmos.DrawLine(seg.Origin, seg.Target);
#endif
            }

            foreach (var cap in targetCaps)
            {
                Gizmos.color = cap.CapColor;
                Gizmos.DrawSphere(cap.Position, 0.2f);
            }
        }
    }
}
