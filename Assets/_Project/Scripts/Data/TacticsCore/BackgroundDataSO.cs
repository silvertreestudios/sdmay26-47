using System.Collections.Generic;
using UnityEngine;

namespace TacticsGame.Data.TacticsCore
{
    [CreateAssetMenu(menuName = "Tactics Core/Data/Background")]
    public class BackgroundDataSO : TacticsGameElementSO
    {
        [Header("Attributes")]
        public List<AttributeChoiceSet> AttributeBoosts = new List<AttributeChoiceSet>();

        [Header("Training")]
        public List<SkillTrainingEntry> TrainedSkills = new List<SkillTrainingEntry>();
        public List<string> LoreSkills = new List<string>();

        [Header("Grants")]
        public List<FeatureGrant> GrantedFeats = new List<FeatureGrant>();
    }
}
