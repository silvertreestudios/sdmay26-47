using System;
using System.Collections.Generic;
using PathfinderTactics.Core;
using PathfinderTactics.Grid;
using UnityEngine;
using TMPro;

namespace PathfinderTactics.Characters
{
    [RequireComponent(typeof(CharacterController))]
    public class Unit : MonoBehaviour
    {
        [Header("Configuration")]
        [SerializeField]
        private UnitStatsSO stats;
        private int currentHP;
        // Public Properties
        public GridPosition CurrentGridPosition { get; private set; }

        // Physics & Movement State
        private CharacterController characterController;
        private float verticalVelocity;
        private float gravity = -9.81f;
        private float jumpHeight = 1.5f;

        //Contains all feats (exacting strike for now)
        [SerializeField]
        private FeatLoadoutSO featLoadout;

        // Budget is used to track how far a unit can move
        private int movementBudgetRemaining;

        // 3 actions per turn. Here is where it begins to get messy :P
        private int actionPointsRemaining;

        // Used to track how many attacks have been made for the multiple attack penalty (MAP). Resets at the start of each turn.
        private int attack_count = 0; 

        // Honestly theres no way we need this to be anything other than 3 but
        // Useful for debugging
        private int totalActionPointsPerTurn = 3;

        private bool selected = false;

        #region Action Economy
        public void StartTurn()
        {
            actionPointsRemaining = totalActionPointsPerTurn;
            attack_count = 0; 
        }

        public void SpendActionPoint()
        {
            actionPointsRemaining--;
        }

        public void SpendActionPoints(int amount)
        {
            actionPointsRemaining -= amount;
        }

        public int GetActionPointsRemaining()
        {
            return actionPointsRemaining;
        }

        #endregion

        private void Awake()
        {
            UnitManager.AllUnits.Add(this);
            characterController = GetComponent<CharacterController>();
            currentHP = getTotalHP();


        }

        private void Start()
        {
            UnitActionSystem.Instance.OnSelectedUnitChanged +=
                Select_unit;
        }

        void Update()
        {
            if (!selected) return;
            //Search for units in range
            foreach (Unit other in UnitManager.AllUnits)
            {
                if (other == this) continue;
                //TODO: Make range equal to weapon range. Range is in tiles.
                if (IsUnitInRange(other, 1))
                {
                    Renderer[] renderers = other.GetComponentsInChildren<Renderer>();

                    foreach (Renderer r in renderers)
                    {
                        r.material.color = Color.red;
                    }
                }
                else
                {
                    // Reset to white if NOT in range
                    Renderer[] renderers = other.GetComponentsInChildren<Renderer>();

                    foreach (Renderer r in renderers)
                    {
                        r.material.color = Color.white;
                    }
                }
            }
        }

        #region Movement Budget
        // Called when a unit's turn begins or when it's selected for movement.
        public void StartMoveAction()
        {
            // Reset the budget to the maximum allowed for this unit.
            movementBudgetRemaining = GetMaxMoveCost();
        }

        // Call this to spend budget when moving.
        public void SpendMovement(int amount)
        {
            movementBudgetRemaining -= amount;
        }

        public int GetMovementBudgetRemaining()
        {
            return movementBudgetRemaining;
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
                verticalVelocity = -2f;
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
            // Only allow jumping if the character is on the ground
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
            // The CharacterController must be temporarily disabled to teleport it.
            characterController.enabled = false;
            transform.position = GridSystem.Instance.GetWorldPosition(gridPosition);
            characterController.enabled = true;
        }

        public void FinalizeMove(GridPosition finalPosition)
        {
            GridSystem.Instance.MoveUnit(this, finalPosition);
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

        public int getArmorClass()
        {
            if (stats == null)
                return 10; // Default AC

            return stats.armorClass;
        }

        public int getTotalHP()
        {
            if (stats == null)
                return 0;

            return stats.TotalHP;
        }


        public bool IsUnitInRange(Unit other, int range)
        {
            int dx = Mathf.Abs(other.CurrentGridPosition.x - CurrentGridPosition.x);
            int dz = Mathf.Abs(other.CurrentGridPosition.z - CurrentGridPosition.z);

            int distance = dx + dz;

            return distance <= range;
        }

        public void Attack(Unit target)
        {
            TextMeshProUGUI rollText = GameObject.Find("Roll_results").GetComponent<TextMeshProUGUI>();

            // Simple attack logic
            int roll = UnityEngine.Random.Range(1, 21);
            int strength = stats.strength;
            // Profcienciey is expertise for now (Fighter level 1) expertise = 4 + lvl,
            int proficiency = 5;
            int penalty = -1 * (attack_count * 5);
            int attackValue = roll + strength + proficiency + penalty;

            AppendRoll(rollText, $"Attack Roll d20: {roll}: total: {attackValue}");

            attack_count++; // Increment attack count regardless of hit or miss

            if (roll != 20)
            {
                if (target.Defend_against_attack(attackValue))
                {
                    Debug.Log($"{gameObject.name} attacked {target.gameObject.name} but was blocked!");
                    return;
                }
                else
                {
                   
                    //TODO: damage change based on weapon (right now hardCoded longsword damage)
                    int damage = UnityEngine.Random.Range(1, 9) + 4;
                    AppendRoll(rollText, $"Damage Roll d8: {damage - 4} total : {damage}");
                    target.currentHP -= damage;
                    target.currentHP = Math.Max(0, target.currentHP);
                    Debug.Log($"{gameObject.name} attacked {target.gameObject.name} for {damage} damage!");
                    
                }
            }
            else
            {

                int damage = 2 * (UnityEngine.Random.Range(1, 9) + 4);
                AppendRoll(rollText, $"CRIT Damage Roll d8: {damage / 2 - 4} total : {damage}");
                target.currentHP -= damage;
                target.currentHP = Math.Max(0, target.currentHP);
                Debug.Log($"CRIT! {gameObject.name} attacked {target.gameObject.name} for {damage} damage!");

            }
            Debug.Log("${target.gameObject.name} has {target.currentHP} health!");


        }

        public bool Defend_against_attack(int attackValue)
        {
            int ac = stats.armorClass;
            return attackValue <= ac;
        }


        private void OnDestroy()
        {
            UnitManager.AllUnits.Remove(this);
        }

        public int GetCurrentHP()
        {
            return currentHP;
        }
        public int ReduceCurrentHP(int amount)
        {
            currentHP -= amount;
            currentHP = Math.Max(0, currentHP);
            return currentHP;
        }

        public int GetAttackCount()
        {
            return attack_count;
        }

        public int ReduceAttackCount(int amount)
        {
            attack_count -= amount;
            return attack_count;
        }

        private void Select_unit(object sender, EventArgs e)
        {
            if (UnitActionSystem.Instance.SelectedUnit == this)
            {
                selected = true;
            }
            else
            {
                foreach (Unit other in UnitManager.AllUnits)
                {
                    if (other == this) continue;
                    
                    Renderer[] renderers = other.GetComponentsInChildren<Renderer>();

                    foreach (Renderer r in renderers)
                    {
                        r.material.color = Color.white;
                    }
                    
                }
                selected = false;
            }
        }

        public FeatLoadoutSO GetFeatLoadout()
        {
            return featLoadout;
        }

        public UnitStatsSO GetUnitStats()
        {
            return stats;
        }

        //Its here im realizing I am adding way too much to the unit class that does not need to be here. I will fix it later and move some methods elsewhere.
        private void AppendRoll(TextMeshProUGUI textBox, string message)
        {
            textBox.text += message + "\n";
            // Trim old text if too long
            if (textBox.text.Length > 100)
            {
                textBox.text = textBox.text.Substring(textBox.text.Length - 100);
            }
        }

    }
}
