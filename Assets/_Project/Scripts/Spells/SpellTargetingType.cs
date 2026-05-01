namespace TacticsGame.Spells
{
    /// <summary>
    /// How the spell selects its target(s) on the grid.
    /// Used by CastSpellAction to determine cursor behavior and valid positions.
    /// </summary>
    public enum SpellTargetingType
    {
        SingleTarget, // Click on a creature
        GroundTarget, // Click on a tile, spell resolves there
        Area, // AoE emanation from caster
        Self, // Targets only the caster
        Line, // Line from caster in a direction
        Cone, // Cone from caster in a direction
    }
}
