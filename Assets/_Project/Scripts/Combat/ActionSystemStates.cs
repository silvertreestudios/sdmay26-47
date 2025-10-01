namespace PathfinderTactics.Core
{
    public enum GamePhase
    {
        UnitSelection, // Player is free to select any of their units
        FreeMovement, // Player has selected a unit and is actively moving it
        ActionSelection, // Player has finished moving and is choosing an action (Attack, etc.)
        Busy, // An action is being executed, block all input
    }
}
