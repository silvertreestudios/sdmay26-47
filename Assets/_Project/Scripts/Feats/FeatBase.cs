using TacticsGame.Characters;
using UnityEngine;

//All feats should implment this interface, can modify it as needed, and include methods that do nothing for some feats in neccesarry
namespace TacticsGame.Feats
{
    public interface FeatBase
    {
        // Every feat MUST implement this true = success, false = cannot perform
        public abstract bool Perform(Unit parent, Unit target);
    }
}
