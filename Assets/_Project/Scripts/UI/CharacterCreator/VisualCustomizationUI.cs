using TacticsGame.Characters.Visuals;
using UnityEngine;

namespace TacticsGame.UI.CharacterCreator
{
    public class VisualCustomizationUI : MonoBehaviour
    {
        [SerializeField]
        private CharacterCreatorUI mainUI;

        [SerializeField]
        private VisualPartManager previewManager;

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
            gameObject.SetActive(state == CreatorState.Visuals);
        }

        // Called by UI Button to select a mesh part
        public void SelectVisualPart(VisualPartSO part)
        {
            mainUI.UpdatePayload(payload =>
            {
                payload.VisualPartIDs[part.Slot.ToString()] = part.PartID;
            });

            // Update preview immediately
            if (previewManager != null)
            {
                previewManager.EquipPart(part);
            }
        }

        // Called by UI Color Picker
        public void SetArmorColor(Color color)
        {
            mainUI.UpdatePayload(payload =>
            {
                payload.ArmorColor = color;
            });

            if (previewManager != null)
            {
                previewManager.SetArmorColor(color);
            }
        }
    }
}
