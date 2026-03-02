using UnityEngine;
using PathfinderTactics.Characters;
using TMPro;

namespace PathfinderTactics.Feats
{
    public class exacting_strike : FeatBase
    {
        public override bool Perform(Unit parent, Unit target)
        {

            Debug.Log($"Exacting Strike!");
            //Press type feat, so check if we have attacked already
            if (parent.GetActionPointsRemaining() < 1 && parent.GetAttackCount() > 0)
            {
                Debug.Log("Not enough actions.");
                return false; 
            }

            parent.SpendActionPoints(1);

            //Here down is a near-direct copy and paste of attack logic from the Unit class, should be refactored to avoid this
            TextMeshProUGUI rollText = GameObject.Find("Roll_results").GetComponent<TextMeshProUGUI>();
            rollText.text = "Exacting Strike!"; 
            // Clear previous rolls
            // Simple attack logic
            int roll = UnityEngine.Random.Range(1, 21);
            int strength = parent.GetUnitStats().strength;
            // Profcienciey is expertise for now (Fighter level 1) expertise = 4 + lvl,
            int proficiency = 5;
            int penalty = -1 * (parent.GetAttackCount() * 5);
            int attackValue = roll + strength + proficiency + penalty;


            if (roll != 20)
            {
                if (target.Defend_against_attack(attackValue))
                {
                    rollText.text = "Exacting Strike blocked! MAP stayed the same!";
                    return true;
                }
                else
                {
                    //TODO: damage change based on weapon (right now hardCoded longsword damage)
                    int damage = UnityEngine.Random.Range(1, 9) + 4;
                    target.ReduceCurrentHP(damage);

                    //Only reduce on succesful hit
                    rollText.text = "Exacting Strike HIT! MAP increased!";
                    parent.ReduceAttackCount(-1);
                }
            }
            else
            {

                int damage = 2 * (UnityEngine.Random.Range(1, 9) + 4);
                target.ReduceCurrentHP(damage);

                parent.ReduceAttackCount(-1);
                rollText.text = "Exacting Strike CRIT! MAP increased!";
            }

            return true;

        }
    }
}
