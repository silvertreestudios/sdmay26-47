namespace TacticsGame.Characters
{
    public enum ConditionType
    {
        // Numeric Conditions

        // Frightened (X)
        // Status penalty = X to ALL checks and DCs.
        // At end of your turn -> value decreases by 1.
        Frightened,

        // Enfeebled (X)
        // Status penalty = X to ALL Strength-based rolls and DCs.
        // Affects:
        // - Melee attack rolls using STR
        // - STR-based damage rolls
        // - Athletics checks
        // - STR-based DCs
        Enfeebled,

        // Clumsy (X)
        // Status penalty = X to ALL Dexterity-based rolls and DCs.
        // Affects:
        // - Armor Class
        // - Reflex saves
        // - Ranged attack rolls
        // - DEX skills (Stealth, Thievery, Acrobatics)
        Clumsy,

        // Sickened (X)
        // Status penalty = X to ALL checks and DCs.
        // Cannot willingly ingest anything (food, potions, etc).
        // 1 Action: Retch -> Fortitude save vs effect DC:
        //   Success: reduce Sickened by 1
        //   Critical Success: reduce Sickened by 2
        Sickened,

        // Stupefied (X)
        // Status penalty = X to INT/WIS/CHA-based checks and DCs.
        // Spellcasting: must pass flat check DC = 5 + X or spell is lost.
        Stupefied,

        // Stunned (X)
        // Lose X actions at the start of your turn.
        // Each action lost reduces Stunned by 1.
        // While Stunned > 0 you cannot act.
        Stunned,

        // Slowed (X)
        // Lose X actions at the start of each turn.
        // Minimum actions per turn = 1.
        // Does NOT decrease automatically.
        Slowed,

        // Quickened
        // Gain +1 extra action each turn.
        // Extra action can ONLY be used for specific actions defined by the effect
        // (commonly Stride or Strike).
        Quickened,

        // Dying (X)
        // You are unconscious and near death.
        // Start of turn -> Recovery Check:
        //   DC = 10 + dying value
        //   Crit Success: dying -2
        //   Success: dying -1
        //   Failure: dying +1
        //   Crit Failure: dying +2
        // Death occurs at dying 4 (modified by Doomed).
        Dying,

        // Wounded (X)
        // When you gain Dying -> increase dying by Wounded value.
        // When you recover from Dying -> Wounded increases by 1.
        // Removed by full healing + rest (GM dependent).
        Wounded,

        // Doomed (X)
        // Reduces the dying value needed to die.
        // Death occurs at dying (4 - Doomed).
        Doomed,

        // Drained (X)
        // Max HP reduced by (Level × X).
        // Immediately lose that many HP from current HP as well.
        // Status penalty = X to CON-based checks (including Fortitude saves).
        Drained,

        // Binary Conditions

        // Off-Guard
        // –2 circumstance penalty to AC.
        // Common sources: flanking, prone, grabbed, restrained, blinded.
        OffGuard,

        // Prone
        // Off-Guard.
        // –2 circumstance penalty to attack rolls.
        // +2 circumstance bonus to AC vs ranged attacks.
        // Only Crawl for movement.
        // Stand action removes.
        Prone,

        // Grabbed
        // Off-Guard.
        // Immobilized.
        // Must Escape to break free.
        // Manipulate actions require flat check DC 5.
        Grabbed,

        // Restrained
        // Stronger version of Grabbed.
        // Off-Guard.
        // Immobilized.
        // Cannot use most actions except Escape or those allowed by the GM/effect.
        Restrained,

        // Immobilized
        // Cannot use actions with the Move trait that move you from your space.
        // Escape may remove depending on source.
        Immobilized,

        // Unconscious
        // Cannot act or perceive surroundings.
        // Off-Guard.
        // Usually Prone.
        // Drop held items.
        // Typically caused by Dying or Sleep effects.
        Unconscious,

        // Blinded
        // Cannot see.
        // Automatically critically fail Perception checks requiring sight.
        // Creatures are treated as Hidden from you.
        // You are Off-Guard.
        Blinded,

        // Deafened
        // Cannot hear.
        // Automatically fail Perception checks requiring hearing.
        Deafened,

        // Invisible
        // Cannot be seen.
        // Creatures treat you as Undetected unless they successfully detect you
        // via other senses or actions like Seek.
        Invisible,

        // Concealed
        // The creature can still be seen/targeted, but it's harder to pinpoint
        // (e.g., fog, smoke). This affects targeting flat checks.
        Concealed,

        // Fatigued
        // –1 status penalty to AC and saving throws.
        // Removed after a full night's rest.
        // Or in this case, prolly just next level
        Fatigued,
    }

    public enum DamageType
    {
        Untyped,
        Piercing,
        Slashing,
        Bludgeoning,
        Fire,
        Cold,
        Acid,
        Electricity,
        Poison,
        Bleed,
        Mental,
        Force,
        Sonic,
        Positive,
        Negative,
    }

    public enum ActionTag
    {
        None,
        Stride,
        Strike,
        Step,
        Sustain, // Used for Quickened restrictions
    }

    public enum DetectionState
    {
        Observed,
        Hidden,
        Undetected,
        Unnoticed,
    }
}
