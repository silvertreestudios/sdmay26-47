using System.Collections;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using PathfinderTactics.Characters;
using PathfinderTactics.Core;
using PathfinderTactics.Reactions;
using PathfinderTactics.Combat;

namespace PathfinderTactics.Tests
{
    public class UnitHealthTests
    {
        private GameObject reactionManagerGo;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            ServiceLocator.ClearAll();
            reactionManagerGo = new GameObject("ReactionManager");
            reactionManagerGo.AddComponent<ReactionManager>();
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            ServiceLocator.ClearAll();
            if (reactionManagerGo != null)
            {
                Object.Destroy(reactionManagerGo);
            }

            // Cleanup any stray GameObjects
            var allUnits = Object.FindObjectsByType<Unit>(FindObjectsSortMode.None);
            foreach (var unit in allUnits)
            {
                Object.Destroy(unit.gameObject);
            }

            yield return null;
        }

        /// <summary>
        /// Suppresses the expected ServiceLocator errors that fire when Unit and its
        /// RequireComponent dependencies (UnitGridObject, UnitMovement, etc.) call
        /// Start() without a full game environment.
        /// </summary>
        private void SuppressExpectedServiceErrors()
        {
            // These errors come from ServiceLocator.Get<T>() when services aren't registered.
            // They are harmless in a test context but Unity's test runner treats unhandled
            // Debug.LogError as a failure. LogAssert.Expect tells the runner to expect them.
            LogAssert.Expect(LogType.Error, new Regex("Service .* not found"));
            LogAssert.Expect(LogType.Error, new Regex("Service .* not found"));
            LogAssert.Expect(LogType.Error, new Regex("Service .* not found"));
            LogAssert.Expect(LogType.Error, new Regex("Service .* not found"));
            LogAssert.Expect(LogType.Error, new Regex("Service .* not found"));
            // NullReferenceException from Unit.Start() when UnitActionSystem is null
            LogAssert.ignoreFailingMessages = true;
        }

        private (GameObject, Unit, UnitHealth, UnitConditions) CreateTestUnit(string name)
        {
            var go = new GameObject(name);
            var unit = go.AddComponent<Unit>();
            var conditions = go.GetComponent<UnitConditions>(); // Added by RequireComponent
            var health = go.AddComponent<UnitHealth>();
            return (go, unit, health, conditions);
        }

        [UnityTest]
        public IEnumerator ApplyHealing_CappedAtMaxHealth()
        {
            SuppressExpectedServiceErrors();
            var (go, unit, health, _) = CreateTestUnit("HealUnit");
            yield return null;

            // Health is initialized to 20
            health.ApplyDamage(null, 5, DamageType.Slashing);
            Assert.AreEqual(15, health.GetCurrentHealth());

            health.ApplyHealing(10);
            Assert.AreEqual(20, health.GetCurrentHealth(), "Healing should not exceed base max health of 20.");

            LogAssert.ignoreFailingMessages = false;
        }

        [UnityTest]
        public IEnumerator ReachingZeroHP_AppliesDyingAndUnconscious()
        {
            SuppressExpectedServiceErrors();
            var (go, unit, health, conditions) = CreateTestUnit("DyingUnit");
            yield return null;

            health.ApplyDamage(null, 25, DamageType.Slashing);
            yield return null;

            Assert.AreEqual(0, health.GetCurrentHealth());
            Assert.IsTrue(conditions.HasCondition(ConditionType.Dying), "Should have Dying condition at 0 HP.");
            Assert.IsTrue(conditions.HasCondition(ConditionType.Unconscious), "Should be Unconscious at 0 HP.");
            Assert.AreEqual(1, conditions.GetConditionValue(ConditionType.Dying), "Initial Dying value should be 1.");

            LogAssert.ignoreFailingMessages = false;
        }

        [UnityTest]
        public IEnumerator DamageWhileDying_IncreasesDyingValue()
        {
            SuppressExpectedServiceErrors();
            var (go, unit, health, conditions) = CreateTestUnit("MultiDyingUnit");
            yield return null;

            // Go to 0 HP
            health.ApplyDamage(null, 20, DamageType.Slashing);
            yield return null;
            Assert.AreEqual(1, conditions.GetConditionValue(ConditionType.Dying));

            // Take more damage while at 0 HP
            health.ApplyDamage(null, 5, DamageType.Slashing);
            yield return null;

            Assert.AreEqual(2, conditions.GetConditionValue(ConditionType.Dying), "Dying value should increase when taking damage while already at 0 HP.");

            LogAssert.ignoreFailingMessages = false;
        }

        [UnityTest]
        public IEnumerator DrainedCondition_ReducesCurrentAndMaxHP()
        {
            SuppressExpectedServiceErrors();
            var (go, unit, health, conditions) = CreateTestUnit("DrainedUnit");
            yield return null;

            // Initial: 20/20
            Assert.AreEqual(20, health.GetMaxHealth());
            Assert.AreEqual(20, health.GetCurrentHealth());

            // Apply Drained 2. (Level 1 unit * drained 2 = 2 HP reduction)
            conditions.ApplyCondition(ConditionType.Drained, 2);
            yield return null;

            Assert.AreEqual(18, health.GetMaxHealth(), "Max HP should be reduced by Drained.");
            Assert.AreEqual(18, health.GetCurrentHealth(), "Current HP should also be reduced by Drained.");

            LogAssert.ignoreFailingMessages = false;
        }

        [UnityTest]
        public IEnumerator HealingWhileDying_RemovesDyingAndIncreasesWounded()
        {
            SuppressExpectedServiceErrors();
            var (go, unit, health, conditions) = CreateTestUnit("RecoverUnit");
            yield return null;

            // Go down
            health.ApplyDamage(null, 20, DamageType.Slashing);
            yield return null;
            Assert.IsTrue(conditions.HasCondition(ConditionType.Dying));

            // Heal
            health.ApplyHealing(5);
            yield return null;

            Assert.AreEqual(5, health.GetCurrentHealth());
            Assert.IsFalse(conditions.HasCondition(ConditionType.Dying), "Dying condition should be removed upon healing.");
            Assert.IsFalse(conditions.HasCondition(ConditionType.Unconscious), "Unconscious condition should be removed upon healing.");
            Assert.AreEqual(1, conditions.GetConditionValue(ConditionType.Wounded), "Wounded value should increase by 1 after recovering from Dying.");

            LogAssert.ignoreFailingMessages = false;
        }
    }
}
