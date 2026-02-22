using PathfinderTactics.Actions;
using PathfinderTactics.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PathfinderTactics.UI
{
    public class ActionButtonUI : MonoBehaviour
    {
        [SerializeField]
        private TextMeshProUGUI textMeshPro;

        [SerializeField]
        private Button button;

        private BaseAction baseAction;

        public void SetBaseAction(BaseAction baseAction)
        {
            this.baseAction = baseAction;
            textMeshPro.text = baseAction.GetActionName().ToUpper();

            button.onClick.AddListener(() =>
            {
                // The player wants to use this action. Let them pick a target.
                UnitActionSystem.Instance.SetSelectedAction(baseAction);
            });
        }
    }
}
