namespace TacticsGame.Core
{
    public enum GamePhase
    {
        UnitSelection, // Player is free to select any of their units
        FreeMovement, // Player has selected a unit and is actively moving it
        ActionSelection, // Player has finished moving and is choosing an action (Attack, etc.)
        ActionTargeting, // Player is targeting a unit to use an action on
        Busy, // An action is being executed, block all input
        EagleEye, // Free camera top down view for observing map
    }
}
