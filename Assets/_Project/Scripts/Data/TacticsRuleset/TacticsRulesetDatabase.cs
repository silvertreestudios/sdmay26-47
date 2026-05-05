using System;
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

        public TacticsGame.Characters.Visuals.VisualPartSO GetVisualPartById(string id)
        {
            if (string.IsNullOrEmpty(id))
                return null;

            return AllVisualParts.FirstOrDefault(p =>
                p != null && (p.PartID == id || p.name == id)
            );
        }

        public WeaponSO GetWeaponById(string id)
        {
            if (string.IsNullOrEmpty(id))
                return null;
            return AllWeapons.FirstOrDefault(w => w != null && (w.itemName == id || w.name == id));
        }

        public ArmorSO GetArmorById(string id)
        {
            if (string.IsNullOrEmpty(id))
                return null;
            return AllArmor.FirstOrDefault(a => a != null && (a.itemName == id || a.name == id));
        }

        public ShieldSO GetShieldById(string id)
        {
            if (string.IsNullOrEmpty(id))
                return null;
            return AllShields.FirstOrDefault(s => s != null && (s.itemName == id || s.name == id));
        }

        public SpellSO GetSpellById(string id)
        {
            if (string.IsNullOrEmpty(id))
                return null;
            return AllSpells.FirstOrDefault(s =>
                s != null && (s.Id == id || s.Slug == id || s.name == id)
            );
        }

        public AncestryDataSO GetCoreAncestry(string sourceId) =>
            FindCoreById(AllAncestries, sourceId);

        public HeritageDataSO GetCoreHeritage(string sourceId) =>
            FindCoreById(AllHeritages, sourceId);

        public BackgroundDataSO GetCoreBackground(string sourceId) =>
            FindCoreById(AllBackgrounds, sourceId);

        public TacticsClassSO GetCoreClass(string sourceId) => FindCoreById(AllClasses, sourceId);

        public FeatureDataSO GetCoreFeature(string sourceId) => FindCoreById(AllFeatures, sourceId);

        public List<HeritageDataSO> GetCoreHeritagesForAncestry(AncestryDataSO ancestry)
        {
            if (ancestry == null)
            {
                Debug.Log("[Heritages] GetCoreHeritagesForAncestry called with null ancestry.");
                return new List<HeritageDataSO>();
            }

            Debug.Log(
                $"[Heritages] Fetching heritages for ancestry: {ancestry.name} (SourceId: {ancestry.SourceId}, Slug: {ancestry.Slug}). Total Heritages in DB: {AllHeritages.Count}"
            );

            var matched = new List<HeritageDataSO>();
            foreach (var heritage in AllHeritages)
            {
                if (heritage == null)
                    continue;

                bool isMatch = false;
                string matchReason = string.Empty;

                if (heritage.ParentAncestryAsset == ancestry)
                {
                    isMatch = true;
                    matchReason = "Direct Object Reference";
                }
                else if (heritage.ParentAncestryAsset != null)
                {
                    if (
                        !string.IsNullOrEmpty(ancestry.SourceId)
                        && string.Equals(
                            heritage.ParentAncestryAsset.SourceId,
                            ancestry.SourceId,
                            StringComparison.OrdinalIgnoreCase
                        )
                    )
                    {
                        isMatch = true;
                        matchReason = "Asset SourceId Match";
                    }
                    else if (
                        !string.IsNullOrEmpty(ancestry.Slug)
                        && string.Equals(
                            heritage.ParentAncestryAsset.Slug,
                            ancestry.Slug,
                            StringComparison.OrdinalIgnoreCase
                        )
                    )
                    {
                        isMatch = true;
                        matchReason = "Asset Slug Match";
                    }
                    else if (
                        string.Equals(
                            heritage.ParentAncestryAsset.name,
                            ancestry.name,
                            StringComparison.OrdinalIgnoreCase
                        )
                    )
                    {
                        isMatch = true;
                        matchReason = "Asset Name Match";
                    }
                }

                if (!isMatch && heritage.ParentAncestry != null)
                {
                    if (
                        !string.IsNullOrEmpty(ancestry.SourceId)
                        && string.Equals(
                            heritage.ParentAncestry.SourceId,
                            ancestry.SourceId,
                            StringComparison.OrdinalIgnoreCase
                        )
                    )
                    {
                        isMatch = true;
                        matchReason = "PackRef SourceId Match";
                    }
                    else if (
                        !string.IsNullOrEmpty(ancestry.SourceId)
                        && !string.IsNullOrEmpty(heritage.ParentAncestry.SourceUuid)
                        && heritage.ParentAncestry.SourceUuid.EndsWith(
                            ancestry.SourceId,
                            StringComparison.OrdinalIgnoreCase
                        )
                    )
                    {
                        isMatch = true;
                        matchReason = "PackRef SourceUuid EndsWith Match";
                    }
                    else if (
                        !string.IsNullOrEmpty(ancestry.Slug)
                        && string.Equals(
                            heritage.ParentAncestry.Slug,
                            ancestry.Slug,
                            StringComparison.OrdinalIgnoreCase
                        )
                    )
                    {
                        isMatch = true;
                        matchReason = "PackRef Slug Match";
                    }
                    else if (
                        !string.IsNullOrEmpty(ancestry.DisplayName)
                        && string.Equals(
                            heritage.ParentAncestry.DisplayName,
                            ancestry.DisplayName,
                            StringComparison.OrdinalIgnoreCase
                        )
                    )
                    {
                        isMatch = true;
                        matchReason = "PackRef DisplayName Match";
                    }
                }

                if (isMatch)
                {
                    Debug.Log($"[Heritages] {heritage.DisplayName} MATCHED via {matchReason}");
                    matched.Add(heritage);
                }
            }

            matched = matched.OrderBy(h => h.DisplayName).ToList();

            Debug.Log($"[Heritages] Found {matched.Count} heritages for {ancestry.name}.");
            return matched;
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
