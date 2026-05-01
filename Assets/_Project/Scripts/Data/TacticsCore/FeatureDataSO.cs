using System.Collections.Generic;
using UnityEngine;

namespace TacticsGame.Data.TacticsCore
{
    [CreateAssetMenu(menuName = "Tactics Core/Data/Feature")]
    public class FeatureDataSO : TacticsGameElementSO
    {
        [Header("Feature")]
        public FeatureCategory Category;
        public int Level;
        public List<FeatureGrant> GrantedFeatures = new List<FeatureGrant>();
        public List<ProficiencyUpgrade> ProficiencyUpgrades = new List<ProficiencyUpgrade>();
        public int FocusPointDelta;
    }
}
