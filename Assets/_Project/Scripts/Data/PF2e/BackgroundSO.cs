using System.Collections.Generic;
using UnityEngine;

namespace PathfinderTactics.Data.PF2e
{
    [CreateAssetMenu(menuName = "PF2e/Background")]
    public class BackgroundSO : GameElementSO
    {
        [Header("Ability Boosts")]
        public List<AbilityBoostEntry> Boosts = new List<AbilityBoostEntry>();

        [Header("Trained Skills")]
        public List<string> TrainedSkills = new List<string>();
        public List<string> LoreSkills = new List<string>();

        [Header("Granted Feats")]
        public List<string> GrantedFeatIds = new List<string>();
    }
}
