using System;
using UnityEngine;

namespace TacticsGame.Combat
{
    /// <summary>
    /// Handles the visual flight of a projectile along an arcing path.
    /// Resloves the actual combat logic via a callback upon impact.
    /// </summary>
    public class Projectile : MonoBehaviour
    {
        [Header("Settings")]
        [SerializeField]
        private float speed = 25f;

        [SerializeField]
        private float arcHeight = 2.0f;

        [Header("Visuals")]
        [SerializeField]
        private GameObject impactEffectPrefab;

        private Vector3 startPos;
        private Vector3 targetPos;
        private Action onHitCallback;

        private float totalDistance;
        private float currentDistance;

        /// <summary>
        /// Initializes the projectile and starts its flight.
        /// </summary>
        public void Setup(Vector3 start, Vector3 target, Action onHit)
        {
            startPos = start;
            targetPos = target;
            onHitCallback = onHit;

            totalDistance = Vector3.Distance(start, target);
            currentDistance = 0f;

            transform.position = start;

            // Initial look at target
            transform.LookAt(target);
        }

        private void Update()
        {
            if (totalDistance <= 0)
                return;

            currentDistance += Time.deltaTime * speed;
            float t = currentDistance / totalDistance;

            if (t >= 1f)
            {
                CompleteFlight();
                return;
            }

            Vector3 nextPos = Vector3.Lerp(startPos, targetPos, t);

            float verticalOffset = Mathf.Sin(t * Mathf.PI) * arcHeight;
            nextPos.y += verticalOffset;

            Vector3 travelDir = (nextPos - transform.position).normalized;
            if (travelDir != Vector3.zero)
            {
                transform.forward = travelDir;
            }

            transform.position = nextPos;
        }

        private void CompleteFlight()
        {
            transform.position = targetPos;

            if (impactEffectPrefab != null)
            {
                Instantiate(impactEffectPrefab, targetPos, Quaternion.identity);
            }

            onHitCallback?.Invoke();
            Destroy(gameObject);
        }
    }
}
