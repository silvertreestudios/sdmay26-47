using System;
using System.Collections.Generic;
using TacticsGame.Characters;
using TacticsGame.Core;
using TacticsGame.Grid;
using TacticsGame.Objects;
using UnityEngine;

namespace TacticsGame.Actions
{
    /// <summary>
    /// Interact action that identifies adjacent doors and
    /// allows the player to open, pick locks, or force them open.
    /// </summary>
    public class InteractDoorAction : BaseAction
    {
        [Header("Interact Settings")]
        [SerializeField]
        private int interactRange = 1;

        public override string GetActionName() => "Interact";

        public override void TakeAction(Vector3Int targetPosition, Action onActionComplete)
        {
            this.onActionComplete = onActionComplete;
            isActive = true;

            // Find the door at the target position
            Door door = FindDoorAtPosition(targetPosition);

            if (door != null)
            {
                // Skill checks based on door state
                if (door.CurrentState == DoorState.Locked)
                {
                    int thieveryMod = unit.GetSkillModifier(SkillType.Thievery);
                    int thieveryRoll = UnityEngine.Random.Range(1, 21) + thieveryMod;
                    door.TryPickLock(unit, thieveryRoll);
                }
                else if (door.CurrentState == DoorState.Stuck)
                {
                    int athleticsMod = unit.GetSkillModifier(SkillType.Athletics);
                    int athleticsRoll = UnityEngine.Random.Range(1, 21) + athleticsMod;
                    door.TryForceOpen(unit, athleticsRoll);
                }
                else
                {
                    door.Interact(unit);
                }
            }
            else
            {
                Debug.LogWarning($"[InteractDoorAction] No door found at {targetPosition}!");
            }

            ActionComplete();
        }

        /// <summary>
        /// Defines where the cursor is allowed to move (the red outline).
        /// Returns all tiles within the interaction radius.
        /// </summary>
        public override List<Vector3Int> GetActionRangeGridPositions()
        {
            List<Vector3Int> rangePositions = new List<Vector3Int>();
            Vector3Int unitPos = unit.CurrentLayeredPosition;

            for (int x = -interactRange; x <= interactRange; x++)
            {
                for (int z = -interactRange; z <= interactRange; z++)
                {
                    Vector3Int checkPos = unitPos + new Vector3Int(x, 0, z);
                    rangePositions.Add(checkPos);
                }
            }

            return rangePositions;
        }

        /// <summary>
        /// Defines which tiles are actually valid targets (the highlighted cursor).
        /// Only returns tiles that contain a Door.
        /// </summary>
        public override List<Vector3Int> GetValidActionGridPositions()
        {
            List<Vector3Int> validPositions = new List<Vector3Int>();
            List<Vector3Int> rangePositions = GetActionRangeGridPositions();

            foreach (Vector3Int pos in rangePositions)
            {
                if (FindDoorAtPosition(pos) != null)
                {
                    validPositions.Add(pos);
                }
            }

            return validPositions;
        }

        private Door FindDoorAtPosition(Vector3Int gridPos)
        {
            // TODO: Use GridObjectManager for O(1) lookup.
            // For now, search the scene for doors at the coordinate.
            Door[] allDoors = UnityEngine.Object.FindObjectsByType<Door>(FindObjectsSortMode.None);
            GridSystem grid = ServiceLocator.Get<GridSystem>();

            foreach (var door in allDoors)
            {
                Vector3Int doorGridPos = grid.GetLayeredGridPosition(door.transform.position);
                if (doorGridPos == gridPos)
                {
                    return door;
                }
            }
            return null;
        }

        private void ActionComplete()
        {
            isActive = false;
            onActionComplete?.Invoke();
        }

        public override bool IsManipulateAction => true;
    }
}
