using PathfinderTactics.Characters;
using PathfinderTactics.Core;
using TMPro;
using UnityEngine;

namespace PathfinderTactics.UI
{
    public class UnitTooltipUI : MonoBehaviour
    {
        [Header("References")]
        [SerializeField]
        private GameObject container;

        [SerializeField]
        private TextMeshProUGUI unitNameText;

        [SerializeField]
        private TextMeshProUGUI healthText;

        private Unit currentUnit;
        private IDamageable currentHealth;

        private void Awake()
        {
            ServiceLocator.Register(this);
            Hide();
        }

        private void OnDestroy()
        {
            ServiceLocator.Unregister<UnitTooltipUI>();
        }

        private void Update()
        {
            // Real-time updates while visible (instantly shows damage numbers dropping)
            if (container.activeSelf && currentUnit != null && currentHealth != null)
            {
                healthText.text =
                    $"HP: {currentHealth.GetCurrentHealth()} / {currentHealth.GetMaxHealth()}";
            }
        }

        public void Show(Unit unit)
        {
            currentUnit = unit;
            currentHealth = unit.GetComponent<IDamageable>();

            unitNameText.text = unit.gameObject.name;

            container.SetActive(true);
        }

        public void Hide()
        {
            currentUnit = null;
            currentHealth = null;
            container.SetActive(false);
        }
    }
}
