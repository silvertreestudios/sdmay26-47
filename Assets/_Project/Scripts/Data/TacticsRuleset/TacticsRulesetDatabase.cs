using System.Collections.Generic;
using System.Linq;
using TacticsGame.Data.TacticsCore;
using TacticsGame.Items;
using UnityEngine;

namespace TacticsGame.Data.TacticsRuleset
{
    [CreateAssetMenu(menuName = "TacticsRuleset/Database")]
    public class TacticsRulesetDatabase : ScriptableObject
    {
        [Header("Runtime Compendium")]
        [Tooltip("Contains all imported tactical ruleset spells and rituals.")]
        public List<SpellSO> AllSpells = new List<SpellSO>();

        [Tooltip("Contains all imported tactical ruleset feats (Class, Ancestry, General, Skill).")]
        public List<FeatSO> AllFeats = new List<FeatSO>();

        [Tooltip(
            "Contains generic items like equipment, basic actions, conditions, and ancestries."
        )]
        public List<GameElementSO> AllItems = new List<GameElementSO>();

        [Header("Character Creation")]
        [Tooltip("Imported generic ancestry data used by the character creator.")]
        public List<AncestryDataSO> AllAncestries = new List<AncestryDataSO>();

        [Tooltip("Imported generic heritage data used by the character creator.")]
        public List<HeritageDataSO> AllHeritages = new List<HeritageDataSO>();

        [Tooltip("Imported generic background data used by the character creator.")]
        public List<BackgroundDataSO> AllBackgrounds = new List<BackgroundDataSO>();

        [Tooltip("Imported generic class data used by the character creator.")]
        public List<TacticsClassSO> AllClasses = new List<TacticsClassSO>();

        [Tooltip("Imported generic feature data used by the character creator.")]
        public List<FeatureDataSO> AllFeatures = new List<FeatureDataSO>();

        [Header("Runtime Equipment")]
        public List<WeaponSO> AllWeapons = new List<WeaponSO>();
        public List<ArmorSO> AllArmor = new List<ArmorSO>();
        public List<ShieldSO> AllShields = new List<ShieldSO>();

        [Header("Visuals (Character Creator)")]
        [Tooltip("All 3D visual parts available for character creation.")]
        public List<TacticsGame.Characters.Visuals.VisualPartSO> AllVisualParts =
            new List<TacticsGame.Characters.Visuals.VisualPartSO>();

        // Cache for fast UI Toolkit retrieval
        private Dictionary<
            TacticsGame.Characters.Visuals.VisualSlot,
            List<TacticsGame.Characters.Visuals.VisualPartSO>
        > _visualsCache;

        public List<TacticsGame.Characters.Visuals.VisualPartSO> GetVisualsForSlot(
            TacticsGame.Characters.Visuals.VisualSlot slot
        )
        {
            if (_visualsCache == null)
            {
                _visualsCache =
                    new Dictionary<
                        TacticsGame.Characters.Visuals.VisualSlot,
                        List<TacticsGame.Characters.Visuals.VisualPartSO>
                    >();
                foreach (var part in AllVisualParts)
                {
                    if (part == null)
                        continue;
                    if (!_visualsCache.ContainsKey(part.Slot))
                        _visualsCache[part.Slot] =
                            new List<TacticsGame.Characters.Visuals.VisualPartSO>();

                    _visualsCache[part.Slot].Add(part);
                }
            }

            if (_visualsCache.TryGetValue(slot, out var list))
                return list;

            return new List<TacticsGame.Characters.Visuals.VisualPartSO>();
        }

        public AncestryDataSO GetCoreAncestry(string sourceId) =>
            FindCoreById(AllAncestries, sourceId);

        public HeritageDataSO GetCoreHeritage(string sourceId) =>
            FindCoreById(AllHeritages, sourceId);

        public BackgroundDataSO GetCoreBackground(string sourceId) =>
            FindCoreById(AllBackgrounds, sourceId);

        public TacticsClassSO GetCoreClass(string sourceId) => FindCoreById(AllClasses, sourceId);

        public FeatureDataSO GetCoreFeature(string sourceId) => FindCoreById(AllFeatures, sourceId);

        public List<HeritageDataSO> GetCoreHeritagesForAncestry(string ancestrySourceId)
        {
            if (string.IsNullOrEmpty(ancestrySourceId))
                return new List<HeritageDataSO>();

            return AllHeritages
                .Where(heritage =>
                    heritage != null
                    && (
                        heritage.ParentAncestryAsset?.SourceId == ancestrySourceId
                        || heritage.ParentAncestry.SourceId == ancestrySourceId
                    )
                )
                .OrderBy(heritage => heritage.DisplayName)
                .ToList();
        }

        private static T FindCoreById<T>(IEnumerable<T> entries, string sourceId)
            where T : TacticsGameElementSO
        {
            if (string.IsNullOrEmpty(sourceId))
                return null;

            return entries?.FirstOrDefault(entry =>
                entry != null
                && (entry.SourceId == sourceId || entry.Slug == sourceId || entry.name == sourceId)
            );
        }
    }
}
