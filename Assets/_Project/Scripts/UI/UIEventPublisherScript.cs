using UnityEngine;
using System;

namespace PathfinderTactics.UI
{
    /// <summary>
    /// Publishes UI button press events for UI actions.
    /// </summary>

    public class UIEventPublisherScript : MonoBehaviour
    {

        public static UIEventPublisherScript Instance { get; private set; }


        public event EventHandler OnStrideButtonPressed;
        public event EventHandler OnAttackButtonPressed;
        public event EventHandler OnPassButtonPressed;

        private void Awake()
        {
            Instance = this;
        }

        public void StrideButtonPressed()
        {
            Debug.Log("Stride Pressed!");
            OnStrideButtonPressed?.Invoke(this, EventArgs.Empty);
        }

        public void AttackButtonPressed()
        {
            Debug.Log("Attack Pressed!");
            OnAttackButtonPressed?.Invoke(this, EventArgs.Empty);
        }

        public void PassButtonPressed()
        {
            Debug.Log("Pass Pressed!");
            OnPassButtonPressed?.Invoke(this, EventArgs.Empty);
        }


    }

}
