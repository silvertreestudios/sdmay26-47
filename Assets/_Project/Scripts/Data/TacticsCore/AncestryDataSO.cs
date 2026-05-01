using System.Collections.Generic;
using UnityEngine;

namespace TacticsGame.Data.TacticsCore
{
    [CreateAssetMenu(menuName = "Tactics Core/Data/Ancestry")]
    public class AncestryDataSO : TacticsGameElementSO
    {
        [Header("Base Statistics")]
        public int HitPoints;
        public int Speed;
        public int Reach;
        public CreatureSize Size;

        [Header("Senses")]
        public SenseType Sense;
        public string CustomSense;

        [Header("Attributes")]
        public List<AttributeChoiceSet> AttributeBoosts = new List<AttributeChoiceSet>();
        public List<AttributeChoiceSet> AttributeFlaws = new List<AttributeChoiceSet>();

        [Header("Languages")]
        public List<LanguageEntry> StartingLanguages = new List<LanguageEntry>();
        public List<LanguageEntry> AdditionalLanguageOptions = new List<LanguageEntry>();
        public int AdditionalLanguageCount;

        [Header("Grants")]
        public List<FeatureGrant> GrantedFeatures = new List<FeatureGrant>();
        public List<HeritageDataSO> Heritages = new List<HeritageDataSO>();
    }
}
