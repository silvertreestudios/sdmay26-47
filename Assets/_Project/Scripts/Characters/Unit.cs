using System;
using System.Collections.Generic;
using PathfinderTactics.Actions;
using PathfinderTactics.Core;
using PathfinderTactics.Grid;
using TMPro;
using UnityEngine;

namespace PathfinderTactics.Characters
{
    [RequireComponent(typeof(CharacterController))]
    public class Unit : MonoBehaviour
    {
        [Header("Configuration")]
        [SerializeField]
        private Faction faction = Faction.Player; // Default to Player

        public Faction GetFaction() => faction;

        /// <summary>
        /// Returns true if the other unit is on a different team.
        /// </summary>
        public bool IsEnemy(Unit otherUnit)
        {
            return otherUnit.GetFaction() != this.faction;
        }

        [Header("Configuration")]
        [SerializeField]
        private UnitStatsSO stats;


        //Contains all feats this unit can use (exacting strike for now)
        [SerializeField]
        private FeatLoadoutSO featLoadout;

        // Public Properties
        public GridPosition CurrentGridPosition { get; private set; }

        // Physics & Movement State
        private CharacterController characterController;
        private float verticalVelocity;
        private float gravity = -9.81f;
        private float jumpHeight = 1.5f;

        // Budget is used to track how far a unit can move
        private int movementBudgetRemaining;

        // 3 actions per turn
        private int actionPointsRemaining;

        // Honestly theres no way we need this to be anything other than 3 but
        // Useful for debugging
        private int totalActionPointsPerTurn = 3;

        private bool selected = false;

        // Modular Actions
        private BaseAction[] baseActionArray;

        public int AttacksThisTurn { get; private set; } = 0;

        public bool HasReactionAvailable { get; private set; } = true;

        #region Action Economy
        public void StartTurn()
        {
            actionPointsRemaining = totalActionPointsPerTurn;
            AttacksThisTurn = 0;
            HasReactionAvailable = true;
        }

        public void SpendReaction() => HasReactionAvailable = false;

        public void RestoreReaction() => HasReactionAvailable = true;

        public void IncrementAttacksThisTurn()
        {
            AttacksThisTurn++;
        }

        public void SpendActionPoints(int amount)
        {
            actionPointsRemaining -= amount;
        }

        public int GetActionPointsRemaining()
        {
            return actionPointsRemaining;
        }

        public BaseAction[] GetBaseActionArray()
        {
            return baseActionArray;
        }
        #endregion

        private void Awake()
        {
            UnitManager.AllUnits.Add(this);
            characterController = GetComponent<CharacterController>();

            // Auto-discover actions
            baseActionArray = GetComponents<BaseAction>();

            Debug.Log($"[UNIT BOOTUP] {gameObject.name} found {baseActionArray.Length} actions.");
            foreach (var action in baseActionArray)
            {
                Debug.Log($"   -> Action loaded: {action.GetActionName()}");
            }
        }

        private void Start()
        {
            UnitActionSystem.Instance.OnSelectedUnitChanged += Select_unit;

            // Register self on grid at start
            CurrentGridPosition = GridSystem.Instance.GetGridPosition(transform.position);
            GridSystem.Instance.AddUnitAt(this, CurrentGridPosition);

            // Snap to ensure alignment
            SnapToGrid(GridSystem.Instance.GetWorldPosition(CurrentGridPosition));

            // Temporary Debug: Color units based on faction
            var meshRenderer = GetComponentInChildren<MeshRenderer>();
            if (meshRenderer != null)
            {
                if (faction == Faction.Player)
                    meshRenderer.material.color = Color.blue;
                else if (faction == Faction.Enemy)
                    meshRenderer.material.color = Color.red;
            }
        }

        void Update()
        {
            if (!selected)
                return;
        }

        #region Movement Budget
        public void StartMoveAction()
        {
            movementBudgetRemaining = GetMaxMoveCost();
        }

        public void SpendMovement(int amount)
        {
            movementBudgetRemaining -= amount;
        }
        #endregion

        #region Movement Execution
        // This method is called every frame from the UnitActionSystem during FreeMovement
        public void HandleMovement(Vector3 moveDirection)
        {
            // Gravity and Grounding
            if (characterController.isGrounded && verticalVelocity < 0)
            {
                // Small downward force to keep the character stuck to the ground
                verticalVelocity = -5f;
            }

            // Apply gravity over time
            verticalVelocity += gravity * Time.deltaTime;

            // Combine horizontal and vertical motion
            Vector3 finalMoveVector = moveDirection + (Vector3.up * verticalVelocity);

            characterController.Move(finalMoveVector * Time.deltaTime);

            // Update Facing Direction
            if (moveDirection != Vector3.zero)
            {
                transform.forward = Vector3.Slerp(
                    transform.forward,
                    moveDirection,
                    Time.deltaTime * 15f
                );
            }
        }

        public void HandleJump()
        {
            if (characterController.isGrounded)
            {
                // Calculate the upward velocity needed to reach a specific height
                verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);
            }
        }
        #endregion

        #region State Management
        public int GetMoveDistanceInCells()
        {
            if (stats == null)
                return 0;
            return stats.speedInFeet / 5;
        }

        public int GetMaxMoveCost()
        {
            return GetMoveDistanceInCells() * Pathfinding.MOVE_STRAIGHT_COST;
        }

        public void SetInitialPosition(GridPosition gridPosition)
        {
            CurrentGridPosition = gridPosition;
            characterController.enabled = false;
            transform.position = GridSystem.Instance.GetWorldPosition(gridPosition);
            characterController.enabled = true;
        }

        public void FinalizeMove(GridPosition finalPosition)
        {
            GridSystem.Instance.MoveUnit(this, CurrentGridPosition, finalPosition);
            CurrentGridPosition = finalPosition;
        }
        #endregion

        public float GetUnitRadius()
        {
            if (characterController != null)
                return characterController.radius;
            return 0.25f;
        }

        public void SnapToGrid(Vector3 newPosition)
        {
            if (characterController != null)
            {
                characterController.enabled = false;
                transform.position = newPosition;
                characterController.enabled = true;
            }
            else
            {
                transform.position = newPosition;
            }
        }

        public UnitStatsSO GetStats() => stats;

        public int getArmorClass()
        {
            if (stats == null)
                return 10;
            return stats.armorClass;
        }

        public int getTotalHP()
        {
            if (stats == null)
                return 0;
            return stats.TotalHP;
        }

        private void OnDestroy()
        {
            UnitManager.AllUnits.Remove(this);
        }

        private void Select_unit(object sender, EventArgs e)
        {
            selected = (UnitActionSystem.Instance.SelectedUnit == this);
        }
    }
}
