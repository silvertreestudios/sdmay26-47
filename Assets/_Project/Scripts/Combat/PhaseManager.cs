using System;
using TacticsGame.Core;
using UnityEngine;

namespace TacticsGame.Combat
{
    public class PhaseManager : MonoBehaviour
    {
        public event EventHandler<GamePhase> OnPhaseChanged;

        private GamePhase currentPhase;
        public GamePhase CurrentPhase => currentPhase;

        private void Awake()
        {
            ServiceLocator.Register(this);
            SetPhase(GamePhase.UnitSelection);
        }

        private void OnDestroy()
        {
            ServiceLocator.Unregister<PhaseManager>();
        }

        public void SetPhase(GamePhase newPhase)
        {
            if (currentPhase == newPhase)
                return;

            // Debug.Log($"[STATE MACHINE] Phase changing from {currentPhase} to {newPhase}");
            currentPhase = newPhase;

            OnPhaseChanged?.Invoke(this, currentPhase);
        }
    }
}
