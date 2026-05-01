using System.Collections.Generic;
using UnityEngine;

namespace TacticsGame.Data.TacticsRuleset
{
    [CreateAssetMenu(menuName = "TacticsRuleset/Background")]
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
