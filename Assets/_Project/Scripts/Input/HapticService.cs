using System.Collections;
using PathfinderTactics.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace PathfinderTactics.InputSystem
{
    /// <summary>
    /// Service for triggering controller haptic feedback.
    /// </summary>
    public class HapticService : MonoBehaviour
    {
        private Coroutine rumbleCoroutine;

        private void Awake()
        {
            ServiceLocator.Register(this);
        }

        private void OnDestroy()
        {
            ServiceLocator.Unregister<HapticService>();
            StopAllCoroutines();
            ResetRumble();
        }

        /// <summary>
        /// Triggers a vibration on the current gamepad.
        /// </summary>
        /// <param name="lowFreq">Low-frequency motor speed (0-1).</param>
        /// <param name="highFreq">High-frequency motor speed (0-1).</param>
        /// <param name="duration">Duration in seconds.</param>
        public void TriggerRumble(float lowFreq, float highFreq, float duration)
        {
            if (rumbleCoroutine != null)
                StopCoroutine(rumbleCoroutine);

            rumbleCoroutine = StartCoroutine(RumbleRoutine(lowFreq, highFreq, duration));
        }

        private IEnumerator RumbleRoutine(float lowFreq, float highFreq, float duration)
        {
            if (Gamepad.all.Count == 0)
            {
                Debug.LogWarning("[HapticService] No Gamepads found in Gamepad.all!");
                yield break;
            }

            foreach (var gamepad in Gamepad.all)
            {
                try
                {
                    gamepad.SetMotorSpeeds(lowFreq, highFreq);
                }
                catch (System.Exception) { }
            }

            yield return new WaitForSeconds(duration);

            foreach (var gamepad in Gamepad.all)
            {
                try
                {
                    gamepad.SetMotorSpeeds(0, 0);
                }
                catch { }
            }
        }

        private void ResetRumble()
        {
            Gamepad.current?.SetMotorSpeeds(0, 0);
        }

        private void OnApplicationQuit()
        {
            ResetRumble();
        }
    }
}
