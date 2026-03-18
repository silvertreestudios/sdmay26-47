using System.Linq;
using UnityEngine;

namespace PathfinderTactics.Characters
{
    /// <summary>
    /// Static helper for resolving PF2e Resistance, Weakness, and Immunity.
    /// Follows the order: Immunity -> Weakness -> Resistance -> Clamp.
    /// </summary>
    public static class RWICalculator
    {
        public static int ResolveDamage(int incomingDamage, DamageType type, Unit target)
        {
            if (target == null)
                return incomingDamage;

            var profile = target.RWIProfile;
            if (profile == null)
                return incomingDamage;

            int finalDamage = incomingDamage;

            // Immunity
            if (profile.Immunities.Contains(type))
            {
                Debug.Log(
                    $"<color=cyan>[IMMUNITY]</color> {target.name} is immune to {type} -> Damage = 0"
                );
                return 0;
            }

            // Weakness
            var weakness = profile.Weaknesses.FirstOrDefault(w => w.Type == type);
            if (weakness != null && weakness.Value > 0)
            {
                finalDamage += weakness.Value;
                Debug.Log(
                    $"<color=red>[WEAKNESS]</color> {target.name} takes +{weakness.Value} {type} damage (Total: {finalDamage})"
                );
            }

            // Resistance
            var resistance = profile.Resistances.FirstOrDefault(r => r.Type == type);
            if (resistance != null && resistance.Value > 0)
            {
                int prev = finalDamage;
                finalDamage = Mathf.Max(0, finalDamage - resistance.Value);
                int actualBlocked = prev - finalDamage;
                if (actualBlocked > 0)
                {
                    Debug.Log(
                        $"<color=yellow>[RESISTANCE]</color> {target.name} resists {actualBlocked} {type} damage (Final: {finalDamage})"
                    );
                }
            }

            // Clamp
            finalDamage = Mathf.Max(0, finalDamage);

            return finalDamage;
        }
    }
}
