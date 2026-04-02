using UnityEngine;
using PathfinderTactics.Characters;

//All feats should implment this interface, can modify it as needed, and include methods that do nothing for some feats in neccesarry
namespace PathfinderTactics.Feats
{
    public interface FeatBase
    {
        // Every feat MUST implement this true = success, false = cannot perform
        public abstract bool Perform(Unit parent, Unit target);
    }
}