using PathfinderTactics.Characters;
using UnityEngine;

namespace PathfinderTactics.DebugTools
{
    [RequireComponent(typeof(UnitConditions))]
    public class ConditionDebugger : MonoBehaviour
    {
        [Header("Injection Settings")]
        public ConditionType conditionToTest;
        public int valueToApply = 1;

        [Header("Persistent Damage Settings")]
        public DamageType damageTypeToTest;
        public int diceCount = 1;
        public int diceFaces = 6;
        public int flatDamage = 0;

        private UnitConditions conditions;

        private void Awake()
        {
            conditions = GetComponent<UnitConditions>();
        }

        // The [ContextMenu] attribute adds a button when you right-click the component in the Inspector!

        [ContextMenu("Inject Condition")]
        public void TestApplyCondition()
        {
            if (Application.isPlaying)
            {
                conditions.ApplyCondition(conditionToTest, valueToApply);
            }
            else
            {
                Debug.LogWarning("You must be in Play Mode to test conditions.");
            }
        }

        [ContextMenu("Remove Condition")]
        public void TestRemoveCondition()
        {
            if (Application.isPlaying)
            {
                conditions.RemoveCondition(conditionToTest);
            }
        }

        [ContextMenu("Inject Persistent Damage")]
        public void TestApplyPersistent()
        {
            if (Application.isPlaying)
            {
                conditions.ApplyPersistentDamage(
                    damageTypeToTest,
                    diceCount,
                    diceFaces,
                    flatDamage
                );
                Debug.Log(
                    $"Applied {diceCount}d{diceFaces} + {flatDamage} {damageTypeToTest} Persistent Damage."
                );
            }
        }

        [ContextMenu("Force Turn Start (Test AP)")]
        public void TestTurnStart()
        {
            if (Application.isPlaying)
            {
                int apMod = conditions.HandleTurnStart(out ActionTag restriction);
                Debug.Log(
                    $"<color=yellow>Turn Started.</color> Action Point Modifier: {apMod}. Restriction: {restriction}"
                );
            }
        }

        [ContextMenu("Force Turn End (Test Decay)")]
        public void TestTurnEnd()
        {
            if (Application.isPlaying)
            {
                Debug.Log(
                    "<color=yellow>Turn Ended. Processing decay and persistent damage...</color>"
                );
                conditions.HandleTurnEnd();
            }
        }
    }
}
