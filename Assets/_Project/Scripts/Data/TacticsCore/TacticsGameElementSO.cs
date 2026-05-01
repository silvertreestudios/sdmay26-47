using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace TacticsGame.Data.TacticsCore
{
    public abstract class TacticsGameElementSO : ScriptableObject
    {
        [Header("Identity")]
        public string SourceId;
        public string Slug;
        public string DisplayName;
        public string IconPath;

        [Header("Text")]
        [TextArea(5, 10)]
        public string Description;
        public string SourceTitle;

        [Header("Metadata")]
        public string SourcePack;
        public string Rarity;
        public string SourceJsonHash;
        public List<string> Traits = new List<string>();
        public List<string> TraitsLower = new List<string>();

        [Header("Rules")]
        public List<RawRuleEntry> RawRules = new List<RawRuleEntry>();

        public void BuildDerivedFields()
        {
            TraitsLower = Traits
                .Where(trait => !string.IsNullOrWhiteSpace(trait))
                .Select(trait => trait.ToLowerInvariant())
                .Distinct()
                .ToList();
        }
    }
}
