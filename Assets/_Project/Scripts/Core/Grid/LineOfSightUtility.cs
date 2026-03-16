using System;
using PathfinderTactics.Characters;
using PathfinderTactics.Core;
using PathfinderTactics.Grid;
using UnityEngine;

namespace PathfinderTactics.Core
{
    public static class LineOfSightUtility
    {
        // Shoot from a little higher to avoid clipping the floor
        private const float HEIGHT_OFFSET = 1f;

        /// <summary>
        /// Returns -1 if Line of Effect is completely blocked.
        /// Returns 0 for No Cover, 1 for Lesser Cover, 2 for Standard Cover, 4 for Greater Cover.
        /// </summary>
        public static int GetCoverBonus(GridPosition originGridPos, GridPosition targetGridPos)
        {
            Vector3 originWorld =
                ServiceLocator.Get<GridSystem>().GetWorldPosition(originGridPos)
                + (Vector3.up * HEIGHT_OFFSET);
            Vector3 targetWorld =
                ServiceLocator.Get<GridSystem>().GetWorldPosition(targetGridPos)
                + (Vector3.up * HEIGHT_OFFSET);

            Vector3 direction = (targetWorld - originWorld).normalized;
            float distance = Vector3.Distance(originWorld, targetWorld);

            // Define the separate layers
            LayerMask obstacleMask = LayerMask.GetMask("Obstacles");
            LayerMask halfCoverMask = LayerMask.GetMask("HalfCover");
            LayerMask unitMask = LayerMask.GetMask("Units");

            LayerMask combinedMask = obstacleMask | halfCoverMask | unitMask;

            RaycastHit[] hits = Physics.RaycastAll(originWorld, direction, distance, combinedMask);

            Debug.DrawLine(originWorld, targetWorld, Color.magenta, 5f);

            // Print out exactly what the raycast hit first
            if (hits.Length > 0)
            {
                Debug.Log(
                    $"Raycast hit: {hits[0].collider.gameObject.name} on Layer: {LayerMask.LayerToName(hits[0].collider.gameObject.layer)}"
                );
            }

            Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

            bool hasStandardCover = false;
            bool hasLesserCover = false;

            Unit originUnit = ServiceLocator.Get<GridSystem>().GetUnitAt(originGridPos);
            Unit targetUnit = ServiceLocator.Get<GridSystem>().GetUnitAt(targetGridPos);

            int originSizeInt = originUnit != null ? (int)originUnit.GetUnitSize() : 2;
            int targetSizeInt = targetUnit != null ? (int)targetUnit.GetUnitSize() : 2;

            foreach (RaycastHit hit in hits)
            {
                // Did we hit a solid wall?
                if (((1 << hit.collider.gameObject.layer) & obstacleMask) != 0)
                {
                    return -1; // NO LINE OF EFFECT. Stop checking immediately.
                }

                // Did we hit a cover?
                if (((1 << hit.collider.gameObject.layer) & halfCoverMask) != 0)
                {
                    hasStandardCover = true; // Flag it, but check the rest of the line
                    continue;
                }

                // Did we hit a Unit?
                if (((1 << hit.collider.gameObject.layer) & unitMask) != 0)
                {
                    Unit hitUnit = hit.collider.GetComponentInParent<Unit>();

                    if (hitUnit != null)
                    {
                        if (hitUnit.CurrentGridPosition == originGridPos)
                            continue;
                        if (hitUnit.CurrentGridPosition == targetGridPos)
                            break;

                        int interveningSizeInt = (int)hitUnit.GetUnitSize();
                        if (interveningSizeInt == (int)UnitSize.Tiny)
                            continue;

                        if (
                            interveningSizeInt >= originSizeInt + 2
                            && interveningSizeInt >= targetSizeInt + 2
                        )
                        {
                            hasStandardCover = true; // Massive creature counts as Standard Cover
                        }
                        else
                        {
                            hasLesserCover = true;
                        }
                    }
                }
            }

            // Return the highest applicable cover bonus
            if (hasStandardCover)
                return 2;
            if (hasLesserCover)
                return 1;

            return 0; // Clear shot
        }

        /// <summary>
        /// Checks if there is a clear, unobstructed straight line between two grid positions.
        /// </summary>
        public static bool HasLineOfSight(
            GridPosition originGridPos,
            GridPosition targetGridPos,
            LayerMask obstacleLayer
        )
        {
            Vector3 originWorld =
                ServiceLocator.Get<GridSystem>().GetWorldPosition(originGridPos)
                + (Vector3.up * HEIGHT_OFFSET);
            Vector3 targetWorld =
                ServiceLocator.Get<GridSystem>().GetWorldPosition(targetGridPos)
                + (Vector3.up * HEIGHT_OFFSET);

            Vector3 direction = (targetWorld - originWorld).normalized;
            float distance = Vector3.Distance(originWorld, targetWorld);

            // Cast a ray from origin to target.
            // If it hits something on the Obstacles layer before reaching the target distance, LoS is blocked
            if (Physics.Raycast(originWorld, direction, distance, obstacleLayer))
            {
                return false;
            }

            return true; // The path is clear
        }

        /// <summary>
        /// Basic PF2e Cover Check. Returns true if the target is adjacent to an obstacle
        /// that sits between them and the attacker.
        /// </summary>
        public static bool HasStandardCover(
            GridPosition attackerPos,
            GridPosition targetPos,
            LayerMask obstacleLayer
        )
        {
            // if they don't even have LoS, they can't be attacked at all
            if (!HasLineOfSight(attackerPos, targetPos, obstacleLayer))
                return false;

            // TODO: implement the specific PF2e cover math here next,
            // checking if the ray clips the corner of an obstacle tile.
            return false;
        }
    }
}
