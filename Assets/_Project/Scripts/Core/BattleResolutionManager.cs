using System.Collections;
using System.Linq;
using TacticsGame.Characters;
using TacticsGame.Combat;
using TacticsGame.UI;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TacticsGame.Core
{
    /// <summary>
    /// Monitors the state of the battle and transitions to Victory or Defeat scenes.
    /// Place this in any scene where a tactical battle takes place.
    /// </summary>
    public class BattleResolutionManager : MonoBehaviour
    {
        [Header("Scene Configuration")]
        [Tooltip("The scene to load when all enemies are defeated.")]
        [SerializeField]
        private string victorySceneName = "VictoryScene";

        [Tooltip("The scene to load when all player units are defeated.")]
        [SerializeField]
        private string defeatSceneName = "DefeatScene";

        [Tooltip("The scene that the Victory scene should continue to.")]
        [SerializeField]
        private string nextSceneName = "NextScene";

        [Header("Ruleset Overrides")]
        [Tooltip(
            "Units in this list will be ignored when calculating Victory/Defeat. If all other units die, the game resolves regardless of these units' health."
        )]
        [SerializeField]
        private System.Collections.Generic.List<Unit> ignoredUnits =
            new System.Collections.Generic.List<Unit>();

        [Header("Delay")]
        [Tooltip("Minimum time to wait after the last unit falls before transitioning.")]
        [SerializeField]
        private float resolutionDelay = 3.0f;

        private bool isResolving = false;
        private string currentBattleScene;

        private void Start()
        {
            currentBattleScene = SceneManager.GetActiveScene().name;

            // Store the current scene as the 'Last Battle Scene' so Retry works
            if (GlobalGameState.Instance != null)
            {
                GlobalGameState.Instance.SetLastBattleScene(currentBattleScene);
                GlobalGameState.Instance.SetNextStoryScene(nextSceneName);
            }
        }

        private void OnEnable()
        {
            GameEvents.OnUnitHealthChanged += HandleHealthChanged;
        }

        private void OnDisable()
        {
            GameEvents.OnUnitHealthChanged -= HandleHealthChanged;
        }

        private void HandleHealthChanged(Unit unit, int current, int max)
        {
            if (isResolving)
                return;

            // Trigger checks whenever a unit reaches 0 HP
            if (current <= 0)
            {
                StartCoroutine(CheckResolutionCoroutine());
            }
        }

        private IEnumerator CheckResolutionCoroutine()
        {
            // Wait for any immediate action processing to start
            yield return new WaitForSeconds(0.5f);

            // Wait until the action system is no longer 'Busy' (processing animations/VFX)
            if (ServiceLocator.TryGet<PhaseManager>(out var phaseManager))
            {
                while (phaseManager.CurrentPhase == GamePhase.Busy)
                {
                    yield return new WaitForSeconds(0.2f);
                }
            }

            // Additional delay to let the death sink in
            yield return new WaitForSeconds(resolutionDelay);

            if (isResolving)
                yield break;

            // Check conditions
            var allUnits = UnitManager.AllUnits.Where(u => u != null).ToList();

            // Safety: Ensure units have actually been registered before concluding a win/loss
            bool hasPlayers = allUnits.Any(u => u.GetFaction() == Faction.Player);
            bool hasEnemies = allUnits.Any(u => u.GetFaction() == Faction.Enemy);

            if (!hasPlayers || !hasEnemies)
            {
                // Wait for units to spawn if they haven't yet
                yield break;
            }

            // Filter by alive units (HP > 0) and ignore units in the list
            bool anyPlayerAlive = allUnits.Any(u =>
                u.GetFaction() == Faction.Player
                && !ignoredUnits.Contains(u)
                && u.GetComponent<UnitHealth>().GetCurrentHealth() > 0
            );
            bool anyEnemyAlive = allUnits.Any(u =>
                u.GetFaction() == Faction.Enemy
                && !ignoredUnits.Contains(u)
                && u.GetComponent<UnitHealth>().GetCurrentHealth() > 0
            );

            // Defeat has priority (player loses if both die)
            if (!anyPlayerAlive)
            {
                TriggerDefeat();
            }
            else if (!anyEnemyAlive)
            {
                TriggerVictory();
            }
        }

        private void TriggerVictory()
        {
            if (isResolving)
                return;
            isResolving = true;
            Debug.Log(
                "<color=green>[BattleResolution] VICTORY detected. Transitioning to story...</color>"
            );
            LoadingManager.Instance.LoadScene(victorySceneName);
        }

        private void TriggerDefeat()
        {
            if (isResolving)
                return;
            isResolving = true;
            Debug.Log(
                "<color=red>[BattleResolution] DEFEAT detected. Transitioning to loss screen...</color>"
            );
            LoadingManager.Instance.LoadScene(defeatSceneName);
        }
    }
}
