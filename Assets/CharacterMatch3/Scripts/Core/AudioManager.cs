using UnityEngine;

namespace CharacterMatch3.Core
{
    public sealed class AudioManager : MonoBehaviour
    {
        public static AudioManager Instance { get; private set; }

        [Header("Placeholder clips")]
        public AudioClip swap;
        public AudioClip invalidSwap;
        public AudioClip normalMatch;
        public AudioClip cascade;
        public AudioClip lineClear;
        public AudioClip burst;
        public AudioClip rainbowActivation;
        public AudioClip blockerBreak;
        public AudioClip tokenDelivered;
        public AudioClip win;
        public AudioClip lose;
        public AudioClip buttonClick;

        private AudioSource source;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
            source = GetComponent<AudioSource>();
            if (source == null)
            {
                source = gameObject.AddComponent<AudioSource>();
            }
        }

        public void Play(AudioClip clip)
        {
            if (clip == null || source == null || !CharacterMatch3.Save.SaveManager.Data.soundEnabled)
            {
                return;
            }

            source.PlayOneShot(clip);
        }

        public void PlayButton()
        {
            Play(buttonClick);
        }
    }
}
