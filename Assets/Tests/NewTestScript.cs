using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using TacticsGame.Characters;
using TacticsGame.Core;
using TacticsGame.Reactions;

public class NewTestScript
{
    [UnityTest]
    public IEnumerator UnitTakesDamage_HealthDecreases()
    {
        // 1. Setup ServiceLocator & ReactionManager
        ServiceLocator.ClearAll();
        var rmGo = new GameObject("ReactionManager");
        var rm = rmGo.AddComponent<ReactionManager>();
        
        // Let Unity initialize the objects properly.
        yield return null; 

        // 2. Setup Unit and UnitHealth
        var attackerGo = new GameObject("Attacker");
        var attacker = attackerGo.AddComponent<Unit>();

        var defenderGo = new GameObject("Defender");
        var defender = defenderGo.AddComponent<Unit>();
        var defenderHealth = defenderGo.AddComponent<UnitHealth>(); // automatically adds UnitConditions
        yield return null; // Let Unity run Awake on components

        // Verify initial setup is correct
        Assert.AreEqual(20, defenderHealth.GetCurrentHealth(), "Initial health should be 20.");

        // 3. Apply Damage
        defenderHealth.ApplyDamage(attacker, 5, DamageType.Slashing);
        
        // Let any reactions process (they are synchronous but just in case)
        yield return null;

        // 4. Verify Health Changed
        Assert.AreEqual(15, defenderHealth.GetCurrentHealth(), "Health should drop to 15 after taking 5 damage.");

        // Cleanup
        ServiceLocator.ClearAll();
        GameObject.DestroyImmediate(rmGo);
        GameObject.DestroyImmediate(attackerGo);
        GameObject.DestroyImmediate(defenderGo);
    }
}
