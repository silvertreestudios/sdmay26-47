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
                // TODO: Show a sub-menu here (Pick Lock, Force, Open).
                // For now, try a standard Open, and fallback to Force if Locked/Stuck.

                if (door.CurrentState == DoorState.Locked)
                {
                    // Use Thievery Modifier
                    int thieveryMod = unit.GetSkillModifier(SkillType.Thievery);
                    int thieveryRoll = UnityEngine.Random.Range(1, 21) + thieveryMod;
                    door.TryPickLock(unit, thieveryRoll);
                }
                else if (door.CurrentState == DoorState.Stuck)
                {
                    // Use Athletics Modifier
                    int athleticsMod = unit.GetSkillModifier(SkillType.Athletics);
                    int athleticsRoll = UnityEngine.Random.Range(1, 21) + athleticsMod;
                    door.TryForceOpen(unit, athleticsRoll);
                }
                else
                {
                    door.Interact(unit);
                }
            }

            // End action
            ActionComplete();
        }

        public override List<Vector3Int> GetValidActionGridPositions()
        {
            List<Vector3Int> validPositions = new List<Vector3Int>();
            GridSystem grid = ServiceLocator.Get<GridSystem>();

            Vector3Int unitPos = unit.CurrentLayeredPosition;

            // Check adjacent 8 cells
            for (int x = -interactRange; x <= interactRange; x++)
            {
                for (int z = -interactRange; z <= interactRange; z++)
                {
                    if (x == 0 && z == 0)
                        continue;

                    Vector3Int checkPos = unitPos + new Vector3Int(x, 0, z);

                    // If a door exists at this grid coordinate, it's a valid target
                    if (FindDoorAtPosition(checkPos) != null)
                    {
                        validPositions.Add(checkPos);
                    }
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
