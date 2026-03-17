using System.Collections.Generic;
using PathfinderTactics.Core;
using PathfinderTactics.Grid;
using PathfinderTactics.ScriptableObjects;
using UnityEngine;

namespace PathfinderTactics.Characters
{
    /// <summary>
    /// Projects auras and applies effects to units within range.
    /// </summary>
    public class UnitAuraEmitter : MonoBehaviour
    {
        [SerializeField]
        private List<AuraEffectSO> auras = new List<AuraEffectSO>();

        private Unit unit;
        private GridSystem gridSystem;
        private TurnManager turnManager;

        // Tracks (Unit, Aura, Round) to prevent double-triggering logic
        private HashSet<(Unit, AuraEffectSO, int)> triggerHistory =
            new HashSet<(Unit, AuraEffectSO, int)>();

        // Tracks units currently inside each aura to detect OnExit
        private Dictionary<AuraEffectSO, HashSet<Unit>> unitsInAuras =
            new Dictionary<AuraEffectSO, HashSet<Unit>>();

        private void Awake()
        {
            unit = GetComponent<Unit>();
        }

        public List<AuraEffectSO> GetAuras() => auras;

        private void Start()
        {
            gridSystem = ServiceLocator.Get<GridSystem>();
            turnManager = ServiceLocator.Get<TurnManager>();

            foreach (var aura in auras)
            {
                unitsInAuras[aura] = new HashSet<Unit>();
            }
        }

        /// <summary>
        /// Scans surroundings and triggers effects based on the trigger type.
        /// Called by TurnManager (StartTurn) and UnitActionSystem (Move).
        /// </summary>
        public void UpdateAuras(AuraTriggerType context)
        {
            foreach (var aura in auras)
            {
                ProcessAura(aura, context);
            }
        }

        private void ProcessAura(AuraEffectSO aura, AuraTriggerType context)
        {
            List<Unit> currentAffected = gridSystem.GetUnitsInRadius(
                unit.CurrentGridPosition,
                aura.radiusInTiles
            );
            HashSet<Unit> previousAffected = unitsInAuras[aura];

            int currentRound = turnManager.RoundCount;

            // Detect Exiting Units
            foreach (Unit prev in previousAffected)
            {
                if (!currentAffected.Contains(prev))
                {
                    // Unit left the aura
                    if (
                        aura.triggerType == AuraTriggerType.OnExit
                        || aura.triggerType == AuraTriggerType.Both
                    )
                    {
                        ApplyAuraEffect(aura, prev, currentRound);
                    }
                    Debug.Log(
                        $"<color=gray>[AURA EXIT]</color> {prev.name} is no longer affected by {unit.name}'s {aura.auraName}"
                    );
                }
            }

            // Detect Entering / Staying Units
            foreach (Unit target in currentAffected)
            {
                if (!aura.ShouldAffect(unit, target))
                    continue;

                bool isNew = !previousAffected.Contains(target);

                if (
                    context == AuraTriggerType.OnStartTurn
                    && aura.triggerType == AuraTriggerType.OnStartTurn
                )
                {
                    ApplyAuraEffect(aura, target, currentRound);
                }
                else if (
                    isNew
                    && (
                        aura.triggerType == AuraTriggerType.OnEnter
                        || aura.triggerType == AuraTriggerType.Both
                    )
                )
                {
                    ApplyAuraEffect(aura, target, currentRound);
                }
            }

            // Update tracked units
            previousAffected.Clear();
            foreach (Unit target in currentAffected)
            {
                if (aura.ShouldAffect(unit, target))
                {
                    previousAffected.Add(target);
                }
            }
        }

        private void ApplyAuraEffect(AuraEffectSO aura, Unit target, int round)
        {
            // Double-trigger protection
            if (aura.oncePerTurn)
            {
                if (triggerHistory.Contains((target, aura, round)))
                {
                    return;
                }
                triggerHistory.Add((target, aura, round));
            }

            Debug.Log(
                $"<color=magenta>[AURA]</color> {unit.name}'s {aura.auraName} affecting {target.name}"
            );

            switch (aura.effectType)
            {
                case AuraEffectType.ApplyCondition:
                    var conditions = target.GetComponent<UnitConditions>();
                    if (conditions != null)
                    {
                        conditions.ApplyCondition(
                            aura.condition.conditionType,
                            aura.condition.value,
                            unit
                        );
                    }
                    break;
                case AuraEffectType.DealDamage:
                    var health = target.GetComponent<IDamageable>();
                    if (health != null)
                    {
                        int dmg = 0;
                        for (int i = 0; i < aura.damage.diceCount; i++)
                        {
                            dmg += UnityEngine.Random.Range(1, aura.damage.diceSides + 1);
                        }
                        dmg += aura.damage.flatBonus;
                        health.ApplyDamage(unit, dmg, false);
                    }
                    break;
            }
        }

        /// <summary>
        /// Cleanup history from previous rounds to save memory.
        /// Possibly call this at the start of a round.
        /// </summary>
        public void ClearOldHistory(int currentRound)
        {
            triggerHistory.RemoveWhere(item => item.Item3 < currentRound);
        }
    }
}
