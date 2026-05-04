using System.Collections;
using TacticsGame.Characters;
using TacticsGame.Core;
using UnityEngine;

namespace TacticsGame.Spells.Effects
{
    /// <summary>
    /// Resolution Phase: Forces the target to move a certain distance.
    /// Supports Push, Pull, and Teleport styles.
    /// </summary>
    [CreateAssetMenu(menuName = "TacticsRuleset/Spell Effects/Forced Movement")]
    public class ForcedMovementEffectSO : SpellEffectSO
    {
        public enum MovementType
        {
            PushFromCaster,
            PullTowardsCaster,
            ShiftInCasterDirection,
            TeleportToTargetCell, // Used for things like Dimension Door
        }

        [Header("Movement Configuration")]
        public MovementType Type = MovementType.PushFromCaster;
        public int DistanceInFeet = 5;
        public bool OnlyOnFailure = true;
        public bool IsInteractive = false; // If true, player picks the destination

        private void OnEnable()
        {
            // We run this in the Roll phase so we can queue interactive movements
            // before the visuals/animation phase starts.
            Phase = SpellEffectPhase.Roll;
        }

        public override void Apply(SpellCastContext context)
        {
            foreach (Unit target in context.AffectedUnits)
            {
                int distanceToMove = DistanceInFeet;

                // Check if we should move based on roll result
                if (OnlyOnFailure || IsInteractive)
                {
                    if (context.RollResults.TryGetValue(target, out var result))
                    {
                        // Acid Grip scaling:
                        // Success = 5ft (DistanceInFeet)
                        // Failure = 10ft (DistanceInFeet * 2)
                        // Crit Failure = 20ft (DistanceInFeet * 4)
                        if (context.SpellData.ElementName.Contains("Acid Grip"))
                        {
                            if (result.Degree == Core.Degree.Success)
                                distanceToMove = 5;
                            else if (result.Degree == Core.Degree.Failure)
                                distanceToMove = 10;
                            else if (result.Degree == Core.Degree.CriticalFailure)
                                distanceToMove = 20;
                            else
                                continue; // Crit Success = nothing
                        }
                        else
                        {
                            // Standard OnlyOnFailure logic
                            if (result.Degree > Core.Degree.Failure)
                                continue;
                        }
                    }
                    else if (OnlyOnFailure)
                    {
                        continue;
                    }
                }

                if (IsInteractive)
                {
                    context.PendingMovements.Add(
                        new ForcedMovementRequest
                        {
                            Target = target,
                            MaxTiles = distanceToMove / 5,
                            IsInteractive = true,
                        }
                    );
                    continue;
                }

                Vector3Int startCell = target.CurrentLayeredPosition;
                Vector3Int targetCell = CalculateTargetCell(context, target);

                if (targetCell != startCell)
                {
                    // For now, instantly move.
                    // In a more polished version, would trigger a MoveAction or Animation.
                    target.FinalizeMove(targetCell);

                    // Also snap visual transform
                    target.SnapToGrid(
                        ServiceLocator.Get<Grid.GridSystem>().GetWorldPosition(targetCell)
                    );

                    Debug.Log(
                        $"<color=blue>[FORCED MOVE]</color> {target.name} moved from {startCell} to {targetCell} ({Type})"
                    );
                }
            }
        }

        private Vector3Int CalculateTargetCell(SpellCastContext context, Unit target)
        {
            Vector3Int casterPos = context.Caster.CurrentLayeredPosition;
            Vector3Int targetPos = target.CurrentLayeredPosition;
            int tiles = DistanceInFeet / 5;

            Vector3Int direction = Vector3Int.zero;

            switch (Type)
            {
                case MovementType.PushFromCaster:
                    direction = targetPos - casterPos;
                    break;
                case MovementType.PullTowardsCaster:
                    direction = casterPos - targetPos;
                    break;
                case MovementType.ShiftInCasterDirection:
                    // Use caster's current forward direction
                    Vector3 forward = context.Caster.transform.forward;
                    direction = new Vector3Int(
                        Mathf.RoundToInt(forward.x),
                        0,
                        Mathf.RoundToInt(forward.z)
                    );
                    break;
            }

            // Clamp direction to axis
            if (Mathf.Abs(direction.x) > Mathf.Abs(direction.z))
                direction = new Vector3Int(direction.x > 0 ? 1 : -1, 0, 0);
            else if (Mathf.Abs(direction.z) > 0)
                direction = new Vector3Int(0, 0, direction.z > 0 ? 1 : -1);

            Vector3Int finalPos = targetPos + (direction * tiles);

            // TODO: Check for obstacles/walkability in the grid
            return finalPos;
        }
    }
}
