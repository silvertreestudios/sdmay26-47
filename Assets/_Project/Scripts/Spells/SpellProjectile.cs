using System;
using System.Collections;
using UnityEngine;

namespace PathfinderTactics.Spells
{
    /// <summary>
    /// Handles the visual travel of a spell projectile from caster to target.
    /// Uses a vanilla Unity Coroutine for movement interpolation.
    /// </summary>
    public class SpellProjectile : MonoBehaviour
    {
        private Action onHitCallback;

        /// <summary>
        /// Launches the projectile towards the target.
        /// </summary>
        public void Launch(Vector3 startPos, Vector3 targetPos, float speed, Action onHit)
        {
            this.onHitCallback = onHit;
            transform.position = startPos;

            // Orient towards target
            transform.LookAt(targetPos);

            // Start the movement routine
            StartCoroutine(MoveRoutine(targetPos, speed));
        }

        private IEnumerator MoveRoutine(Vector3 targetPos, float speed)
        {
            // Move until we are close enough to the target
            while (Vector3.Distance(transform.position, targetPos) > 0.01f)
            {
                transform.position = Vector3.MoveTowards(
                    transform.position,
                    targetPos,
                    speed * Time.deltaTime
                );
                yield return null;
            }

            // Ensure we snap to exact target position at the end
            transform.position = targetPos;
            HandleHit();
        }

        private void HandleHit()
        {
            onHitCallback?.Invoke();
            Destroy(gameObject);
        }
    }
}
