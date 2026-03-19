using System;
using System.Collections.Generic;
using PathfinderTactics.Core;
using PathfinderTactics.Grid;
using UnityEngine;

namespace PathfinderTactics.Characters
{
    /// <summary>
    /// Perspective-based stealth renderer.
    /// Visuals are evaluated strictly from the active unit's perspective only.
    /// </summary>
    [RequireComponent(typeof(UnitStealth))]
    public class UnitStealthRenderer : MonoBehaviour
    {
        // Global perspective tracking so we don't "re-render" for every renderer when the
        // active unit moves during movement preview.
        private static Unit s_lastPerspectiveUnit;
        private static GridPosition s_lastPerspectivePos;
        private static int s_perspectiveVersion;

        private int localPerspectiveVersion = -1;

        private Unit unit;
        private UnitStealth stealth;
        private UnitConditions conditions;
        private MeshRenderer[] renderers;

        private void Awake()
        {
            unit = GetComponent<Unit>();
            stealth = GetComponent<UnitStealth>();
            conditions = GetComponent<UnitConditions>();
            renderers = GetComponentsInChildren<MeshRenderer>();
        }

        private void OnEnable()
        {
            if (stealth != null)
                stealth.OnDetectionStateChanged += HandleStealthChanged;
            if (conditions != null)
                conditions.OnConditionsChanged += HandleConditionsChanged;

            if (ServiceLocator.TryGet<TurnManager>(out var turnManager) && turnManager != null)
                turnManager.OnTurnChanged += TurnManager_OnTurnChanged;
            if (ServiceLocator.TryGet<UnitActionSystem>(out var uas) && uas != null)
                uas.OnSelectedUnitChanged += Uas_OnSelectedUnitChanged;

            UpdateVisuals();
        }

        private void OnDisable()
        {
            if (stealth != null)
                stealth.OnDetectionStateChanged -= HandleStealthChanged;
            if (conditions != null)
                conditions.OnConditionsChanged -= HandleConditionsChanged;

            if (ServiceLocator.TryGet<TurnManager>(out var turnManager) && turnManager != null)
                turnManager.OnTurnChanged -= TurnManager_OnTurnChanged;
            if (ServiceLocator.TryGet<UnitActionSystem>(out var uas) && uas != null)
                uas.OnSelectedUnitChanged -= Uas_OnSelectedUnitChanged;
        }

        private void HandleStealthChanged(
            Unit observer,
            DetectionState oldState,
            DetectionState newState
        )
        {
            UpdateVisuals();
        }

        private void HandleConditionsChanged()
        {
            UpdateVisuals();
        }

        private void TurnManager_OnTurnChanged(object sender, System.EventArgs e)
        {
            s_perspectiveVersion++;
            localPerspectiveVersion = -1;
            UpdateVisuals();
        }

        private void Uas_OnSelectedUnitChanged(object sender, System.EventArgs e)
        {
            s_perspectiveVersion++;
            localPerspectiveVersion = -1;
            UpdateVisuals();
        }

        private void Update()
        {
            Unit perspective = GetActivePerspectiveUnit();
            if (perspective == null)
                return;

            // During movement preview, the selected unit moves via transform,
            // but its GridPosition may not be finalized yet. For visuals, use the
            // current transform -> predicted grid position.
            GridSystem grid = ServiceLocator.TryGet<GridSystem>(out var gs) ? gs : null;
            if (grid == null)
                return;

            GridPosition predictedPos = grid.GetGridPosition(perspective.transform.position);

            if (
                perspective != s_lastPerspectiveUnit
                || predictedPos.x != s_lastPerspectivePos.x
                || predictedPos.z != s_lastPerspectivePos.z
            )
            {
                s_lastPerspectiveUnit = perspective;
                s_lastPerspectivePos = predictedPos;
                s_perspectiveVersion++;
                localPerspectiveVersion = -1;
            }

            if (localPerspectiveVersion != s_perspectiveVersion)
            {
                localPerspectiveVersion = s_perspectiveVersion;
                UpdateVisuals();
            }
        }

        private void UpdateVisuals()
        {
            if (stealth == null || renderers == null)
                return;

            Unit activeUnit = GetActivePerspectiveUnit();
            if (activeUnit == null)
            {
                // No active perspective: default to invisible.
                foreach (MeshRenderer r in renderers)
                {
                    if (r != null)
                        r.enabled = false;
                }
                return;
            }

            // Detection is strictly per-perspective: this is the only observer that matters.
            DetectionState bestState = stealth.GetDetectionState(activeUnit);

            bool actorIsInvisible =
                conditions != null && conditions.HasCondition(ConditionType.Invisible);

            bool actorIsConcealed =
                conditions != null && conditions.HasCondition(ConditionType.Concealed);

            // Invisible edge-case: never display Observed visuals.
            if (actorIsInvisible && bestState == DetectionState.Observed)
                bestState = DetectionState.Hidden;

            // Preview-only passive escalation:
            // Allow Hidden -> Observed visuals during movement preview when the active
            // unit can precisely sense and there is no cover/concealment.
            //
            // Undetected must remain invisible (acceptance criterion).
            if (bestState == DetectionState.Hidden)
            {
                if (ServiceLocator.TryGet<GridSystem>(out var grid))
                {
                    GridPosition observerPos = grid.GetGridPosition(activeUnit.transform.position);
                    GridPosition actorPos = unit.CurrentGridPosition;

                    if (
                        CanPreciselySenseAtPositions(activeUnit, observerPos, unit, actorPos)
                        && !HasCoverOrConcealmentAtPositions(actorPos, observerPos)
                    )
                    {
                        bestState = DetectionState.Observed;
                    }
                }
            }

            foreach (MeshRenderer r in renderers)
            {
                if (r == null)
                    continue;

                switch (bestState)
                {
                    case DetectionState.Observed:
                        r.enabled = true;

                        {
                            // Observed: normal faction color (same as Unit.cs Start()).
                            // If Concealed, tint slightly toward purple to hint concealment
                            // without using transparency.
                            Color baseColor =
                                unit.GetFaction() == Faction.Player ? Color.blue : Color.red;
                            if (actorIsConcealed)
                                baseColor = Color.Lerp(
                                    baseColor,
                                    new Color(0.7f, 0.2f, 0.9f, 1f),
                                    0.35f
                                );

                            r.material.color = baseColor;
                        }
                        break;

                    case DetectionState.Hidden:
                        r.enabled = true;
                        if (actorIsInvisible)
                        {
                            // Hidden + Invisible: very light gray silhouette.
                            r.material.color = new Color(0.85f, 0.85f, 0.85f, 1f);
                        }
                        else
                        {
                            // Hidden: dark gray silhouette.
                            // If Concealed too, tint slightly purple.
                            Color c = new Color(0.25f, 0.25f, 0.25f, 1f);
                            if (actorIsConcealed)
                                c = Color.Lerp(c, new Color(0.7f, 0.2f, 0.9f, 1f), 0.25f);
                            r.material.color = c;
                        }
                        break;

                    case DetectionState.Undetected:
                    case DetectionState.Unnoticed:
                        r.enabled = false;
                        break;
                }
            }
        }

        private Unit GetActivePerspectiveUnit()
        {
            // Authoritative during combat: the acting unit.
            if (ServiceLocator.TryGet<TurnManager>(out var tm) && tm != null)
            {
                if (tm.CurrentUnit != null)
                    return tm.CurrentUnit;
            }

            // Fallback for editor/testing / exploration.
            if (ServiceLocator.TryGet<UnitActionSystem>(out var uas) && uas != null)
                return uas.SelectedUnit;

            return null;
        }

        private static bool CanPreciselySenseAtPositions(
            Unit observer,
            GridPosition observerPos,
            Unit actor,
            GridPosition actorPos
        )
        {
            if (observer == null || actor == null)
                return false;

            UnitConditions observerConditions = observer.GetComponent<UnitConditions>();
            if (
                observerConditions != null
                && observerConditions.HasCondition(ConditionType.Blinded)
            )
                return false;

            UnitConditions actorConditions = actor.GetComponent<UnitConditions>();
            if (actorConditions != null && actorConditions.HasCondition(ConditionType.Invisible))
                return false;

            int obstacleLayerMask = LayerMask.GetMask("Obstacles");
            return LineOfSightUtility.HasLineOfSight(observerPos, actorPos, obstacleLayerMask);
        }

        private bool HasCoverOrConcealmentAtPositions(
            GridPosition actorPos,
            GridPosition observerPos
        )
        {
            int cover = LineOfSightUtility.GetCoverBonus(observerPos, actorPos);
            // Mirrors StealthResolver.HasCoverOrConcealmentAt: solid block or standard+.
            return cover == -1 || cover >= 2;
        }
    }
}
