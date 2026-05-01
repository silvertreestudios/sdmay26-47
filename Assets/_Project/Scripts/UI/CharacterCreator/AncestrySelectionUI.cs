using TacticsGame.Data.TacticsRuleset;
using UnityEngine;

namespace TacticsGame.UI.CharacterCreator
{
    public class AncestrySelectionUI : MonoBehaviour
    {
        [SerializeField]
        private CharacterCreatorUI mainUI;

        private void Start()
        {
            if (mainUI != null)
            {
                mainUI.OnStateChanged += HandleStateChanged;
            }
        }

        private void OnDestroy()
        {
            if (mainUI != null)
            {
                mainUI.OnStateChanged -= HandleStateChanged;
            }
        }

        private void HandleStateChanged(CreatorState state)
        {
            gameObject.SetActive(state == CreatorState.Ancestry);
            if (state == CreatorState.Ancestry)
            {
                PopulateAncestries();
            }
        }

        private void PopulateAncestries()
        {
            // TODO: Legacy GameObject creator path. The active UITK creator reads imported
            // AncestryDataSO entries from TacticsRulesetDatabase.AllAncestries.
        }

        // Called by UI Button
        public void SelectAncestry(AncestrySO ancestry)
        {
            mainUI.UpdatePayload(payload =>
            {
                payload.AncestryID = ancestry.Id;
                // Update base stats from ancestry (Now derived by TacticsRulesetRuleCalculator via AncestryID)
                // payload.TotalHP = ancestry.HP;
                // payload.BaseSpeedInFeet = ancestry.Speed;
                // payload.Size = (TacticsGame.Characters.UnitSize)System.Enum.Parse(typeof(TacticsGame.Characters.UnitSize), ancestry.Size.ToString());
            });
        }
    }
}
