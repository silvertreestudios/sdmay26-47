namespace PathfinderTactics.Spells
{
    /// <summary>
    /// Determines when a spell effect executes during resolution.
    /// Resolver iterates phases in order: Targeting -> Roll -> Resolution -> Aftermath.
    /// </summary>
    public enum SpellEffectPhase
    {
        Targeting, // Compute AoE cells, gather affected units
        Roll, // Attack rolls, saving throws, degree modifications
        Resolution, // Damage, healing, condition application
        Aftermath, // Post-resolution triggers, cleanup
    }
}
