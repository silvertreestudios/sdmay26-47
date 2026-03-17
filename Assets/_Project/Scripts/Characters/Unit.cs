using System;
using System.Collections.Generic;
using PathfinderTactics.Actions;
using PathfinderTactics.Combat;
using PathfinderTactics.Core;
using PathfinderTactics.Data.PF2e;
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
    [RequireComponent(typeof(UnitEquipment))]
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
        private UnitEquipment equipment;

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

            equipment = GetComponent<UnitEquipment>();
            if (equipment == null)
                equipment = gameObject.AddComponent<UnitEquipment>();

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

        // PF2e Rule Properties
        public int Level => (stats != null) ? stats.level : 1;
        public bool HasAllAroundVision => (stats != null) && stats.hasAllAroundVision;
        public bool HasDenyAdvantage => (stats != null) && stats.hasDenyAdvantage;

        // Capability Flags
        public bool CanAct => (conditions != null) && conditions.CanAct;
        public bool CanMakeMeleeAttacks => (conditions != null) && conditions.CanMakeMeleeAttacks;

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

            int speed = stats.baseSpeedInFeet;

            // Apply armor penalty if unit doesn't meet strength requirement
            if (equipment != null)
            {
                var armor = equipment.GetArmor();
                if (armor != null)
                {
                    // PF2e: If Strength < Strength Req, apply Speed Penalty
                    if (stats.strength < armor.strengthRequirement)
                    {
                        speed += armor.speedPenaltyFeet; // Penalty is negative, e.g., -5 or -10
                    }
                }
            }

            // Minimum Speed in PF2e is usually 5ft (1 cell) if penalties are too high.
            return Mathf.Max(5, speed) / 5;
        }

        public int GetMaxMoveCost()
        {
            return GetMoveDistanceInCells() * Pathfinding.MOVE_STRAIGHT_COST;
        }

        public int GetAbilityModifier(AbilityScore stat)
        {
            if (stats == null)
                return 0;
            switch (stat)
            {
                case AbilityScore.STR:
                    return PF2E_Core.GetAbilityModifier(stats.strength);
                case AbilityScore.DEX:
                    return PF2E_Core.GetAbilityModifier(stats.dexterity);
                case AbilityScore.CON:
                    return PF2E_Core.GetAbilityModifier(stats.constitution);
                case AbilityScore.INT:
                    return PF2E_Core.GetAbilityModifier(stats.intelligence);
                case AbilityScore.WIS:
                    return PF2E_Core.GetAbilityModifier(stats.wisdom);
                case AbilityScore.CHA:
                    return PF2E_Core.GetAbilityModifier(stats.charisma);
                default:
                    return 0;
            }
        }

        public int GetSaveModifier(SavingThrowType type)
        {
            if (stats == null)
                return 0;

            AbilityScore ability;
            switch (type)
            {
                case SavingThrowType.Fortitude:
                    ability = AbilityScore.CON;
                    break;
                case SavingThrowType.Reflex:
                    ability = AbilityScore.DEX;
                    break;
                case SavingThrowType.Will:
                    ability = AbilityScore.WIS;
                    break;
                default:
                    return 0;
            }

            // TODO: Simplified: Assuming Trained (+2) for all saves as a baseline for now
            return PF2E_Core.CalculateModifier(
                stats.level,
                Proficiency.Trained,
                GetAbilityModifier(ability)
            );
        }

        public int GetSpellDC(
            AbilityScore castingStat = AbilityScore.INT,
            Proficiency prof = Proficiency.Trained
        )
        {
            return PF2E_Core.CalculateSpellDC(
                this,
                stats.level,
                prof,
                GetAbilityModifier(castingStat)
            );
        }

        public UnitStatsSO GetStats() => stats;

        public int GetArmorClass(
            Unit attacker = null,
            AttackType incomingAttackType = AttackType.Melee
        )
        {
            return GetArmorClassBreakdown(attacker, incomingAttackType).totalAC;
        }

        public ArmorClassBreakdown GetArmorClassBreakdown(
            Unit attacker,
            AttackType incomingAttackType
        )
        {
            ArmorClassBreakdown breakdown = new ArmorClassBreakdown();
            if (stats == null)
            {
                breakdown.totalAC = 10;
                breakdown.baseAC = 10;
                return breakdown;
            }

            int dexMod = GetAbilityModifier(AbilityScore.DEX);
            int itemBonus = 0;

            // NOTE: Proficiency calculation should pull from class later (stubbed as Trained)
            Proficiency armorProf = Proficiency.Trained;

            if (equipment != null)
            {
                var armor = equipment.GetArmor();
                if (armor != null)
                {
                    itemBonus = armor.acBonus;
                    // Dex Cap check
                    dexMod = Mathf.Min(dexMod, armor.dexCap);
                }
            }

            breakdown.baseAC =
                10 + PF2E_Core.CalculateModifier(stats.level, armorProf, dexMod) + itemBonus;

            if (conditions == null)
            {
                breakdown.totalAC = breakdown.baseAC;
                return breakdown;
            }

            string statusPenaltySource;
            breakdown.statusPenalty = PF2E_Core.GetStatusPenalty(
                conditions,
                AbilityScore.DEX,
                out statusPenaltySource
            );
            breakdown.statusPenaltySources = statusPenaltySource;

            List<string> circumList = new List<string>();
            int circumstanceMod = 0;

            // Resolve Off-Guard status (Conditions + Flanking) via Combat Rules
            if (CombatRules.IsOffGuard(attacker, this, incomingAttackType))
            {
                circumstanceMod -= 2;
                circumList.Add("Off-Guard (-2)");
            }

            if (
                incomingAttackType == AttackType.Ranged
                && conditions.HasCondition(ConditionType.Prone)
            )
            {
                circumstanceMod += 2;
                circumList.Add("Prone (+2 vs Ranged)");
            }

            breakdown.circumstanceMod = circumstanceMod;
            breakdown.circumstanceModSources = string.Join(", ", circumList);
            breakdown.totalAC =
                breakdown.baseAC + breakdown.statusPenalty + breakdown.circumstanceMod;

            return breakdown;
        }

        public int getTotalHP()
        {
            if (stats == null)
                return 0;
            return stats.TotalHP;
        }
    }
}
