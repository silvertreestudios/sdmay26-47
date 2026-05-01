using UnityEngine;

namespace TacticsGame.UI.CharacterCreator
{
    public class EquipmentSelectionUI : MonoBehaviour
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
            gameObject.SetActive(state == CreatorState.Equipment);
            if (state == CreatorState.Equipment)
            {
                PopulateLoadouts();
            }
        }

        private void PopulateLoadouts()
        {
            // TODO: Legacy GameObject creator path. The active UITK creator reads equipment
            // directly from TacticsRulesetDatabase.AllWeapons/AllArmor/AllShields.
        }
    }
}
