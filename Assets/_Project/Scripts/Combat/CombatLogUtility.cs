using TacticsGame.Actions;
using TacticsGame.Characters;
using TacticsGame.Core;
using UnityEngine;

namespace TacticsGame.Combat
{
    /// <summary>
    /// Static utility for standardizing high-fidelity combat feedback in the Unity console.
    /// Exposes the full "Damage Pipeline" including rolls, modifiers, AC breakdown, and final HP.
    /// </summary>
    public static class CombatLogUtility
    {
        public static void LogAttackStage(
            Unit attacker,
            BaseAction action,
            int d20,
            int bonus,
            int map,
            int rangePenalty = 0
        )
        {
            int total = d20 + bonus + map + rangePenalty;
            string color = "white";
            if (d20 == 20)
                color = "yellow";
            else if (d20 == 1)
                color = "red";

            string mapStr = map != 0 ? $" | MAP: {map}" : "";
            string rangeStr = rangePenalty != 0 ? $" | Range: {rangePenalty}" : "";

            Debug.Log(
                $"<b><color=cyan>[ATTACK STAGE]</color></b> {attacker.name} uses <i>{action.GetActionName()}</i>\n"
                    + $"🎲 Roll: <color={color}><b>{d20}</b></color> + Bonus: {bonus}{mapStr}{rangeStr} ==> <b>Total: {total}</b>"
            );
        }

        public static void LogDefenseStage(Unit target, ArmorClassBreakdown ac, int cover)
        {
            int finalAC = ac.totalAC + cover;
            string statusStr =
                ac.statusPenalty != 0
                    ? $" | <color=red>Status: {ac.statusPenaltySources} ({ac.statusPenalty})</color>"
                    : "";
            string circumStr =
                ac.circumstanceMod != 0
                    ? $" | <color=orange>Circumstance: {ac.circumstanceModSources}</color>"
                    : "";
            string coverStr = cover > 0 ? $" | <color=cyan>Cover: +{cover}</color>" : "";

            Debug.Log(
                $"<b><color=yellow>[DEFENSE STAGE]</color></b> {target.name}\n"
                    + $"🛡️ Base AC: {ac.baseAC}{statusStr}{circumStr}{coverStr} ==> <b>Final AC: {finalAC}</b>"
            );
        }

        public static void LogResult(Degree result)
        {
            string color = result switch
            {
                Degree.CriticalSuccess => "lime",
                Degree.Success => "green",
                Degree.Failure => "orange",
                Degree.CriticalFailure => "red",
                _ => "white",
            };

            Debug.Log(
                $"<b><color=white>[RESULT]</color></b> <color={color}><b>{result.ToString().ToUpper()}</b></color>"
            );
        }

        public static void LogDamageStage(
            Unit target,
            int diceRoll,
            int modifiers,
            DamageType type,
            bool isCrit
        )
        {
            int total = diceRoll + modifiers;
            if (isCrit)
                total *= 2;

            string critStr = isCrit ? " <color=red><b>(CRITICAL X2!)</b></color>" : "";
            string modStr = modifiers != 0 ? $" + Mod: {modifiers}" : "";

            Debug.Log(
                $"<b><color=red>[DAMAGE STAGE]</color></b> Targeting {target.name}\n"
                    + $"💥 Dice: {diceRoll}{modStr}{critStr} ==> <b>{total} {type} Damage</b>"
            );
        }

        public static void LogFinalImpact(Unit target, int finalDamage, int currentHP, int maxHP)
        {
            string healthColor = (float)currentHP / maxHP > 0.5f ? "green" : "red";
            Debug.Log(
                $"<b><color=white>[FINAL IMPACT]</color></b> {target.name} took <b>{finalDamage}</b> damage.\n"
                    + $"❤️ HP: <color={healthColor}>{currentHP}/{maxHP}</color>"
            );
        }
    }
}
