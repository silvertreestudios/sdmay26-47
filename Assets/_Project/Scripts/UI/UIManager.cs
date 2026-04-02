using PathfinderTactics.Characters;
using PathfinderTactics.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PathfinderTactics.UI
{
    public class UIManager : MonoBehaviour
    {
        [Header("References")]
        [SerializeField]
        private TextMeshProUGUI apText;

        [SerializeField]
        private Button endTurnButton;

        [SerializeField]
        private GameObject actionPanel;

        [SerializeField]
        private Transform images;
        [SerializeField]
        private Transform descriptions;

        [SerializeField]
        private GameObject PlayerIcon;
        [SerializeField]
        private GameObject EnemyIcon;
        [SerializeField]
        private GameObject PlayerDesc;
        [SerializeField]
        private Transform TurnOrderPanel;


        private void Awake()
        {
            TurnManager.Instance.OnCombatStarted += CombatStarted;
        }

        private void Start()
        {
            // Subscribe to game events
            UnitActionSystem.Instance.OnSelectedUnitChanged +=
                UnitActionSystem_OnSelectedUnitChanged;

            UnitActionSystem.Instance.OnActionCompleted += onActionCompleted;
            TurnManager.Instance.OnTurnChanged += TurnChanged;
        // Set up button listener
        endTurnButton.onClick.AddListener(() =>
            {
                // When clicked, tell the action system to end the current unit's turn
                UnitActionSystem.Instance.EndTurn();
            });

            UpdateStatus();
            UpdateActionUI(); // Initial update
            AddParty();
        }

        private void TurnChanged(object sender, System.EventArgs e)
        {
            TurnOrderPanel.GetChild(0).SetAsLastSibling();
        }

        private void CombatStarted(object sender, TurnManager.OnTurnOrderedEventArgs e)
        {

            foreach (Unit unit in e.turnOrder)
            {
                GameObject imageIcon;
                if (unit.GetFaction() == Faction.Player)
                {
                    imageIcon = Instantiate(PlayerIcon, TurnOrderPanel);
                }
                else
                {
                    imageIcon = Instantiate(EnemyIcon, TurnOrderPanel);
                }
                imageIcon.name = $"{unit.gameObject.name}_icon";
            }
        }

        private void OnDestroy()
        {
            if (UnitActionSystem.Instance != null)
            {
                UnitActionSystem.Instance.OnSelectedUnitChanged -=
                    UnitActionSystem_OnSelectedUnitChanged;
            }
        }

        private void UnitActionSystem_OnSelectedUnitChanged(object sender, System.EventArgs e)
        {
            UpdateActionUI();
        }

        private void onActionCompleted(object sender, System.EventArgs e)
        {
            UpdateStatus();
        }

        private void UpdateActionUI()
        {
            Unit selectedUnit = UnitActionSystem.Instance.SelectedUnit;

            if (selectedUnit != null)
            {
                // A unit is selected, show the UI
                actionPanel.SetActive(true);
                apText.text = $"Action Points: {selectedUnit.GetActionPointsRemaining()}";
            }
            //else
            //{
            //    // No unit selected, hide the UI
            //    actionPanel.SetActive(false);
            //}
        }

        // TODO: Remove this as its kinda not needed and not scalable and replaced.
        // useful for quickly looking at health for now ig.
        private void UpdateStatus()
        {
            // Ensure units exist
            if (UnitManager.AllUnits.Count <= 0)
                return;
            /**
                        //// Player 1 (Index 0)
                        //if (UnitManager.AllUnits.Count > 0)
                        //{
                        //    Unit unit1 = UnitManager.AllUnits[0];
                        //    UnitHealth health1 = unit1.GetComponent<UnitHealth>();
                        //    int p1Health = (health1 != null) ? health1.GetCurrentHealth() : 0;

                        //    var p1Text = statusPanel.transform.Find("Player_one_health");
                        //    if (p1Text != null)
                        //        p1Text.GetComponent<TextMeshProUGUI>().text =
                        //            $"Player one health \n {p1Health}";
                        //}

                        //// Player 2 (Index 1) - Only if we have at least 2 units
                        //if (UnitManager.AllUnits.Count > 1)
                        //{
                        //    Unit unit2 = UnitManager.AllUnits[1];
                        //    UnitHealth health2 = unit2.GetComponent<UnitHealth>();
                        //    int p2Health = (health2 != null) ? health2.GetCurrentHealth() : 0;

                        //    var p2Text = statusPanel.transform.Find("Player_two_health");
                        //    if (p2Text != null)
                        //        p2Text.GetComponent<TextMeshProUGUI>().text =
                        //            $"Player two health \n {p2Health}";
                        //}
            **/
            foreach (Unit unit in UnitManager.AllUnits)
            {
                if (unit.GetFaction() == Faction.Player)
                {
                    var unitName = unit.gameObject.name;
                    GameObject textIcon = GameObject.Find($"{unitName}_desc");
                    if (textIcon == null) {continue; }
                    var health = unit.gameObject.GetComponent<UnitHealth>().GetCurrentHealth();
                    var maxHealth = unit.gameObject.GetComponent<UnitHealth>().GetMaxHealth();
                    textIcon.GetComponent<TextMeshProUGUI>().text = $"{unitName} \n" + $" HP: {health} / " + $"{maxHealth}";
                }
            }

        }

    private void AddParty()
        {

            foreach (Unit unit in UnitManager.AllUnits)
            {
                if (unit.GetFaction() == Faction.Player)
                {
                    var unitName = unit.gameObject.name;

                    GameObject imageIcon = Instantiate(PlayerIcon, images);
                    imageIcon.name = $"{unitName}_icon";

                    GameObject textIcon = Instantiate(PlayerDesc, descriptions);

                    var health = unit.gameObject.GetComponent<UnitHealth>().GetCurrentHealth();
                    var maxHealth = unit.gameObject.GetComponent<UnitHealth>().GetMaxHealth();

                    textIcon.name = $"{unit.gameObject.name}_desc";
                    textIcon.GetComponent<TextMeshProUGUI>().text  = $"{unitName} \n" + $" HP: {health} / " + $"{maxHealth}";
                }
            }
        }

    }
}