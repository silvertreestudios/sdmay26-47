using System;
using UnityEngine;

namespace TacticsGame.Core
{
    /// <summary>
    /// Global manager for handling background music playback.
    /// Registers with the ServiceLocator for easy access from other systems.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public class MusicManager : MonoBehaviour
    {
        [Header("Configuration")]
        [SerializeField]
        private AudioClip defaultBackgroundMusic;

        [SerializeField]
        private bool playOnStart = true;

        [SerializeField]
        [Range(0f, 1f)]
        private float defaultVolume = 0.5f;

        private AudioSource audioSource;

        private void Awake()
        {
            ServiceLocator.Register(this);

            audioSource = GetComponent<AudioSource>();
            audioSource.loop = true;
            audioSource.playOnAwake = false;
            audioSource.volume = defaultVolume;
        }

        private void OnDestroy()
        {
            ServiceLocator.Unregister<MusicManager>();
        }

        private void Start()
        {
            if (playOnStart && defaultBackgroundMusic != null)
            {
                PlayMusic(defaultBackgroundMusic);
            }
        }

        /// <summary>
        /// Instantly changes and plays the background music.
        /// </summary>
        public void PlayMusic(AudioClip clip)
        {
            if (audioSource.clip == clip && audioSource.isPlaying)
                return;

            audioSource.clip = clip;
            audioSource.Play();

            Debug.Log($"<color=cyan>[MusicManager]</color> Now playing: {clip.name}");
        }

        /// <summary>
        /// Stops the current music.
        /// </summary>
        public void StopMusic()
        {
            audioSource.Stop();
        }

        /// <summary>
        /// Adjusts the music volume.
        /// </summary>
        public void SetVolume(float volume)
        {
            audioSource.volume = Mathf.Clamp01(volume);
        }
    }
}
