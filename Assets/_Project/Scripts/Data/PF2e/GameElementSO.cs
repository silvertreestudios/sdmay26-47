using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace PathfinderTactics.Data.PF2e
{
    public abstract class GameElementSO : ScriptableObject
    {
        [Header("Identity")]
        public string Id; // Mapped from Foundry _id
        public string Slug; // Mapped from Foundry slug
        public string ElementName;
        public Sprite Icon;

        [Header("UI & Tooltips")]
        [TextArea(5, 10)]
        public string Description;
        public string SourceBook;

        [Header("Tags & Metadata")]
        public List<string> Traits = new List<string>();

        [HideInInspector]
        public List<string> TraitsLower = new List<string>();
        public string CompendiumSource; // e.g. "pf2e.spells", "pf2e.classes"

        [Header("Cross-References")]
        // E.g. A Class grants Feats, or an Item grants a Spell
        public List<string> GrantedItemIds = new List<string>();

        [Header("Effect Hooks")]
        // If this item/spell natively applies Conditions like Frightened
        public List<string> AppliedEffectIds = new List<string>();

        /// <summary>
        /// Call after importing to build cached/derived fields like TraitsLower.
        /// </summary>
        public void BuildDerivedFields()
        {
            TraitsLower = Traits.Select(t => t.ToLower()).ToList();
        }
    }
}
