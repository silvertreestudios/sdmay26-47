using System;
using System.Collections.Generic;
using PathfinderTactics.Actions;
using PathfinderTactics.Characters;
using PathfinderTactics.Combat;
using PathfinderTactics.Core;
using PathfinderTactics.Grid;
using PathfinderTactics.Items;
using TMPro;
using UnityEngine;

namespace PathfinderTactics.Feats
{
    public class exacting_strike : BaseAction, FeatBase
    {
        private const bool STEALTH_DEBUG = false;

        private Unit targetUnit;
        private Unit intendedTargetUnit;
        private Vector3Int intendedTargetTile;

        public override bool IsUnitTargeted => true;

        public override string GetActionName()
        {
            var weapon = GetWeapon();
            string weaponName = weapon != null ? weapon.itemName : "Unarmed";
            return $"Exacting Strike - {weaponName}";
        }

        [HideInInspector]
        public WeaponSO activeWeapon;

        public WeaponSO GetWeapon()
        {
            if (activeWeapon != null)
                return activeWeapon;
            var equipment = GetComponent<UnitEquipment>();
            return equipment != null ? equipment.GetMainWeapon() : null;
        }

        private int GetMaxRange()
        {
            var weapon = GetWeapon();
            if (weapon != null)
            {
                return Mathf.Max(1, weapon.reachFeet / 5);
            }
            return 1;
        }

        public override List<Vector3Int> GetActionRangeGridPositions()
        {
            int range = GetMaxRange();
            List<Vector3Int> rangePositions = new List<Vector3Int>();
            HashSet<Vector3Int> added = new HashSet<Vector3Int>();
            GridSystem grid = ServiceLocator.Get<GridSystem>();
            Vector3Int unitPos = unit.CurrentLayeredPosition;

            for (int x = -range; x <= range; x++)
            {
                for (int z = -range; z <= range; z++)
                {
                    Vector2Int colKey = new Vector2Int(unitPos.x + x, unitPos.z + z);
                    List<GridNode> column = grid.GetColumn(colKey);
                    if (column == null || column.Count == 0)
                        continue;

                    foreach (GridNode node in column)
                    {
                        if (PF2E_Core.GetPF2eDistance3D(unitPos, node.Coordinates) <= range)
                        {
                            if (added.Add(node.Coordinates))
                                rangePositions.Add(node.Coordinates);
                        }
                    }
                }
            }
            return rangePositions;
        }

        private void Update()
        {
            if (!isActive || targetUnit == null)
                return;

            float rotateSpeed = 10f;
            Vector3 aimDir = (targetUnit.transform.position - transform.position).normalized;
            aimDir.y = 0f;

            if (aimDir.sqrMagnitude > 0.001f)
            {
                transform.forward = Vector3.Lerp(
                    transform.forward,
                    aimDir,
                    Time.deltaTime * rotateSpeed
                );
            }
        }

        private void HandleStrikeConnects()
        {
            PerformStrikeLogic();
        }

        private void HandleAnimationEnd()
        {
            ActionComplete();
        }

        private void ActionComplete()
        {
            if (!isActive)
                return;

            CancelInvoke(nameof(FallbackActionComplete));

            isActive = false;
            var visuals = unit.GetComponentInChildren<UnitVisuals>();
            if (visuals != null)
            {
                visuals.OnStrikeConnects -= HandleStrikeConnects;
                visuals.OnAnimationEnd -= HandleAnimationEnd;
            }
            onActionComplete?.Invoke();
        }

        private void FallbackActionComplete()
        {
            Debug.LogWarning(
                $"<color=orange>[FAILSAFE]</color> The Animator swallowed the ActionComplete event! Rescuing the game state via failsafe timer."
            );
            ActionComplete();
        }

        // Satisfy the FeatBase interface contract.
        // In this modern animator-driven workflow, PerformStrikeLogic() resolves the math precisely mid-animation instead.
        public bool Perform(Unit parent, Unit target)
        {
            return true;
        }

        public override void TakeAction(Vector3Int targetPosition, Action onActionComplete)
        {
            if (!CanExecuteAction())
            {
                onActionComplete?.Invoke();
                return;
            }

            if (
                ServiceLocator.TryGet<TargetLockService>(out var tls)
                && tls.IsActive
                && tls.CurrentTarget != null
            )
            {
                targetUnit = tls.CurrentTarget;
            }
            else
            {
                targetUnit = ServiceLocator.Get<GridSystem>().GetUnitAt(targetPosition);
            }

            if (targetUnit == null)
            {
                Debug.LogError("No unit found at target position!");
                onActionComplete?.Invoke();
                return;
            }

            intendedTargetUnit = targetUnit;
            intendedTargetTile = targetPosition;
            this.onActionComplete = onActionComplete;
            isActive = true;

            var visuals = unit.GetComponentInChildren<UnitVisuals>();
            if (visuals != null)
            {
                var weapon = GetWeapon();
                if (weapon != null)
                {
                    visuals.SetWeaponType(weapon.weaponAnimType);
                }

                visuals.OnStrikeConnects += HandleStrikeConnects;
                visuals.OnAnimationEnd += HandleAnimationEnd;
                visuals.TriggerMeleeAttack();

                Invoke(nameof(FallbackActionComplete), 2.0f);
            }
            else
            {
                PerformStrikeLogic();
                ActionComplete();
            }
        }

        private void PerformStrikeLogic()
        {
            var stats = unit.GetStats();
            if (stats == null || targetUnit == null)
                return;

            GridSystem grid = ServiceLocator.Get<GridSystem>();
            Unit actualTarget = grid.GetUnitAt(intendedTargetUnit.CurrentLayeredPosition);
            if (actualTarget == null || actualTarget != intendedTargetUnit)
            {
                unit.IncrementAttacksThisTurn();
                BreakStealth();
                return;
            }

            targetUnit = actualTarget;

            UnitStealth targetStealth = targetUnit.GetComponent<UnitStealth>();
            if (targetStealth != null)
            {
                DetectionState targetState = targetStealth.GetDetectionState(unit);

                if (targetState == DetectionState.Unnoticed)
                {
                    unit.IncrementAttacksThisTurn();
                    BreakStealth();
                    return;
                }

                if (targetState != DetectionState.Undetected)
                {
                    int flatCheckDC = targetStealth.RequiresFlatCheckToTarget(unit);
                    if (flatCheckDC > 0)
                    {
                        int flatRoll = UnityEngine.Random.Range(1, 21);
                        if (flatRoll < flatCheckDC)
                        {
                            // Exacting Strike: Failed flat check means a miss, but does not increase MAP
                            Debug.Log(
                                "<color=grey>[SENSES]</color> Exacting Strike missed! Failed flat check (MAP does not increase)."
                            );
                            BreakStealth();
                            return;
                        }
                    }
                }
            }

            var weapon = GetWeapon();
            if (weapon == null)
                return;

            bool isAgileWeapon = weapon.HasTrait(WeaponTrait.Agile);
            bool isFinesseWeapon = weapon.HasTrait(WeaponTrait.Finesse);

            int level = stats.level;

            int strengthMod = unit.GetAbilityModifier(AbilityScore.STR);
            int dexMod = unit.GetAbilityModifier(AbilityScore.DEX);
            int attackStatMod = isFinesseWeapon ? Mathf.Max(strengthMod, dexMod) : strengthMod;
            AbilityScore attackStat =
                (isFinesseWeapon && dexMod > strengthMod) ? AbilityScore.DEX : AbilityScore.STR;

            Proficiency weaponProf = Proficiency.Trained;

            int mapPenalty = 0;
            int attacksMade = unit.AttacksThisTurn;

            if (attacksMade == 1)
                mapPenalty = isAgileWeapon ? -4 : -5;
            else if (attacksMade >= 2)
                mapPenalty = isAgileWeapon ? -8 : -10;

            int attackBonus = PF2E_Core.CalculateAttackRollModifier(
                unit,
                attackStat,
                attackStatMod,
                level,
                weaponProf,
                AttackType.Melee,
                mapPenalty
            );

            ArmorClassBreakdown acBreakdown = targetUnit.GetArmorClassBreakdown(
                unit,
                AttackType.Melee
            );
            int baseAC = acBreakdown.totalAC;

            int coverBonus = LineOfSightUtility.GetCoverBonus(
                unit.CurrentLayeredPosition,
                targetUnit.CurrentLayeredPosition
            );

            if (coverBonus == -1)
            {
                // Attack aborted completely
                return;
            }

            int finalAC = baseAC + coverBonus;

            int d20 = UnityEngine.Random.Range(1, 21);
            Degree result = PF2E_Core.CheckResult(d20, attackBonus, finalAC);

            Debug.Log(
                $"<b>[EXACTING STRIKE]</b> Rolled {d20} + {attackBonus} vs AC {finalAC} -> <color={(result == Degree.Success || result == Degree.CriticalSuccess ? "green" : "red")}>{result}</color>"
            );

            if (result == Degree.Success || result == Degree.CriticalSuccess)
            {
                // EXACTING STRIKE RULE: Only increment MAP on a hit!
                unit.IncrementAttacksThisTurn();

                int weaponDiceRoll = 0;
                for (int i = 0; i < weapon.damageDice.count; i++)
                {
                    weaponDiceRoll += UnityEngine.Random.Range(1, weapon.damageDice.sides + 1);
                }

                int damage = weaponDiceRoll + strengthMod;

                var myConditions = unit.GetComponent<UnitConditions>();
                if (myConditions != null && myConditions.HasCondition(ConditionType.Enfeebled))
                {
                    int enfValue = myConditions.GetConditionValue(ConditionType.Enfeebled);
                    damage -= enfValue;
                    if (damage < 1)
                        damage = 1;
                }

                if (result == Degree.CriticalSuccess)
                {
                    damage *= 2;
                }

                var targetHealth = targetUnit.GetComponent<IDamageable>();
                if (targetHealth != null)
                {
                    targetHealth.ApplyDamage(
                        unit,
                        damage,
                        weapon.damageType,
                        result == Degree.CriticalSuccess
                    );
                    Debug.Log($"Exacting Strike dealt {damage} Damage to {targetUnit.name}!");
                }
            }
            else
            {
                // Missed! Exacting Strike bypasses MAP.
                Debug.Log(
                    "<color=cyan>Exacting Strike Miss! Multiple Attack Penalty DOES NOT increase!</color>"
                );

                var targetVisuals = targetUnit.GetComponentInChildren<UnitVisuals>();
                if (targetVisuals != null)
                {
                    targetVisuals.TriggerDodge();
                }
            }

            BreakStealth();
        }

        private void BreakStealth()
        {
            StealthResolver.OnNoiseGenerated(unit);
            StealthResolver.BreakStealthAfterAttack(unit);
        }

        public override bool IsValidActionGridPosition(Vector3Int targetPosition)
        {
            return GetValidActionGridPositions().Contains(targetPosition);
        }

        public override List<Vector3Int> GetValidActionGridPositions()
        {
            int range = GetMaxRange();
            List<Vector3Int> validPositions = new List<Vector3Int>();
            HashSet<Vector3Int> added = new HashSet<Vector3Int>();
            GridSystem grid = ServiceLocator.Get<GridSystem>();
            Vector3Int unitPos = unit.CurrentLayeredPosition;

            for (int x = -range; x <= range; x++)
            {
                for (int z = -range; z <= range; z++)
                {
                    Vector2Int colKey = new Vector2Int(unitPos.x + x, unitPos.z + z);
                    List<GridNode> column = grid.GetColumn(colKey);
                    if (column == null || column.Count == 0)
                        continue;

                    foreach (GridNode node in column)
                    {
                        Vector3Int testPos = node.Coordinates;
                        if (testPos == unitPos)
                            continue;

                        if (PF2E_Core.GetPF2eDistance3D(unitPos, testPos) > range)
                            continue;

                        Unit target = grid.GetUnitAt(testPos);
                        if (target == null)
                            continue;
                        if (target.GetFaction() == unit.GetFaction())
                            continue;

                        var targetStealth = target.GetComponent<UnitStealth>();
                        if (
                            targetStealth != null
                            && targetStealth.GetDetectionState(unit) == DetectionState.Unnoticed
                        )
                            continue;

                        if (!LineOfSightUtility.HasLineOfEffect(unitPos, testPos))
                            continue;

                        if (!LineOfSightUtility.Evaluate(unitPos, testPos).HasLineOfSight)
                            continue;

                        if (added.Add(testPos))
                            validPositions.Add(testPos);
                    }
                }
            }

            return validPositions;
        }
    }
}
