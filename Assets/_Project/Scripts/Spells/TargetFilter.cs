namespace PathfinderTactics.Spells
{
    /// <summary>
    /// Filter applied by each SpellEffectSO to determine which units it affects.
    /// Evaluated by UnitQueryService when populating AffectedUnits.
    /// </summary>
    public enum TargetFilter
    {
        All, // Every unit in the affected area
        Enemies, // Only opposing faction
        Allies, // Only same faction
        ExcludeCaster, // All except the casting unit
        LivingOnly, // Skip dead/unconscious
    }
}
