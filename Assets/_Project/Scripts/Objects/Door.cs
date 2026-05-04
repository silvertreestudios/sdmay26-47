using System.Collections;
using TacticsGame.Characters;
using TacticsGame.Core;
using TacticsGame.Grid;
using UnityEngine;

namespace TacticsGame.Objects
{
    public class Door : MonoBehaviour, IGridEntity, IDamageable
    {
        [Header("Configuration")]
        [SerializeField]
        private DoorStatsSO doorStats;

        [SerializeField]
        private Transform hingeTransform;

        [SerializeField]
        private GameObject visionBlocker;

        [SerializeField]
        private GameObject doorModel;

        [SerializeField]
        private DoorState initialState = DoorState.Closed;

        [Header("Animation Settings")]
        [SerializeField]
        private float openRotation = 90f;

        [SerializeField]
        private float closeRotation = 0f;

        [SerializeField]
        private float rotationSpeed = 5f;

        private DoorState currentState;
        private int currentHP;
        private bool isRotating;

        public DoorState CurrentState => currentState;
        public DoorStatsSO Stats => doorStats;

        // IDamageable implementation
        public bool IsDead => currentState == DoorState.Destroyed;

        public int GetCurrentHealth() => currentHP;

        public int GetMaxHealth() => doorStats != null ? doorStats.MaxHP : 0;

        public event System.EventHandler OnDeath;
        public event System.EventHandler OnHealthChanged;
        public event System.EventHandler<string> OnStatusMessage;

        // IGridEntity implementation
        public Vector3Int CurrentPosition =>
            ServiceLocator.Get<GridSystem>().GetLayeredGridPosition(transform.position);
        public bool BlocksMovement =>
            (currentState != DoorState.Open && currentState != DoorState.Destroyed);
        public CoverType CoverType =>
            (currentState != DoorState.Open && currentState != DoorState.Destroyed)
                ? CoverType.Total
                : CoverType.None;

        private GridNode registeredNode;

        private void Start()
        {
            if (doorStats != null)
                currentHP = doorStats.MaxHP;

            SetState(initialState, true);
            RegisterWithGrid();
        }

        private void OnDestroy()
        {
            UnregisterFromGrid();
        }

        private void RegisterWithGrid()
        {
            GridSystem grid = ServiceLocator.Get<GridSystem>();
            if (grid == null)
                return;

            registeredNode = grid.GetNode(CurrentPosition);
            if (registeredNode != null && !registeredNode.Entities.Contains(this))
            {
                registeredNode.Entities.Add(this);
                grid.TriggerGridObjectChanged(CurrentPosition);
            }
        }

        private void UnregisterFromGrid()
        {
            if (registeredNode != null && registeredNode.Entities.Contains(this))
            {
                registeredNode.Entities.Remove(this);

                GridSystem grid = ServiceLocator.Get<GridSystem>();
                if (grid != null)
                    grid.TriggerGridObjectChanged(registeredNode.Coordinates);
            }
            registeredNode = null;
        }

        public void SetState(DoorState newState, bool instant = false)
        {
            if (currentState == DoorState.Destroyed && newState != DoorState.Destroyed)
                return; // Cannot un-destroy a door normally

            currentState = newState;
            UpdateVisionBlocker();

            float targetRot = (currentState == DoorState.Open) ? openRotation : closeRotation;

            if (instant)
            {
                if (hingeTransform != null)
                    hingeTransform.localRotation = Quaternion.Euler(0, targetRot, 0);
            }
            else
            {
                StartCoroutine(RotateDoor(targetRot));
            }
        }

        private IEnumerator RotateDoor(float targetY)
        {
            if (hingeTransform == null)
                yield break;

            isRotating = true;
            Quaternion targetRotation = Quaternion.Euler(0, targetY, 0);

            while (Quaternion.Angle(hingeTransform.localRotation, targetRotation) > 0.1f)
            {
                hingeTransform.localRotation = Quaternion.Slerp(
                    hingeTransform.localRotation,
                    targetRotation,
                    Time.deltaTime * rotationSpeed
                );
                yield return null;
            }

            hingeTransform.localRotation = targetRotation;
            isRotating = false;
        }

        private void UpdateVisionBlocker()
        {
            if (visionBlocker == null)
                return;

            // Vision blocker is active if the door is NOT open and NOT destroyed
            bool shouldBlock = (
                currentState != DoorState.Open && currentState != DoorState.Destroyed
            );
            visionBlocker.SetActive(shouldBlock);

            // Hide door model if destroyed
            if (doorModel != null)
            {
                doorModel.SetActive(currentState != DoorState.Destroyed);
            }

            // Update Grid System walkability if necessary
            UpdateGridNode();
        }

        private void UpdateGridNode()
        {
            GridSystem grid = ServiceLocator.Get<GridSystem>();
            if (grid == null)
                return;

            // Trigger a refresh for any systems watching this cell
            grid.TriggerGridObjectChanged(CurrentPosition);
        }

        #region Interaction Methods

        public bool Interact(Unit unit)
        {
            if (currentState == DoorState.Destroyed)
                return false;
            if (currentState == DoorState.Locked || currentState == DoorState.Stuck)
                return false;

            if (currentState == DoorState.Open)
            {
                // Safety check: Don't close if someone is standing in the doorway
                GridSystem grid = ServiceLocator.Get<GridSystem>();
                if (grid != null && grid.IsPositionOccupied(CurrentPosition))
                {
                    Debug.Log(
                        $"<color=yellow>[DOOR]</color> Cannot close door; {grid.GetUnitAt(CurrentPosition).name} is in the way!"
                    );
                    return false;
                }

                SetState(DoorState.Closed);
                return true;
            }
            else
            {
                SetState(DoorState.Open);
                return true;
            }
        }

        public void TryForceOpen(Unit unit, int athleticsCheckResult)
        {
            if (athleticsCheckResult >= doorStats.ForceOpenDC)
            {
                Debug.Log($"<color=green>[DOOR]</color> {unit.name} forced the door open!");
                SetState(DoorState.Open);
            }
            else
            {
                Debug.Log($"<color=red>[DOOR]</color> {unit.name} failed to force the door open.");
            }
        }

        public void TryPickLock(Unit unit, int thieveryCheckResult)
        {
            if (currentState != DoorState.Locked)
                return;

            if (thieveryCheckResult >= doorStats.PickLockDC)
            {
                Debug.Log($"<color=green>[DOOR]</color> {unit.name} picked the lock!");
                SetState(DoorState.Closed); // Becomes normal closed door
            }
        }

        #region IDamageable Implementation

        public void ApplyDamage(
            Unit source,
            int amount,
            DamageType type,
            bool isCriticalHit = false
        )
        {
            if (currentState == DoorState.Destroyed)
                return;

            // Apply Hardness
            int finalDamage = Mathf.Max(0, amount - doorStats.Hardness);

            currentHP -= finalDamage;
            OnHealthChanged?.Invoke(this, System.EventArgs.Empty);

            Debug.Log(
                $"<color=orange>[DOOR]</color> Door took {finalDamage} {type} damage from {source?.name}. HP: {currentHP}/{doorStats.MaxHP}"
            );

            if (currentHP <= 0)
            {
                SetState(DoorState.Destroyed);
                OnDeath?.Invoke(this, System.EventArgs.Empty);
            }
        }

        public void ApplyHealing(int amount)
        {
            if (currentState == DoorState.Destroyed)
                return;

            currentHP = Mathf.Min(doorStats.MaxHP, currentHP + amount);
            OnHealthChanged?.Invoke(this, System.EventArgs.Empty);
        }

        #endregion

        #endregion
    }
}
