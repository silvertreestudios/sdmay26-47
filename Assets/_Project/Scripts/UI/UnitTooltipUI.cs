using PathfinderTactics.Characters;
using TMPro;
using UnityEngine;

namespace PathfinderTactics.UI
{
    public class UnitTooltipUI : MonoBehaviour
    {
        public static UnitTooltipUI Instance { get; private set; }

        [Header("References")]
        [SerializeField]
        private GameObject container;

        [SerializeField]
        private TextMeshProUGUI unitNameText;

        [SerializeField]
        private TextMeshProUGUI healthText;

        private Unit currentUnit;
        private UnitHealth currentHealth;

        private void Awake()
        {
            if (Instance != null)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            Hide();
        }

        private void Update()
        {
            //// Real-time updates while visible (instantly shows damage numbers dropping)
            //if (container.activeSelf && currentUnit != null && currentHealth != null)
            //{
            //    healthText.text =
            //        $"HP: {currentHealth.GetCurrentHealth()} / {currentHealth.GetMaxHealth()}";
            //}
        }

        public void Show(Unit unit)
        {
            currentUnit = unit;
            currentHealth = unit.GetComponent<UnitHealth>();

            //unitNameText.text = unit.gameObject.name;

            //container.SetActive(true);
        }

        public void Hide()
        {
            currentUnit = null;
            currentHealth = null;
            //container.SetActive(false);
        }
    }
}
