using System;
using System.Collections.Generic;
using PathfinderTactics.Actions;
using PathfinderTactics.Core;
using PathfinderTactics.Grid;
using UnityEngine;

namespace PathfinderTactics.Characters
{
    public enum UnitSize
    {
        Tiny = 0,
        Small = 1,
        Medium = 2,
        Large = 3,
        Huge = 4,
        Gargantuan = 5,
    }

    [RequireComponent(typeof(UnitActionEconomy))]
    [RequireComponent(typeof(UnitGridObject))]
    [RequireComponent(typeof(UnitMovement))]
    [RequireComponent(typeof(UnitStealth))]
    [RequireComponent(typeof(UnitConditions))]
    public class Unit : MonoBehaviour, ITargetable
    {
        [Header("Team Configuration")]
        [SerializeField]
        private Faction faction = Faction.Player;

        public Faction GetFaction() => faction;

        public bool IsEnemy(Unit otherUnit)
        {
            return otherUnit.GetFaction() != this.faction;
        }

        [Header("Configuration")]
        [SerializeField]
        private UnitStatsSO stats;

        [Header("PF2e Attributes")]
        [SerializeField]
        private UnitSize unitSize = UnitSize.Medium;

        public UnitSize GetUnitSize() => unitSize;

        // Dependencies
        private UnitActionEconomy actionEconomy;
        private UnitGridObject gridObject;
        private UnitMovement movement;
        private UnitConditions conditions;

        private bool selected = false;

        private void Awake()
        {
            UnitManager.AllUnits.Add(this);

            actionEconomy = GetComponent<UnitActionEconomy>();
            if (actionEconomy == null)
                actionEconomy = gameObject.AddComponent<UnitActionEconomy>();

            gridObject = GetComponent<UnitGridObject>();
            if (gridObject == null)
                gridObject = gameObject.AddComponent<UnitGridObject>();

            movement = GetComponent<UnitMovement>();
            if (movement == null)
                movement = gameObject.AddComponent<UnitMovement>();

            conditions = GetComponent<UnitConditions>();
            if (conditions == null)
                conditions = gameObject.AddComponent<UnitConditions>();

            var stealth = GetComponent<UnitStealth>();
            if (stealth == null)
                stealth = gameObject.AddComponent<UnitStealth>();
        }

        private void Start()
        {
            ServiceLocator.Get<UnitActionSystem>().OnSelectedUnitChanged += Select_unit;

            var meshRenderer = GetComponentInChildren<MeshRenderer>();
            if (meshRenderer != null)
            {
                if (faction == Faction.Player)
                    meshRenderer.material.color = Color.blue;
                else if (faction == Faction.Enemy)
                    meshRenderer.material.color = Color.red;
            }
        }

        private void OnDestroy()
        {
            UnitManager.AllUnits.Remove(this);
            if (ServiceLocator.TryGet<UnitActionSystem>(out var uas))
            {
                uas.OnSelectedUnitChanged -= Select_unit;
            }
        }

        private void Select_unit(object sender, EventArgs e)
        {
            selected = (ServiceLocator.Get<UnitActionSystem>().SelectedUnit == this);
        }

        // Facade Methods to ActionEconomy
        public int AttacksThisTurn => actionEconomy.AttacksThisTurn;
        public bool HasReactionAvailable => actionEconomy.HasReactionAvailable;

        public void StartTurn() => actionEconomy.StartTurn();

        public void SpendReaction() => actionEconomy.SpendReaction();

        public void RestoreReaction() => actionEconomy.RestoreReaction();

        public void IncrementAttacksThisTurn() => actionEconomy.IncrementAttacksThisTurn();

        public void SpendActionPoints(int amount) => actionEconomy.SpendActionPoints(amount);

        public int GetActionPointsRemaining() => actionEconomy.GetActionPointsRemaining();

        public BaseAction[] GetBaseActionArray() => actionEconomy.GetBaseActionArray();

        // Facade Methods to GridObject
        public GridPosition CurrentGridPosition => gridObject.CurrentGridPosition;
        public Transform Transform => transform;

        public void SetInitialPosition(GridPosition gridPosition) =>
            gridObject.SetInitialPosition(gridPosition);

        public void FinalizeMove(GridPosition finalPosition) =>
            gridObject.FinalizeMove(finalPosition);

        // Facade Methods to Movement
        public void MoveAlongPath(List<GridPosition> path, Action onComplete) =>
            movement.MoveAlongPath(path, onComplete);

        public void StartMoveAction() => movement.StartMoveAction();

        public void SpendMovement(int amount) => movement.SpendMovement(amount);

        public void HandleMovement(Vector3 moveDirection) => movement.HandleMovement(moveDirection);

        public void HandleJump() => movement.HandleJump();

        public float GetUnitRadius() => movement.GetUnitRadius();

        public void SnapToGrid(Vector3 newPosition) => movement.SnapToGrid(newPosition);

        // Stats & Formulas
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

        public UnitStatsSO GetStats() => stats;

        public int GetArmorClass(AttackType incomingAttackType = AttackType.Melee)
        {
            int baseAC = stats == null ? 10 : stats.armorClass;

            if (conditions == null)
                return baseAC;

            int statusPenalty = PF2E_Core.GetStatusPenalty(conditions, AbilityScore.DEX);
            int circumstanceMod = 0;

            if (conditions.IsOffGuard())
            {
                circumstanceMod -= 2;
            }

            if (
                incomingAttackType == AttackType.Ranged
                && conditions.HasCondition(ConditionType.Prone)
            )
            {
                circumstanceMod += 2;
            }

            return baseAC + statusPenalty + circumstanceMod;
        }

        public int getTotalHP()
        {
            if (stats == null)
                return 0;
            return stats.TotalHP;
        }
    }
}
