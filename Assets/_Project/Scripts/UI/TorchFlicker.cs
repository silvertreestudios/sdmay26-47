using System.Collections.Generic;
using UnityEngine;

namespace TacticsGame.UI
{
    [RequireComponent(typeof(Light))]
    public class TorchFlicker : MonoBehaviour
    {
        [Header("References")]
        public Light torchLight;

        [Header("Intensity (Brightness)")]
        public float minIntensity = 1.5f;
        public float maxIntensity = 2.4f;

        [Tooltip("Higher = faster frequency of brightness changes.")]
        public float intensitySpeed = 0.07f;

        [Header("Range (Radius)")]
        public bool flickerRange = true;
        public float minRange = 8.5f;
        public float maxRange = 10.0f;
        public float rangeSpeed = 0.05f;

        [Header("Smoothing")]
        [Range(1, 50)]
        [Tooltip("Higher = smoother transitions, but less 'snappy' flickering.")]
        public int smoothingAmount = 10;

        [Header("Movement (Wobble)")]
        public bool wobblePosition = true;
        public float wobbleAmount = 0.04f;
        public float wobbleSpeed = 0.12f;

        private Queue<float> smoothQueue = new Queue<float>();
        private float lastSum = 0;
        private Vector3 originalPosition;
        private float noiseOffset;

        private void Start()
        {
            if (torchLight == null)
                torchLight = GetComponent<Light>();

            originalPosition = transform.localPosition;
            noiseOffset = Random.Range(0f, 9999f);

            // Initialize the queue to prevent a 'pop' at the start
            for (int i = 0; i < smoothingAmount; i++)
            {
                smoothQueue.Enqueue(minIntensity);
                lastSum += minIntensity;
            }
        }

        private void Update()
        {
            if (torchLight == null)
                return;

            // Intensity Flicker with Perlin Noise
            float noiseIntensity = Mathf.PerlinNoise(
                noiseOffset,
                Time.time * (intensitySpeed * 100f)
            );
            float rawValue = Mathf.Lerp(minIntensity, maxIntensity, noiseIntensity);

            // Rolling Average Smoothing
            lastSum -= smoothQueue.Dequeue();
            smoothQueue.Enqueue(rawValue);
            lastSum += rawValue;

            torchLight.intensity = lastSum / smoothingAmount;

            // Range Breathing
            if (flickerRange)
            {
                float noiseRange = Mathf.PerlinNoise(
                    noiseOffset + 100f,
                    Time.time * (rangeSpeed * 100f)
                );
                torchLight.range = Mathf.Lerp(minRange, maxRange, noiseRange);
            }

            // Position Wobble
            if (wobblePosition)
            {
                float tx =
                    (Mathf.PerlinNoise(noiseOffset + 200f, Time.time * (wobbleSpeed * 100f)) - 0.5f)
                    * wobbleAmount;
                float ty =
                    (Mathf.PerlinNoise(noiseOffset + 300f, Time.time * (wobbleSpeed * 100f)) - 0.5f)
                    * wobbleAmount;
                float tz =
                    (Mathf.PerlinNoise(noiseOffset + 400f, Time.time * (wobbleSpeed * 100f)) - 0.5f)
                    * wobbleAmount;
                transform.localPosition = originalPosition + new Vector3(tx, ty, tz);
            }
        }

        private void OnValidate()
        {
            if (torchLight == null)
                torchLight = GetComponent<Light>();
        }
    }
}
