using System.Collections.Generic;
using UnityEngine;

namespace TacticsGame.Data.TacticsCore
{
    [CreateAssetMenu(menuName = "Tactics Core/Data/Heritage")]
    public class HeritageDataSO : TacticsGameElementSO
    {
        [Header("Parent")]
        public PackReference ParentAncestry = new PackReference();
        public AncestryDataSO ParentAncestryAsset;

        [Header("Grants")]
        public List<FeatureGrant> GrantedFeatures = new List<FeatureGrant>();
    }
}
