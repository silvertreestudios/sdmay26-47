namespace TacticsGame.Reactions
{
    public enum ReactionMode
    {
        Auto, // Triggers automatically without asking
        Prompt, // Pauses the game and asks the player via UI
        Conditional, // Triggers automatically ONLY IF specific conditions are met
    }
}
