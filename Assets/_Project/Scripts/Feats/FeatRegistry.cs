using System;
using System.Collections.Generic;
//Any Feats we add put them in a list here, then if you want to use them make a script with their name implementing it, and there is probaly a JSON file that will help in the packs. Add the feats to the feat loadout you want to use with it. 
//This system could be changed but I tried to make it modular and expandable
namespace PathfinderTactics.Feats
{
    public static class FeatRegistry
    {
        private static Dictionary<string, Func<FeatBase>> registry =
            new Dictionary<string, Func<FeatBase>>()
        {
            { "exacting-strike", () => new exacting_strike() }
            // Add more feats here
        };

        public static FeatBase CreateFeat(string featName)
        {
            if (registry.TryGetValue(featName, out var constructor))
                return constructor();

            return null;
        }
    }
}
