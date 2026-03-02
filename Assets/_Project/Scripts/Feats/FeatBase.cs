using UnityEngine;
using PathfinderTactics.Characters;

//All feats should be able to extend from this class, can modify it as needed, and include methods that do nothing for some feats in neccesarry
namespace PathfinderTactics.Feats
{
    public abstract class FeatBase
    {
        // Every feat MUST implement this true = success, false = cannot perform
        public abstract bool Perform(Unit parent, Unit target);
    }
}